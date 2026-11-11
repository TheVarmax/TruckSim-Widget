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
            WindowGuard.Attach(this, DataContainer);
            
            // Allow moving the window by dragging it. Only the end of a drag is a position the
            // user chose; moves made by the application (showing, restoring, re-centring after a
            // width change) never become the saved position.
            MouseLeftButtonDown += (s, e) =>
            {
                if (_mainWindow.IsLocked) return;
                DragMove();
                if (WindowState != WindowState.Normal) return;
                _targetCenterLeft = GetTrueCenterLeft();
                UserMoved?.Invoke();
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

            var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation(currentOpacity, show ? 1 : 0, duration) { EasingFunction = easing, FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop };

            if (!show)
            {
                opacityAnim.Completed += (s, e) =>
                {
                    element.BeginAnimation(UIElement.OpacityProperty, null);
                    element.Opacity = 0;
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
                    element.BeginAnimation(UIElement.OpacityProperty, null);
                    element.Opacity = 1;
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

            var widthAnim = new System.Windows.Media.Animation.DoubleAnimation(currentWidth, targetWidth, duration) { EasingFunction = easing, FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop };
            widthAnim.Completed += (s, e) =>
            {
                element.BeginAnimation(FrameworkElement.WidthProperty, null);
                element.Width = targetWidth;
            };
            element.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);

            if (element.RenderTransform is System.Windows.Media.ScaleTransform rst)
            {
                double currentScale = rst.ScaleX;
                var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation(currentScale, show ? 1.0 : 0.6, duration) { EasingFunction = easing, FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop };
                scaleAnim.Completed += (s, e) =>
                {
                    rst.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
                    rst.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
                    rst.ScaleX = show ? 1.0 : 0.6;
                    rst.ScaleY = show ? 1.0 : 0.6;
                };
                rst.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
                rst.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            }
        }

        private bool _isUpdatingPosition = false;
        private double _targetCenterLeft = double.NaN;

        /// <summary>The user finished dragging the HUD (its new position is final).</summary>
        public event Action? UserMoved;

        /// <summary>Horizontal centre the HUD keeps while its width changes.</summary>
        public double TargetCenterLeft => _targetCenterLeft;

        /// <summary>
        /// Puts the visible card's horizontal centre at <paramref name="centerLeft"/> and its window
        /// top at <paramref name="top"/>. The centre is kept when the width changes later.
        /// </summary>
        public void PlaceAtCenter(double centerLeft, double top)
        {
            _isUpdatingPosition = true;
            try
            {
                _targetCenterLeft = centerLeft;
                Left = centerLeft - GetCardWidth() / 2;
                Top = top;
            }
            finally
            {
                _isUpdatingPosition = false;
            }
        }

        /// <summary>Keeps the current centre for later width changes (after a correction by the app).</summary>
        public void KeepCurrentCenter() => _targetCenterLeft = GetTrueCenterLeft();

        private void DataContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (double.IsNaN(_targetCenterLeft)) return;
            // Only re-center while the window is in its normal state; otherwise a maximized /
            // minimized size would push Left (and the saved HUD position) off-screen.
            if (WindowState != WindowState.Normal) return;

            if (e.WidthChanged && e.PreviousSize.Width > 0 && !_isUpdatingPosition)
            {
                _isUpdatingPosition = true;
                // Always computed from the chosen centre, never from where the card is shown now,
                // so after a widening near an edge the narrower HUD lands exactly on it again.
                double before = this.Left;
                this.Left = _targetCenterLeft - CardWidth(e.NewSize.Width) / 2;
                // Near a screen edge the centre cannot be kept without pushing the card off the
                // monitor: the card is shown inside for now. The chosen centre stays the target.
                WindowGuard.FitToScreen(this, () => this.Left = before);
                _isUpdatingPosition = false;
            }
        }

        // One rule for every centre calculation (saving, restoring, re-centring): the window is
        // the card plus a 16 px margin on each side, all scaled by the HUD scale.
        private double CardWidth(double dataWidth) => (32 + dataWidth) * (UIScaleTransform?.ScaleX ?? 1.0);

        /// <summary>Width of the window around the card, from the laid-out card (measured when not laid out yet).</summary>
        public double GetCardWidth()
        {
            double width = DataContainer.ActualWidth;
            if (width <= 0)
            {
                Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = DataContainer.DesiredSize.Width;
            }
            return CardWidth(width);
        }

        public double GetTrueCenterLeft() => this.Left + GetCardWidth() / 2;

        public double GetBaseWidth() => GetCardWidth();

        public double GetDesiredBaseWidth() => GetCardWidth();

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
        private void BtnTray_Click(object sender, RoutedEventArgs e) => _mainWindow.BtnTray_Click(sender, e);

        /// <summary>Localized name of the "Minimize to tray" command (tooltip and screen readers).</summary>
        public void SetTrayCommandText(string text)
        {
            if (BtnTray == null) return;
            BtnTray.ToolTip = text;
            System.Windows.Automation.AutomationProperties.SetName(BtnTray, text);
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e) => _mainWindow.BtnClose_Click(sender, e);

        private bool _isClosingFromMainWindow = false;
        internal void MarkClosingFromMainWindow() => _isClosingFromMainWindow = true;

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (!_isClosingFromMainWindow)
            {
                _mainWindow.BtnClose_Click(_mainWindow, new RoutedEventArgs());
            }
        }
    }
}
