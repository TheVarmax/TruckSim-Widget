using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ETSOverlay
{
    public partial class ModernColorPickerWindow : Window
    {
        public string SelectedHex { get; private set; } = "#FFFFFF";
        private bool _isUpdating = false;

        private double _currentHue = 0;
        private double _currentSaturation = 1;
        private double _currentValue = 1;
        private bool _isDraggingCanvas = false;

        public ModernColorPickerWindow(string initialHex, bool isUk)
        {
            InitializeComponent();
            
            BtnCancel.Content = isUk ? "Скасувати" : "Cancel";
            BtnSave.Content = isUk ? "Вибрати" : "Select";
            TitleBlock.Text = isUk ? "Виберіть колір" : "Choose Color";

            MouseLeftButtonDown += (s, e) => { DragMove(); };
            Loaded += ModernColorPickerWindow_Loaded;

            GenerateSwatches();
            SetColorFromHex(initialHex);
        }

        private void ModernColorPickerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateThumbsFromHsv();
            UpdateCanvasBackground();
        }

        private void GenerateSwatches()
        {
            string[] swatches = new string[]
            {
                "#F44336", "#E91E63", "#9C27B0", "#673AB7", "#3F51B5", "#2196F3", "#03A9F4", "#00BCD4", "#009688", "#4CAF50",
                "#8BC34A", "#CDDC39", "#FFEB3B", "#FFC107", "#FF9800", "#FF5722", "#795548", "#9E9E9E", "#607D8B", "#000000",
                "#FFFFFF", "#EF5350", "#EC407A", "#AB47BC", "#7E57C2", "#5C6BC0", "#42A5F5", "#29B6F6", "#26C6DA", "#26A69A",
                "#66BB6A", "#9CCC65", "#D4E157", "#FFEE58", "#FFCA28", "#FFA726", "#FF7043", "#8D6E63", "#BDBDBD", "#78909C"
            };

            foreach (var hex in swatches)
            {
                var btn = new Button
                {
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(0),
                    Tag = hex
                };

                btn.Click += (s, e) => { SetColorFromHex(hex); };
                SwatchesPanel.Children.Add(btn);
            }
        }

        private void SetColorFromHex(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                RgbToHsv(color, out _currentHue, out _currentSaturation, out _currentValue);
                
                _isUpdating = true;
                HueSlider.Value = _currentHue;
                _isUpdating = false;

                UpdateThumbsFromHsv();
                UpdateCanvasBackground();
                UpdatePreview();
            }
            catch { }
        }

        private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _currentHue = HueSlider.Value;
            UpdateCanvasBackground();
            
            if (HueSlider.ActualHeight > 0)
            {
                double hueY = (1.0 - (_currentHue / 360.0)) * HueSlider.ActualHeight;
                Canvas.SetTop(HueThumb, hueY - 3);
            }

            if (!_isUpdating)
            {
                UpdatePreview();
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingCanvas = true;
            ColorCanvas.CaptureMouse();
            UpdateCanvasColor(e.GetPosition(ColorCanvas));
            e.Handled = true;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingCanvas)
            {
                UpdateCanvasColor(e.GetPosition(ColorCanvas));
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingCanvas = false;
            ColorCanvas.ReleaseMouseCapture();
        }

        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            // Do not release capture here so dragging outside works
        }

        private void UpdateCanvasColor(Point p)
        {
            double width = ColorCanvas.ActualWidth;
            double height = ColorCanvas.ActualHeight;

            if (width == 0 || height == 0) return;

            double x = Math.Max(0, Math.Min(width, p.X));
            double y = Math.Max(0, Math.Min(height, p.Y));

            Canvas.SetLeft(ColorThumb, x - 7);
            Canvas.SetTop(ColorThumb, y - 7);

            _currentSaturation = x / width;
            _currentValue = 1.0 - (y / height); // 1 at top, 0 at bottom

            UpdatePreview();
        }

        private void UpdateThumbsFromHsv()
        {
            if (ColorCanvas.ActualWidth == 0) return;

            double x = _currentSaturation * ColorCanvas.ActualWidth;
            double y = (1.0 - _currentValue) * ColorCanvas.ActualHeight;

            Canvas.SetLeft(ColorThumb, x - 7);
            Canvas.SetTop(ColorThumb, y - 7);

            double hueY = (1.0 - (_currentHue / 360.0)) * HueSlider.ActualHeight;
            Canvas.SetTop(HueThumb, hueY - 3);
        }

        private void UpdateCanvasBackground()
        {
            if (ColorCanvasBackground != null)
            {
                var hueColor = HsvToRgb(_currentHue, 1.0, 1.0);
                ColorCanvasBackground.Background = new SolidColorBrush(hueColor);
            }
        }

        private void UpdatePreview()
        {
            var color = HsvToRgb(_currentHue, _currentSaturation, _currentValue);
            
            SelectedHex = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            
            if (PreviewBorder != null)
                PreviewBorder.Background = new SolidColorBrush(color);
            
            if (PreviewHex != null)
                PreviewHex.Text = SelectedHex;
        }

        private Color HsvToRgb(double h, double s, double v)
        {
            int hi = Convert.ToInt32(Math.Floor(h / 60)) % 6;
            double f = h / 60 - Math.Floor(h / 60);

            v = v * 255;
            int vInt = Convert.ToInt32(v);
            int p = Convert.ToInt32(v * (1 - s));
            int q = Convert.ToInt32(v * (1 - f * s));
            int t = Convert.ToInt32(v * (1 - (1 - f) * s));

            if (hi == 0) return Color.FromArgb(255, (byte)vInt, (byte)t, (byte)p);
            else if (hi == 1) return Color.FromArgb(255, (byte)q, (byte)vInt, (byte)p);
            else if (hi == 2) return Color.FromArgb(255, (byte)p, (byte)vInt, (byte)t);
            else if (hi == 3) return Color.FromArgb(255, (byte)p, (byte)q, (byte)vInt);
            else if (hi == 4) return Color.FromArgb(255, (byte)t, (byte)p, (byte)vInt);
            else return Color.FromArgb(255, (byte)vInt, (byte)p, (byte)q);
        }

        private void RgbToHsv(Color color, out double h, out double s, out double v)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            v = max / 255.0;
            s = max == 0 ? 0 : 1d - (1d * min / max);

            if (max == min) h = 0;
            else if (max == color.R) h = (60 * (color.G - color.B) / (double)(max - min) + 360) % 360;
            else if (max == color.G) h = (60 * (color.B - color.R) / (double)(max - min) + 120) % 360;
            else h = (60 * (color.R - color.G) / (double)(max - min) + 240) % 360;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
