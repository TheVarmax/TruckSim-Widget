using System;
using System.Windows;
using System.Windows.Controls;
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

            LocationChanged += (s, e) =>
            {
                if (!_isUpdatingPosition)
                {
                    _targetCenterLeft = GetTrueCenterLeft();
                }
            };

            ApplyVisibilitySettings(false);
        }

        public void ApplyVisibilitySettings(bool animate = false)
        {
            if (_mainWindow == null) return;

            bool showCurrentSpeed = _mainWindow.GetHudShowCurrentSpeed();
            bool showMaxSpeed = _mainWindow.GetHudShowMaxSpeed();
            bool showDeliveryType = _mainWindow.GetHudShowDeliveryType();

            if (animate)
            {
                AnimateElement(SpeedContainer, showCurrentSpeed);
                AnimateElement(SpeedSeparator, showCurrentSpeed);

                AnimateElement(MaxSpeedContainer, showMaxSpeed);
                AnimateElement(MaxSpeedSeparator, showMaxSpeed);

                AnimateElement(TypeContainer, showDeliveryType);
                AnimateElement(TypeSeparator, showDeliveryType);
            }
            else
            {
                SetElementVisibilityInstant(SpeedContainer, showCurrentSpeed);
                SetElementVisibilityInstant(SpeedSeparator, showCurrentSpeed);
                SetElementVisibilityInstant(MaxSpeedContainer, showMaxSpeed);
                SetElementVisibilityInstant(MaxSpeedSeparator, showMaxSpeed);
                SetElementVisibilityInstant(TypeContainer, showDeliveryType);
                SetElementVisibilityInstant(TypeSeparator, showDeliveryType);
            }
        }

        private void SetElementVisibilityInstant(FrameworkElement element, bool show)
        {
            element.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            element.Opacity = show ? 1 : 0;
            element.BeginAnimation(UIElement.OpacityProperty, null);

            element.BeginAnimation(FrameworkElement.WidthProperty, null);
            if (element.Tag is string tagStr && double.TryParse(tagStr, out double w))
            {
                element.Width = show ? w : 0;
            }
            else
            {
                element.Width = double.NaN;
            }

            if (element.RenderTransform is System.Windows.Media.ScaleTransform rst)
            {
                rst.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
                rst.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
                rst.ScaleX = show ? 1 : 0.6;
                rst.ScaleY = show ? 1 : 0.6;
            }
        }

        private void AnimateElement(FrameworkElement element, bool show)
        {
            // Skip animation if already in the target state
            if (show && element.Visibility == Visibility.Visible && Math.Abs(element.Opacity - 1.0) < 0.01) return;
            if (!show && element.Visibility == Visibility.Collapsed) return;

            double currentOpacity = element.Opacity;
            var duration = TimeSpan.FromSeconds(0.3);
            var easing = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };

            var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation(currentOpacity, show ? 1 : 0, duration) { EasingFunction = easing };

            if (!show)
            {
                opacityAnim.Completed += (s, e) =>
                {
                    if (element.Opacity < 0.01)
                    {
                        element.Visibility = Visibility.Collapsed;
                        element.BeginAnimation(FrameworkElement.WidthProperty, null);
                        if (element.Tag is string tg && double.TryParse(tg, out double tw))
                            element.Width = tw;
                        else
                            element.Width = double.NaN;
                    }
                };
            }
            else
            {
                opacityAnim.Completed += (s, e) =>
                {
                    if (element.Opacity > 0.99)
                    {
                        element.BeginAnimation(FrameworkElement.WidthProperty, null);
                        if (element.Tag is string tg && double.TryParse(tg, out double tw))
                            element.Width = tw;
                        else
                            element.Width = double.NaN;
                    }
                };
            }

            element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

            double currentWidth = element.ActualWidth;
            
            if (show)
            {
                element.BeginAnimation(FrameworkElement.WidthProperty, null);
                element.Width = 0; // Lock width to 0 BEFORE showing to prevent any layout flash
                element.Visibility = Visibility.Visible;
            }

            // Measure without modifying live layout properties to prevent screen flashing
            double fullWidth;
            if (element.Tag is string tagStr && double.TryParse(tagStr, out double w))
            {
                fullWidth = w;
            }
            else
            {
                // Temporarily detach property to measure unconstrained desired size
                object localWidth = element.ReadLocalValue(FrameworkElement.WidthProperty);
                element.BeginAnimation(FrameworkElement.WidthProperty, null);
                element.Width = double.NaN;
                
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                fullWidth = element.DesiredSize.Width;
                
                // Restore local width so it doesn't affect live layout before animation starts
                if (localWidth == DependencyProperty.UnsetValue)
                    element.ClearValue(FrameworkElement.WidthProperty);
                else
                    element.Width = (double)localWidth;
            }

            if (currentWidth == 0 && !show) currentWidth = fullWidth;
            double targetWidth = show ? fullWidth : 0;

            var widthAnim = new System.Windows.Media.Animation.DoubleAnimation(currentWidth, targetWidth, duration) { EasingFunction = easing };
            element.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);

            if (element.RenderTransform is System.Windows.Media.ScaleTransform rst)
            {
                double currentScale = rst.ScaleX;
                var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation(currentScale, show ? 1.0 : 0.6, duration) { EasingFunction = easing };
                rst.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
                rst.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            }
        }

        private bool _isUpdatingPosition = false;
        private double _targetCenterLeft = double.NaN;

        private void DataContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (double.IsNaN(_targetCenterLeft)) return;

            if (e.WidthChanged && e.PreviousSize.Width > 0 && !_isUpdatingPosition)
            {
                _isUpdatingPosition = true;
                double scale = UIScaleTransform?.ScaleX ?? 1.0;
                this.Left = _targetCenterLeft - (32 + e.NewSize.Width) * scale / 2;
                _isUpdatingPosition = false;
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

        public bool SuppressAnimations { get; set; } = false;

        public void UpdateData(string tbStatus, System.Windows.Media.Brush tbForeground, string distance, string speed, System.Windows.Media.Brush speedForeground, string maxSpeed, string deliveryType, System.Windows.Media.Brush deliveryTypeBrush)
        {
            TbStatus.Text = tbStatus;
            TbStatus.Foreground = tbForeground;
            TbStatusDot.Fill = tbForeground; // Match dot color with text
            
            if (SuppressAnimations)
            {
                DistanceInfo.Text = distance;
                SpeedValue.Text = speed;
                MaxSpeedValue.Text = maxSpeed;
                SuppressAnimations = false;
            }
            else
            {
                UpdateTextWithAnimation(DistanceInfo, distance);
                UpdateTextWithAnimation(SpeedValue, speed);
                UpdateTextWithAnimation(MaxSpeedValue, maxSpeed);
            }
            
            SpeedValue.Foreground = speedForeground;
            
            DeliveryType.Text = deliveryType;
            DeliveryType.Foreground = deliveryTypeBrush;
        }

        private void UpdateTextWithAnimation(TextBlock tb, string newText)
        {
            if (tb.Text == newText) return;
            
            double oldWidth = tb.ActualWidth;
            tb.Text = newText;
            
            if (oldWidth == 0 || !this.IsLoaded) return;

            tb.BeginAnimation(FrameworkElement.WidthProperty, null);
            tb.Width = double.NaN;
            
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double newWidth = tb.DesiredSize.Width;
            
            if (Math.Abs(oldWidth - newWidth) > 0.5)
            {
                var anim = new DoubleAnimation(oldWidth, newWidth, TimeSpan.FromSeconds(0.25)) 
                { 
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } 
                };
                anim.Completed += (s, e) =>
                {
                    tb.BeginAnimation(FrameworkElement.WidthProperty, null);
                    tb.Width = double.NaN;
                };
                tb.BeginAnimation(FrameworkElement.WidthProperty, anim);
            }
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Environment.Exit(0);
        }
    }
}
