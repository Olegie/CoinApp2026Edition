using CoinApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CoinApp.Views
{
    /// <summary>
    /// Interaction logic for ConvertView.xaml
    /// </summary>
    public partial class ConvertView : Window
    {
        private bool IsMaximized = false; // Максимізація вікна

        private ConvertViewModel _viewModel;
        public ConvertView()
        {
            InitializeComponent();
            _viewModel = new ConvertViewModel();
            DataContext = _viewModel;
        }


        private void ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.IsDropDownOpen = true;
            }
            else
            {
                MessageBox.Show($"{sender} is not a ComboBox.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.IsDropDownOpen = false;
            }
            else
            {
                MessageBox.Show($"{sender} is not a ComboBox.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Swap_Button_Click(object sender, RoutedEventArgs e)
        {
        
            if (string.IsNullOrEmpty(_viewModel.FirstSelectedCurrencyName) ||
                string.IsNullOrEmpty(_viewModel.SecondSelectedCurrencyName))
            {
                MessageBox.Show("Please select both currencies to swap.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var tempCurrency = _viewModel.FirstSelectedCurrencyName;

            _viewModel.FirstSelectedCurrencyName = _viewModel.SecondSelectedCurrencyName;
            _viewModel.SecondSelectedCurrencyName = tempCurrency;

            try
            {
                //зміна exchangeRate
                await _viewModel.UpdateExchangeRateAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update exchange rate. Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Оновлення полів
                UpdateUIAfterSwap();
            }
        }

        private void UpdateUIAfterSwap()
        {
            // оновлення комбобоксів
            CurrencyComboBox1.SelectedItem = _viewModel.FirstSelectedCurrencyName;
            CurrencyComboBox2.SelectedItem = _viewModel.SecondSelectedCurrencyName;

            // очищення результату
            ResultTextBox.Text = string.Empty;
        }

        private async void GetExchangeRateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_viewModel.FirstSelectedCurrencyName) ||
                string.IsNullOrEmpty(_viewModel.SecondSelectedCurrencyName))
            {
                MessageBox.Show("Please select both currencies.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string amountText = InputAmountTextBox.Text;
            amountText = amountText.Replace(',', '.').Trim();

            if (string.IsNullOrWhiteSpace(amountText))
            {
                MessageBox.Show("Please enter an amount.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            
            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
            {
                MessageBox.Show("Please enter a valid number.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (amount < 0)
            {
                MessageBox.Show("The amount cannot be negative.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _viewModel.UpdateExchangeRateAsync();

            if (_viewModel.ExchangeRate == null)
            {
                MessageBox.Show("Failed to retrieve exchange rate. Please try again later.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            decimal exchangeRate = _viewModel.ExchangeRate.Value;
            decimal result = exchangeRate * amount;
            string fromCurrency = _viewModel.FirstSelectedCurrencyName;
            string toCurrency = _viewModel.SecondSelectedCurrencyName;
            string formattedResult = result.ToString("F6", CultureInfo.InvariantCulture);

            ResultTextBox.Text = $"{amountText} {fromCurrency} = {formattedResult} {toCurrency}";


        }

        // Переміщення вікна при зажатій ЛКМ
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // Максимізація вікна
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (IsMaximized)
                {
                    this.WindowState = WindowState.Normal;
                    this.Width = 1080;
                    this.Height = 720;
                    IsMaximized = false;
                }
                else
                {
                    this.WindowState = WindowState.Maximized;
                    IsMaximized = true;
                }
            }
        }

        // Оновлення вікна
        private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            // Показати панель завантаження
            ConvBorder.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;

            try
            {
                // Оновлення даних у ViewModel
                await _viewModel.UpdateExchangeRateAsync();

                await Task.Delay(2500);
                // Оновлення полів після оновлення даних
                UpdateUIAfterSwap();
            }
            catch (Exception ex)
            {
                // Показати повідомлення про помилку
                MessageBox.Show($"Failed to refresh data. Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Сховати панель завантаження і відновити видимість основного контенту
                LoadingPanel.Visibility = Visibility.Collapsed;
                ConvBorder.Visibility = Visibility.Visible;
            }
        }


    }
}
