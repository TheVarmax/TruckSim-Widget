using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace ETSOverlay
{
    public partial class UpdateSuccessWindow : Window
    {
        private string? _releaseUrl;

        public UpdateSuccessWindow(string language, string? releaseUrl = null)
        {
            InitializeComponent();
            _releaseUrl = releaseUrl ?? "https://github.com/TheVarmax/TruckSim-Widget/releases/latest";

            
            // Set language specific text
            if (language == "uk")
            {
                TitleBlock.Text = "ОНОВЛЕННЯ ВСТАНОВЛЕНО";
                MessageBlock.Text = "Оновлення успішно встановлено!";
                SubmessageBlock.Text = "Дякуємо за використання TruckSim Widget. Ви можете слідкувати за новинами на нашому сайті або в репозиторії GitHub.";
                BtnWebsite.Content = "Сайт проєкту";
                BtnClose.Content = "Закрити";
                BtnReleaseNotes.Content = "Опис оновлення";
            }
            else
            {
                TitleBlock.Text = "UPDATE SUCCESSFUL";
                MessageBlock.Text = "Update successfully installed!";
                SubmessageBlock.Text = "Thank you for using TruckSim Widget. You can follow news on our website or GitHub repository.";
                BtnWebsite.Content = "Project Website";
                BtnClose.Content = "Close";
                BtnReleaseNotes.Content = "Release Notes";
            }

            MouseLeftButtonDown += (s, e) => { DragMove(); };
        }

        private void BtnWebsite_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://trucksim.maksym.uk");
            Close();
        }

        private void BtnReleaseNotes_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(_releaseUrl!);
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
