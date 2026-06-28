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
                StatusLabel.Text = "Статус:";
                PlanLabel.Text = "План:";
                LastValidatedLabel.Text = "Остання перевірка:";
                BtnCloseActive.Content = "Закрити";
                BtnDeactivate.Content = "Деактивувати";
            }
            else
            {
                TitleBlock.Text = "Supporter";
                InactiveDescText.Text = "Unlock additional customization options and support future development.";
                LicenseKeyLabel.Text = "License Key";
                BtnCloseInactive.Content = "Close";
                BtnActivate.Content = "Activate";
                StatusLabel.Text = "Status:";
                PlanLabel.Text = "Plan:";
                LastValidatedLabel.Text = "Last Validated:";
                BtnCloseActive.Content = "Close";
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
                    ? licenseManager.LastValidationTime.ToString("g") 
                    : "Never";
            }
            else
            {
                ActivePanel.Visibility = Visibility.Collapsed;
                InactivePanel.Visibility = Visibility.Visible;

                if (!string.IsNullOrWhiteSpace(licenseManager.LicenseKey) && string.IsNullOrWhiteSpace(LicenseKeyInput.Text))
                {
                    LicenseKeyInput.Text = licenseManager.LicenseKey;
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
                ShowMessage("Please enter a valid License Key.", true);
                return;
            }

            BtnActivate.IsEnabled = false;
            BtnActivate.Content = _mainWindow.GetUiLanguage() == "uk" ? "Активація..." : "Activating...";

            var (success, message) = await LicenseManager.Instance.ActivateAsync(LicenseKeyInput.Text, MainWindow.GetCurrentVersion());
            
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
    }
}
