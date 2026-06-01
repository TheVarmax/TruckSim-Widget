using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace TruckSimUpdater;

static class Program
{
    private const string SuccessUrl = "https://successful.maksym.uk";
    private const string SupportEmail = "info@maksym.uk";

    // Файлы/папки, которые НЕ нужно перезаписывать при обновлении (пользовательские данные)
    private static readonly HashSet<string> ProtectedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "settings.json",
        "state.dat",
        "updater.exe",
        "app_log.txt",
        "log.txt"
    };

    // Цвета в стиле виджета
    private static readonly Color BgMain = Color.FromArgb(26, 28, 32);       // #1A1C20
    private static readonly Color BgCard = Color.FromArgb(37, 40, 48);       // #252830
    private static readonly Color BgBorder = Color.FromArgb(53, 56, 64);     // #353840
    private static readonly Color AccentColor = Color.FromArgb(122, 197, 205); // #7AC5CD
    private static readonly Color TextPrimary = Color.White;
    private static readonly Color TextSecondary = Color.FromArgb(176, 176, 176); // #B0B0B0
    private static readonly Color SuccessColor = Color.FromArgb(82, 193, 79);  // #52C14F
    private static readonly Color ErrorColor = Color.FromArgb(232, 65, 65);    // #E84141
    private static readonly Color ProgressTrack = Color.FromArgb(42, 44, 49);  // #2A2C31

    // Размеры окна
    private const int WindowWidth = 420;
    private const int WindowHeight = 260;
    private const int CornerRadius = 12;

    // Для перетаскивания окна без рамки
    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string logPath = "";

        try
        {
            if (args.Length < 6)
            {
                MessageBox.Show(
                    "Usage: updater.exe <downloadUrl> <assetName> <appDir> <appExe> <logPath> <language>",
                    "TruckSim Widget Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            string downloadUrl = args[0];
            string assetName = args[1];
            string appDir = args[2];
            string appExe = args[3];
            logPath = args[4];
            string language = args[5]; // "uk" или "en"

            WriteLog(logPath, "=== UPDATER STARTED ===");
            WriteLog(logPath, $"Download URL: {downloadUrl}");
            WriteLog(logPath, $"Asset name: {assetName}");
            WriteLog(logPath, $"App dir: {appDir}");
            WriteLog(logPath, $"App exe: {appExe}");
            WriteLog(logPath, $"Language: {language}");

            // Запускаем форму обновления
            Application.Run(new UpdateForm(downloadUrl, assetName, appDir, appExe, logPath, language));
        }
        catch (Exception ex)
        {
            string errorMessage = $"Update failed: {ex.Message}\n\n{ex.StackTrace}";
            WriteLog(logPath, $"=== UPDATE FAILED ===\n{errorMessage}");
            ShowErrorDialog(errorMessage, logPath, "en");
        }
    }

    /// <summary>
    /// Стилизованная форма обновления
    /// </summary>
    private class UpdateForm : Form
    {
        private readonly string _downloadUrl;
        private readonly string _assetName;
        private readonly string _appDir;
        private readonly string _appExe;
        private readonly string _logPath;
        private readonly string _lang;

        private Label _titleLabel = null!;
        private Label _statusLabel = null!;
        private Panel _progressTrack = null!;
        private Panel _progressFill = null!;
        private Label _progressLabel = null!;
        private Label _stepLabel = null!;
        private System.Windows.Forms.Timer _dotTimer = null!;
        private int _dotCount = 0;
        private string _currentStatusBase = "";

        public UpdateForm(string downloadUrl, string assetName, string appDir, string appExe, string logPath, string language)
        {
            _downloadUrl = downloadUrl;
            _assetName = assetName;
            _appDir = appDir;
            _appExe = appExe;
            _logPath = logPath;
            _lang = language;

            InitializeUI();
        }

        private void InitializeUI()
        {
            // Форма без рамки
            Text = "TruckSim Widget — Update";
            Size = new Size(WindowWidth, WindowHeight);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgMain;
            DoubleBuffered = true;
            ShowInTaskbar = true;

            // Перетаскивание
            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };

            // Иконка TruckSim Widget
            var iconLabel = new Label
            {
                Text = "🚛",
                Font = new Font("Segoe UI Emoji", 18),
                ForeColor = TextPrimary,
                Location = new Point(24, 22),
                AutoSize = true
            };
            iconLabel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };

            // Заголовок
            _titleLabel = new Label
            {
                Text = "TruckSim Widget",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(64, 24),
                AutoSize = true
            };
            _titleLabel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };

            // Подзаголовок
            var subtitleLabel = new Label
            {
                Text = _lang == "uk" ? "Оновлення" : "Updating",
                Font = new Font("Segoe UI", 11),
                ForeColor = AccentColor,
                Location = new Point(66, 52),
                AutoSize = true
            };
            subtitleLabel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };

            // Разделитель
            var separator = new Panel
            {
                BackColor = BgBorder,
                Location = new Point(24, 82),
                Size = new Size(WindowWidth - 48, 1)
            };

            // Шаг обновления (1/4, 2/4, ...)
            _stepLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9),
                ForeColor = TextSecondary,
                Location = new Point(24, 96),
                AutoSize = true
            };

            // Статус
            _statusLabel = new Label
            {
                Text = _lang == "uk" ? "Підготовка..." : "Preparing...",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(24, 118),
                Size = new Size(WindowWidth - 48, 28),
                AutoEllipsis = true
            };

            // Прогресс-бар — трек
            _progressTrack = new Panel
            {
                BackColor = ProgressTrack,
                Location = new Point(24, 158),
                Size = new Size(WindowWidth - 48, 10)
            };
            MakeRounded(_progressTrack, 5);

            // Прогресс-бар — заполнение
            _progressFill = new Panel
            {
                BackColor = AccentColor,
                Location = new Point(0, 0),
                Size = new Size(0, 10)
            };
            _progressTrack.Controls.Add(_progressFill);

            // Процент
            _progressLabel = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = AccentColor,
                Location = new Point(24, 176),
                AutoSize = true
            };

            // Размер скачивания
            Controls.AddRange(new Control[] { iconLabel, _titleLabel, subtitleLabel, separator, _stepLabel, _statusLabel, _progressTrack, _progressLabel });

            // Таймер для анимации точек
            _dotTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _dotTimer.Tick += (s, e) =>
            {
                _dotCount = (_dotCount + 1) % 4;
                string dots = new string('.', _dotCount);
                _statusLabel.Text = _currentStatusBase + dots;
            };

            // Закруглённые углы формы
            Region = CreateRoundedRegion(WindowWidth, WindowHeight, CornerRadius);

            // Запускаем обновление после показа формы
            Shown += async (s, e) => await RunUpdateAsync();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Тонкая рамка
            using var pen = new Pen(BgBorder, 1);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = CreateRoundedRectPath(rect, CornerRadius);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }

        private async Task RunUpdateAsync()
        {
            try
            {
                // Шаг 1: Ждём закрытия основного приложения
                SetStep(1, 4);
                SetStatus(_lang == "uk" ? "Очікування закриття програми" : "Waiting for app to close", true);
                WriteLog(_logPath, "Waiting for main application to close...");

                await Task.Run(() => WaitForProcessToExit("TruckSim Widget", timeoutSeconds: 30));
                WriteLog(_logPath, "Main application closed.");

                // Шаг 2: Скачиваем
                SetStep(2, 4);
                SetStatus(_lang == "uk" ? "Завантаження оновлення" : "Downloading update", true);
                WriteLog(_logPath, $"Downloading update from: {_downloadUrl}");

                string tempDir = Path.Combine(Path.GetTempPath(), "TruckSimWidget_Update");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                string zipPath = Path.Combine(tempDir, _assetName);
                await DownloadWithProgressAsync(_downloadUrl, zipPath);
                WriteLog(_logPath, $"Download complete: {new FileInfo(zipPath).Length} bytes");

                // Шаг 3: Распаковываем и заменяем файлы
                SetStep(3, 4);
                SetStatus(_lang == "uk" ? "Встановлення оновлення" : "Installing update", true);
                SetProgress(0);

                string extractDir = Path.Combine(tempDir, "extracted");
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);

                WriteLog(_logPath, $"Extracting ZIP to: {extractDir}");
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractDir));
                SetProgress(30);

                string sourceDir = FindSourceDirectory(extractDir);
                WriteLog(_logPath, $"Source directory: {sourceDir}");

                WriteLog(_logPath, "Replacing application files...");
                int copiedCount = await Task.Run(() => CopyFilesRecursive(sourceDir, _appDir, _logPath));
                WriteLog(_logPath, $"Files replaced: {copiedCount}");
                SetProgress(80);

                // Очистка временных файлов
                WriteLog(_logPath, "Cleaning up temp files...");
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    WriteLog(_logPath, $"Cleanup warning: {ex.Message}");
                }
                SetProgress(100);

                WriteLog(_logPath, "=== UPDATE SUCCESSFUL ===");

                // Шаг 4: Готово
                SetStep(4, 4);
                _dotTimer.Stop();
                _statusLabel.ForeColor = SuccessColor;
                _statusLabel.Text = _lang == "uk" ? "✅ Оновлення встановлено!" : "✅ Update installed!";
                _progressFill.BackColor = SuccessColor;

                // Открываем donate страницу
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = SuccessUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    WriteLog(_logPath, $"Failed to open success page: {ex.Message}");
                }

                // Небольшая пауза перед перезапуском
                await Task.Delay(1500);

                // Перезапускаем приложение
                WriteLog(_logPath, $"Restarting application: {_appExe}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = _appExe,
                    WorkingDirectory = _appDir,
                    UseShellExecute = true
                });

                Close();
            }
            catch (Exception ex)
            {
                _dotTimer.Stop();
                string errorMessage = $"Update failed: {ex.Message}\n\n{ex.StackTrace}";
                WriteLog(_logPath, $"=== UPDATE FAILED ===\n{errorMessage}");

                _statusLabel.ForeColor = ErrorColor;
                _statusLabel.Text = _lang == "uk" ? "❌ Помилка оновлення" : "❌ Update failed";
                _progressFill.BackColor = ErrorColor;

                ShowErrorDialog(errorMessage, _logPath, _lang);
                Close();
            }
        }

        private async Task DownloadWithProgressAsync(string url, string filePath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TruckSimWidget-Updater/1.0");
            client.Timeout = TimeSpan.FromMinutes(10);

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long downloadedBytes = 0;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;
            DateTime lastUiUpdate = DateTime.Now;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                if ((DateTime.Now - lastUiUpdate).TotalMilliseconds > 80)
                {
                    lastUiUpdate = DateTime.Now;
                    if (totalBytes > 0)
                    {
                        double progress = (double)downloadedBytes / totalBytes * 100;
                        string sizeMb = (downloadedBytes / 1024.0 / 1024.0).ToString("F1");
                        string totalMb = (totalBytes / 1024.0 / 1024.0).ToString("F1");

                        Invoke(() =>
                        {
                            SetProgress(progress);
                            _progressLabel.Text = _lang == "uk"
                                ? $"{sizeMb} / {totalMb} МБ ({progress:F0}%)"
                                : $"{sizeMb} / {totalMb} MB ({progress:F0}%)";
                        });
                    }
                }
            }

            Invoke(() =>
            {
                SetProgress(100);
                _progressLabel.Text = _lang == "uk"
                    ? $"✅ {(downloadedBytes / 1024.0 / 1024.0):F1} МБ"
                    : $"✅ {(downloadedBytes / 1024.0 / 1024.0):F1} MB";
            });
        }

        private void SetStatus(string text, bool animate)
        {
            _currentStatusBase = text;
            _dotCount = 0;
            _statusLabel.ForeColor = TextPrimary;
            _statusLabel.Text = text;

            if (animate)
            {
                _dotTimer.Start();
            }
            else
            {
                _dotTimer.Stop();
            }
        }

        private void SetStep(int step, int total)
        {
            _stepLabel.Text = _lang == "uk"
                ? $"Крок {step} з {total}"
                : $"Step {step} of {total}";
        }

        private void SetProgress(double percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            int trackWidth = _progressTrack.Width;
            int fillWidth = (int)(trackWidth * percent / 100.0);
            _progressFill.Size = new Size(fillWidth, _progressTrack.Height);
            MakeRounded(_progressFill, 5);

            if (percent > 0)
            {
                _progressLabel.Text = $"{percent:F0}%";
            }
        }

        private static void MakeRounded(Control control, int radius)
        {
            control.Region = CreateRoundedRegion(control.Width, control.Height, radius);
        }

        private static Region CreateRoundedRegion(int width, int height, int radius)
        {
            using var path = CreateRoundedRectPath(new Rectangle(0, 0, width, height), radius);
            return new Region(path);
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Ждёт завершения процесса по имени с таймаутом
    /// </summary>
    private static void WaitForProcessToExit(string processName, int timeoutSeconds)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
                return;

            foreach (var p in processes)
            {
                try { p.WaitForExit(1000); } catch { }
                p.Dispose();
            }

            Thread.Sleep(500);
        }

        // Если таймаут — пробуем убить процесс
        foreach (var p in Process.GetProcessesByName(processName))
        {
            try { p.Kill(); p.WaitForExit(5000); } catch { }
            p.Dispose();
        }
    }

    /// <summary>
    /// Находит папку с файлами внутри распакованного ZIP.
    /// ZIP имеет структуру: корень / TruckSim Widget (x.x.x) / файлы
    /// </summary>
    private static string FindSourceDirectory(string extractDir)
    {
        // Проверяем, есть ли вложенная папка
        var subdirs = Directory.GetDirectories(extractDir);
        if (subdirs.Length == 1)
        {
            // Проверяем, что внутри есть exe или dll (признак папки с приложением)
            string candidate = subdirs[0];
            if (Directory.GetFiles(candidate, "*.exe").Length > 0 ||
                Directory.GetFiles(candidate, "*.dll").Length > 0)
            {
                return candidate;
            }
        }

        // Если вложенных папок нет или их несколько — файлы в корне
        if (Directory.GetFiles(extractDir, "*.exe").Length > 0 ||
            Directory.GetFiles(extractDir, "*.dll").Length > 0)
        {
            return extractDir;
        }

        // Рекурсивный поиск
        foreach (var dir in subdirs)
        {
            string found = FindSourceDirectory(dir);
            if (found != dir || Directory.GetFiles(dir, "*.exe").Length > 0)
                return found;
        }

        return extractDir;
    }

    /// <summary>
    /// Рекурсивно копирует файлы из source в destination, пропуская защищённые файлы
    /// </summary>
    private static int CopyFilesRecursive(string sourceDir, string destDir, string logPath)
    {
        int count = 0;

        foreach (string sourceFile in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(sourceFile);

            // Пропускаем защищённые файлы
            if (ProtectedFiles.Contains(fileName))
            {
                WriteLog(logPath, $"  Skipped (protected): {fileName}");
                continue;
            }

            string destFile = Path.Combine(destDir, fileName);

            // Пытаемся скопировать с повторными попытками
            bool copied = false;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    File.Copy(sourceFile, destFile, overwrite: true);
                    copied = true;
                    count++;
                    break;
                }
                catch (IOException) when (attempt < 3)
                {
                    WriteLog(logPath, $"  Retry {attempt}/3: {fileName}");
                    Thread.Sleep(500 * attempt);
                }
            }

            if (!copied)
            {
                WriteLog(logPath, $"  FAILED to copy: {fileName}");
                throw new IOException($"Cannot overwrite file: {fileName}. The file may be locked by another process.");
            }
        }

        // Рекурсивно обрабатываем подпапки
        foreach (string sourceSubDir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(sourceSubDir);
            string destSubDir = Path.Combine(destDir, dirName);

            if (!Directory.Exists(destSubDir))
                Directory.CreateDirectory(destSubDir);

            count += CopyFilesRecursive(sourceSubDir, destSubDir, logPath);
        }

        return count;
    }

    /// <summary>
    /// Показывает диалог ошибки обновления с кнопками «Скопировать почту» и «Сохранить лог»
    /// </summary>
    private static void ShowErrorDialog(string errorMessage, string logPath, string lang)
    {
        bool isUk = lang == "uk";

        var form = new Form
        {
            Text = "TruckSim Widget — " + (isUk ? "Помилка оновлення" : "Update Error"),
            Width = 500,
            Height = 340,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = BgMain,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 10)
        };

        var lblTitle = new Label
        {
            Text = isUk ? "❌ Помилка оновлення" : "❌ Update Failed",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = ErrorColor,
            Location = new Point(20, 15),
            AutoSize = true
        };

        var lblMessage = new Label
        {
            Text = (isUk ? "Під час оновлення виникла помилка:\n" : "An error occurred during the update:\n") +
                   (errorMessage.Length > 200 ? errorMessage[..200] + "..." : errorMessage),
            Location = new Point(20, 55),
            Size = new Size(440, 80),
            ForeColor = TextSecondary
        };

        var lblContact = new Label
        {
            Text = isUk
                ? $"Будь ласка, надішліть лог на {SupportEmail} для підтримки."
                : $"Please send the log file to {SupportEmail} for support.",
            Location = new Point(20, 140),
            AutoSize = true,
            ForeColor = Color.FromArgb(180, 180, 180)
        };

        var btnCopyEmail = new Button
        {
            Text = $"📋 {(isUk ? "Копіювати email" : "Copy email")}: {SupportEmail}",
            Location = new Point(20, 180),
            Size = new Size(220, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = BgCard,
            ForeColor = TextPrimary,
            Cursor = Cursors.Hand
        };
        btnCopyEmail.FlatAppearance.BorderColor = BgBorder;
        btnCopyEmail.Click += (s, e) =>
        {
            try
            {
                Clipboard.SetText(SupportEmail);
                btnCopyEmail.Text = isUk ? "✅ Скопійовано!" : "✅ Copied!";
            }
            catch { }
        };

        var btnSaveLog = new Button
        {
            Text = isUk ? "💾 Зберегти лог..." : "💾 Save log file...",
            Location = new Point(250, 180),
            Size = new Size(220, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = BgCard,
            ForeColor = TextPrimary,
            Cursor = Cursors.Hand
        };
        btnSaveLog.FlatAppearance.BorderColor = BgBorder;
        btnSaveLog.Click += (s, e) =>
        {
            try
            {
                using var dialog = new SaveFileDialog
                {
                    Filter = isUk
                        ? "Текстові файли (*.txt)|*.txt|Усі файли (*.*)|*.*"
                        : "Log files (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = "trucksim_widget_log.txt",
                    Title = isUk ? "Зберегти лог" : "Save update log"
                };

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                    {
                        File.Copy(logPath, dialog.FileName, overwrite: true);
                        btnSaveLog.Text = isUk ? "✅ Лог збережено!" : "✅ Log saved!";
                    }
                    else
                    {
                        // Если лог-файл не найден, сохраняем текст ошибки
                        File.WriteAllText(dialog.FileName, errorMessage);
                        btnSaveLog.Text = isUk ? "✅ Помилку збережено!" : "✅ Error saved!";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    (isUk ? "Не вдалося зберегти лог: " : "Failed to save log: ") + ex.Message,
                    isUk ? "Помилка" : "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var btnClose = new Button
        {
            Text = isUk ? "Закрити" : "Close",
            Location = new Point(170, 240),
            Size = new Size(150, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(70, 70, 80),
            ForeColor = TextPrimary,
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 110);

        form.Controls.AddRange(new Control[] { lblTitle, lblMessage, lblContact, btnCopyEmail, btnSaveLog, btnClose });
        form.AcceptButton = btnClose;
        form.ShowDialog();
    }

    /// <summary>
    /// Записывает сообщение в лог-файл
    /// </summary>
    private static void WriteLog(string logPath, string message)
    {
        if (string.IsNullOrEmpty(logPath)) return;
        try
        {
            string dir = Path.GetDirectoryName(logPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [UPDATER] {message}\n");
        }
        catch { }
    }
}
