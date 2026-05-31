using System;
using System.Windows;
using System.Windows.Input;

namespace ETSOverlay
{
    public partial class CustomMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.No;

        public CustomMessageBox(string message, string title, string yesText, string noText)
        {
            InitializeComponent();
            TitleBlock.Text = title.ToUpper();
            
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
            BtnNo.Content = noText;

            MouseLeftButtonDown += (s, e) => { DragMove(); };
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        public static MessageBoxResult Show(Window owner, string message, string title, string yesText = "Yes", string noText = "No")
        {
            var dialog = new CustomMessageBox(message, title, yesText, noText)
            {
                Owner = owner
            };
            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}
