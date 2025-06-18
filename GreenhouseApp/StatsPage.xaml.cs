using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace GreenhouseApp
{
    public partial class StatsPage : ContentPage
    {
        private List<Greenhouse> Greenhouses { get; set; }
        private Greenhouse SelectedGreenhouse { get; set; }
        private List<SensorData> SensorDataList { get; set; } = new List<SensorData>();
        private string SelectedPeriod { get; set; } = "Day";

        // Инициализация страницы статистики
        // Загружаем теплицы, данные датчиков и настраиваем Picker
        public StatsPage()
        {
            try
            {
                InitializeComponent();
                LoadGreenhouses();
                LoadSensorData();

                GreenhousePicker.ItemsSource = Greenhouses;
                if (Greenhouses.Any())
                {
                    GreenhousePicker.SelectedIndex = 0;
                    SelectedGreenhouse = Greenhouses[0];
                }
                GreenhousePicker.SelectedIndexChanged += OnPickerSelectedIndexChanged;

                UpdateButtonColors();

                Console.WriteLine($"StatsPage initialized. Greenhouses count: {Greenhouses.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing StatsPage: {ex.Message}");
                DisplayAlert("Ошибка", "Не удалось загрузить страницу статистики. Попробуйте снова.", "OK");
            }
        }

        // Загрузка списка теплиц
        // Читаем теплицы из Preferences, при ошибке создаем пустой список
        private void LoadGreenhouses()
        {
            try
            {
                var greenhousesJson = Preferences.Get("greenhouses", "[]");
                Greenhouses = JsonConvert.DeserializeObject<List<Greenhouse>>(greenhousesJson) ?? new List<Greenhouse>();
                Console.WriteLine($"Loaded {Greenhouses.Count} greenhouses.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading greenhouses: {ex.Message}");
                Greenhouses = new List<Greenhouse>();
            }
        }

        // Загрузка данных с датчиков
        // Читаем данные из Preferences и проверяем на ошибки, например, будущие метки времени
        private void LoadSensorData()
        {
            try
            {
                var sensorDataJson = Preferences.Get("sensor_data", "[]");
                SensorDataList = JsonConvert.DeserializeObject<List<SensorData>>(sensorDataJson) ?? new List<SensorData>();
                Console.WriteLine($"Loaded {SensorDataList.Count} sensor data points.");
                if (SensorDataList.Any())
                {
                    Console.WriteLine($"Sample sensor data: {JsonConvert.SerializeObject(SensorDataList.Take(5), Formatting.Indented)}");
                }

                if (SensorDataList.Any(d => d.Timestamp > DateTime.Now))
                    Console.WriteLine("Warning: Sensor data contains future timestamps.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading sensor data: {ex.Message}");
                SensorDataList = new List<SensorData>();
            }
        }

        // Обработчик изменения выбора теплицы
        // Обновляем выбранную теплицу и перерисовываем графики
        private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var picker = (Picker)sender;
                SelectedGreenhouse = picker.SelectedItem as Greenhouse;
                Console.WriteLine($"Selected greenhouse: {SelectedGreenhouse?.Name ?? "None"}");
                TempCanvas.InvalidateSurface();
                HumidityCanvas.InvalidateSurface();
                SoilCanvas.InvalidateSurface();
                CO2Canvas.InvalidateSurface();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnPickerSelectedIndexChanged: {ex.Message}");
            }
        }

        // Обновление цвета кнопок периода
        // Подсвечиваем активный период и обновляем метку периода
        private void UpdateButtonColors()
        {
            DayButton.BackgroundColor = SelectedPeriod == "Day" ? Color.FromHex("#23B5DE") : Color.FromHex("#A9A9A9");
            WeekButton.BackgroundColor = SelectedPeriod == "Week" ? Color.FromHex("#23B5DE") : Color.FromHex("#A9A9A9");
            MonthButton.BackgroundColor = SelectedPeriod == "Month" ? Color.FromHex("#23B5DE") : Color.FromHex("#A9A9A9");
            PeriodLabel.Text = $"Текущий период: {SelectedPeriod switch { "Day" => "День", "Week" => "Неделя", "Month" => "Месяц", _ => "День" }}";
        }

        // Обработчик кнопки "День"
        // Устанавливаем период "День" и обновляем графики
        private void OnDayButtonClicked(object sender, EventArgs e)
        {
            try
            {
                SelectedPeriod = "Day";
                Console.WriteLine("Selected period: Day");
                UpdateButtonColors();
                TempCanvas.InvalidateSurface();
                HumidityCanvas.InvalidateSurface();
                SoilCanvas.InvalidateSurface();
                CO2Canvas.InvalidateSurface();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnDayButtonClicked: {ex.Message}");
            }
        }

        // Обработчик кнопки "Неделя"
        // Устанавливаем период "Неделя" и обновляем графики
        private void OnWeekButtonClicked(object sender, EventArgs e)
        {
            try
            {
                SelectedPeriod = "Week";
                Console.WriteLine("Selected period: Week");
                UpdateButtonColors();
                TempCanvas.InvalidateSurface();
                HumidityCanvas.InvalidateSurface();
                SoilCanvas.InvalidateSurface();
                CO2Canvas.InvalidateSurface();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnWeekButtonClicked: {ex.Message}");
            }
        }

        // Обработчик кнопки "Месяц"
        // Устанавливаем период "Месяц" и обновляем графики
        private void OnMonthButtonClicked(object sender, EventArgs e)
        {
            try
            {
                SelectedPeriod = "Month";
                Console.WriteLine("Selected period: Month");
                UpdateButtonColors();
                TempCanvas.InvalidateSurface();
                HumidityCanvas.InvalidateSurface();
                SoilCanvas.InvalidateSurface();
                CO2Canvas.InvalidateSurface();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnMonthButtonClicked: {ex.Message}");
            }
        }

        // Фильтрация данных для выбранного периода
        // Возвращаем данные для выбранной теплицы за указанный период
        private List<SensorData> GetFilteredData()
        {
            if (SelectedGreenhouse == null)
                return new List<SensorData>();

            var data = SensorDataList
                .Where(d => d.GreenhouseName == SelectedGreenhouse.Name)
                .OrderBy(d => d.Timestamp)
                .ToList();

            DateTime now = DateTime.Now;
            TimeSpan period = SelectedPeriod switch
            {
                "Day" => TimeSpan.FromDays(1),
                "Week" => TimeSpan.FromDays(7),
                "Month" => TimeSpan.FromDays(30),
                _ => TimeSpan.FromDays(1)
            };

            data = data.Where(d => d.Timestamp >= now - period && d.Timestamp <= now).ToList();

            if (SelectedPeriod == "Week" || SelectedPeriod == "Month")
            {
                data = AggregateDataByDay(data);
            }

            return data;
        }

        // Агрегация данных по дням
        // Усредняем данные для каждого дня, если период "Неделя" или "Месяц"
        private List<SensorData> AggregateDataByDay(List<SensorData> data)
        {
            var grouped = data.GroupBy(d => d.Timestamp.Date)
                .Select(g => new SensorData
                {
                    GreenhouseName = g.First().GreenhouseName,
                    Timestamp = g.Key,
                    Temperature = (int)Math.Round(g.Where(d => d.IsConnected).Select(d => d.Temperature).DefaultIfEmpty(0).Average()),
                    Humidity = (int)Math.Round(g.Where(d => d.IsConnected).Select(d => d.Humidity).DefaultIfEmpty(0).Average()),
                    SoilMoisture = (int)Math.Round(g.Where(d => d.IsConnected).Select(d => d.SoilMoisture).DefaultIfEmpty(0).Average()),
                    CO2 = (int)Math.Round(g.Where(d => d.IsConnected).Select(d => d.CO2).DefaultIfEmpty(0).Average()),
                    IsConnected = g.Any(d => d.IsConnected)
                })
                .OrderBy(d => d.Timestamp)
                .ToList();
            return grouped;
        }

        // Отрисовка графика
        // Рисуем график для заданного параметра (температура, влажность и т.д.) с помощью SkiaSharp
        private void DrawGraph(SKCanvas canvas, SKPaintSurfaceEventArgs args, string metric, Func<SensorData, double> valueSelector, SKColor color, string unit)
        {
            try
            {
                canvas.Clear(SKColors.White);
                using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 16, IsAntialias = true };

                if (SelectedGreenhouse == null)
                {
                    canvas.DrawText("Выберите теплицу", args.Info.Width / 2 - 80, args.Info.Height / 2, textPaint);
                    return;
                }

                var data = GetFilteredData();
                if (!data.Any())
                {
                    canvas.DrawText($"Нет данных за {SelectedPeriod}", args.Info.Width / 2 - 80, args.Info.Height / 2, textPaint);
                    return;
                }

                float width = args.Info.Width;
                float height = args.Info.Height;
                float margin = 45;
                float graphHeight = height - 2 * margin;
                float graphWidth = width - 2 * margin;

                float maxY = data.Where(d => d.IsConnected).Select(d => (float)valueSelector(d)).DefaultIfEmpty(0).Max();
                if (maxY == 0)
                {
                    canvas.DrawText("Нет данных для графика", args.Info.Width / 2 - 80, args.Info.Height / 2, textPaint);
                    return;
                }

                using var axisPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 2, IsAntialias = true };
                canvas.DrawLine(margin, margin, margin, height - margin, axisPaint);
                canvas.DrawLine(margin, height - margin, width - margin, height - margin, axisPaint);

                canvas.DrawText($"{metric} ({unit})", margin - 25, margin + 20, textPaint);

                DateTime minTime = data.Min(d => d.Timestamp);
                DateTime maxTime = data.Max(d => d.Timestamp);
                if (maxTime > DateTime.Now)
                    maxTime = DateTime.Now;
                double timeRange = (maxTime - minTime).TotalSeconds;
                if (timeRange == 0)
                {
                    canvas.DrawText("Недостаточно данных", args.Info.Width / 2 - 80, args.Info.Height / 2, textPaint);
                    return;
                }

                using var tickPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 1, IsAntialias = true };
                int tickCount = SelectedPeriod switch { "Day" => 6, "Week" => 7, "Month" => 5, _ => 6 };
                double totalDays = (maxTime - minTime).TotalDays;
                for (int i = 0; i <= tickCount; i++)
                {
                    float x = margin + (graphWidth * i / tickCount);
                    canvas.DrawLine(x, height - margin, x, height - margin + 5, tickPaint);
                    DateTime tickTime = minTime.AddDays(i * totalDays / tickCount);
                    string timeLabel = SelectedPeriod switch
                    {
                        "Day" => tickTime.ToString("HH:mm"),
                        "Week" => tickTime.ToString("MMM dd"),
                        "Month" => tickTime.ToString("MMM dd"),
                        _ => tickTime.ToString("HH:mm")
                    };
                    canvas.DrawText(timeLabel, x - 20, height - margin + 20, textPaint);
                }

                string xAxisLabel = SelectedPeriod == "Day" ? "Время (ч)" : "Время (дни)";
                canvas.DrawText(xAxisLabel, width / 2 - 30, height - margin + 40, textPaint);

                for (int i = 0; i <= 5; i++)
                {
                    float y = height - margin - (graphHeight * i / 5);
                    canvas.DrawLine(margin - 5, y, margin, y, tickPaint);
                    canvas.DrawText($"{(maxY * i / 5):0.#}", margin - 25, y + 5, textPaint);
                }

                using var dataPaint = new SKPaint { Color = color, StrokeWidth = 2, IsAntialias = true };
                using var offlinePaint = new SKPaint { Color = SKColors.Red, StrokeWidth = 2, IsAntialias = true, PathEffect = SKPathEffect.CreateDash(new float[] { 10, 10 }, 20) };

                for (int i = 1; i < data.Count; i++)
                {
                    var prev = data[i - 1];
                    var curr = data[i];

                    float x1 = margin + graphWidth * (float)((prev.Timestamp - minTime).TotalSeconds / timeRange);
                    float x2 = margin + graphWidth * (float)((curr.Timestamp - minTime).TotalSeconds / timeRange);

                    if (prev.IsConnected && curr.IsConnected)
                    {
                        float y1 = height - margin - graphHeight * (float)(valueSelector(prev) / maxY);
                        float y2 = height - margin - graphHeight * (float)(valueSelector(curr) / maxY);
                        canvas.DrawLine(x1, y1, x2, y2, dataPaint);
                    }
                    else
                    {
                        float y = height - margin;
                        canvas.DrawLine(x1, y, x2, y, offlinePaint);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DrawGraph ({metric}): {ex.Message}");
                using var errorTextPaint = new SKPaint { Color = SKColors.Black, TextSize = 16, IsAntialias = true };
                canvas.DrawText("Ошибка при построении графика", args.Info.Width / 2 - 80, args.Info.Height / 2, errorTextPaint);
            }
        }

        // Отрисовка графика температуры
        // Вызываем общий метод DrawGraph для температуры
        private void OnTempCanvasPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            DrawGraph(args.Surface.Canvas, args, "Температура", d => d.Temperature, SKColors.Red, "°C");
        }

        // Отрисовка графика влажности воздуха
        // Вызываем общий метод DrawGraph для влажности
        private void OnHumidityCanvasPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            DrawGraph(args.Surface.Canvas, args, "Влажность", d => d.Humidity, SKColors.Blue, "%");
        }

        // Отрисовка графика влажности почвы
        // Вызываем общий метод DrawGraph для влажности почвы
        private void OnSoilCanvasPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            DrawGraph(args.Surface.Canvas, args, "Влажность почвы", d => d.SoilMoisture, SKColors.Green, "%");
        }

        // Отрисовка графика CO2
        // Вызываем общий метод DrawGraph для уровня CO2
        private void OnCO2CanvasPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            DrawGraph(args.Surface.Canvas, args, "CO2", d => d.CO2, SKColors.Purple, "ppm");
        }
    }
}