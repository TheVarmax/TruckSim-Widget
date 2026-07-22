using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System;

namespace ETSOverlay
{
    public partial class VisibilitySettingsWindow : Window
    {
        private MainWindow _mainWindow;
        private bool _isLoaded = false;

        public VisibilitySettingsWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            
            // Load current states from MainWindow
            ToggleGameCards.IsChecked = _mainWindow.GetShowDistance();
            ToggleRoute.IsChecked = _mainWindow.GetShowRoute();
            ToggleBottomCards.IsChecked = _mainWindow.GetShowBottomInfo();

            UpdateLocalization(_mainWindow.GetUiLanguage() == "uk");
            _isLoaded = true;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
            this.BeginAnimation(Window.OpacityProperty, fadeOut);
            await System.Threading.Tasks.Task.Delay(200);
            this.Close();
        }

        private void Toggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || _mainWindow == null) return;
            
            _mainWindow.SetVisibilitySettings(
                ToggleGameCards.IsChecked ?? true,
                ToggleRoute.IsChecked ?? true,
                ToggleBottomCards.IsChecked ?? true
            );
        }

        public void UpdateLocalization(bool isUk)
        {
            WindowTitle.Text = isUk ? "Кастомний вигляд" : "Custom Layout";
            HintText.Text = isUk ? "Виберіть блоки для відображення:" : "Select blocks to display:";
            LblGameCards.Text = isUk ? "Статус гри та Дистанція" : "Game Status & Distance";
            LblRoute.Text = isUk ? "Маршрут призначення" : "Destination Route";
            LblBottomCards.Text = isUk ? "Швидкість, Вантаж, Тип" : "Speed, Cargo, Type";
        }
    }
}
