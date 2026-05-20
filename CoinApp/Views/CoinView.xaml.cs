using CoinApp.Utilities;
using CoinApp.ViewModels;
using System;
using System.Collections.Generic;
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
using static MaterialDesignThemes.Wpf.Theme;

namespace CoinApp.Views
{
    /// <summary>
    /// Interaction logic for CoinView.xaml
    /// </summary>
    public partial class CoinView : Window
    {

        private string _coinId;

        private CoinViewModel _viewModel;
        public CoinView(string coinId)
        {
            InitializeComponent();
            _coinId = coinId;

            _viewModel = new CoinViewModel(_coinId);
            DataContext = _viewModel; //встановлюю дата контекст для вікна
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
                // Робимо видимою панель завантаження
                LoadingPanel.Visibility = Visibility.Visible;
                ChartContainer.Visibility = Visibility.Collapsed;
                CoinDescriptionContainer.Visibility = Visibility.Collapsed;

                _viewModel.Refresh_data();

                await Task.Delay(2500); // Затримка для демонстрації завантаження

                // Приховуємо панель завантаження
                LoadingPanel.Visibility = Visibility.Collapsed;
                CoinDescriptionContainer.Visibility=Visibility.Visible;
                ChartContainer.Visibility = Visibility.Visible;
            }
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

    }
}
