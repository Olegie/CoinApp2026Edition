using CoinApp.Utilities;
using CoinApp.ViewModels;
using CoinApp.Views;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace CoinApp
{
    /// <summary>
    /// Логіка взаємодії для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel; // Встановлюємо DataContext для вікна
        }

        private async void Show_Top_10_Currencies_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.ShowingTopCurrencies = true;

                try
                {
                    await _viewModel.RefreshDataAsync(); // Очікуємо завершення асинхронної операції
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to refresh data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                Top_Currencies_button_Icon.Visibility = Visibility.Visible;
                All_Currencies_button.Margin = new Thickness(40, 0, 30, 0);
                All_Currencies_button_Icon.Visibility = Visibility.Collapsed;
            }
        }


        private async void Show_All_Currencies_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.ShowingTopCurrencies = false;

                try
                {
                    await _viewModel.RefreshDataAsync(); // Очікуємо завершення асинхронної операції
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to refresh data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                All_Currencies_button_Icon.Visibility = Visibility.Visible;
                All_Currencies_button.Margin = new Thickness(40, 0, 0, 0);
                Top_Currencies_button_Icon.Visibility = Visibility.Collapsed;
            }
        }

        // Максимізація вікна
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowStateManager.ToggleMaximize(this);
            }
        }

        // Переміщення вікна при зажатій ЛКМ
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // Оновлення вікна
        private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                // Робимо видимою панель завантаження, а таблицю навпаки приховуємо
                LoadingPanel.Visibility = Visibility.Visible;
                CurrenciesDataGrid.Visibility = Visibility.Collapsed;

                await _viewModel.RefreshDataAsync();

                await Task.Delay(2500);

                // Робимо видимою таблицю, а панель навпаки приховуємо
                LoadingPanel.Visibility = Visibility.Collapsed;
                CurrenciesDataGrid.Visibility = Visibility.Visible;
            }
        }

        private void Go_To_Coin_Page_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Отримуємо ID валюти з тегу кнопки
                string currencyId = button.Tag as string;

                if (!string.IsNullOrEmpty(currencyId))
                {
                    var coinWin = new CoinView(currencyId);
                    WindowStateManager.Capture(this);
                    WindowStateManager.Apply(coinWin);

                    coinWin.Show();
                    this.Close();
                }
                else
                {
                    // Виводимо повідомлення, якщо ID валюти не знайдено
                    MessageBox.Show("Currency ID not found.");
                }
            }
        }
    }
}
