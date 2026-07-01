using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ETSOverlay
{
    public class TileColorViewModel : INotifyPropertyChanged
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";

        private string _hexValue = "";
        public string HexValue
        {
            get => _hexValue;
            set
            {
                if (_hexValue != value)
                {
                    _hexValue = value;
                    OnPropertyChanged(nameof(HexValue));
                    UpdateBrush();
                }
            }
        }

        private SolidColorBrush _currentColorBrush = Brushes.Transparent;
        public SolidColorBrush CurrentColorBrush
        {
            get => _currentColorBrush;
            private set
            {
                _currentColorBrush = value;
                OnPropertyChanged(nameof(CurrentColorBrush));
            }
        }

        private void UpdateBrush()
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(_hexValue);
                CurrentColorBrush = new SolidColorBrush(color);
            }
            catch
            {
                CurrentColorBrush = Brushes.Transparent;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class CustomColorsWindow : Window
    {
        public ObservableCollection<TileColorViewModel> TileColors { get; set; }
        public Dictionary<string, string> FinalColors { get; private set; }
        public bool IsSaved { get; private set; }
        private bool _isUk;

        public CustomColorsWindow(Dictionary<string, string> initialColors, bool isUk)
        {
            _isUk = isUk;
            InitializeComponent();
            TileColors = new ObservableCollection<TileColorViewModel>();
            FinalColors = new Dictionary<string, string>();
            IsSaved = false;

            TitleBlock.Text = isUk ? "Налаштування кольорів плиток" : "Customize Tile Colors";
            BtnCancel.Content = isUk ? "Скасувати" : "Cancel";
            BtnSave.Content = isUk ? "Зберегти" : "Save";

            AddTile("Sim", isUk ? "TrucksBook / Симулятор" : "TrucksBook / Sim", initialColors);
            AddTile("Status", isUk ? "Статус" : "Status", initialColors);
            AddTile("Game", isUk ? "Гра" : "Game", initialColors);
            AddTile("Distance", isUk ? "Відстань" : "Distance", initialColors);
            AddTile("Route", isUk ? "Маршрут" : "Route", initialColors);
            AddTile("Speed", isUk ? "Швидкість" : "Speed", initialColors);
            AddTile("Max", isUk ? "Макс. Швидкість" : "Max Speed", initialColors);
            AddTile("Type", isUk ? "Тип доставки" : "Delivery Type", initialColors);

            ColorItemsControl.ItemsSource = TileColors;
            MouseLeftButtonDown += (s, e) => { DragMove(); };
        }

        private void AddTile(string key, string displayName, Dictionary<string, string> initialColors)
        {
            string hex = "#7AC5CD"; // Default fallback (Teal)
            if (initialColors.TryGetValue(key, out var savedColor))
            {
                // Handle legacy preset names
                hex = savedColor.ToLowerInvariant() switch
                {
                    "blue" => "#4DA8DA",
                    "amber" => "#FFC107",
                    "violet" => "#9D4EDD",
                    "red" => "#E63946",
                    "teal" => "#7AC5CD",
                    "green" => "#4CAF50",
                    _ => savedColor
                };
            }

            TileColors.Add(new TileColorViewModel
            {
                Key = key,
                DisplayName = displayName,
                HexValue = hex
            });
        }



        private void Palette_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TileColorViewModel vm)
            {
                var dialog = new ModernColorPickerWindow(vm.HexValue, _isUk)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    vm.HexValue = dialog.SelectedHex;
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsSaved = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tile in TileColors)
            {
                // Minimal validation: must start with # and be 4, 7, or 9 chars long
                if (tile.HexValue.StartsWith("#") && (tile.HexValue.Length == 4 || tile.HexValue.Length == 7 || tile.HexValue.Length == 9))
                {
                    FinalColors[tile.Key] = tile.HexValue;
                }
                else
                {
                    FinalColors[tile.Key] = "#7AC5CD"; // Fallback if user typed nonsense
                }
            }
            IsSaved = true;
            Close();
        }
    }
}
