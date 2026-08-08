using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ETSOverlay
{
    public partial class HudWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private bool _isHovering = false;

        public HudWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            
            // Allow moving the window by dragging it
            MouseLeftButtonDown += (s, e) =>
            {
                if (!_mainWindow.IsLocked) DragMove();
            };
        }

        private void DataContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged && e.PreviousSize.Width > 0)
            {
                double widthDiff = e.NewSize.Width - e.PreviousSize.Width;
                double scale = UIScaleTransform?.ScaleX ?? 1.0;
                this.Left -= (widthDiff * scale) / 2;
            }
        }

        public double GetTrueCenterLeft()
        {
            double scale = UIScaleTransform?.ScaleX ?? 1.0;
            return this.Left + (32 + DataContainer.ActualWidth) * scale / 2;
        }

        public double GetBaseWidth()
        {
            double scale = UIScaleTransform?.ScaleX ?? 1.0;
            return (32 + DataContainer.ActualWidth) * scale;
        }

        public double GetDesiredBaseWidth()
        {
            double scale = UIScaleTransform?.ScaleX ?? 1.0;
            return (32 + DataContainer.DesiredSize.Width) * scale;
        }

        public void SetScale(double scale)
        {
            if (UIScaleTransform != null)
            {
                UIScaleTransform.ScaleX = scale;
                UIScaleTransform.ScaleY = scale;
            }
        }

        public void UpdatePinIcon(bool isPinned)
        {
            if (PinIcon != null)
            {
                PinIcon.Fill = isPinned ? System.Windows.Media.Brushes.Gold : System.Windows.Media.Brushes.Gray;
            }
        }

        private double _currentBgOpacity = 1.0;

        public void SetOpacity(double backgroundOpacity, double interfaceOpacity, double textOpacity)
        {
            _currentBgOpacity = backgroundOpacity;
            if (DataBg != null) DataBg.Opacity = backgroundOpacity;
            
            // Text elements
            if (TbStatus != null) TbStatus.Opacity = textOpacity;
            if (DistanceInfo != null) DistanceInfo.Opacity = textOpacity;
            if (SpeedLabel != null) SpeedLabel.Opacity = textOpacity;
            if (SpeedValue != null) SpeedValue.Opacity = textOpacity;
            if (SpeedUnit != null) SpeedUnit.Opacity = textOpacity;
            if (MaxSpeedLabel != null) MaxSpeedLabel.Opacity = textOpacity;
            if (MaxSpeedValue != null) MaxSpeedValue.Opacity = textOpacity;
            if (MaxUnit != null) MaxUnit.Opacity = textOpacity;
            if (DeliveryType != null) DeliveryType.Opacity = textOpacity;
            
            // Interface elements (Dot, Path, etc - although Dot is inside a StackPanel with Text, it will fade with it, but let's be precise if needed. The easiest is just fading the StackPanels but since they are in Grid, we can leave as is or just apply to what's inside)
            if (TbStatusDot != null) TbStatusDot.Opacity = interfaceOpacity;
        }

        public void UpdateData(string tbStatus, System.Windows.Media.Brush tbForeground, string distance, string speed, string maxSpeed, string deliveryType, System.Windows.Media.Brush deliveryTypeBrush)
        {
            TbStatus.Text = tbStatus;
            TbStatus.Foreground = tbForeground;
            TbStatusDot.Fill = tbForeground; // Match dot color with text
            
            DistanceInfo.Text = distance;
            SpeedValue.Text = speed;
            MaxSpeedValue.Text = maxSpeed;
            DeliveryType.Text = deliveryType;
            DeliveryType.Foreground = deliveryTypeBrush;
        }

        private void MainBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            _isHovering = true;
            HoverActionPanel.Visibility = Visibility.Visible;
            var anim = new DoubleAnimation(_currentBgOpacity, TimeSpan.FromSeconds(0.2));
            HoverActionPanel.BeginAnimation(OpacityProperty, anim);
        }

        private void MainBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            _isHovering = false;
            var anim = new DoubleAnimation(0.0, TimeSpan.FromSeconds(0.2));
            anim.Completed += (s, ev) => 
            {
                if (!_isHovering) HoverActionPanel.Visibility = Visibility.Collapsed;
            };
            HoverActionPanel.BeginAnimation(OpacityProperty, anim);
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e) => _mainWindow.BtnSettings_Click(sender, e);
        private void BtnTopmost_Click(object sender, RoutedEventArgs e) => _mainWindow.BtnTopmost_Click(sender, e);
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => _mainWindow.BtnMinimize_Click(sender, e);
        private void BtnClose_Click(object sender, RoutedEventArgs e) => _mainWindow.BtnClose_Click(sender, e);
    }
}
