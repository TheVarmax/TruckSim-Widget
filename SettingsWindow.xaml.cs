using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ETSOverlay
{
    public partial class SettingsWindow : Window
    {
        private MainWindow _mainWindow;
        private bool _suppressEvents = false;
        private bool _isUk = false;

        public SettingsWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            MouseLeftButtonDown += (s, e) => { DragMove(); };
            _suppressEvents = false;

            UpdateLicenseUI();
            LicenseManager.Instance.OnLicenseChanged += () =>
            {
                Dispatcher.Invoke(() => 
                {
                    UpdateLicenseUI();
                    SyncAppearanceValues();
                });
            };
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
            this.BeginAnimation(Window.OpacityProperty, fadeOut);
            await System.Threading.Tasks.Task.Delay(200);
            Hide();
        }

        // --- Tabs Handlers ---

        private void TabGeneralBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (TabGeneralContent != null && TabAppearanceContent != null)
            {
                TabGeneralContent.Visibility = Visibility.Visible;
                TabAppearanceContent.Visibility = Visibility.Collapsed;
            }
        }

        private void TabAppearanceBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (TabGeneralContent != null && TabAppearanceContent != null)
            {
                TabGeneralContent.Visibility = Visibility.Collapsed;
                TabAppearanceContent.Visibility = Visibility.Visible;
                if (!_suppressEvents) SyncAppearanceValues();
            }
        }

        // --- General Event Handlers ---

        private async void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            ApplyAppearanceSettings();
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
            this.BeginAnimation(Window.OpacityProperty, fadeOut);
            await System.Threading.Tasks.Task.Delay(200);
            Hide();
        }

        private void UIModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || _mainWindow == null) return;
            if (UIModeSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _mainWindow.OnUIModeChanged(tag);
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressEvents || _mainWindow == null) return;
            _mainWindow.OnOpacityChanged(OpacitySlider.Value);
            OpacityValue.Text = $"{(int)Math.Round(OpacitySlider.Value)}%";
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || _mainWindow == null) return;
            if (LanguageSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _mainWindow.OnLanguageChanged(tag);
            }
        }

        private void SpeedWarningBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressEvents || _mainWindow == null) return;
            if (int.TryParse(SpeedWarningBox.Text, out var value))
            {
                _mainWindow.OnSpeedWarningChanged(Math.Max(0, value));
            }
        }

        private void SpeedWarningUp_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.OnSpeedWarningUp();
        }

        private void SpeedWarningDown_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.OnSpeedWarningDown();
        }

        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.OnCheckUpdate();
        }

        private void BtnDonate_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.OnDonate();
        }

        private void BtnManageLicense_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LicenseDialog(_mainWindow);
            dialog.ShowDialog();
        }

        public void UpdateLicenseUI()
        {
            var licenseManager = LicenseManager.Instance;
            
            if (licenseManager.Status == "active")
            {
                LicenseSectionTitle.Text = "★ Supporter";
                LicenseSectionTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5C542")); // Gold
                
                string statusText = "🟢 Active";
                if (licenseManager.ExpiresAt.HasValue)
                {
                    string dateStr = licenseManager.ExpiresAt.Value.ToString("dd.MM.yyyy");
                    statusText += _isUk ? $" (до {dateStr})" : $" (until {dateStr})";
                }
                
                LicenseStatusText.Text = statusText;
                LicenseStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#43A047")); // Green
                BtnManageLicense.Content = _isUk ? "Керувати" : "Manage";
            }
            else
            {
                LicenseSectionTitle.Text = _isUk ? "⭐ Стати Supporter" : "⭐ Become a Supporter";
                LicenseSectionTitle.Foreground = new SolidColorBrush(Colors.White);
                LicenseStatusText.Text = "Free";
                LicenseStatusText.Foreground = new SolidColorBrush(Colors.White);
                BtnManageLicense.Content = _isUk ? "Стати Supporter" : "Become a Supporter";
            }
        }

        private void ScaleSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || _mainWindow == null) return;
            if (ScaleSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int scale))
            {
                _mainWindow.OnScaleChanged(scale);
            }
        }

        private void AutoHideToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents || _mainWindow == null) return;
            _mainWindow.OnAutoHideEnabledChanged(true);
        }

        private void AutoHideToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents || _mainWindow == null) return;
            _mainWindow.OnAutoHideEnabledChanged(false);
        }

        // --- Appearance Logic ---

        private void SyncAppearanceValues()
        {
            if (_mainWindow == null || ThemeSelector == null) return;
            _suppressEvents = true;

            var license = LicenseManager.Instance;
            bool isSupporter = license.Status == "active";
            bool canUseThemes = isSupporter || license.HasFeature("appearance.themes");
            bool canUseAccents = isSupporter || license.HasFeature("appearance.accents");
            bool canUseCardStyles = isSupporter || license.HasFeature("appearance.cardStyle");

            PopulateCombo(ThemeSelector, new[] {
                ("classic", "Classic", "Класична", false),
                ("midnight", "Midnight", "Опівнічна", true),
                ("carbon", "Carbon", "Карбон", true),
                ("oled", "OLED Black", "OLED Чорна", true)
            }, _mainWindow.ActiveTheme, canUseThemes);

            PopulateCombo(CardStyleSelector, new[] {
                ("standard", "Standard", "Стандартний", false),
                ("rounded", "Rounded", "Заокруглений", true),
                ("compact", "Compact", "Компактний", true)
            }, _mainWindow.ActiveCardStyle, canUseCardStyles);

            PopulateCombo(AccentModeSelector, new[] {
                ("standard", "Standard", "Стандартний", false),
                ("uniform", "Uniform", "Однаковий", true),
                ("custom", "Custom", "Користувацький", true)
            }, _mainWindow.ActiveAccentMode, canUseAccents);

            PopulateCombo(GlobalAccentSelector, GetAccentOptions(), _mainWindow.ActiveAccent, canUseAccents);

            PopulateCustomAccentCombo(CmbColorSim, "Sim");
            PopulateCustomAccentCombo(CmbColorStatus, "Status");
            PopulateCustomAccentCombo(CmbColorGame, "Game");
            PopulateCustomAccentCombo(CmbColorDistance, "Distance");
            PopulateCustomAccentCombo(CmbColorRoute, "Route");
            PopulateCustomAccentCombo(CmbColorSpeed, "Speed");
            PopulateCustomAccentCombo(CmbColorMax, "Max");
            PopulateCustomAccentCombo(CmbColorType, "Type");

            UpdateAppearanceVisibility();

            _suppressEvents = false;
        }

        private (string id, string en, string uk, bool premium)[] GetAccentOptions()
        {
            return new[] {
                ("teal", "Teal", "Бірюзовий", false),
                ("blue", "Blue", "Синій", true),
                ("amber", "Amber", "Янтарний", true),
                ("violet", "Violet", "Фіолетовий", true),
                ("red", "Red", "Червоний", true)
            };
        }

        private void PopulateCombo(ComboBox combo, (string id, string en, string uk, bool premium)[] items, string activeValue, bool hasFeature)
        {
            combo.Items.Clear();
            foreach (var item in items)
            {
                var cbi = new ComboBoxItem();
                cbi.Tag = item.id;
                
                string text = _isUk ? item.uk : item.en;
                if (item.premium && !hasFeature)
                {
                    cbi.Content = "⭐ " + text;
                    cbi.IsEnabled = false;
                    cbi.ToolTip = _isUk ? "Доступно тільки з підпискою Supporter" : "Available only with Supporter subscription";
                }
                else
                {
                    cbi.Content = text;
                    cbi.IsEnabled = true;
                    cbi.ToolTip = null;
                }
                combo.Items.Add(cbi);

                if (item.id == activeValue)
                {
                    combo.SelectedItem = cbi;
                }
            }

            if (combo.SelectedItem == null && combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private void PopulateCustomAccentCombo(ComboBox combo, string tileKey)
        {
            var options = GetAccentOptions();
            var hasFeature = LicenseManager.Instance.Status == "active" || LicenseManager.Instance.HasFeature("appearance.accents");
            
            string activeValue = "teal";
            if (_mainWindow.SavedCustomAccents != null && _mainWindow.SavedCustomAccents.TryGetValue(tileKey, out var v))
            {
                activeValue = v;
            }

            PopulateCombo(combo, options, activeValue, hasFeature);
        }

        private void AppearanceSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyAppearanceSettings();
        }

        private void AccentMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAppearanceVisibility();
            ApplyAppearanceSettings();
        }

        private void CustomColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyAppearanceSettings();
        }

        private void UpdateAppearanceVisibility()
        {
            if (AccentModeSelector == null || GlobalAccentPanel == null || CustomAccentsPanel == null) return;
            
            var modeItem = AccentModeSelector.SelectedItem as ComboBoxItem;
            string mode = modeItem?.Tag as string ?? "standard";

            if (mode == "custom")
            {
                GlobalAccentPanel.Visibility = Visibility.Collapsed;
                CustomAccentsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                GlobalAccentPanel.Visibility = Visibility.Visible;
                CustomAccentsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyAppearanceSettings()
        {
            if (_suppressEvents || _mainWindow == null) return;

            var license = LicenseManager.Instance;
            bool isSupporter = license.Status == "active";
            bool canUseThemes = isSupporter || license.HasFeature("appearance.themes");
            bool canUseAccents = isSupporter || license.HasFeature("appearance.accents");
            bool canUseCardStyles = isSupporter || license.HasFeature("appearance.cardStyle");

            string theme = canUseThemes ? ((ThemeSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "classic") : _mainWindow.SavedTheme;
            string cardStyle = canUseCardStyles ? ((CardStyleSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "standard") : _mainWindow.SavedCardStyle;
            string accentMode = canUseAccents ? ((AccentModeSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "standard") : _mainWindow.SavedAccentMode;
            string globalAccent = canUseAccents ? ((GlobalAccentSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "teal") : _mainWindow.SavedAccent;

            var customAccents = new Dictionary<string, string>();
            if (accentMode == "custom")
            {
                customAccents["Sim"] = (CmbColorSim.SelectedItem as ComboBoxItem)?.Tag as string ?? "teal";
                customAccents["Status"] = (CmbColorStatus.SelectedItem as ComboBoxItem)?.Tag as string ?? "teal";
                customAccents["Game"] = (CmbColorGame.SelectedItem as ComboBoxItem)?.Tag as string ?? "teal";
                customAccents["Distance"] = (CmbColorDistance.SelectedItem as ComboBoxItem)?.Tag as string ?? "teal";
                customAccents["Route"] = (CmbColorRoute.SelectedItem as ComboBoxItem)?.Tag as string ?? "teal";
                customAccents["Speed"] = (CmbColorSpeed.SelectedItem as ComboBoxItem)?.Tag as string ?? "teal";
                customAccents["Max"] = (CmbColorMax.SelectedItem as ComboBoxItem)?.Tag as string ?? "teal";
                customAccents["Type"] = (CmbColorType.SelectedItem as ComboBoxItem)?.Tag as string ?? "teal";
            }
            else if (!canUseAccents && _mainWindow.SavedAccentMode == "custom")
            {
                // Preserve saved custom accents if they can't change it right now
                foreach(var kvp in _mainWindow.SavedCustomAccents)
                {
                    customAccents[kvp.Key] = kvp.Value;
                }
            }

            _mainWindow.SetAppearance(theme, globalAccent, cardStyle, accentMode, customAccents);
        }

        // --- Public methods for MainWindow to sync state ---

        public void SuppressEvents(bool suppress)
        {
            _suppressEvents = suppress;
        }

        public void UpdateLocalization(bool isUk)
        {
            _isUk = isUk;
            _suppressEvents = true;
            SettingsTitle.Text = isUk ? "НАЛАШТУВАННЯ" : "SETTINGS";
            LanguageLabel.Text = isUk ? "Мова" : "Language";
            OpacityLabel.Text = isUk ? "Прозорість" : "Opacity";
            UIModeLabel.Text = isUk ? "Режим інтерфейсу" : "UI Mode";
            if (UIModeSelector.Items.Count >= 2)
            {
                ((ComboBoxItem)UIModeSelector.Items[0]).Content = isUk ? "Повний" : "Full Interface";
                ((ComboBoxItem)UIModeSelector.Items[1]).Content = isUk ? "Мінімалізм" : "Minimalism";
            }
            SpeedWarningLabel.Text = isUk ? "Поріг швидкості" : "Speed warning";
            ScaleLabel.Text = isUk ? "Масштаб" : "Scale";
            AutoHideLabel.Text = isUk ? "Автоскривання" : "Auto-hide";
            AutoHideHint.Text = isUk
                ? "Використовує лише статус TruckBook. Тимчасово приховує вибраний режим інтерфейсу, коли статус спокійний."
                : "Uses TruckBook status only. Temporarily hides the selected UI mode when status is calm.";
            if (!_mainWindow._isCheckingUpdate)
            {
                BtnCheckUpdate.Content = isUk ? "🔄 Перевірити оновлення" : "🔄 Check for updates";
            }
            BtnDonate.Content = isUk ? "💛 Підтримати" : "💛 Donate";
            
            // Tab localization
            TabGeneralBtn.Content = isUk ? "Загальні" : "General";
            TabAppearanceBtn.Content = isUk ? "Вигляд" : "Appearance";

            // Appearance labels localization
            ThemeLabel.Text = isUk ? "Тема" : "Theme";
            CardStyleLabel.Text = isUk ? "Стиль карток" : "Card Style";
            AccentModeLabel.Text = isUk ? "Режим кольору акценту" : "Accent Mode";
            GlobalAccentLabel.Text = isUk ? "Загальний колір" : "Global Accent";
            CustomAccentsTitle.Text = isUk ? "Кольори плиток" : "Tile Colors";
            AccentModeHint.Text = isUk 
                ? "Однаковий застосовує акцент всюди. Користувацький дозволяє вибрати колір для кожної плитки." 
                : "Uniform applies your accent everywhere. Custom allows picking per tile.";

            LblColorSim.Text = isUk ? "TrucksBook / Симулятор" : "TrucksBook / Sim";
            LblColorStatus.Text = isUk ? "Статус" : "Status";
            LblColorGame.Text = isUk ? "Гра" : "Game";
            LblColorDistance.Text = isUk ? "Відстань" : "Distance";
            LblColorRoute.Text = isUk ? "Маршрут" : "Route";
            LblColorSpeed.Text = isUk ? "Швидкість" : "Speed";
            LblColorMax.Text = isUk ? "Макс. Швидкість" : "Max Speed";
            LblColorType.Text = isUk ? "Тип доставки" : "Delivery Type";

            _suppressEvents = false;
            UpdateLicenseUI();
            SyncAppearanceValues();
        }

        public void SetUIMode(string mode)
        {
            _suppressEvents = true;
            foreach (ComboBoxItem item in UIModeSelector.Items)
            {
                if (item.Tag is string tag && tag == mode)
                {
                    UIModeSelector.SelectedItem = item;
                    break;
                }
            }
            _suppressEvents = false;
        }

        public void SetAutoHideEnabled(bool enabled)
        {
            _suppressEvents = true;
            AutoHideToggle.IsChecked = enabled;
            _suppressEvents = false;
        }

        public void SetOpacity(double value)
        {
            _suppressEvents = true;
            OpacitySlider.Value = value;
            OpacityValue.Text = $"{(int)Math.Round(value)}%";
            _suppressEvents = false;
        }

        public void SetLanguage(string lang)
        {
            _suppressEvents = true;
            foreach (var item in LanguageSelector.Items)
            {
                if (item is ComboBoxItem comboItem && comboItem.Tag is string tag && tag == lang)
                {
                    LanguageSelector.SelectedItem = comboItem;
                    break;
                }
            }
            _suppressEvents = false;
        }

        public void SetSpeedWarningText(string text)
        {
            _suppressEvents = true;
            SpeedWarningBox.Text = text;
            _suppressEvents = false;
        }

        public void SetVersionText(string text)
        {
            VersionLabel.Text = text;
        }

        public void SetScale(int scalePercent)
        {
            _suppressEvents = true;
            foreach (var item in ScaleSelector.Items)
            {
                if (item is ComboBoxItem comboItem && comboItem.Tag is string tag && tag == scalePercent.ToString())
                {
                    ScaleSelector.SelectedItem = comboItem;
                    break;
                }
            }
            _suppressEvents = false;
        }
    }
}
