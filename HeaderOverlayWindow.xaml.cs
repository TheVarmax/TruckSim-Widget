using System;
using System.Windows;
using System.Windows.Input;

namespace ETSOverlay
{
    public partial class HeaderOverlayWindow : Window
    {
        private readonly MainWindow _mainWindow;

        public HeaderOverlayWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            MouseLeftButtonDown += (s, e) =>
            {
                if (!_mainWindow.IsLocked) _mainWindow.DragMove();
            };
            MouseEnter += HeaderOverlay_MouseEnter;
            MouseLeave += HeaderOverlay_MouseLeave;
        }

        public void UpdatePinIcon(bool isPinned)
        {
            if (PinIcon != null)
            {
                PinIcon.Fill = isPinned ? System.Windows.Media.Brushes.Gold : System.Windows.Media.Brushes.Gray;
            }
        }

        public void UpdateWidth(double width)
        {
            OverlayBorder.Width = width;
        }

        public void SetScale(double scale)
        {
            if (HeaderScaleTransform != null)
            {
                HeaderScaleTransform.ScaleX = scale;
                HeaderScaleTransform.ScaleY = scale;
            }
        }

        public void SetOpacity(double opacity)
        {
            OverlayBorder.Opacity = opacity;
        }

        public void AnimateOpacity(double toOpacity, double durationSeconds)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(toOpacity, TimeSpan.FromSeconds(durationSeconds));
            OverlayBorder.BeginAnimation(OpacityProperty, anim);
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e) => _mainWindow.BtnSettings_Click(sender, e);
        private void BtnTopmost_Click(object sender, RoutedEventArgs e) => _mainWindow.BtnTopmost_Click(sender, e);
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => _mainWindow.BtnMinimize_Click(sender, e);
        private void BtnClose_Click(object sender, RoutedEventArgs e) => _mainWindow.BtnClose_Click(sender, e);

        private void HeaderOverlay_MouseEnter(object sender, MouseEventArgs e)
        {
            _mainWindow.NotifyOverlayHover(true);
        }

        private void HeaderOverlay_MouseLeave(object sender, MouseEventArgs e)
        {
            _mainWindow.NotifyOverlayHover(false);
        }
    }
}
