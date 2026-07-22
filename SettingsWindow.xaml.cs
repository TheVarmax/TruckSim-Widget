using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using Microsoft.Win32;

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
                    UpdateCloudTab();
                    if (TabLogbookContent != null && TabLogbookContent.Visibility == Visibility.Visible)
                    {
                        LoadLogbookTrips();
                    }
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
            if (TabGeneralContent != null && TabAppearanceContent != null && TabCloudContent != null && TabLogbookContent != null)
            {
                TabGeneralContent.Visibility = Visibility.Visible;
                TabAppearanceContent.Visibility = Visibility.Collapsed;
                TabCloudContent.Visibility = Visibility.Collapsed;
                TabLogbookContent.Visibility = Visibility.Collapsed;
            }
        }

        private void TabAppearanceBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (TabGeneralContent != null && TabAppearanceContent != null && TabCloudContent != null && TabLogbookContent != null)
            {
                TabGeneralContent.Visibility = Visibility.Collapsed;
                TabAppearanceContent.Visibility = Visibility.Visible;
                TabCloudContent.Visibility = Visibility.Collapsed;
                TabLogbookContent.Visibility = Visibility.Collapsed;
                if (!_suppressEvents) SyncAppearanceValues();
            }
        }

        private void TabCloudBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (TabGeneralContent != null && TabAppearanceContent != null && TabCloudContent != null && TabLogbookContent != null)
            {
                TabGeneralContent.Visibility = Visibility.Collapsed;
                TabAppearanceContent.Visibility = Visibility.Collapsed;
                TabCloudContent.Visibility = Visibility.Visible;
                TabLogbookContent.Visibility = Visibility.Collapsed;
                UpdateCloudTab();
            }
        }

        private void TabLogbookBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (TabGeneralContent != null && TabAppearanceContent != null && TabCloudContent != null && TabLogbookContent != null)
            {
                TabGeneralContent.Visibility = Visibility.Collapsed;
                TabAppearanceContent.Visibility = Visibility.Collapsed;
                TabCloudContent.Visibility = Visibility.Collapsed;
                TabLogbookContent.Visibility = Visibility.Visible;
                LoadLogbookTrips();
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
            if (CheckPremiumSelection((ComboBox)sender, e)) return;
            if (_suppressEvents || _mainWindow == null) return;
            if (UIModeSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (CustomModeConfigBtn != null)
                {
                    CustomModeConfigBtn.Visibility = (tag == "custom") ? Visibility.Visible : Visibility.Collapsed;
                }
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

        private void CustomModeConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;
            var w = new VisibilitySettingsWindow(_mainWindow);
            w.Owner = this;
            w.ShowDialog();
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
                string planName = string.IsNullOrEmpty(licenseManager.CurrentPlan) 
                    ? "Supporter" 
                    : char.ToUpper(licenseManager.CurrentPlan[0]) + licenseManager.CurrentPlan.Substring(1);

                LicenseSectionTitle.Text = $"★ {planName}";
                LicenseSectionTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5C542")); // Gold
                
                string statusText = _isUk ? "🟢 Активна" : "🟢 Active";
                if (licenseManager.ExpiresAt.HasValue)
                {
                    string dateStr = licenseManager.ExpiresAt.Value.ToString("dd.MM.yyyy");
                    statusText += _isUk ? $" (до {dateStr})" : $" (until {dateStr})";
                }
                else
                {
                    statusText += " (Lifetime)";
                }
                
                LicenseStatusText.Text = statusText;
                LicenseStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#43A047")); // Green
                BtnManageLicense.Content = _isUk ? "Керувати" : "Manage";
            }
            else
            {
                LicenseSectionTitle.Text = _isUk ? "⭐ Стати Supporter" : "⭐ Become a Supporter";
                LicenseSectionTitle.Foreground = new SolidColorBrush(Colors.White);
                LicenseStatusText.Foreground = new SolidColorBrush(Colors.White);
                BtnManageLicense.Content = _isUk ? "Стати Supporter" : "Become a Supporter";
            }
            
            SyncGeneralValues();
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

        private void SyncGeneralValues()
        {
            if (_mainWindow == null || UIModeSelector == null) return;
            _suppressEvents = true;

            var license = LicenseManager.Instance;
            bool isSupporter = license.Status == "active";

            string activeMode = "full";
            if (UIModeSelector.SelectedItem is ComboBoxItem cbi && cbi.Tag is string currentSelection)
            {
                activeMode = currentSelection;
            }

            PopulateCombo(UIModeSelector, new[] {
                ("full", "Full Interface", "Повний інтерфейс", false),
                ("minimal", "Minimalism", "Мінімалізм", false),
                ("custom", "Custom", "Кастомний", true)
            }, activeMode, isSupporter);

            if (UIModeSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (CustomModeConfigBtn != null)
                {
                    CustomModeConfigBtn.Visibility = (tag == "custom") ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            _suppressEvents = false;
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
                ("rounded", "Rounded", "Заокруглений", true)
            }, _mainWindow.ActiveCardStyle, canUseCardStyles);

            PopulateCombo(AccentModeSelector, new[] {
                ("standard", "Standard", "Стандартний", false),
                ("uniform", "Uniform", "Однаковий", true),
                ("custom", "Custom", "Користувацький", true)
            }, _mainWindow.ActiveAccentMode, canUseAccents);

            PopulateCombo(GlobalAccentSelector, GetAccentOptions(), _mainWindow.ActiveAccent, canUseAccents);


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
                    cbi.ToolTip = _isUk ? "Доступно тільки з підпискою Supporter" : "Available only with Supporter subscription";
                }
                else
                {
                    cbi.Content = text;
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

        private int _toastId = 0;
        private bool CheckPremiumSelection(ComboBox comboBox, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return false;
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item && item.Content?.ToString().StartsWith("⭐") == true)
            {
                _suppressEvents = true;
                if (e.RemovedItems.Count > 0)
                {
                    comboBox.SelectedItem = e.RemovedItems[0];
                }
                else
                {
                    comboBox.SelectedIndex = 0;
                }
                _suppressEvents = false;
                ShowToast(_isUk ? "Потрібна підписка Supporter" : "Requires Supporter subscription");
                return true;
            }
            return false;
        }

        private async void ShowToast(string message)
        {
            ToastText.Text = message;
            ToastOverlay.Visibility = Visibility.Visible;
            
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromSeconds(0.2));
            var slideUp = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromSeconds(0.2))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            
            ToastOverlay.BeginAnimation(OpacityProperty, fadeIn);
            ToastTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);

            int currentId = ++_toastId;
            await System.Threading.Tasks.Task.Delay(3000);
            if (currentId != _toastId) return;

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
            var slideDown = new System.Windows.Media.Animation.DoubleAnimation(10, TimeSpan.FromSeconds(0.2));
            
            fadeOut.Completed += (s, ev) => 
            {
                if (currentId == _toastId) ToastOverlay.Visibility = Visibility.Collapsed;
            };
            
            ToastOverlay.BeginAnimation(OpacityProperty, fadeOut);
            ToastTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideDown);
        }



        private void AppearanceSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CheckPremiumSelection((ComboBox)sender, e)) return;
            ApplyAppearanceSettings();
        }

        private void AccentMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CheckPremiumSelection((ComboBox)sender, e)) return;
            UpdateAppearanceVisibility();
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

        private void BtnCustomizeTileColors_Click(object sender, RoutedEventArgs e)
        {
            var license = LicenseManager.Instance;
            bool canUseAccents = license.Status == "active" || license.HasFeature("appearance.accents");
            
            if (!canUseAccents)
            {
                // Optionally show a premium message
                return;
            }

            var dialog = new CustomColorsWindow(_mainWindow.SavedCustomAccents, _isUk)
            {
                Owner = this
            };

            dialog.OnPreviewColors += (previewColors) =>
            {
                var activeTheme = _mainWindow.ActiveTheme;
                var activeAccent = _mainWindow.ActiveAccent;
                var activeCardStyle = _mainWindow.ActiveCardStyle;
                var activeAccentMode = _mainWindow.ActiveAccentMode;
                ThemeManager.Instance.ApplyTheme(activeTheme, activeAccent, activeCardStyle, activeAccentMode, previewColors);
            };
            
            dialog.ShowDialog();
            
            if (dialog.IsSaved)
            {
                _mainWindow.SavedCustomAccents = dialog.FinalColors;
                ApplyAppearanceSettings();
            }
            else
            {
                // Restore original appearance if cancelled
                ApplyAppearanceSettings();
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
                foreach(var kvp in _mainWindow.SavedCustomAccents)
                {
                    customAccents[kvp.Key] = kvp.Value;
                }
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
            if (CustomModeConfigBtn != null) CustomModeConfigBtn.Content = isUk ? "Змінити" : "Change";
            
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


            // Cloud Tab localization
            TabCloudBtn.Content = isUk ? "Хмара" : "Cloud";
            TabLogbookBtn.Content = isUk ? "Журнал" : "Logbook";
            CloudSyncTitle.Text = isUk ? "Хмарна синхронізація" : "Cloud Sync";
            CloudStatusLabel.Text = isUk ? "Статус:" : "Status:";
            CloudEnableLabel.Text = isUk ? "Увімкнути хмарну синхронізацію" : "Enable Cloud Sync";
            CloudSyncHint.Text = isUk
                ? "Якщо увімкнено, налаштування віджета зберігаються у ваш профіль TruckSim Cloud."
                : "When enabled, widget settings are saved to your TruckSim Cloud profile.";
            BtnCloudSyncNow.Content = isUk ? "Синхронізувати зараз" : "Sync now";
            BtnCloudDownload.Content = isUk ? "Завантажити з хмари" : "Download from cloud";
            BtnCloudUpload.Content = isUk ? "Вивантажити з цього ПК" : "Upload this PC";
            BtnCloudDelete.Content = isUk ? "Видалити хмарну копію" : "Delete cloud backup";

            _suppressEvents = false;
            UpdateLicenseUI();
            SyncAppearanceValues();
            UpdateCloudTab();
        }

        public void SetUIMode(string mode)
        {
            _suppressEvents = true;
            foreach (ComboBoxItem item in UIModeSelector.Items)
            {
                if (item.Tag is string tag && tag == mode)
                {
                    UIModeSelector.SelectedItem = item;
                    if (CustomModeConfigBtn != null)
                    {
                        CustomModeConfigBtn.Visibility = (tag == "custom") ? Visibility.Visible : Visibility.Collapsed;
                    }
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

        // --- Cloud Sync ---
        public void UpdateCloudTab()
        {
            var licenseManager = LicenseManager.Instance;
            bool isSupporter = licenseManager.Status == "active";

            CloudSyncToggle.IsEnabled = false;
            BtnCloudSyncNow.IsEnabled = false;
            BtnCloudDownload.IsEnabled = false;
            BtnCloudUpload.IsEnabled = false;
            BtnCloudDelete.IsEnabled = false;

            if (!isSupporter)
            {
                CloudStatusValue.Text = _isUk ? "Доступно для Supporter" : "Available for Supporter";
                CloudStatusValue.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8F98"));
                return;
            }

            if (!licenseManager.HasValidatedThisSession && licenseManager.GetFeaturesList().Count == 0)
            {
                CloudStatusValue.Text = _isUk ? "Перевіряємо можливості ліцензії..." : "Checking license features...";
                CloudStatusValue.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8F98"));
                return;
            }

            bool hasCloud = licenseManager.HasFeature("cloud_sync");

            if (!hasCloud)
            {
                CloudStatusValue.Text = _isUk ? "Cloud Sync не входить до цієї ліцензії" : "Cloud Sync is not included in this license";
                CloudStatusValue.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8F98"));
                return;
            }

            CloudSyncToggle.IsEnabled = true;

            _suppressEvents = true;
            CloudSyncToggle.IsChecked = _mainWindow.CloudSyncEnabled;
            _suppressEvents = false;

            if (!_mainWindow.CloudSyncEnabled)
            {
                CloudStatusValue.Text = _isUk ? "Готово" : "Ready";
                CloudStatusValue.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8F98"));
                BtnCloudSyncNow.IsEnabled = false;
            }
            else
            {
                if (_mainWindow.CloudSyncStatus == "Conflict")
                {
                    CloudStatusValue.Text = _isUk ? "Конфлікт" : "Conflict";
                    CloudStatusValue.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E57373"));
                }
                else if (_mainWindow.CloudSyncStatus == "Cloud unavailable" || licenseManager.LastValidationFailed)
                {
                    CloudStatusValue.Text = _isUk ? "Хмара недоступна" : "Cloud unavailable";
                    CloudStatusValue.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E57373"));
                }
                else
                {
                    CloudStatusValue.Text = _isUk ? "Увімкнено" : "Enabled";
                    CloudStatusValue.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAE50"));
                }
                
                BtnCloudSyncNow.IsEnabled = true;
            }
            
            BtnCloudDownload.IsEnabled = true;
            BtnCloudUpload.IsEnabled = true;
            BtnCloudDelete.IsEnabled = true;
        }

        private async void CloudSyncToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            
            _mainWindow.SetCloudSyncEnabled(true);
            UpdateCloudTab();
            
            // Start upload/download flow silently. The MainWindow InitializeCloudSyncAsync does this.
        }

        private void CloudSyncToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            _mainWindow.SetCloudSyncEnabled(false);
            UpdateCloudTab();
        }

        private void BtnCloudSyncNow_Click(object sender, RoutedEventArgs e)
        {
            BtnCloudSyncNow.IsEnabled = false;
            _ = _mainWindow.UploadCloudSyncAsync(false);
        }

        private void BtnCloudDownload_Click(object sender, RoutedEventArgs e)
        {
            BtnCloudDownload.IsEnabled = false;
            _ = _mainWindow.DownloadCloudSyncAsync();
        }

        private void BtnCloudUpload_Click(object sender, RoutedEventArgs e)
        {
            BtnCloudUpload.IsEnabled = false;
            _ = _mainWindow.UploadCloudSyncAsync(false);
        }

        private async void BtnCloudDelete_Click(object sender, RoutedEventArgs e)
        {
            string title = _isUk ? "Видалення з хмари" : "Delete from cloud";
            string body = _isUk 
                ? "Видалити хмарну копію? Це видалить лише копію в хмарі. Локальні налаштування на цьому ПК залишаться без змін." 
                : "Delete cloud backup? This removes only the cloud copy. Local settings on this PC will stay unchanged.";
            string yesBtn = _isUk ? "Так" : "Yes";
            string noBtn = _isUk ? "Ні" : "Cancel";

            var result = CustomMessageBox.Show(this, body, title, yesBtn, noBtn);
            if (result == MessageBoxResult.Yes)
            {
                BtnCloudDelete.IsEnabled = false;
                await _mainWindow.DeleteCloudSyncAsync();
            }
        }

        public void SyncFromCloud()
        {
            _suppressEvents = true;
            SetUIMode(_mainWindow.ExportCloudSyncSettings().UiMode);
            OpacitySlider.Value = _mainWindow.ExportCloudSyncSettings().WindowOpacity * 100;
            OpacityValue.Text = $"{(int)Math.Round(OpacitySlider.Value)}%";
            SetLanguage(_mainWindow.ExportCloudSyncSettings().UiLanguage);
            SetAutoHideEnabled(_mainWindow.ExportCloudSyncSettings().AutoHideEnabled);
            SetScale(_mainWindow.ExportCloudSyncSettings().UiScale);
            // Ignore speed warnings to avoid complex logic if gametype differs
            SyncAppearanceValues();
            _suppressEvents = false;
            UpdateCloudTab();
        }

        // --- Logbook Logic ---

        private List<TripRecord> _currentTrips = new();

        private void LoadLogbookTrips()
        {
            var license = LicenseManager.Instance;
            bool isSupporter = license.Status == "active";

            _currentTrips = TripLogbookService.Instance.LoadTrips(isSupporter ? null : 5);

            LogbookTripsList.Children.Clear();
            LogbookDetailView.Visibility = Visibility.Collapsed;
            LogbookListView.Visibility = Visibility.Visible;

            LogbookTitleLabel.Text = _isUk ? $"Останні рейси ({_currentTrips.Count})" : $"Recent Trips ({_currentTrips.Count})";

            if (_currentTrips.Count == 0)
            {
                LogbookEmptyText.Visibility = Visibility.Visible;
                LogbookEmptyText.Text = _isUk ? "Ще немає записаних рейсів." : "No trips recorded yet.";
            }
            else
            {
                LogbookEmptyText.Visibility = Visibility.Collapsed;
                foreach (var trip in _currentTrips)
                {
                    LogbookTripsList.Children.Add(CreateTripCard(trip));
                }
            }

            if (!isSupporter && _currentTrips.Count > 0)
            {
                LogbookFreeLimitText.Visibility = Visibility.Visible;
                LogbookFreeLimitText.Text = _isUk 
                    ? "Показано останні 5 рейсів. Supporter відкриває всю історію."
                    : "Showing last 5 trips. Supporter unlocks full history.";
            }
            else
            {
                LogbookFreeLimitText.Visibility = Visibility.Collapsed;
            }
        }

        private UIElement CreateTripCard(TripRecord trip)
        {
            var border = new Border
            {
                Background = (Brush)FindResource("CardBackgroundBrush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var routeBlock = new TextBlock
            {
                Text = $"{_mainWindow.GetLocalizedCity(trip.Origin).ToUpper()} → {_mainWindow.GetLocalizedCity(trip.Destination).ToUpper()}",
                Foreground = (Brush)FindResource("AccentColorBrush"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(routeBlock, 0);
            grid.Children.Add(routeBlock);

            var cargoBlock = new TextBlock
            {
                Text = string.IsNullOrEmpty(trip.CargoName) ? (_isUk ? "Вантаж відсутній" : "No cargo") : trip.CargoName,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0B0B0")),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(cargoBlock, 1);
            grid.Children.Add(cargoBlock);

            string distUnit = _mainWindow.UseMiles ? (_isUk ? "миль" : "mi") : (_isUk ? "км" : "km");
            var statsBlock = new TextBlock
            {
                Text = $"{(_mainWindow.UseMiles ? trip.DistanceKm * 0.621371f : trip.DistanceKm):F0} {distUnit}  •  {trip.Duration.Hours:D2}:{trip.Duration.Minutes:D2}  •  {trip.EndTimeUtc.ToLocalTime():dd.MM.yyyy HH:mm}",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8F98")),
                FontSize = 11
            };
            Grid.SetRow(statsBlock, 2);
            grid.Children.Add(statsBlock);

            border.Child = grid;

            border.MouseLeftButtonUp += (s, e) => ShowTripDetail(trip);
            border.MouseEnter += (s, e) => border.Background = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)); // Very light highlight
            border.MouseLeave += (s, e) => border.Background = (Brush)FindResource("CardBackgroundBrush");

            return border;
        }

        private void ShowTripDetail(TripRecord trip)
        {
            if (trip == null) return;

            var license = LicenseManager.Instance;
            bool isSupporter = license.Status == "active";

            LogbookListView.Visibility = Visibility.Collapsed;
            LogbookDetailView.Visibility = Visibility.Visible;

            LogbookDetailTitle.Text = _isUk ? "Деталі рейсу" : "Trip Details";
            LogbookDetailPanel.Children.Clear();
            
            bool useMiles = trip.GameType == "ATS";

            string distUnit = useMiles ? (_isUk ? "миль" : "mi") : (_isUk ? "км" : "km");
            
            // Free tier details
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Маршрут:" : "Route:", $"{_mainWindow.GetLocalizedCity(trip.Origin)} → {_mainWindow.GetLocalizedCity(trip.Destination)}"));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Вантаж:" : "Cargo:", string.IsNullOrEmpty(trip.CargoName) ? "-" : trip.CargoName));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Відстань:" : "Distance:", $"{(useMiles ? trip.DistanceKm * 0.621371f : trip.DistanceKm):F1} {distUnit}"));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Час в дорозі:" : "Duration:", $"{trip.Duration.Hours:D2}:{trip.Duration.Minutes:D2}:{trip.Duration.Seconds:D2}"));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Початок:" : "Started:", trip.StartTimeUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss")));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Завершено:" : "Completed:", trip.EndTimeUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss")));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Гра:" : "Game:", trip.GameType));

            string speedUnit = useMiles ? (_isUk ? "миль/год" : "mph") : (_isUk ? "км/год" : "km/h");
            
            // Convert to gallons if UseMiles is true, otherwise keep Liters.
            float fuelVol = useMiles ? trip.TotalFuelConsumedL / 3.78541f : trip.TotalFuelConsumedL;
            string volUnit = useMiles ? (_isUk ? "гал" : "gal") : (_isUk ? "л" : "L");
            
            // If UseMiles is true, calculate gal/100mi. Else L/100km.
            float fuelCons = useMiles ? (fuelVol / (trip.DistanceKm / 1.60934f)) * 100f : trip.AvgFuelConsumptionLPer100Km;
            string consUnit = useMiles ? (_isUk ? "гал/100миль" : "gal/100mi") : (_isUk ? "л/100км" : "L/100km");

            // Supporter tier details
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Дохід:" : "Income:", isSupporter ? $"€{trip.Income:N0}" : "🔒 •••••", !isSupporter));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Сер. швидкість:" : "Avg Speed:", isSupporter ? $"{trip.AverageSpeedKmh / (useMiles ? 1.60934f : 1f):F0} {speedUnit}" : "🔒 •••••", !isSupporter));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Макс. швидкість:" : "Max Speed:", isSupporter ? $"{trip.MaxSpeedKmh / (useMiles ? 1.60934f : 1f):F0} {speedUnit}" : "🔒 •••••", !isSupporter));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Витрачено палива:" : "Fuel Consumed:", isSupporter ? $"{fuelVol:F1} {volUnit}" : "🔒 •••••", !isSupporter));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Сер. витрата:" : "Avg Consumption:", isSupporter ? $"{fuelCons:F1} {consUnit}" : "🔒 •••••", !isSupporter));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Пошкодження вантажівки:" : "Truck Damage:", isSupporter ? $"{trip.TruckDamagePercent:F1}%" : "🔒 •••••", !isSupporter));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Пошкодження причепа:" : "Trailer Damage:", isSupporter ? $"{trip.TrailerDamagePercent:F1}%" : "🔒 •••••", !isSupporter));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Пошкодження вантажу:" : "Cargo Damage:", isSupporter ? $"{trip.CargoDamagePercent:F1}%" : "🔒 •••••", !isSupporter));
            LogbookDetailPanel.Children.Add(CreateDetailRow(_isUk ? "Вантажівка:" : "Truck:", isSupporter ? $"{trip.TruckBrand} {trip.TruckName}" : "🔒 •••••", !isSupporter));

            LogbookExportGrid.Visibility = isSupporter ? Visibility.Visible : Visibility.Collapsed;
        }

        private UIElement CreateDetailRow(string labelText, string valueText, bool isLocked = false)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = labelText,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0B0B0")),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var valBlock = new TextBlock
            {
                Text = valueText,
                Foreground = isLocked ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8F98")) : Brushes.White,
                FontSize = 13,
                FontWeight = isLocked ? FontWeights.Normal : FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);

            return grid;
        }

        private void BtnLogbookBack_Click(object sender, RoutedEventArgs e)
        {
            LogbookDetailView.Visibility = Visibility.Collapsed;
            LogbookListView.Visibility = Visibility.Visible;
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"TripLogbook_Export_{DateTime.Now:yyyyMMdd}.csv",
                Title = _isUk ? "Експорт в CSV" : "Export to CSV"
            };

            if (sfd.ShowDialog() == true)
            {
                TripLogbookService.Instance.ExportToCsv(_currentTrips, sfd.FileName);
                CustomMessageBox.Show(this, _isUk ? "Дані успішно експортовано." : "Data exported successfully.", _isUk ? "Експорт" : "Export", "OK", "");
            }
        }

        private void BtnExportJson_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = $"TripLogbook_Export_{DateTime.Now:yyyyMMdd}.json",
                Title = _isUk ? "Експорт в JSON" : "Export to JSON"
            };

            if (sfd.ShowDialog() == true)
            {
                TripLogbookService.Instance.ExportToJson(_currentTrips, sfd.FileName);
                CustomMessageBox.Show(this, _isUk ? "Дані успішно експортовано." : "Data exported successfully.", _isUk ? "Експорт" : "Export", "OK", "");
            }
        }
    }
}
