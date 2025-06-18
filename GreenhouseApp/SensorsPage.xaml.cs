using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using NModbus;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace GreenhouseApp
{
    public partial class SensorsPage : ContentPage
    {
        private IDispatcherTimer _timer;
        private IDispatcherTimer _saveTimer;
        private bool _isUpdating = false;
        private List<Greenhouse> Greenhouses { get; set; }
        private List<Greenhouse> ConnectedGreenhouses { get; set; }
        private Greenhouse SelectedGreenhouse { get; set; }
        private List<SensorData> SensorDataList { get; set; } = new List<SensorData>();

        // Инициализация страницы датчиков
        // Настраиваем список теплиц, загружаем данные и инициализируем таймеры
        public SensorsPage(List<Greenhouse> connectedGreenhouses)
        {
            InitializeComponent();
            ConnectedGreenhouses = connectedGreenhouses;
            Greenhouses = connectedGreenhouses;
            LoadSensorData();

            GreenhousePicker.ItemsSource = Greenhouses;
            GreenhousePicker.SelectedIndexChanged += OnPickerSelectedIndexChanged;
            GreenhousePicker.ItemDisplayBinding = new Binding("Name");
            InitializeTimers();
        }

        // Настройка таймеров
        // Один таймер для обновления данных каждые 2 секунды, другой для сохранения каждую минуту
        private void InitializeTimers()
        {
            _timer = Dispatcher.CreateTimer();
            if (_timer != null)
            {
                _timer.Interval = TimeSpan.FromSeconds(2);
                _timer.Tick += async (s, e) => await RefreshDataAsync();
                _timer.Start();
            }

            _saveTimer = Dispatcher.CreateTimer();
            if (_saveTimer != null)
            {
                _saveTimer.Interval = TimeSpan.FromMinutes(1);
                _saveTimer.Tick += async (s, e) => await SaveAllGreenhousesDataAsync();
                _saveTimer.Start();
            }
        }

        // Загрузка данных с датчиков
        // Читаем данные из Preferences, при ошибке создаем пустой список
        private void LoadSensorData()
        {
            try
            {
                var sensorDataJson = Preferences.Get("sensor_data", "[]");
                SensorDataList = JsonConvert.DeserializeObject<List<SensorData>>(sensorDataJson) ?? new List<SensorData>();
                Console.WriteLine($"SensorsPage: Loaded {SensorDataList.Count} sensor data points.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SensorsPage: Error loading sensor data: {ex.Message}");
                SensorDataList = new List<SensorData>();
            }
        }

        // Сохранение данных с датчиков
        // Сериализуем данные и сохраняем в Preferences
        private async Task SaveSensorDataAsync()
        {
            try
            {
                var sensorDataJson = JsonConvert.SerializeObject(SensorDataList);
                Preferences.Set("sensor_data", sensorDataJson);
                Console.WriteLine($"SensorsPage: Saved {SensorDataList.Count} sensor data points.");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SensorsPage: Error saving sensor data: {ex.Message}");
            }
        }

        // Сохранение данных для всех теплиц
        // Собираем данные с каждой подключенной теплицы и сохраняем их
        private async Task SaveAllGreenhousesDataAsync()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                foreach (var greenhouse in ConnectedGreenhouses)
                {
                    await CollectDataForGreenhouseAsync(greenhouse);
                }
                await SaveSensorDataAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SensorsPage: Error in SaveAllGreenhousesDataAsync: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        // Сбор данных с конкретной теплицы
        // Читаем данные через Modbus и добавляем их в список, при ошибке добавляем оффлайн-данные
        private async Task CollectDataForGreenhouseAsync(Greenhouse greenhouse)
        {
            try
            {
                string modbusIp = greenhouse.Ip;
                int modbusPort = greenhouse.Port;

                if (string.IsNullOrEmpty(modbusIp) || modbusPort == 0)
                {
                    Console.WriteLine($"SensorsPage: Invalid connection parameters for {greenhouse.Name}.");
                    AddOfflineDataPoint(greenhouse);
                    return;
                }

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(modbusIp, modbusPort);

                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                {
                    AddOfflineDataPoint(greenhouse);
                    throw new TimeoutException($"SensorsPage: Connection timeout for {greenhouse.Name}.");
                }

                if (!client.Connected)
                {
                    AddOfflineDataPoint(greenhouse);
                    throw new SocketException((int)SocketError.ConnectionRefused);
                }

                var factory = new ModbusFactory();
                var master = factory.CreateMaster(client);

                ushort[] registers = await Task.Run(() => master.ReadHoldingRegisters(1, 0, 4));

                if (registers == null || registers.Length < 4)
                {
                    AddOfflineDataPoint(greenhouse);
                    throw new InvalidOperationException($"SensorsPage: Insufficient data from {greenhouse.Name}.");
                }

                double soilMoisture = registers[3];
                if (soilMoisture >= 1000)
                {
                    soilMoisture = 1000;
                    soilMoisture = Math.Round((1 - (soilMoisture / 1000)) * 100, 1);
                }
                else
                {
                    soilMoisture = Math.Round((1 - (soilMoisture / 1000)) * 100, 1);
                }

                var sensorData = new SensorData
                {
                    Timestamp = DateTime.Now,
                    Temperature = registers[0],
                    Humidity = registers[1],
                    SoilMoisture = soilMoisture,
                    CO2 = registers[2],
                    IsConnected = true,
                    GreenhouseName = greenhouse.Name
                };

                SensorDataList.Add(sensorData);
                Console.WriteLine($"SensorsPage: Collected data for {greenhouse.Name}: Temp={sensorData.Temperature}, Humidity={sensorData.Humidity}, Soil={sensorData.SoilMoisture}, CO2={sensorData.CO2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SensorsPage: Error collecting data for {greenhouse.Name}: {ex.Message}");
                AddOfflineDataPoint(greenhouse);
            }
        }

        // Обновление данных на странице
        // Читаем данные с выбранной теплицы через Modbus и обновляем метки на UI
        private async Task RefreshDataAsync()
        {
            if (_isUpdating || SelectedGreenhouse == null) return;

            _isUpdating = true;

            try
            {
                string modbusIp = SelectedGreenhouse.Ip;
                int modbusPort = SelectedGreenhouse.Port;

                if (string.IsNullOrEmpty(modbusIp) || modbusPort == 0)
                {
                    Console.WriteLine("SensorsPage: Invalid connection parameters for selected greenhouse.");
                    return;
                }

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(modbusIp, modbusPort);

                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                {
                    throw new TimeoutException("SensorsPage: Connection timeout for selected greenhouse.");
                }

                if (!client.Connected)
                {
                    throw new SocketException((int)SocketError.ConnectionRefused);
                }

                var factory = new ModbusFactory();
                var master = factory.CreateMaster(client);

                ushort[] registers = await Task.Run(() => master.ReadHoldingRegisters(1, 0, 4));

                if (registers == null || registers.Length < 4)
                {
                    throw new InvalidOperationException("SensorsPage: Insufficient data from selected greenhouse.");
                }

                double soilMoisture = registers[3];
                if (soilMoisture >= 1000)
                {
                    soilMoisture = 1000;
                    soilMoisture = Math.Round((1 - (soilMoisture / 1000)) * 100, 1);
                }
                else
                {
                    soilMoisture = Math.Round((1 - (soilMoisture / 1000)) * 100, 1);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        TemperatureLabel.Text = $"{registers[0]:F1} °C";
                        HumidityLabel.Text = $"{registers[1]} %";
                        SoilMoistureLabel.Text = $"{soilMoisture:F1} %";
                        CO2Label.Text = $"{registers[2]} ppm";
                    }
                    catch (Exception uiEx)
                    {
                        Console.WriteLine($"SensorsPage: Error updating UI: {uiEx.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SensorsPage: Error in RefreshDataAsync: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        // Добавление данных оффлайн
        // Создает запись с нулевыми значениями для неработающей теплицы
        private void AddOfflineDataPoint(Greenhouse greenhouse)
        {
            SensorDataList.Add(new SensorData
            {
                Timestamp = DateTime.Now,
                Temperature = 0,
                Humidity = 0,
                SoilMoisture = 0,
                CO2 = 0,
                IsConnected = false,
                GreenhouseName = greenhouse.Name
            });
        }

        // Обработчик выбора теплицы
        // Обновляет выбранную теплицу и выводит информацию в консоль
        private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            var picker = (Picker)sender;
            SelectedGreenhouse = picker.SelectedItem as Greenhouse;
            Console.WriteLine($"SensorsPage: Selected greenhouse for display: {SelectedGreenhouse?.Name ?? "None"}");
        }

        // Очистка при уходе со страницы
        // Останавливаем таймеры при закрытии страницы
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _timer?.Stop();
            _saveTimer?.Stop();
        }
    }
}