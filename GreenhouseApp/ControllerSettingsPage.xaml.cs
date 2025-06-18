using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;

namespace GreenhouseApp
{
    public partial class ControllerSettingsPage : ContentPage
    {
        private List<Greenhouse> ConnectedGreenhouses { get; set; }
        private Greenhouse SelectedGreenhouse { get; set; }
        private List<Device> Devices { get; set; }
        private const string DevicesFileName = "devices.json";

        // Инициализация страницы настроек
        // Передаем список теплиц, загружаем устройства и настраиваем интерфейс
        public ControllerSettingsPage(List<Greenhouse> connectedGreenhouses)
        {
            InitializeComponent();
            ConnectedGreenhouses = connectedGreenhouses ?? new List<Greenhouse>();
            LoadDevices();
            SetupUI();
        }

        // Настройка интерфейса
        // Устанавливаем начальные значения для слайдеров температуры, влажности почвы и освещенности
        private void SetupUI()
        {
            try
            {
                // Настройка слайдера температуры (15–40°C, начальное значение 25°C)
                TemperatureSlider.Minimum = 15;
                TemperatureSlider.Maximum = 40;
                TemperatureSlider.Value = 25;
                TemperatureSlider.ValueChanged += (s, e) => TemperatureLabel.Text = $"{e.NewValue:F0} °C";

                // Настройка слайдера влажности почвы (0–100%, начальное значение 40%)
                SoilMoistureSlider.Minimum = 0;
                SoilMoistureSlider.Maximum = 100;
                SoilMoistureSlider.Value = 40;
                SoilMoistureSlider.ValueChanged += (s, e) => SoilMoistureLabel.Text = $"{e.NewValue:F0} %";

                // Настройка слайдера длительности освещения (0–24 часа, начальное значение 12 часов)
                DaylightSlider.Minimum = 0;
                DaylightSlider.Maximum = 24;
                DaylightSlider.Value = 12;
                DaylightSlider.ValueChanged += (s, e) => DaylightLabel.Text = $"{e.NewValue:F0} часов";

                SetupPicker();
                System.Diagnostics.Debug.WriteLine("ControllerSettingsPage: UI setup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Error in SetupUI: {ex}");
                DisplayAlert("Ошибка", "Не удалось настроить интерфейс.", "OK");
            }
        }

        // Настройка выпадающего списка для выбора теплицы
        // Привязываем список теплиц к Picker и устанавливаем обработчик выбора
        private void SetupPicker()
        {
            try
            {
                GreenhousePicker.ItemsSource = ConnectedGreenhouses;
                GreenhousePicker.ItemDisplayBinding = new Binding("Name");
                GreenhousePicker.SelectedIndexChanged += OnPickerSelectedIndexChanged;

                if (ConnectedGreenhouses.Any())
                {
                    GreenhousePicker.SelectedIndex = 0;
                    SelectedGreenhouse = ConnectedGreenhouses[0];
                    LoadSettings();
                }
                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Selected greenhouse: {SelectedGreenhouse?.Name ?? "None"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Error in SetupPicker: {ex}");
                DisplayAlert("Ошибка", "Не удалось настроить выбор теплицы.", "OK");
            }
        }

        // Обработчик выбора теплицы
        // При выборе теплицы обновляем настройки слайдеров на основе сохраненных данных
        private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                SelectedGreenhouse = GreenhousePicker.SelectedItem as Greenhouse;
                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Selected greenhouse: {SelectedGreenhouse?.Name ?? "None"}");
                LoadSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Error in OnPickerSelectedIndexChanged: {ex}");
            }
        }

        // Загрузка списка устройств из файла
        // Читаем устройства из JSON-файла, если файл отсутствует — создаем пустой список
        private void LoadDevices()
        {
            try
            {
                var filePath = Path.Combine(FileSystem.AppDataDirectory, DevicesFileName);
                if (File.Exists(filePath))
                {
                    var devicesJson = File.ReadAllText(filePath);
                    Devices = JsonConvert.DeserializeObject<List<Device>>(devicesJson) ?? new List<Device>();
                }
                else
                {
                    Devices = new List<Device>();
                }
                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Loaded {Devices.Count} devices.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Error loading devices: {ex}");
                Devices = new List<Device>();
            }
        }

        // Загрузка настроек для выбранной теплицы
        // Извлекаем параметры (температура, влажность, освещение) для устройств теплицы
        private void LoadSettings()
        {
            if (SelectedGreenhouse == null) return;

            try
            {
                var ventilationDevice = Devices.FirstOrDefault(d => d.Type == "Ventilation" && d.GreenhouseName == SelectedGreenhouse.Name);
                TemperatureSlider.Value = ventilationDevice?.TemperatureTrigger ?? 25;

                var doorDevice = Devices.FirstOrDefault(d => d.Type == "Door" && d.GreenhouseName == SelectedGreenhouse.Name);
                SoilMoistureSlider.Value = doorDevice?.SoilMoistureTrigger ?? 40;

                var lightDevice = Devices.FirstOrDefault(d => d.Type == "NewLight" && d.GreenhouseName == SelectedGreenhouse.Name);
                DaylightSlider.Value = lightDevice?.DaylightHours ?? 12;

                TemperatureLabel.Text = $"{TemperatureSlider.Value:F0} °C";
                SoilMoistureLabel.Text = $"{SoilMoistureSlider.Value:F0} %";
                DaylightLabel.Text = $"{DaylightSlider.Value:F0} часов";

                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Loaded settings for {SelectedGreenhouse.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Error loading settings: {ex}");
            }
        }

        // Сохранение настроек при нажатии на кнопку
        // Обновляем параметры устройств (температура, влажность, освещение) и сохраняем их в JSON-файл
        private async void OnSaveButtonClicked(object sender, EventArgs e)
        {
            if (SelectedGreenhouse == null)
            {
                await DisplayAlert("Ошибка", "Выберите теплицу.", "OK");
                return;
            }

            try
            {
                // Обновляем или добавляем устройство вентиляции с новым порогом температуры
                var ventilationDevice = Devices.FirstOrDefault(d => d.Type == "Ventilation" && d.GreenhouseName == SelectedGreenhouse.Name);
                if (ventilationDevice != null)
                {
                    ventilationDevice.TemperatureTrigger = Math.Round(TemperatureSlider.Value);
                }
                else
                {
                    Devices.Add(new Device
                    {
                        Name = $"Ventilation_{SelectedGreenhouse.Name}",
                        Type = "Ventilation",
                        Register = 0,
                        IsOn = false,
                        IsAuto = false,
                        GreenhouseName = SelectedGreenhouse.Name,
                        TemperatureTrigger = Math.Round(TemperatureSlider.Value)
                    });
                }

                // Обновляем или добавляем устройство двери с новым порогом влажности почвы
                var doorDevice = Devices.FirstOrDefault(d => d.Type == "Door" && d.GreenhouseName == SelectedGreenhouse.Name);
                if (doorDevice != null)
                {
                    doorDevice.SoilMoistureTrigger = Math.Round(SoilMoistureSlider.Value);
                }
                else
                {
                    Devices.Add(new Device
                    {
                        Name = $"Door_{SelectedGreenhouse.Name}",
                        Type = "Door",
                        Register = 0,
                        IsOn = false,
                        IsAuto = false,
                        GreenhouseName = SelectedGreenhouse.Name,
                        SoilMoistureTrigger = Math.Round(SoilMoistureSlider.Value)
                    });
                }

                // Обновляем или добавляем устройство освещения с новой длительностью освещения
                var lightDevice = Devices.FirstOrDefault(d => d.Type == "NewLight" && d.GreenhouseName == SelectedGreenhouse.Name);
                if (lightDevice != null)
                {
                    lightDevice.DaylightHours = (int)Math.Round(DaylightSlider.Value);
                    lightDevice.LightOnTime = null;
                }
                else
                {
                    Devices.Add(new Device
                    {
                        Name = $"Light_{SelectedGreenhouse.Name}",
                        Type = "NewLight",
                        Register = 0,
                        IsOn = false,
                        IsAuto = false,
                        GreenhouseName = SelectedGreenhouse.Name,
                        DaylightHours = (int)Math.Round(DaylightSlider.Value)
                    });
                }

                // Сохраняем обновленный список устройств в JSON-файл
                var filePath = Path.Combine(FileSystem.AppDataDirectory, DevicesFileName);
                var devicesJson = JsonConvert.SerializeObject(Devices, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, devicesJson);
                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Saved settings for {SelectedGreenhouse.Name}");

                await DisplayAlert("Успех", "Настройки сохранены.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControllerSettingsPage: Error saving settings: {ex}");
                await DisplayAlert("Ошибка", $"Не удалось сохранить настройки: {ex.Message}", "OK");
            }
        }
    }
}