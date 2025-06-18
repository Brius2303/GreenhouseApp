using System;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using Code4Bugs.Utils.IO.Modbus;
using System.Net.Sockets;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;

namespace GreenhouseApp
{
    public partial class MainPage : FlyoutPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        // Обработчик клика по пункту "Главная"
        // Переключает на главную страницу и закрывает боковое меню
        private void OnHomeClicked(object sender, EventArgs e)
        {
            Detail = new NavigationPage(new MainPage());
            IsPresented = false;
        }

        // Обработчик клика по пункту "Датчики"
        // Переключает на страницу датчиков и передает список подключенных теплиц
        private void OnSensorsClicked(object sender, EventArgs e)
        {
            Detail = new NavigationPage(new SensorsPage(ConnectedGreenhouses));
            IsPresented = false;
        }

        // Обработчик клика по пункту "Управление"
        // Переключает на страницу управления и передает список подключенных теплиц
        private void OnControlClicked(object sender, EventArgs e)
        {
            Detail = new NavigationPage(new ControlPage(ConnectedGreenhouses));
            IsPresented = false;
        }

        // Обработчик клика по пункту "Статистика"
        // Переключает на страницу статистики
        private void OnStatsClicked(object sender, EventArgs e)
        {
            Detail = new NavigationPage(new StatsPage());
            IsPresented = false;
        }

        // Обработчик клика по пункту "Настройки контроллера"
        // Переключает на страницу настроек контроллера и передает список подключенных теплиц
        private void OnControllerSettingsClicked(object sender, EventArgs e)
        {
            Detail = new NavigationPage(new ControllerSettingsPage(ConnectedGreenhouses));
            IsPresented = false;
        }

        // Обработчик клика по пункту "О программе"
        // Переключает на страницу "О программе"
        private void OnAboutPage(object sender, EventArgs e)
        {
            Detail = new NavigationPage(new AboutPage());
            IsPresented = false;
        }

        // Инициализация при появлении страницы
        // Загружаем теплицы, проверяем подключения и анимируем изображения
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadGreenhouses();
            await CheckAllGreenhousesConnectionsAsync();
            await Task.Delay(50);
            await CreateAndAnimateImages();
        }

        // Сохранение списка теплиц
        // Сериализуем данные теплиц и сохраняем в Preferences
        private async Task SaveGreenhouses()
        {
            if (Greenhouses != null)
            {
                var greenhousesJson = JsonConvert.SerializeObject(Greenhouses);
                Preferences.Set("greenhouses", greenhousesJson);
            }
        }

        // Загрузка списка теплиц
        // Читаем данные теплиц из Preferences, при ошибке создаем пустой список
        private async Task LoadGreenhouses()
        {
            var greenhousesJson = Preferences.Get("greenhouses", "[]");
            Greenhouses = JsonConvert.DeserializeObject<List<Greenhouse>>(greenhousesJson) ?? new List<Greenhouse>();
        }

        // Сохранение списка подключенных теплиц
        // Сериализуем данные подключенных теплиц и сохраняем в Preferences
        private async Task SaveConnectedGreenhouses()
        {
            if (ConnectedGreenhouses != null)
            {
                var connectedJson = JsonConvert.SerializeObject(ConnectedGreenhouses);
                Preferences.Set("ConnectedGreenhouses", connectedJson);
            }
        }

        // Проверка подключения всех теплиц
        // Проверяем каждую теплицу и обновляем список подключенных
        private async Task CheckAllGreenhousesConnectionsAsync()
        {
            ConnectedGreenhouses.Clear();

            foreach (var greenhouse in Greenhouses)
            {
                bool isConnected = await CheckGreenhouseConnection(greenhouse.Ip, greenhouse.Port);
                if (isConnected)
                {
                    ConnectedGreenhouses.Add(greenhouse);
                }
            }

            await SaveConnectedGreenhouses();
        }

        // Создание и анимация изображений на главной странице
        // Располагаем изображения в круге с анимацией и добавляем обработчики нажатий
        private async Task CreateAndAnimateImages()
        {
            MainAbsoluteLayout.Children.Clear();

            string[] imageSources =
            {
                "Resources/Images/control.png",
                "Resources/Images/sensors.png",
                "Resources/Images/stats.png",
                "Resources/Images/settings.png",
                "Resources/Images/about.png"
            };

            Page[] pages =
            {
                new ControlPage(ConnectedGreenhouses),
                new SensorsPage(ConnectedGreenhouses),
                new StatsPage(),
                new ControllerSettingsPage(ConnectedGreenhouses),
                new AboutPage()
            };

            string centralImage = ConnectedGreenhouses.Any()
                ? "Resources/Images/on.png"
                : "Resources/Images/off.png";

            await EnsureLayoutSizeInitialized();
            double centerX = MainAbsoluteLayout.Width / 2;
            double centerY = MainAbsoluteLayout.Height / 2;
            double radius = 150;

            var centralButton = new Image
            {
                Source = ImageSource.FromFile(centralImage),
                WidthRequest = 100,
                HeightRequest = 100,
                Opacity = 1
            };

            var centralTapGestureRecognizer = new TapGestureRecognizer();
            centralTapGestureRecognizer.Tapped += async (s, e) =>
            {
                await centralButton.FadeTo(0.5, 100);
                await Task.Delay(100);
                await centralButton.FadeTo(1, 100);

                await ShowGreenhouseSelectionDialog();
            };
            centralButton.GestureRecognizers.Add(centralTapGestureRecognizer);

            AbsoluteLayout.SetLayoutBounds(centralButton, new Rect(centerX - 60, centerY - 60, 120, 120));
            AbsoluteLayout.SetLayoutFlags(centralButton, AbsoluteLayoutFlags.None);
            MainAbsoluteLayout.Children.Add(centralButton);

            for (int i = 0; i < imageSources.Length; i++)
            {
                var image = new Image
                {
                    Source = ImageSource.FromFile(imageSources[i]),
                    WidthRequest = 100,
                    HeightRequest = 100,
                    Opacity = 1
                };

                var tapGestureRecognizer = new TapGestureRecognizer();
                int index = i;
                tapGestureRecognizer.Tapped += async (s, e) =>
                {
                    await image.FadeTo(0.5, 100);
                    await Task.Delay(100);
                    await image.FadeTo(1, 100);

                    Detail = new NavigationPage(pages[index]);
                    IsPresented = false;
                };
                image.GestureRecognizers.Add(tapGestureRecognizer);

                double angle = 2 * Math.PI * i / imageSources.Length - Math.PI / 2;
                double targetX = centerX + radius * Math.Cos(angle) - image.WidthRequest / 2;
                double targetY = centerY + radius * Math.Sin(angle) - image.HeightRequest / 2;

                AbsoluteLayout.SetLayoutBounds(image, new Rect(centerX - image.WidthRequest / 2, centerY - image.HeightRequest / 2, 100, 100));
                AbsoluteLayout.SetLayoutFlags(image, AbsoluteLayoutFlags.None);
                MainAbsoluteLayout.Children.Add(image);

                var translationTask = image.TranslateTo(targetX - (centerX - 50), targetY - (centerY - 50), 150, Easing.CubicOut);
                var fadeInTask = image.FadeTo(1, 150);

                await Task.WhenAll(translationTask, fadeInTask);
            }

            await MainAbsoluteLayout.FadeTo(1, 150);
        }

        private List<Greenhouse> Greenhouses { get; set; } = new List<Greenhouse>();
        private List<Greenhouse> ConnectedGreenhouses { get; set; } = new List<Greenhouse>();

        // Отображение диалога выбора теплицы
        // Показывает список теплиц с возможностью добавления или удаления
        private async Task ShowGreenhouseSelectionDialog()
        {
            var greenhouseNames = Greenhouses.Select(g =>
            {
                bool isConnected = ConnectedGreenhouses.Any(c => c.Ip == g.Ip && c.Port == g.Port);
                string status = isConnected ? "⭐" : "☆";
                return $"{status} {g.Name}";
            }).ToList();
            greenhouseNames.Add("🟢 Добавить теплицу");
            greenhouseNames.Add("❌ Удалить теплицу");

            string result = await Application.Current.MainPage.DisplayActionSheet(
                "Выберите действие",
                "Отмена",
                null,
                greenhouseNames.ToArray()
            );

            if (result == "Отмена")
                return;

            if (result == "🟢 Добавить теплицу")
            {
                await AddGreenhouse();
            }
            else if (result == "❌ Удалить теплицу")
            {
                await ShowGreenhouseDeletionDialog();
            }
            else
            {
                var selectedGreenhouse = Greenhouses.FirstOrDefault(g => $"{g.Name}" == result.Substring(2));
                if (selectedGreenhouse != null)
                {
                    bool isConnected = await CheckGreenhouseConnection(selectedGreenhouse.Ip, selectedGreenhouse.Port);
                    string connectionMessage = isConnected
                        ? $"Подключено к теплице: {selectedGreenhouse.Name}"
                        : "Не удалось подключиться к теплице.";

                    var centralButton = MainAbsoluteLayout.Children.OfType<Image>().FirstOrDefault();
                    if (centralButton != null)
                    {
                        centralButton.Source = ConnectedGreenhouses.Any()
                            ? "Resources/Images/on.png"
                            : "Resources/Images/off.png";
                    }
                    await Application.Current.MainPage.DisplayAlert("Состояние подключения", connectionMessage, "ОК");

                    if (isConnected && !ConnectedGreenhouses.Any(c => c.Ip == selectedGreenhouse.Ip && c.Port == selectedGreenhouse.Port))
                    {
                        ConnectedGreenhouses.Add(selectedGreenhouse);
                        await SaveConnectedGreenhouses();
                        await CreateAndAnimateImages();
                    }
                    else if (!isConnected)
                    {
                        ConnectedGreenhouses.RemoveAll(c => c.Ip == selectedGreenhouse.Ip && c.Port == selectedGreenhouse.Port);
                        await SaveConnectedGreenhouses();
                    }
                }
            }
        }

        // Отображение диалога удаления теплицы
        // Позволяет выбрать и удалить теплицу из списка
        private async Task ShowGreenhouseDeletionDialog()
        {
            var greenhouseNames = Greenhouses.Select(g => $"{g.Name}").ToList();

            string result = await Application.Current.MainPage.DisplayActionSheet(
                "Выберите теплицу для удаления",
                "Отмена",
                null,
                greenhouseNames.ToArray()
            );

            if (result == "Отмена")
                return;

            var selectedGreenhouse = Greenhouses.FirstOrDefault(g => g.Name == result);
            if (selectedGreenhouse != null)
            {
                Greenhouses.Remove(selectedGreenhouse);
                ConnectedGreenhouses.RemoveAll(c => c.Ip == selectedGreenhouse.Ip && c.Port == selectedGreenhouse.Port);
                await SaveGreenhouses();
                await SaveConnectedGreenhouses();
                await CreateAndAnimateImages();

                await Application.Current.MainPage.DisplayAlert("Удаление", "Теплица удалена", "ОК");
            }
        }

        // Проверка подключения к теплице
        // Пытаемся установить соединение с указанным IP и портом с таймаутом 2 секунды
        private async Task<bool> CheckGreenhouseConnection(string ip, int port)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                var timeoutTask = Task.Delay(2000);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                return completedTask == connectTask && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        // Добавление новой теплицы
        // Запрашивает имя, IP и порт, добавляет теплицу в список
        private async Task AddGreenhouse()
        {
            string name = await Application.Current.MainPage.DisplayPromptAsync("Новая теплица", "Введите название теплицы:");
            if (string.IsNullOrWhiteSpace(name))
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Название теплицы не может быть пустым.", "OK");
                return;
            }

            string ip = await Application.Current.MainPage.DisplayPromptAsync("Новая теплица", "Введите IP-адрес теплицы:");
            if (string.IsNullOrWhiteSpace(ip))
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "IP-адрес не может быть пустым.", "OK");
                return;
            }

            string portInput = await Application.Current.MainPage.DisplayPromptAsync("Новая теплица", "Введите порт теплицы:");
            if (!int.TryParse(portInput, out int port))
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Порт должен быть числом.", "OK");
                return;
            }

            Greenhouses.Add(new Greenhouse
            {
                Name = name,
                Ip = ip,
                Port = port
            });
            await SaveGreenhouses();
            await Application.Current.MainPage.DisplayAlert("Успех", "Теплица добавлена!", "ОК");
        }

        // Проверка существования файла
        // Простая проверка, используется для отладки
        private bool FileExists(string filePath)
        {
            return System.IO.File.Exists(filePath);
        }

        // Ожидание инициализации размеров макета
        // Убеждаемся, что размеры MainAbsoluteLayout определены перед анимацией
        private async Task EnsureLayoutSizeInitialized()
        {
            while (MainAbsoluteLayout.Width == 0 || MainAbsoluteLayout.Height == 0)
            {
                await Task.Delay(10);
            }
        }
    }
}