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
                SetBrush("AppSurfaceBrush", "#0E141B");
                SetBrush("AppPanelBrush", "#162832");
                SetBrush("AppCardBrush", "#121C25");
                SetBrush("AppCardSecondaryBrush", "#16232D");
                SetBrush("AppCardAccentBrush", "#172B31");
                SetBrush("AppInputBrush", "#0F1821");
                SetBrush("AppPopupBrush", "#121C25");
                SetBrush("AppTextBrush", "#F4F7FA");
                SetBrush("AppMutedTextBrush", "#9AA8B5");
                SetBrush("AppBorderBrush", "#2B3D4B");
                SetBrush("AppSeparatorBrush", "#263744");
                SetBrush("AppPrimaryBrush", "#0F766E");
                SetBrush("AppPrimaryHoverBrush", "#159A91");
                SetBrush("AppTopHoverBrush", "#1D2B36");
                SetBrush("AppAccentBrush", "#10B981");
                SetBrush("AppMenuTextBrush", "#F8FAFC");
                SetBrush("AppGridLineBrush", "#243642");
            }
            else
            {
                SetBrush("AppSurfaceBrush", "#F6F8FB");
                SetBrush("AppPanelBrush", "#D8E8F0");
                SetBrush("AppCardBrush", "#FFFFFF");
                SetBrush("AppCardSecondaryBrush", "#F8FBFE");
                SetBrush("AppCardAccentBrush", "#EEF6F7");
                SetBrush("AppInputBrush", "#FFFFFF");
                SetBrush("AppPopupBrush", "#FFFFFF");
                SetBrush("AppTextBrush", "#111827");
                SetBrush("AppMutedTextBrush", "#667085");
                SetBrush("AppBorderBrush", "#D6DEE8");
                SetBrush("AppSeparatorBrush", "#D9E1EA");
                SetBrush("AppPrimaryBrush", "#0F766E");
                SetBrush("AppPrimaryHoverBrush", "#155E75");
                SetBrush("AppTopHoverBrush", "#E9F0F6");
                SetBrush("AppAccentBrush", "#10B981");
                SetBrush("AppMenuTextBrush", "#F8FAFC");
                SetBrush("AppGridLineBrush", "#E7EDF4");
            }
        }

        private static void SetBrush(string key, string color)
        {
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }



    }
}
