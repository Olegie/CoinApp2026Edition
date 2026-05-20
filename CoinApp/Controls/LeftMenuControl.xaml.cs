using System;
using System.Windows;
using System.Windows.Controls;
using CoinApp.Utilities;
using CoinApp.Views;

namespace CoinApp.Controls
{
    public partial class LeftMenuControl : UserControl
    {
        public LeftMenuControl()
        {
            InitializeComponent();
        }

        private void Main_Button_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(() => new MainWindow());
        }

        private void Coin_Button_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(() => new CoinView(string.Empty));
        }

        private void Markets_Button_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(() => new MarketsView());
        }

        private void Markets_Search_Button_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(() => new MarketSearchView(string.Empty));
        }

        private void Convert_Button_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(() => new ConvertView());
        }

        private void NavigateTo(Func<Window> createWindow)
        {
            var currentWindow = Window.GetWindow(this);

            if (currentWindow == null)
            {
                MessageBox.Show("Window not found.");
                return;
            }

            var nextWindow = createWindow();
            WindowStateManager.Capture(currentWindow);
            WindowStateManager.Apply(nextWindow);

            nextWindow.Show();
            currentWindow.Close();
        }
    }
}
