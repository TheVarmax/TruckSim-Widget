using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace ETSOverlay
{
    public partial class UpdateSuccessWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int left, top, right, bottom;
        }

        private string? _releaseUrl;

        public UpdateSuccessWindow(string language, string? releaseUrl = null, string? releaseName = null, string? releaseBody = null)
        {
            InitializeComponent();
            _releaseUrl = string.IsNullOrWhiteSpace(releaseUrl) ? "https://github.com/TheVarmax/TruckSim-Widget/releases" : releaseUrl;

            this.Loaded += (s, e) =>
            {
                try
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    IntPtr monitor = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
                    if (monitor != IntPtr.Zero)
                    {
                        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO)) };
                        if (GetMonitorInfo(monitor, ref info))
                        {
                            double workAreaHeight = info.rcWork.bottom - info.rcWork.top;
                            var source = PresentationSource.FromVisual(this);
                            if (source?.CompositionTarget != null)
                            {
                                double dpiY = source.CompositionTarget.TransformToDevice.M22;
                                this.MaxHeight = (workAreaHeight / dpiY) * 0.7;
                                return;
                            }
                        }
                    }
                }
                catch { }
                
                this.MaxHeight = SystemParameters.WorkArea.Height * 0.7;
            };

            if (language == "uk")
            {
                TitleBlock.Text = "ОНОВЛЕННЯ ВСТАНОВЛЕНО";
                BtnWebsite.Content = "Сайт проєкту";
                BtnClose.Content = "Закрити";
                SupportBlock.Text = "Потрібна допомога? Напишіть на support@trucksim.uk";
            }
            else
            {
                TitleBlock.Text = "UPDATE SUCCESSFUL";
                BtnWebsite.Content = "Project Website";
                BtnClose.Content = "Close";
                SupportBlock.Text = "Need help? Contact support@trucksim.uk";
            }

            if (!string.IsNullOrWhiteSpace(releaseName))
            {
                ReleaseNameBlock.Text = releaseName;
            }
            else
            {
                ReleaseNameBlock.Visibility = Visibility.Collapsed;
            }

            if (!string.IsNullOrWhiteSpace(releaseBody))
            {
                MarkdownViewer.Markdown = releaseBody;
            }
            else
            {
                MarkdownViewer.Visibility = Visibility.Collapsed;
                FallbackBlock.Visibility = Visibility.Visible;
                FallbackBlock.Text = language == "uk" ? "Опис оновлення наразі недоступний." : "Release notes are unavailable right now.";
            }

            MouseLeftButtonDown += (s, e) => { DragMove(); };

            CommandBindings.Add(new CommandBinding(NavigationCommands.GoToPage, (sender, e) =>
            {
                if (e.Parameter is string url)
                {
                    OpenUrl(url);
                }
                else if (e.Parameter is Uri uri)
                {
                    OpenUrl(uri.ToString());
                }
            }));
        }

        private void BtnWebsite_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://trucksim.uk");
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

