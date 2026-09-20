using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
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
                    IntPtr monitor = MonitorFromWindow(hwnd, 2);
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
                MarkdownViewer.Markdown = PreprocessReleaseNotes(releaseBody, language);
            }
            else
            {
                MarkdownViewer.Visibility = Visibility.Collapsed;
                FallbackBlock.Visibility = Visibility.Visible;
                FallbackBlock.Text = language == "uk" ? "Опис оновлення наразі недоступний." : "Release notes are unavailable right now.";
            }

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

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

        internal static string PreprocessReleaseNotes(string? markdown, string language)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();
            var regex = new Regex(@"^\s*(?:(>)\s*)?\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION|INFO)\](?::)?(?:\s*(.*))?$", RegexOptions.IgnoreCase);
            bool inSyntheticBlockquote = false;
            bool inCodeBlock = false;

            foreach (var line in lines)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("```"))
                {
                    inCodeBlock = !inCodeBlock;
                    if (inSyntheticBlockquote)
                    {
                        inSyntheticBlockquote = false;
                    }
                    sb.AppendLine(line);
                    continue;
                }

                if (!inCodeBlock)
                {
                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        bool hadQuotePrefix = match.Groups[1].Success;
                        string type = match.Groups[2].Value.ToUpperInvariant();
                        string remaining = match.Groups[3].Value.Trim();

                        string icon = "ℹ️";
                        string title = language == "uk" ? "Примітка" : "Note";

                        switch (type)
                        {
                            case "TIP":
                                icon = "💡";
                                title = language == "uk" ? "Порада" : "Tip";
                                break;
                            case "IMPORTANT":
                                icon = "📢";
                                title = language == "uk" ? "Важливо" : "Important";
                                break;
                            case "WARNING":
                                icon = "⚠️";
                                title = language == "uk" ? "Попередження" : "Warning";
                                break;
                            case "CAUTION":
                                icon = "🛑";
                                title = language == "uk" ? "Увага" : "Caution";
                                break;
                        }

                        while (sb.Length > 0 && (sb[sb.Length - 1] == '\r' || sb[sb.Length - 1] == '\n' || sb[sb.Length - 1] == ' '))
                        {
                            sb.Length--;
                        }

                        if (sb.Length > 0)
                        {
                            sb.AppendLine();
                            sb.AppendLine();
                        }

                        sb.AppendLine($"> {icon} **{title}**");
                        sb.AppendLine(">");
                        if (!string.IsNullOrWhiteSpace(remaining))
                        {
                            sb.AppendLine($"> {remaining}");
                        }

                        inSyntheticBlockquote = !hadQuotePrefix;
                        continue;
                    }

                    if (inSyntheticBlockquote)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            inSyntheticBlockquote = false;
                            sb.AppendLine();
                            continue;
                        }

                        if (trimmed.StartsWith("#") || trimmed.StartsWith("---") || trimmed.StartsWith("***"))
                        {
                            inSyntheticBlockquote = false;
                            sb.AppendLine(line);
                            continue;
                        }

                        if (trimmed.StartsWith(">"))
                        {
                            sb.AppendLine(line);
                        }
                        else
                        {
                            sb.AppendLine($"> {trimmed}");
                        }
                        continue;
                    }
                }

                sb.AppendLine(line);
            }

            return sb.ToString();
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
                Console.WriteLine($"Error opening URL: {ex.Message}");
            }
        }
    }
}

