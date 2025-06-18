using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Net.Sockets;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NModbus;

namespace GreenhouseApp
{
    public partial class ControlPage : ContentPage
    {
        private List<Greenhouse> Greenhouses { get; set; }
        private Greenhouse SelectedGreenhouse { get; set; }
        private static List<Device> Devices { get; set; } = new List<Device>();
        private List<SensorData> SensorDataList { get; set; } = new List<SensorData>();
        private StackLayout DevicesLayout { get; set; }
        private const string DevicesFileName = "devices.json";
        private const string SensorDataFileName = "sensor_data";
        private IDispatcherTimer _autoModeTimer;
        private const double TemperatureHysteresis = 0.5;

        // ������������� �������� ����������
        // �������� ������ ������ � ����������� ������ ��� ��������������� ������
        public ControlPage(List<Greenhouse> greenhouses)
        {
            InitializeComponent();
            Greenhouses = greenhouses ?? new List<Greenhouse>();
            DevicesLayout = new StackLayout { Spacing = 15 };
            InitializeAutoModeTimer();
        }

        // ��������� ������� ��� ��������������� ������
        // ������ ����������� ������ 2 ������� ��� �������� ������� ���������
        private void InitializeAutoModeTimer()
        {
            _autoModeTimer = Dispatcher.CreateTimer();
            if (_autoModeTimer != null)
            {
                _autoModeTimer.Interval = TimeSpan.FromSeconds(2);
                _autoModeTimer.Tick += async (s, e) => await CheckAutoModeAsync();
                _autoModeTimer.Start();
            }
        }

        // ������������� �������� ��� ���������
        // ��������� ����������, ������ ��������, ����������� ��������� � ����� �������
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                System.Diagnostics.Debug.WriteLine("ControlPage: OnAppearing started");
                await LoadDevicesAsync();
                await LoadSensorDataAsync();
                SetupUI();
                SetupPicker();
                System.Diagnostics.Debug.WriteLine("ControlPage: OnAppearing completed");
            }
            catch (Exception ex)
            {
                await DisplayAlert("������", $"������ ������������� ��������: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error in OnAppearing: {ex}");
            }
        }

        // ������� ��� ����� �� ��������
        // ������������ �� ������� � ������������� ������ ��������������� ������
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (GreenhousePicker != null)
            {
                GreenhousePicker.SelectedIndexChanged -= OnPickerSelectedIndexChanged;
                System.Diagnostics.Debug.WriteLine("ControlPage: Unsubscribed GreenhousePicker.SelectedIndexChanged");
            }
            _autoModeTimer?.Stop();
        }

        // �������� ������ ��������� �� �����
        // ������ ���������� �� JSON-�����, ��� ������ ������� ������ ������
        private async Task LoadDevicesAsync()
        {
            try
            {
                var filePath = Path.Combine(FileSystem.AppDataDirectory, DevicesFileName);
                System.Diagnostics.Debug.WriteLine($"ControlPage: Attempting to load devices from {filePath}");
                if (File.Exists(filePath))
                {
                    var devicesJson = await File.ReadAllTextAsync(filePath);
                    if (!string.IsNullOrEmpty(devicesJson))
                    {
                        try
                        {
                            Devices = JsonConvert.DeserializeObject<List<Device>>(devicesJson) ?? new List<Device>();
                            System.Diagnostics.Debug.WriteLine($"ControlPage: Loaded {Devices.Count} devices from {filePath}");
                        }
                        catch (JsonException jsonEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"ControlPage: JSON deserialization failed: {jsonEx}");
                            Devices = new List<Device>();
                            await File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(Devices));
                            System.Diagnostics.Debug.WriteLine("ControlPage: Reset devices.json to empty list");
                        }
                    }
                    else
                    {
                        Devices = new List<Device>();
                        System.Diagnostics.Debug.WriteLine("ControlPage: Devices file is empty, initialized empty list");
                    }
                }
                else
                {
                    Devices = new List<Device>();
                    System.Diagnostics.Debug.WriteLine("ControlPage: Devices file does not exist, initialized empty list");
                }
            }
            catch (Exception ex)
            {
                Devices = new List<Device>();
                await DisplayAlert("������", $"������ ��� �������� ���������: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error loading devices: {ex}");
            }
        }

        // �������� ������ � ��������
        // ������ ������ �� Preferences, ��� ������ ������� ������ ������
        private async Task LoadSensorDataAsync()
        {
            try
            {
                var sensorDataJson = Preferences.Get("sensor_data", "[]");
                SensorDataList = JsonConvert.DeserializeObject<List<SensorData>>(sensorDataJson) ?? new List<SensorData>();
                System.Diagnostics.Debug.WriteLine($"ControlPage: Loaded {SensorDataList.Count} sensor data points");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error loading sensor data: {ex}");
                SensorDataList = new List<SensorData>();
            }
        }

        // ���������� ������ ��������� � ����
        // ����������� ���������� � JSON � ���������� � ����
        private async Task SaveDevicesAsync()
        {
            try
            {
                var filePath = Path.Combine(FileSystem.AppDataDirectory, DevicesFileName);
                var devicesJson = JsonConvert.SerializeObject(Devices, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, devicesJson);
                System.Diagnostics.Debug.WriteLine($"ControlPage: Saved {Devices.Count} devices to {filePath}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("������", $"������ ��� ���������� ���������: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error saving devices: {ex}");
            }
        }

        // �������� ��������������� ������
        // ��������� ������ �������� � ������������� ��������� ������������ (����������, �����, ����)
        private async Task CheckAutoModeAsync()
        {
            if (SelectedGreenhouse == null)
            {
                System.Diagnostics.Debug.WriteLine("ControlPage: CheckAutoModeAsync skipped: No greenhouse selected");
                return;
            }

            try
            {
                var latestSensorData = SensorDataList
                    .Where(s => s.GreenhouseName == SelectedGreenhouse.Name && s.IsConnected)
                    .OrderByDescending(s => s.Timestamp)
                    .FirstOrDefault();

                if (latestSensorData == null)
                {
                    System.Diagnostics.Debug.WriteLine("ControlPage: CheckAutoModeAsync skipped: No valid sensor data");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"ControlPage: Latest sensor data for {SelectedGreenhouse.Name}: Temp={latestSensorData.Temperature}�C, SoilMoisture={latestSensorData.SoilMoisture}%");

                foreach (var device in Devices.Where(d => d.GreenhouseName == SelectedGreenhouse.Name && d.IsAuto))
                {
                    if (device.Type == "Door" && device.SoilMoistureTrigger.HasValue)
                    {
                        bool shouldBeOpen = latestSensorData.SoilMoisture < device.SoilMoistureTrigger.Value;
                        if (device.IsOn != shouldBeOpen)
                        {
                            device.IsOn = shouldBeOpen;
                            await ControlDevice(device.Register, shouldBeOpen ? 90 : 0, device.Name, false);
                            await SaveDevicesAsync();
                            UpdateDevicesUI();
                            System.Diagnostics.Debug.WriteLine($"ControlPage: Auto-mode set {device.Name} to {(shouldBeOpen ? "Open" : "Closed")} based on soil moisture {latestSensorData.SoilMoisture}% (threshold: {device.SoilMoistureTrigger.Value}%)");
                        }
                    }
                    else if (device.Type == "Ventilation" && device.TemperatureTrigger.HasValue)
                    {
                        bool shouldBeOpen;
                        if (device.IsOn)
                        {
                            shouldBeOpen = latestSensorData.Temperature > (device.TemperatureTrigger.Value - TemperatureHysteresis);
                        }
                        else
                        {
                            shouldBeOpen = latestSensorData.Temperature > device.TemperatureTrigger.Value;
                        }

                        if (device.IsOn != shouldBeOpen)
                        {
                            device.IsOn = shouldBeOpen;
                            await ControlDevice(device.Register, shouldBeOpen ? 90 : 0, device.Name, false);
                            await SaveDevicesAsync();
                            UpdateDevicesUI();
                            System.Diagnostics.Debug.WriteLine($"ControlPage: Auto-mode set {device.Name} to {(shouldBeOpen ? "Open" : "Closed")} based on temperature {latestSensorData.Temperature}�C (threshold: {device.TemperatureTrigger.Value}�C, hysteresis: {TemperatureHysteresis}�C)");
                        }
                    }
                    else if (device.Type == "NewLight" && device.DaylightHours.HasValue)
                    {
                        if (!device.LightOnTime.HasValue || (DateTime.Now - device.LightOnTime.Value).TotalHours >= 24)
                        {
                            device.LightOnTime = DateTime.Now;
                            device.IsOn = true;
                            await ControlDevice(device.Register, 1, device.Name, true);
                            await SaveDevicesAsync();
                            UpdateDevicesUI();
                            System.Diagnostics.Debug.WriteLine($"ControlPage: Auto-mode turned on {device.Name} at {device.LightOnTime}");
                        }
                        else if (device.IsOn && (DateTime.Now - device.LightOnTime.Value).TotalHours >= device.DaylightHours.Value)
                        {
                            device.IsOn = false;
                            await ControlDevice(device.Register, 0, device.Name, true);
                            await SaveDevicesAsync();
                            UpdateDevicesUI();
                            System.Diagnostics.Debug.WriteLine($"ControlPage: Auto-mode turned off {device.Name} after {device.DaylightHours.Value} hours");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error in CheckAutoModeAsync: {ex}");
                await DisplayAlert("������", $"������ � ����-������: {ex.Message}", "OK");
            }
        }

        // ��������� ����������� ������ ��� ������ �������
        // ����������� ������ ������ � ������������� ���������� ������
        private void SetupPicker()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("ControlPage: Setting up GreenhousePicker");
                if (GreenhousePicker == null)
                {
                    System.Diagnostics.Debug.WriteLine("ControlPage: GreenhousePicker is null, check XAML");
                    return;
                }
                GreenhousePicker.ItemsSource = Greenhouses;
                GreenhousePicker.ItemDisplayBinding = new Binding("Name");
                GreenhousePicker.SelectedIndexChanged -= OnPickerSelectedIndexChanged;
                GreenhousePicker.SelectedIndexChanged += OnPickerSelectedIndexChanged;

                var savedGreenhouseName = Preferences.Get("SelectedGreenhouseName", string.Empty);
                var savedGreenhouse = Greenhouses.FirstOrDefault(g => g.Name == savedGreenhouseName);
                GreenhousePicker.SelectedItem = savedGreenhouse ?? Greenhouses.FirstOrDefault();
                System.Diagnostics.Debug.WriteLine($"ControlPage: Selected greenhouse: {(savedGreenhouse?.Name ?? "None")}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error in SetupPicker: {ex}");
            }
        }

        // ���������� ��������� ������ �������
        // ��������� ��������� ������� � ��������� ���������
        private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                SelectedGreenhouse = GreenhousePicker.SelectedItem as Greenhouse;
                if (SelectedGreenhouse != null)
                {
                    Preferences.Set("SelectedGreenhouseName", SelectedGreenhouse.Name);
                    System.Diagnostics.Debug.WriteLine($"ControlPage: Selected greenhouse changed to {SelectedGreenhouse.Name}");
                    UpdateDevicesUI();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error in OnPickerSelectedIndexChanged: {ex}");
            }
        }

        // ��������� ���������� ��������
        // ��������� ������ ����������/�������� ��������� � ��������� ������ ���������
        private void SetupUI()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("ControlPage: Setting up UI");
                var existingButtons = MainStackLayout.Children
                    .Where(c => c is Button || c == DevicesLayout)
                    .ToList();
                foreach (var child in existingButtons)
                {
                    MainStackLayout.Children.Remove(child);
                }

                MainStackLayout.Children.Add(new Button
                {
                    Text = "�������� ����������",
                    BackgroundColor = Color.FromArgb("#23B5DE"),
                    BorderColor = Colors.Black,
                    BorderWidth = 2,
                    Command = new Command(async () => await OnAddDeviceClicked())
                });
                MainStackLayout.Children.Add(new Button
                {
                    Text = "������� ����������",
                    BackgroundColor = Color.FromArgb("#DE2323"),
                    BorderColor = Colors.Black,
                    BorderWidth = 2,
                    Command = new Command(async () => await OnRemoveDeviceClicked())
                });
                MainStackLayout.Children.Add(DevicesLayout);
                UpdateDevicesUI();
                System.Diagnostics.Debug.WriteLine("ControlPage: UI setup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error in SetupUI: {ex}");
            }
        }

        // ���������� ������ ����������
        // ����������� ���, ��� � ������� ����������, ��������� ��� � ������
        private async Task OnAddDeviceClicked()
        {
            if (SelectedGreenhouse == null)
            {
                await DisplayAlert("������", "�������� �������.", "OK");
                return;
            }

            try
            {
                var types = new[] { "����", "����������", "����" };
                var type = await DisplayActionSheet("�������� ��� ����������", "������", null, types);
                if (type == "������" || string.IsNullOrEmpty(type)) return;

                var name = await DisplayPromptAsync("��� ����������", "������� ��� ����������:", "OK", "������");
                if (string.IsNullOrEmpty(name)) return;

                var registerStr = await DisplayPromptAsync("�������", "������� ����� �������� (0-65535):", "OK", "������", keyboard: Keyboard.Numeric);
                if (string.IsNullOrEmpty(registerStr))
                {
                    await DisplayAlert("������", "���� �������� ��� �������.", "OK");
                    return;
                }

                if (!ushort.TryParse(registerStr, out ushort register))
                {
                    await DisplayAlert("������", "�������� ������ ��������. ������� ����� �� 0 �� 65535.", "OK");
                    return;
                }

                string deviceType = type switch
                {
                    "����������" => "Ventilation",
                    "����" => "Door",
                    "����" => "NewLight",
                    _ => throw new InvalidOperationException("�������� ��� ����������")
                };

                var device = new Device
                {
                    Name = name,
                    Type = deviceType,
                    Register = register,
                    IsOn = false,
                    IsAuto = false,
                    GreenhouseName = SelectedGreenhouse.Name
                };

                if (deviceType == "Ventilation")
                {
                    device.TemperatureTrigger = 25;
                }
                else if (deviceType == "Door")
                {
                    device.SoilMoistureTrigger = 40;
                }
                else if (deviceType == "NewLight")
                {
                    device.DaylightHours = 12;
                }

                System.Diagnostics.Debug.WriteLine($"ControlPage: Adding device: {device.Name} for greenhouse: {SelectedGreenhouse.Name}");
                Devices.Add(device);
                await SaveDevicesAsync();
                UpdateDevicesUI();
            }
            catch (Exception ex)
            {
                await DisplayAlert("������", $"�� ������� �������� ����������: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error in OnAddDeviceClicked: {ex}");
            }
        }

        // �������� ����������
        // ����������� ���������� ��� �������� � ������� ��� �� ������
        private async Task OnRemoveDeviceClicked()
        {
            if (SelectedGreenhouse == null || !Devices.Any(d => d.GreenhouseName == SelectedGreenhouse.Name))
            {
                await DisplayAlert("������", "��� ��������� ��� �������� ��� �� ������� �������.", "OK");
                return;
            }

            try
            {
                var deviceNames = Devices.Where(d => d.GreenhouseName == SelectedGreenhouse.Name).Select(d => d.Name).ToArray();
                var selectedDevice = await DisplayActionSheet("�������� ���������� ��� ��������", "������", null, deviceNames);
                if (selectedDevice == "������" || string.IsNullOrEmpty(selectedDevice)) return;

                var device = Devices.FirstOrDefault(d => d.Name == selectedDevice && d.GreenhouseName == SelectedGreenhouse.Name);
                if (device != null)
                {
                    Devices.Remove(device);
                    await SaveDevicesAsync();
                    UpdateDevicesUI();
                    System.Diagnostics.Debug.WriteLine($"ControlPage: Removed device: {selectedDevice}");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("������", $"������ ��� �������� ����������: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error in OnRemoveDeviceClicked: {ex}");
            }
        }

        // ���������� ���������� ���������
        // ������� � ������ ������� ������ ��������� ��� ������� �������
        private void UpdateDevicesUI()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("ControlPage: Updating Devices UI");
                DevicesLayout.Children.Clear();
                if (SelectedGreenhouse != null)
                {
                    var greenhouseDevices = Devices.Where(d => d.GreenhouseName == SelectedGreenhouse.Name);
                    foreach (var device in greenhouseDevices)
                    {
                        DevicesLayout.Children.Add(CreateDeviceFrame(device));
                    }
                    System.Diagnostics.Debug.WriteLine($"ControlPage: Added {greenhouseDevices.Count()} devices to UI");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error in UpdateDevicesUI: {ex}");
            }
        }

        // �������� ����������� �������� ��� ����������
        // ������� �������� ���������� � ��������������� (���/����, ����) � ������� ����� �����
        private Frame CreateDeviceFrame(Device device)
        {
            var label = new Label
            {
                Text = device.Name,
                FontSize = 18,
                TextColor = Colors.White
            };

            var onOffSwitch = new Switch
            {
                VerticalOptions = LayoutOptions.Center,
                IsToggled = device.IsOn
            };
            onOffSwitch.Toggled += async (s, e) =>
            {
                try
                {
                    device.IsOn = e.Value;
                    if (device.Type == "NewLight")
                    {
                        await ControlDevice(device.Register, e.Value ? 1 : 0, device.Name, true);
                        if (e.Value) device.LightOnTime = DateTime.Now;
                        else device.LightOnTime = null;
                    }
                    else
                    {
                        await ControlDevice(device.Register, e.Value ? 90 : 0, device.Name, false);
                    }
                    await SaveDevicesAsync();
                    System.Diagnostics.Debug.WriteLine($"ControlPage: Toggled {device.Name} to IsOn={e.Value}");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("������", $"������ ���������� �����������: {ex.Message}", "OK");
                    System.Diagnostics.Debug.WriteLine($"ControlPage: Error in onOffSwitch.Toggled: {ex}");
                }
            };

            var autoSwitch = new Switch
            {
                VerticalOptions = LayoutOptions.Center,
                IsToggled = device.IsAuto
            };
            autoSwitch.Toggled += async (s, e) =>
            {
                try
                {
                    device.IsAuto = e.Value;
                    if (!e.Value && device.Type == "NewLight" && device.IsOn)
                    {
                        device.IsOn = false;
                        await ControlDevice(device.Register, 0, device.Name, true);
                        device.LightOnTime = null;
                    }
                    await SaveDevicesAsync();
                    UpdateDevicesUI();
                    System.Diagnostics.Debug.WriteLine($"ControlPage: Toggled {device.Name} to IsAuto={e.Value}");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("������", $"������ ��������� ���� ������: {ex.Message}", "OK");
                    System.Diagnostics.Debug.WriteLine($"ControlPage: Error in autoSwitch.Toggled: {ex}");
                }
            };

            var changeNameButton = new Button
            {
                Text = "�������� ���",
                BackgroundColor = Color.FromArgb("#23B5DE"),
                BorderColor = Colors.Black,
                BorderWidth = 2
            };
            changeNameButton.Clicked += async (s, e) =>
            {
                try
                {
                    var newName = await DisplayPromptAsync("�������� ���", "������� ����� ���:", "OK", "������");
                    if (!string.IsNullOrEmpty(newName))
                    {
                        device.Name = newName;
                        label.Text = newName;
                        await SaveDevicesAsync();
                        UpdateDevicesUI();
                        System.Diagnostics.Debug.WriteLine($"ControlPage: Renamed device to {newName}");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("������", $"������ ��������� �����: {ex.Message}", "OK");
                    System.Diagnostics.Debug.WriteLine($"ControlPage: Error in changeNameButton.Clicked: {ex}");
                }
            };

            var isDoorOrVentilationOrNewLight = device.Type == "Door" || device.Type == "Ventilation" || device.Type == "NewLight";
            var innerFrame = new Frame
            {
                Padding = 10,
                CornerRadius = 10,
                BackgroundColor = Color.FromArgb("#302f31"),
                HeightRequest = isDoorOrVentilationOrNewLight ? 190 : 0,
                Content = new StackLayout
                {
                    Children =
                    {
                        label,
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitionCollection
                            {
                                new ColumnDefinition { Width = GridLength.Star },
                                new ColumnDefinition { Width = GridLength.Auto }
                            },
                            HorizontalOptions = LayoutOptions.FillAndExpand,
                            Margin = isDoorOrVentilationOrNewLight ? new Thickness(0, 0, 0, 35) : new Thickness(0),
                            Children =
                            {
                                new Label
                                {
                                    Text = isDoorOrVentilationOrNewLight
                                        ? (device.Type == "Door" ? "�������/�������" : device.Type == "Ventilation" ? "�������/�������" : "���./����.")
                                        : "���./����.",
                                    FontSize = 16,
                                    VerticalOptions = LayoutOptions.Center,
                                    Margin = isDoorOrVentilationOrNewLight ? new Thickness(0, 15, 0, 0) : new Thickness(0)
                                }.SetGridColumn(0),
                                onOffSwitch.SetGridColumn(1).SetMargin(isDoorOrVentilationOrNewLight ? new Thickness(0, 15, 0, 0) : new Thickness(0))
                            }
                        },
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitionCollection
                            {
                                new ColumnDefinition { Width = GridLength.Star },
                                new ColumnDefinition { Width = GridLength.Auto }
                            },
                            HorizontalOptions = LayoutOptions.FillAndExpand,
                            Children =
                            {
                                new Label
                                {
                                    Text = "���� �����",
                                    FontSize = 16,
                                    VerticalOptions = LayoutOptions.Center
                                }.SetGridColumn(0),
                                autoSwitch.SetGridColumn(1)
                            }
                        },
                        changeNameButton
                    }
                }
            };

            return new Frame
            {
                CornerRadius = 10,
                Padding = 1,
                BackgroundColor = Colors.Black,
                Content = innerFrame
            };
        }

        // ���������� ����������� ����� Modbus
        // ���������� ������� (���/���� ��� ����) �� ���������� ����� Modbus TCP
        private async Task ControlDevice(ushort registerAddress, int value, string deviceName, bool isBoolean = false)
        {
            if (SelectedGreenhouse == null || string.IsNullOrWhiteSpace(SelectedGreenhouse.Ip) || SelectedGreenhouse.Port == 0)
            {
                await DisplayAlert("������", "�� ������� ������� ��� �� ����� IP-�����/����.", "OK");
                System.Diagnostics.Debug.WriteLine("ControlPage: ControlDevice failed: Invalid greenhouse settings");
                return;
            }

            TcpClient client = null;
            try
            {
                System.Diagnostics.Debug.WriteLine($"ControlPage: Controlling device {deviceName} with value {value} (isBoolean={isBoolean})");
                client = new TcpClient();
                await client.ConnectAsync(SelectedGreenhouse.Ip, SelectedGreenhouse.Port);

                var factory = new ModbusFactory();
                var modbusMaster = factory.CreateMaster(client);
                modbusMaster.Transport.ReadTimeout = 500;
                modbusMaster.Transport.WriteTimeout = 500;

                if (isBoolean)
                {
                    bool boolValue = value == 1;
                    await Task.Run(() => modbusMaster.WriteSingleCoil(1, registerAddress, boolValue));
                }
                else
                {
                    ushort registerValue = (ushort)value;
                    if (value != 0 && value != 90)
                    {
                        await DisplayAlert("������", $"�������� �������� ��� {deviceName}. ��������� 0 ��� 90.", "OK");
                        System.Diagnostics.Debug.WriteLine($"ControlPage: Invalid value {value} for {deviceName}");
                        return;
                    }
                    await Task.Run(() => modbusMaster.WriteSingleRegister(1, registerAddress, registerValue));
                }
                System.Diagnostics.Debug.WriteLine($"ControlPage: Successfully controlled device {deviceName}");
            }
            catch (SocketException ex)
            {
                await DisplayAlert("������", $"������ �����������: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"ControlPage: SocketException in ControlDevice: {ex}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("������", $"������ ���������� �����������: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"ControlPage: Error in ControlDevice: {ex}");
            }
            finally
            {
                client?.Close();
                client?.Dispose();
                System.Diagnostics.Debug.WriteLine("ControlPage: Disposed TcpClient in ControlDevice");
            }
        }
    }

    // ���������� ��� ��������� ������ � Grid
    // ������ ��� ��������� ������� � �������� � ��������� Grid
    public static class GridExtensions
    {
        public static T SetGridColumn<T>(this T element, int column) where T : Element
        {
            Grid.SetColumn(element, column);
            return element;
        }

        public static T SetMargin<T>(this T element, Thickness margin) where T : View
        {
            element.Margin = margin;
            return element;
        }
    }
}