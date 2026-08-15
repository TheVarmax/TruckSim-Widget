using System;
using System.Windows;

namespace ETSOverlay
{
    public partial class LicenseDialog : Window
    {
        private MainWindow _mainWindow;

        public LicenseDialog(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            
            MouseLeftButtonDown += (s, e) => { DragMove(); };

            UpdateUI();
            ApplyLocalization();
            
            LicenseManager.Instance.OnLicenseChanged += UpdateUI;
        }

        private void ApplyLocalization()
        {
            if (_mainWindow.GetUiLanguage() == "uk")
            {
                TitleBlock.Text = "Supporter";
                InactiveDescText.Text = "Відкрийте додаткові налаштування зовнішнього вигляду та підтримайте подальшу розробку.";
                LicenseKeyLabel.Text = "Ключ ліцензії";
                BtnCloseInactive.Content = "Закрити";
                BtnActivate.Content = "Активувати";
                BtnGetLicense.Content = "Отримати ключ";
                StatusLabel.Text = "Статус:";
                PlanLabel.Text = "План:";
                LastValidatedLabel.Text = "Остання перевірка:";
                BtnCloseActive.Content = "Закрити";
                BtnManageSubscription.Content = "Керувати";
                BtnDeactivate.Content = "Деактивувати";
            }
            else
            {
                TitleBlock.Text = "Supporter";
                InactiveDescText.Text = "Unlock additional customization options and support future development.";
                LicenseKeyLabel.Text = "License Key";
                BtnCloseInactive.Content = "Close";
                BtnActivate.Content = "Activate";
                BtnGetLicense.Content = "Get License";
                StatusLabel.Text = "Status:";
                PlanLabel.Text = "Plan:";
                LastValidatedLabel.Text = "Last Validated:";
                BtnCloseActive.Content = "Close";
                BtnManageSubscription.Content = "Manage";
                BtnDeactivate.Content = "Deactivate";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            LicenseManager.Instance.OnLicenseChanged -= UpdateUI;
            base.OnClosed(e);
        }

        private void UpdateUI()
        {
            // Must run on UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateUI);
                return;
            }

            var licenseManager = LicenseManager.Instance;

            if (licenseManager.Status == "active")
            {
                InactivePanel.Visibility = Visibility.Collapsed;
                ActivePanel.Visibility = Visibility.Visible;

                ActivePlanText.Text = string.IsNullOrWhiteSpace(licenseManager.CurrentPlan) ? "Supporter" : licenseManager.CurrentPlan;
                ActiveValidatedText.Text = licenseManager.LastValidationTime > DateTime.MinValue 
                    ? licenseManager.LastValidationTime.ToUniversalTime().ToString("dd MMM yyyy, HH:mm UTC", System.Globalization.CultureInfo.InvariantCulture) 
                    : "Never";

                bool isStripe = licenseManager.Source == "stripe";
                BtnManageSubscription.IsEnabled = isStripe;
                if (!isStripe)
                {
                    BtnManageSubscription.ToolTip = _mainWindow.GetUiLanguage() == "uk" 
                        ? "Керувати ліцензією можна лише якщо вона була оформлена через Stripe." 
                        : "You can only manage your license if it was purchased through Stripe.";
                }
                else
                {
                    BtnManageSubscription.ToolTip = null;
                }
            }
            else
            {
                ActivePanel.Visibility = Visibility.Collapsed;
                InactivePanel.Visibility = Visibility.Visible;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnGetLicense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://trucksim.uk/donate",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void ShowMessage(string text, bool isError)
        {
            MessageBorder.Visibility = Visibility.Visible;
            MessageText.Text = text;
            if (isError)
            {
                MessageBorder.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3A1C20")); // Subtle red
                MessageText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF5252")); // Bright red
            }
            else
            {
                MessageBorder.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1C3A20")); // Subtle green
                MessageText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50")); // Bright green
            }
        }

        private void ClearMessage()
        {
            MessageBorder.Visibility = Visibility.Collapsed;
        }

        private async void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            ClearMessage();
            if (string.IsNullOrWhiteSpace(LicenseKeyInput.Text))
            {
                ShowMessage(_mainWindow.GetUiLanguage() == "uk" ? "Будь ласка, введіть ключ ліцензії." : "Please enter a License Key.", true);
                return;
            }

            string keyText = LicenseKeyInput.Text.Trim().ToUpper();
            if (!System.Text.RegularExpressions.Regex.IsMatch(keyText, @"^TSW-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$"))
            {
                ShowMessage(_mainWindow.GetUiLanguage() == "uk" ? "Невірний формат ключа. Має бути: TSW-XXXX-XXXX-XXXX" : "Invalid key format. Must be: TSW-XXXX-XXXX-XXXX", true);
                return;
            }

            BtnActivate.IsEnabled = false;
            BtnActivate.Content = _mainWindow.GetUiLanguage() == "uk" ? "Активація..." : "Activating...";

            var (success, message) = await LicenseManager.Instance.ActivateAsync(keyText, MainWindow.GetCurrentVersion());
            
            if (!success)
            {
                ShowMessage(message, true);
                BtnActivate.IsEnabled = true;
                BtnActivate.Content = _mainWindow.GetUiLanguage() == "uk" ? "Активувати" : "Activate";
            }
            else
            {
                ShowMessage(_mainWindow.GetUiLanguage() == "uk" ? "Ліцензію успішно активовано." : "License activated successfully.", false);
                BtnActivate.IsEnabled = true;
                BtnActivate.Content = _mainWindow.GetUiLanguage() == "uk" ? "Активувати" : "Activate";
                LicenseKeyInput.Text = string.Empty;
                _mainWindow.SaveStatePublic();
            }
        }

        private async void BtnDeactivate_Click(object sender, RoutedEventArgs e)
        {
            ClearMessage();
            BtnDeactivate.IsEnabled = false;
            BtnDeactivate.Content = _mainWindow.GetUiLanguage() == "uk" ? "Деактивація..." : "Deactivating...";

            var (success, message) = await LicenseManager.Instance.DeactivateAsync();
            
            if (!success)
            {
                ShowMessage(message, true);
            }
            else
            {
                ShowMessage(_mainWindow.GetUiLanguage() == "uk" ? "Ліцензію успішно деактивовано." : "License deactivated successfully.", false);
            }
            
            BtnDeactivate.IsEnabled = true;
            BtnDeactivate.Content = _mainWindow.GetUiLanguage() == "uk" ? "Деактивувати" : "Deactivate";
            _mainWindow.SaveStatePublic();
        }
        private async void BtnManageSubscription_Click(object sender, RoutedEventArgs e)
        {
            ClearMessage();
            BtnManageSubscription.IsEnabled = false;
            BtnManageSubscription.Content = _mainWindow.GetUiLanguage() == "uk" ? "Відкриваємо..." : "Opening...";

            var (success, message, url) = await LicenseManager.Instance.CreatePortalSessionAsync();
            
            if (!success)
            {
                ShowMessage(message, true);
            }
            else if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                    ShowMessage(_mainWindow.GetUiLanguage() == "uk" ? "Відкрито у браузері." : "Opened in browser.", false);
                }
                catch (Exception)
                {
                    ShowMessage(_mainWindow.GetUiLanguage() == "uk" ? "Не вдалося відкрити браузер." : "Failed to open browser.", true);
                }
            }
            
            BtnManageSubscription.IsEnabled = true;
            BtnManageSubscription.Content = _mainWindow.GetUiLanguage() == "uk" ? "Керувати" : "Manage";
        }
    }
}
