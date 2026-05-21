using CoinApp.Utilities;
using CoinApp.ViewModels;
using LiveCharts.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CoinApp.Views
{
    /// <summary>
    /// Interaction logic for CoinView.xaml
    /// </summary>
    public partial class CoinView : Window
    {

        private string _coinId;

        private CoinViewModel _viewModel;
        private bool _isApplyingHistoryBounds;
        private bool _isHistoryLoading;

        public CoinView(string coinId)
        {
            InitializeComponent();
            _coinId = coinId;

            _viewModel = new CoinViewModel(_coinId);
            DataContext = _viewModel; //встановлюю дата контекст для вікна
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            HistoricalChart.AxisX[0].PreviewRangeChanged += HistoryAxis_PreviewRangeChanged;
            UpdateHistoryRangeButtons(_viewModel.SelectedHistoryRangeKey);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CoinViewModel.SeriesCollection))
            {
                Dispatcher.BeginInvoke(new Action(() => ApplyHistoryBounds(resetX: true)), DispatcherPriority.Background);
            }
        }

        private async void Go_To_Market_Search_Page(object sender, RoutedEventArgs e)
        {
            string currencyId = _coinId;

            if (string.IsNullOrEmpty(currencyId))
            {
                // Отримання ID першої валюти з ViewModel, якщо ID валюти не вказано
                currencyId = await _viewModel.GetFirstCurrencyIdAsync();
            }

            if (!string.IsNullOrEmpty(currencyId))
            {
                var marketSearchWin = new MarketSearchView(currencyId);
                WindowStateManager.Capture(this);
                WindowStateManager.Apply(marketSearchWin);

                marketSearchWin.Show();
                this.Close();
            }
            else
            {
                // Виводимо повідомлення, якщо ID валюти не знайдено
                MessageBox.Show("No currency ID found.");
            }
        }

        private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel!=null)
            {
                try
                {
                    // Робимо видимою панель завантаження
                    LoadingPanel.Visibility = Visibility.Visible;
                    ChartContainer.Visibility = Visibility.Collapsed;
                    CoinDescriptionContainer.Visibility = Visibility.Collapsed;

                    await _viewModel.RefreshDataAsync();
                    ResetChartZoom();
                    UpdateHistoryRangeButtons(_viewModel.SelectedHistoryRangeKey);
                }
                finally
                {
                    // Приховуємо панель завантаження
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    CoinDescriptionContainer.Visibility=Visibility.Visible;
                    ChartContainer.Visibility = Visibility.Visible;
                }
            }
        }

        private async void HistoryRange_Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string rangeKey)
            {
                await LoadHistoryRangeAsync(rangeKey);
            }
        }

        private async Task LoadHistoryRangeAsync(string rangeKey)
        {
            if (_isHistoryLoading)
            {
                return;
            }

            _isHistoryLoading = true;
            SetHistoryRangeButtonsEnabled(false);
            HistoryLoadingBar.Visibility = Visibility.Visible;

            try
            {
                await _viewModel.LoadHistoryRangeAsync(rangeKey);
                ResetChartZoom();
                UpdateHistoryRangeButtons(_viewModel.SelectedHistoryRangeKey);
            }
            finally
            {
                HistoryLoadingBar.Visibility = Visibility.Collapsed;
                SetHistoryRangeButtonsEnabled(true);
                _isHistoryLoading = false;
            }
        }

        private void ResetHistoryView_Button_Click(object sender, RoutedEventArgs e)
        {
            ResetChartZoom();
        }

        private void ResetChartZoom()
        {
            ApplyHistoryBounds(resetX: true);

            foreach (var axis in HistoricalChart.AxisY)
            {
                axis.MinValue = double.NaN;
                axis.MaxValue = double.NaN;
            }
        }

        private void HistoryAxis_PreviewRangeChanged(PreviewRangeChangedEventArgs eventArgs)
        {
            if (_isApplyingHistoryBounds || !TryGetHistoryXBounds(out var dataMin, out var dataMax))
            {
                return;
            }

            var previewMin = eventArgs.PreviewMinValue;
            var previewMax = eventArgs.PreviewMaxValue;

            if (double.IsNaN(previewMin) || double.IsNaN(previewMax))
            {
                return;
            }

            var previewRange = previewMax - previewMin;
            var dataRange = dataMax - dataMin;

            if (previewRange > dataRange || previewMin < dataMin || previewMax > dataMax)
            {
                eventArgs.Cancel = true;
            }
        }

        private void ApplyHistoryBounds(bool resetX)
        {
            if (!TryGetHistoryXBounds(out var dataMin, out var dataMax))
            {
                return;
            }

            var axis = HistoricalChart.AxisX[0];

            try
            {
                _isApplyingHistoryBounds = true;
                axis.MinRange = Math.Min(5, Math.Max(1, dataMax - dataMin));
                axis.MaxRange = dataMax - dataMin;

                if (resetX || double.IsNaN(axis.MinValue) || double.IsNaN(axis.MaxValue))
                {
                    axis.MinValue = dataMin;
                    axis.MaxValue = dataMax;
                }
                else
                {
                    ClampCurrentHistoryRange(axis, dataMin, dataMax);
                }
            }
            finally
            {
                _isApplyingHistoryBounds = false;
            }
        }

        private void ClampCurrentHistoryRange(LiveCharts.Wpf.Axis axis, double dataMin, double dataMax)
        {
            var visibleMin = axis.MinValue;
            var visibleMax = axis.MaxValue;

            if (double.IsNaN(visibleMin) || double.IsNaN(visibleMax))
            {
                return;
            }

            var visibleRange = visibleMax - visibleMin;
            var dataRange = dataMax - dataMin;

            if (visibleRange >= dataRange)
            {
                axis.MinValue = dataMin;
                axis.MaxValue = dataMax;
                return;
            }

            if (visibleMin < dataMin)
            {
                axis.MaxValue += dataMin - visibleMin;
                axis.MinValue = dataMin;
            }

            if (axis.MaxValue > dataMax)
            {
                axis.MinValue -= axis.MaxValue - dataMax;
                axis.MaxValue = dataMax;
            }
        }

        private bool TryGetHistoryXBounds(out double dataMin, out double dataMax)
        {
            var pointCount = _viewModel.SeriesCollection?.FirstOrDefault()?.Values?.Count ?? 0;

            dataMin = 0;
            dataMax = Math.Max(0, pointCount - 1);

            return pointCount > 1;
        }

        private void SetHistoryRangeButtonsEnabled(bool isEnabled)
        {
            foreach (var button in GetHistoryRangeButtons())
            {
                button.IsEnabled = isEnabled;
            }
        }

        private void UpdateHistoryRangeButtons(string rangeKey)
        {
            foreach (var button in GetHistoryRangeButtons())
            {
                var isSelected = string.Equals(button.Tag as string, rangeKey, StringComparison.OrdinalIgnoreCase);

                button.SetResourceReference(Button.BackgroundProperty, isSelected ? "AppPrimaryBrush" : "AppInputBrush");
                button.SetResourceReference(Button.ForegroundProperty, isSelected ? "AppMenuTextBrush" : "AppTextBrush");
                button.SetResourceReference(Button.BorderBrushProperty, isSelected ? "AppPrimaryHoverBrush" : "AppBorderBrush");
                button.FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold;
            }
        }

        private IEnumerable<Button> GetHistoryRangeButtons()
        {
            yield return Range7DButton;
            yield return Range1MButton;
            yield return Range3MButton;
            yield return Range1YButton;
        }


        //Максимізація вікна
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowStateManager.ToggleMaximize(this);
            }
        }

        //Переміщення вікна при зажатій ЛКМ
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (HistoricalChart.AxisX.Count > 0)
            {
                HistoricalChart.AxisX[0].PreviewRangeChanged -= HistoryAxis_PreviewRangeChanged;
            }

            base.OnClosed(e);
        }

    }
}
