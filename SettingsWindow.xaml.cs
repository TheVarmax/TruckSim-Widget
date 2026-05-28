using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ETSOverlay
{
    public partial class SettingsWindow : Window
    {
        private MainWindow _mainWindow;
        private bool _suppressEvents = true;

        public SettingsWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            MouseLeftButtonDown += (s, e) => { DragMove(); };
            _suppressEvents = false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        // --- Event Handlers (delegate to MainWindow) ---

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void UIModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (UIModeSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _mainWindow.OnUIModeChanged(tag);
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressEvents) return;
            _mainWindow.OnOpacityChanged(OpacitySlider.Value);
            OpacityValue.Text = $"{(int)Math.Round(OpacitySlider.Value)}%";
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (LanguageSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _mainWindow.OnLanguageChanged(tag);
            }
        }

        private void SpeedWarningBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressEvents) return;
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

        private void ScaleSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (ScaleSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int scale))
            {
                _mainWindow.OnScaleChanged(scale);
            }
        }

        // --- Public methods for MainWindow to sync state ---

        public void SuppressEvents(bool suppress)
        {
            _suppressEvents = suppress;
        }

        public void UpdateLocalization(bool isUk)
        {
            _suppressEvents = true;
            SettingsTitle.Text = isUk ? "НАЛАШТУВАННЯ" : "SETTINGS";
            LanguageLabel.Text = isUk ? "Мова" : "Language";
            OpacityLabel.Text = isUk ? "Прозорість" : "Opacity";
            UIModeLabel.Text = isUk ? "Режим інтерфейсу" : "UI Mode";
            if (UIModeSelector.Items.Count >= 2)
            {
                ((ComboBoxItem)UIModeSelector.Items[0]).Content = isUk ? "Повний інтерфейс" : "Full Interface";
                ((ComboBoxItem)UIModeSelector.Items[1]).Content = isUk ? "Мінімалізм" : "Minimalism";
            }
            SpeedWarningLabel.Text = isUk ? "Поріг швидкості" : "Speed warning";
            ScaleLabel.Text = isUk ? "Масштаб" : "Scale";
            if (!_mainWindow._isCheckingUpdate)
            {
                BtnCheckUpdate.Content = isUk ? "🔄 Перевірити оновлення" : "🔄 Check for updates";
            }
            BtnDonate.Content = isUk ? "💛 Підтримати" : "💛 Donate";
            _suppressEvents = false;
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
