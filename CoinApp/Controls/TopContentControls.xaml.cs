using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CoinApp.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoinApp.Utilities;
using CoinApp.Views;
using System.Globalization;
using MahApps.Metro.IconPacks;

namespace CoinApp.Controls
{
    public partial class TopContentControls : UserControl
    {
        private readonly ApiService _apiService; // Сервіс для взаємодії з API
        private List<string> _currencyNames = new(); // Список всіх назв валют

        private bool _isLanguagePopupOpen;
        private bool _isThemePopupOpen;

        public TopContentControls()
        {
            InitializeComponent();
            _apiService = new ApiService(); // Ініціалізація ApiService
            LoadCurrencyNames(); // Завантаження назв валют
            Loaded += TopContentControls_Loaded;
        }

        private void TopContentControls_Loaded(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Window window)
            {
                window.StateChanged -= Window_StateChanged;
                window.StateChanged += Window_StateChanged;
                UpdateMaximizeButton(window);
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                UpdateMaximizeButton(window);
            }
        }

        // Асинхронний метод для завантаження назв валют
        private async void LoadCurrencyNames()
        {
            try
            {
                var currencies = await _apiService.GetCurrenciesAsync(); // Отримання списку валют з API
                _currencyNames = currencies
                    .Select(c => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(c.Id))
                    .ToList(); // Отримання імен валют
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load currency names: {ex.Message}"); // Виведення повідомлення про помилку
            }
        }

        // Обробник зміни тексту в TextBox
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (autoCompletePopup == null || autoCompleteListBox == null)
            {
                return;
            }

            var textBox = sender as TextBox ?? searchTextBox;
            string searchText = textBox.Text?.ToLowerInvariant() ?? string.Empty; // Отримання введеного тексту в нижньому регістрі

            if (string.IsNullOrEmpty(searchText))
            {
                autoCompletePopup.IsOpen = false;
            }
            else if (_currencyNames != null)
            {
                var filteredNames = _currencyNames
                    .Where(name => name.ToLower().Contains(searchText)) // Фільтрація списку валют
                    .ToList();

                // Оновлення ListBox з результатами
                autoCompleteListBox.ItemsSource = filteredNames;
                autoCompletePopup.IsOpen = filteredNames.Count > 0; // Відкриття Popup, якщо є результати
            }
        }

        // Обробник вибору елемента в ListBox
        private void AutoCompleteListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (autoCompleteListBox.SelectedItem is string selectedName)
            {
                searchTextBox.Text = selectedName; // Вставка вибраної назви у TextBox
                autoCompletePopup.IsOpen = false; // Закриття Popup
                Go_To_Coin_Page(selectedName); // Перехід на сторінку з інформацією про валюту
            }
        }

        // Обробник отримання фокусу на TextBox
        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (searchTextBox.Text == FindResource("TabSeacrhCurrency") as string)
            {
                searchTextBox.Text = "";
            }
        }

        // Обробник втрати фокусу на TextBox
        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                searchTextBox.Text = FindResource("TabSeacrhCurrency") as string;
            }

            autoCompletePopup.IsOpen = false;
        }

        // Обробник натискання кнопки закриття вікна
        private async void Close_This_Window_Button_Click(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);

            if (window == null) return;

            DoubleAnimation fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.5)) // Налаштування анімації зменшення прозорості
            };

            window.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation); // Запуск анімації

            await Task.Delay(500); // Затримка для завершення анімації

            window.Close(); // Закриття вікна
        }

        // Метод для переходу на сторінку з інформацією про валюту
        private void Go_To_Coin_Page(string selectedName)
        {
            Window window = Window.GetWindow(this);

            if (window == null)
            {
                MessageBox.Show("Window not found."); // Обробка випадку, коли вікно не знайдене
                return;
            }

            var coinWin = new CoinView(selectedName.ToLower());
            WindowStateManager.Capture(window);
            WindowStateManager.Apply(coinWin);

            coinWin.Show();
            window.Close();

        }

        // Обробник натискання кнопки для переходу на сторінку з інформацією про валюту
        private void Go_To_Coin_Page_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(searchTextBox.Text) &&
                searchTextBox.Text != FindResource("TabSeacrhCurrency") as string)
            {
                Go_To_Coin_Page(searchTextBox.Text); // Передача тексту пошуку у CoinView
            }
            else
            {
                MessageBox.Show("Please, input a currency name.");
            }
        }

        private void Language_Button_Click(object sender, RoutedEventArgs e)
        {
            _isLanguagePopupOpen = !_isLanguagePopupOpen;
            _isThemePopupOpen = false;
            ThemePopup.IsOpen = false;
            LanguagePopup.IsOpen = _isLanguagePopupOpen;
        }

        private void LanguageOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string languageCode)
            {
                ChangeLanguage(languageCode);
                LanguagePopup.IsOpen = false;
                _isLanguagePopupOpen = false;
            }
        }

        private void ChangeLanguage(string languageCode)
        {
            var localizationDictionaries = Application.Current.Resources.MergedDictionaries
                .Where(IsLocalizationDictionary)
                .ToList();

            foreach (var dictionary in localizationDictionaries)
            {
                Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }

            var resourceUri = languageCode.ToUpperInvariant() switch
            {
                "UKR" => new Uri("Resources/lang.ukr-UKR.xaml", UriKind.Relative),
                _ => new Uri("Resources/lang.xaml", UriKind.Relative)
            };

            try
            {
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = resourceUri });
                searchTextBox.Text = FindResource("TabSeacrhCurrency") as string;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading resource dictionary: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool IsLocalizationDictionary(ResourceDictionary dictionary)
        {
            var source = dictionary.Source?.OriginalString;
            return source != null && source.Contains("Resources/lang", StringComparison.OrdinalIgnoreCase);
        }

        private void Theme_Button_Click(object sender, RoutedEventArgs e)
        {
            _isThemePopupOpen = !_isThemePopupOpen;
            _isLanguagePopupOpen = false;
            LanguagePopup.IsOpen = false;
            ThemePopup.IsOpen = _isThemePopupOpen;
        }

        private void Minimize_Window_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Window window)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        private void Maximize_Window_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Window window)
            {
                WindowStateManager.ToggleMaximize(window);
                UpdateMaximizeButton(window);
            }
        }

        private void UpdateMaximizeButton(Window window)
        {
            if (window.WindowState == WindowState.Maximized)
            {
                MaximizeIcon.Kind = PackIconMaterialKind.WindowRestore;
                MaximizeButton.ToolTip = FindResource("RestoreWindow");
            }
            else
            {
                MaximizeIcon.Kind = PackIconMaterialKind.WindowMaximize;
                MaximizeButton.ToolTip = FindResource("MaximizeWindow");
            }
        }

        private void ThemeOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string theme)
            {
                ApplyTheme(theme.Equals("Dark", StringComparison.OrdinalIgnoreCase));
                ThemePopup.IsOpen = false;
                _isThemePopupOpen = false;
            }
        }

        private static void ApplyTheme(bool dark)
        {
            if (dark)
            {
                SetBrush("AppSurfaceBrush", "#101820");
                SetBrush("AppPanelBrush", "#20384A");
                SetBrush("AppCardBrush", "#1B2733");
                SetBrush("AppCardSecondaryBrush", "#162634");
                SetBrush("AppCardAccentBrush", "#22394B");
                SetBrush("AppInputBrush", "#111C26");
                SetBrush("AppPopupBrush", "#17232E");
                SetBrush("AppTextBrush", "#EEF4F8");
                SetBrush("AppMutedTextBrush", "#A8B3BE");
                SetBrush("AppBorderBrush", "#3E5364");
                SetBrush("AppSeparatorBrush", "#31495A");
                SetBrush("AppPrimaryBrush", "#245F82");
                SetBrush("AppPrimaryHoverBrush", "#317AA5");
                SetBrush("AppTopHoverBrush", "#253645");
                SetBrush("AppAccentBrush", "#E07A45");
                SetBrush("AppMenuTextBrush", "#F1F7FB");
                SetBrush("AppGridLineBrush", "#3E5364");
            }
            else
            {
                SetBrush("AppSurfaceBrush", "#F0F8FF");
                SetBrush("AppPanelBrush", "#71A0BF");
                SetBrush("AppCardBrush", "#E3E3DE");
                SetBrush("AppCardSecondaryBrush", "#D9EFFC");
                SetBrush("AppCardAccentBrush", "#BEE3FA");
                SetBrush("AppInputBrush", "#FFFFFF");
                SetBrush("AppPopupBrush", "#FFFFFF");
                SetBrush("AppTextBrush", "#121518");
                SetBrush("AppMutedTextBrush", "#6E7783");
                SetBrush("AppBorderBrush", "#999FA8");
                SetBrush("AppSeparatorBrush", "#111111");
                SetBrush("AppPrimaryBrush", "#28668F");
                SetBrush("AppPrimaryHoverBrush", "#1D4866");
                SetBrush("AppTopHoverBrush", "#DCDCDC");
                SetBrush("AppAccentBrush", "#CC5500");
                SetBrush("AppMenuTextBrush", "#ECE5FF");
                SetBrush("AppGridLineBrush", "#808080");
            }
        }

        private static void SetBrush(string key, string color)
        {
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }



    }
}
