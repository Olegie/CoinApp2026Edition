using CoinApp.Models;
using CoinApp.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using LiveCharts;
using LiveCharts.Wpf;
using System.Globalization;

namespace CoinApp.ViewModels
{
    public class CoinViewModel : INotifyPropertyChanged
    {
        private const string DefaultHistoryRangeKey = "1M";

        private readonly ApiService _apiService; // Сервіс для взаємодії з API
        private string _coinId; // Ідентифікатор криптовалюти

        // Конструктор класу
        public CoinViewModel(string coinId = null)
        {
            _apiService = new ApiService(); // Ініціалізація сервісу
            _coinId = coinId; // Присвоєння ідентифікатора криптовалюти
            SelectedHistoryRangeKey = DefaultHistoryRangeKey;
            HistorySummary = string.Empty;
            SeriesCollection = new SeriesCollection();
            XAxis = CreateXAxis(Array.Empty<string>());
            YAxis = CreateYAxis();
            LoadCoinData(); // Завантаження даних про криптовалюту
        }

        // Збереження даних про криптовалюту
        private Currency _coin;
        public Currency Coin
        {
            get => _coin;
            private set
            {
                _coin = value; // Присвоєння нового значення
                OnPropertyChanged(); // Оновлення прив'язаних елементів
            }
        }

        // Дані для графіка
        private SeriesCollection _seriesCollection;
        public SeriesCollection SeriesCollection
        {
            get => _seriesCollection;
            private set
            {
                _seriesCollection = value; // Присвоєння нових даних для графіка
                OnPropertyChanged(); // Оновлення прив'язаних елементів
            }
        }

        private Axis _xAxis;
        public Axis XAxis
        {
            get => _xAxis;
            private set
            {
                _xAxis = value; // Присвоєння нових даних для осі X
                OnPropertyChanged(); // Оновлення прив'язаних елементів
            }
        }

        private Axis _yAxis;
        public Axis YAxis
        {
            get => _yAxis;
            private set
            {
                _yAxis = value; // Присвоєння нових даних для осі Y
                OnPropertyChanged(); // Оновлення прив'язаних елементів
            }
        }

        private string _selectedHistoryRangeKey;
        public string SelectedHistoryRangeKey
        {
            get => _selectedHistoryRangeKey;
            private set
            {
                _selectedHistoryRangeKey = value;
                OnPropertyChanged();
            }
        }

        private string _historySummary;
        public string HistorySummary
        {
            get => _historySummary;
            private set
            {
                _historySummary = value;
                OnPropertyChanged();
            }
        }

        private double _xAxisSeparatorStep = 1;
        public double XAxisSeparatorStep
        {
            get => _xAxisSeparatorStep;
            private set
            {
                _xAxisSeparatorStep = value;
                OnPropertyChanged();
            }
        }

        // Метод для завантаження історичних цін
        private async Task LoadHistoricalPricesAsync(string coinId, string rangeKey)
        {
            try
            {
                DateTime endTime = DateTime.UtcNow; // Поточний час
                DateTime startTime = GetHistoryStartTime(endTime, rangeKey);

                // Отримання історичних даних про ціну
                var historicalPrices = await _apiService.GetHistoricalPricesAsync(coinId, startTime, endTime);

                if (historicalPrices != null && historicalPrices.Any())
                {
                    var orderedPrices = historicalPrices
                        .Where(p => p.PriceUsd > 0)
                        .OrderBy(p => p.Time)
                        .ToList();

                    if (!orderedPrices.Any())
                    {
                        SetEmptyHistoricalSeries();
                        return;
                    }

                    var values = orderedPrices.Select(p => (double)p.PriceUsd).ToArray();
                    var labelStep = GetLabelStep(values.Length);
                    var dates = BuildHistoryLabels(orderedPrices, rangeKey, labelStep);

                    // Налаштування серії даних для графіка
                    SeriesCollection = new SeriesCollection
                    {
                        new LineSeries
                        {
                            Title = "Price", // Заголовок серії даних
                            Values = new ChartValues<double>(values), // Значення для графіка
                            StrokeThickness = 3,
                            PointGeometrySize = GetPointGeometrySize(values.Length),
                            LineSmoothness = 0.65
                        }
                    };

                    // Налаштування осі X
                    XAxis = CreateXAxis(dates);
                    XAxisSeparatorStep = labelStep;

                    // Налаштування осі Y
                    YAxis = CreateYAxis();

                    var firstDate = UnixTimeStampToDateTime(orderedPrices.First().Time);
                    var lastDate = UnixTimeStampToDateTime(orderedPrices.Last().Time);
                    HistorySummary = $"{firstDate:d} - {lastDate:d} • {orderedPrices.Count} points";
                }
                else
                {
                    SetEmptyHistoricalSeries();
                }
            }
            catch (Exception ex)
            {
                // Обробка помилок
                MessageBox.Show($"Error loading historical prices: {ex.Message}");
            }
        }

        // Метод для завантаження конкретної валюти
        private async Task LoadCoinDataAsync(string coinId)
        {
            try
            {
                // Отримання даних про конкретну валюту
                Coin = await _apiService.GetCurrencyDetailsAsync(coinId);
            }
            catch (Exception ex)
            {
                // Обробка помилок
                MessageBox.Show($"Error loading coin data: {ex.Message}");
            }
        }

        // Метод для завантаження даних валюти за замовчуванням
        private async Task LoadInitialCoinDataAsync()
        {
            try
            {
                // Отримання списку всіх валют
                var currencies = await _apiService.GetTopCurrenciesAsync();

                if (currencies != null && currencies.Length > 0)
                {
                    // Отримання ідентифікатора першої валюти в списку
                    string firstCoinId = currencies[0].Id;
                    _coinId = firstCoinId;
                    await LoadCoinDataAsync(firstCoinId); // Завантаження даних для першої валюти
                    await LoadHistoricalPricesAsync(firstCoinId, SelectedHistoryRangeKey);
                }
                else
                {
                    Coin = null; // Якщо немає валют, очищення даних
                    SetEmptyHistoricalSeries();
                }
            }
            catch (Exception ex)
            {
                // Обробка помилок
                MessageBox.Show($"Error loading initial coin data: {ex.Message}");
            }
        }

        // Метод для завантаження даних валюти
        private async void LoadCoinData()
        {
            await RefreshDataAsync();
        }

        public async Task RefreshDataAsync()
        {
            if (string.IsNullOrEmpty(_coinId))
            {
                // Якщо ідентифікатор валюти не вказаний, завантажити дані за замовчуванням
                await LoadInitialCoinDataAsync();
            }
            else
            {
                // Якщо ідентифікатор валюти вказаний, завантажити дані для цієї валюти
                await LoadCoinDataAsync(_coinId);
                await LoadHistoricalPricesAsync(_coinId, SelectedHistoryRangeKey);
            }      
        }

        // Метод для перетворення Unix-міток у DateTime
        public DateTime UnixTimeStampToDateTime(long unixTimeStampMillis)
        {
            // Unix timestamp - це кількість мілісекунд, що пройшли з початкової дати
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // Початкова дата (Epoch)
            return epoch.AddMilliseconds(unixTimeStampMillis).ToLocalTime(); // Додавання мілісекунд до Epoch і конвертація у локальний час
        }

        public void Refresh_data()
        {
            LoadCoinData();
        }

        public async Task LoadHistoryRangeAsync(string rangeKey)
        {
            SelectedHistoryRangeKey = NormalizeHistoryRange(rangeKey);

            if (string.IsNullOrEmpty(_coinId))
            {
                _coinId = await GetFirstCurrencyIdAsync();
            }

            if (!string.IsNullOrEmpty(_coinId))
            {
                await LoadHistoricalPricesAsync(_coinId, SelectedHistoryRangeKey);
            }
        }

        private static Axis CreateXAxis(string[] dates)
        {
            return new Axis
            {
                Title = "Date", // Заголовок осі X
                Labels = dates, // Мітки осі X
                Separator = new Separator
                {
                    Step = GetLabelStep(dates.Length)
                }
            };
        }

        private static Axis CreateYAxis()
        {
            return new Axis
            {
                Title = "Price", // Заголовок осі Y
                LabelFormatter = value => value.ToString("C", new CultureInfo("en-US"))//щоб у доларах
            };
        }

        private void SetEmptyHistoricalSeries()
        {
            SeriesCollection = new SeriesCollection();
            XAxis = CreateXAxis(Array.Empty<string>());
            XAxisSeparatorStep = 1;
            YAxis = CreateYAxis();
            HistorySummary = "No historical data";
        }

        private string[] BuildHistoryLabels(List<HistoricalPriceModel> prices, string rangeKey, double labelStep)
        {
            var step = Math.Max(1, (int)Math.Ceiling(labelStep));
            return prices
                .Select((price, index) =>
                    index == 0 || index == prices.Count - 1 || index % step == 0
                        ? FormatHistoryLabel(UnixTimeStampToDateTime(price.Time), rangeKey)
                        : string.Empty)
                .ToArray();
        }

        private static DateTime GetHistoryStartTime(DateTime endTime, string rangeKey)
        {
            return NormalizeHistoryRange(rangeKey) switch
            {
                "7D" => endTime.AddDays(-7),
                "3M" => endTime.AddMonths(-3),
                "1Y" => endTime.AddYears(-1),
                _ => endTime.AddMonths(-1)
            };
        }

        private static string NormalizeHistoryRange(string rangeKey)
        {
            return rangeKey?.ToUpperInvariant() switch
            {
                "7D" => "7D",
                "3M" => "3M",
                "1Y" => "1Y",
                _ => DefaultHistoryRangeKey
            };
        }

        private static string FormatHistoryLabel(DateTime dateTime, string rangeKey)
        {
            return NormalizeHistoryRange(rangeKey) switch
            {
                "7D" => dateTime.ToString("dd/MM HH:mm", CultureInfo.CurrentCulture),
                "1Y" => dateTime.ToString("MMM yy", CultureInfo.CurrentCulture),
                _ => dateTime.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)
            };
        }

        private static double GetPointGeometrySize(int pointCount)
        {
            if (pointCount > 120)
            {
                return 0;
            }

            if (pointCount > 45)
            {
                return 5;
            }

            return 8;
        }

        private static double GetLabelStep(int pointCount)
        {
            return pointCount <= 10 ? 1 : Math.Ceiling(pointCount / 8d);
        }

        public async Task<string> GetFirstCurrencyIdAsync()
        {
            var currencies = await _apiService.GetTopCurrenciesAsync();
            return currencies != null && currencies.Length > 0 ? currencies[0].Id : null;
        }

        // Подія для сповіщення про зміни властивостей
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); // Сповіщення про зміну властивості
        }
    }
}
