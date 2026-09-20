using System;
using System.Windows;
using System.Windows.Input;

namespace ETSOverlay
{
    public partial class CustomMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.No;
        public bool IsDontAskAgainChecked { get; private set; } = false;

        public CustomMessageBox(string message, string title, string yesText, string noText, bool showCheckbox = false, string checkboxText = "")
        {
            InitializeComponent();
            TitleBlock.Text = title.ToUpper();
            
            if (showCheckbox)
            {
                ChkDontAskAgain.Visibility = Visibility.Visible;
                ChkDontAskAgain.Content = checkboxText;
            }
            
            // Split the message into paragraphs and apply formatting
            var paragraphs = message.Split(new[] { "\n\n" }, StringSplitOptions.None);
            for (int i = 0; i < paragraphs.Length; i++)
            {
                var tb = new System.Windows.Controls.TextBlock
                {
                    Text = paragraphs[i],
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10),
                    FontSize = 13
                };

                // First paragraph is bolder and white
                if (i == 0)
                {
                    tb.Foreground = System.Windows.Media.Brushes.White;
                    tb.FontWeight = FontWeights.SemiBold;
                }
                // Last paragraph is faded and italic if there are multiple
                else if (i == paragraphs.Length - 1 && paragraphs.Length > 1)
                {
                    tb.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 160));
                    tb.FontStyle = FontStyles.Italic;
                }
                // Middle paragraphs (like questions) are also white
                else
                {
                    tb.Foreground = System.Windows.Media.Brushes.White;
                }

                MessageContainer.Children.Add(tb);
            }

            BtnYes.Content = yesText;
            if (string.IsNullOrEmpty(noText))
            {
                BtnNo.Visibility = Visibility.Collapsed;
                // Center the Yes button if it's the only one
                BtnYes.Margin = new Thickness(0);
                ButtonsPanel.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                BtnNo.Content = noText;
            }

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            IsDontAskAgainChecked = ChkDontAskAgain.IsChecked ?? false;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            IsDontAskAgainChecked = ChkDontAskAgain.IsChecked ?? false;
            Close();
        }

        private static Window? GetSafeOwner(Window? candidate)
        {
            try
            {
                if (candidate != null && !(candidate is HudWindow) && candidate.IsLoaded && candidate.IsVisible)
                {
                    var handle = new System.Windows.Interop.WindowInteropHelper(candidate).Handle;
                    if (handle != IntPtr.Zero)
                        return candidate;
                }

                if (Application.Current != null)
                {
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win != null && !(win is HudWindow) && win.IsLoaded && win.IsVisible)
                        {
                            var handle = new System.Windows.Interop.WindowInteropHelper(win).Handle;
                            if (handle != IntPtr.Zero)
                                return win;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static MessageBoxResult Show(Window? owner, string message, string title, string yesText, string noText)
        {
            var msgBox = new CustomMessageBox(message, title, yesText, noText);
            var safeOwner = GetSafeOwner(owner);
            if (safeOwner != null)
            {
                try
                {
                    msgBox.Owner = safeOwner;
                }
                catch
                {
                    msgBox.Owner = null;
                    msgBox.ShowInTaskbar = true;
                }
            }
            else
            {
                msgBox.Owner = null;
                msgBox.ShowInTaskbar = true;
                msgBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            msgBox.Loaded += (s, e) =>
            {
                try
                {
                    msgBox.Activate();
                    msgBox.Focus();
                }
                catch
                {
                }
            };
            msgBox.ShowDialog();
            return msgBox.Result;
        }

        public static (MessageBoxResult result, bool isCheckboxChecked) ShowWithCheckbox(Window? owner, string message, string title, string yesText, string noText, string checkboxText)
        {
            var msgBox = new CustomMessageBox(message, title, yesText, noText, true, checkboxText);
            var safeOwner = GetSafeOwner(owner);
            if (safeOwner != null)
            {
                try
                {
                    msgBox.Owner = safeOwner;
                }
                catch
                {
                    msgBox.Owner = null;
                    msgBox.ShowInTaskbar = true;
                }
            }
            else
            {
                msgBox.Owner = null;
                msgBox.ShowInTaskbar = true;
                msgBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            msgBox.Loaded += (s, e) =>
            {
                try
                {
                    msgBox.Activate();
                    msgBox.Focus();
                }
                catch
                {
                }
            };
            msgBox.ShowDialog();
            return (msgBox.Result, msgBox.IsDontAskAgainChecked);
        }
    }
}
