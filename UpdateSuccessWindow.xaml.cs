using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace ETSOverlay
{
    public partial class UpdateSuccessWindow : Window
    {
        public UpdateSuccessWindow(string language)
        {
            InitializeComponent();
            
            // Set language specific text
            if (language == "uk")
            {
                TitleBlock.Text = "ОНОВЛЕННЯ ВСТАНОВЛЕНО";
                MessageBlock.Text = "Оновлення успішно встановлено!";
                SubmessageBlock.Text = "Дякуємо за використання TruckSim Widget. Ви можете слідкувати за новинами на нашому сайті або в репозиторії GitHub.";
                BtnWebsite.Content = "Сайт проєкту";
                BtnClose.Content = "Закрити";
            }
            else
            {
                TitleBlock.Text = "UPDATE SUCCESSFUL";
                MessageBlock.Text = "Update successfully installed!";
                SubmessageBlock.Text = "Thank you for using TruckSim Widget. You can follow news on our website or GitHub repository.";
                BtnWebsite.Content = "Project Website";
                BtnClose.Content = "Close";
            }

            MouseLeftButtonDown += (s, e) => { DragMove(); };
        }

        private void BtnWebsite_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://trucksim.maksym.uk");
            Close();
        }

        private void BtnGitHub_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/TheVarmax/TruckSim-Widget");
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Fallback or ignore
                Console.WriteLine($"Error opening URL: {ex.Message}");
            }
        }
    }
}
