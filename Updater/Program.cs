using System.Diagnostics;
using System.IO.Compression;

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

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string logPath = "";

        try
        {
            if (args.Length < 4)
            {
                MessageBox.Show(
                    "Usage: updater.exe <zipPath> <appDir> <appExe> <logPath>",
                    "TruckSim Widget Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            string zipPath = args[0];
            string appDir = args[1];
            string appExe = args[2];
            logPath = args[3];

            WriteLog(logPath, "=== UPDATER STARTED ===");
            WriteLog(logPath, $"ZIP: {zipPath}");
            WriteLog(logPath, $"App dir: {appDir}");
            WriteLog(logPath, $"App exe: {appExe}");

            // 1. Ждём завершения основного приложения
            WriteLog(logPath, "Waiting for main application to close...");
            WaitForProcessToExit("TruckSim Widget", timeoutSeconds: 30);
            WriteLog(logPath, "Main application closed.");

            // 2. Распаковываем ZIP во временную папку
            string extractDir = Path.Combine(Path.GetDirectoryName(zipPath)!, "extracted");
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);

            WriteLog(logPath, $"Extracting ZIP to: {extractDir}");
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // 3. Находим корневую папку с файлами внутри ZIP
            // Структура ZIP: TruckSim Widget (x.x.x)/ -> файлы
            string sourceDir = FindSourceDirectory(extractDir);
            WriteLog(logPath, $"Source directory: {sourceDir}");

            // 4. Копируем файлы в папку приложения
            WriteLog(logPath, "Replacing application files...");
            int copiedCount = CopyFilesRecursive(sourceDir, appDir, logPath);
            WriteLog(logPath, $"Files replaced: {copiedCount}");

            // 5. Очистка временных файлов
            WriteLog(logPath, "Cleaning up temp files...");
            try
            {
                if (Directory.Exists(Path.GetDirectoryName(zipPath)!))
                    Directory.Delete(Path.GetDirectoryName(zipPath)!, true);
            }
            catch (Exception ex)
            {
                WriteLog(logPath, $"Cleanup warning: {ex.Message}");
            }

            WriteLog(logPath, "=== UPDATE SUCCESSFUL ===");

            // 6. Открываем donate страницу
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
                WriteLog(logPath, $"Failed to open donate page: {ex.Message}");
            }

            // 7. Перезапускаем приложение
            WriteLog(logPath, $"Restarting application: {appExe}");
            Process.Start(new ProcessStartInfo
            {
                FileName = appExe,
                WorkingDirectory = appDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            string errorMessage = $"Update failed: {ex.Message}\n\n{ex.StackTrace}";
            WriteLog(logPath, $"=== UPDATE FAILED ===\n{errorMessage}");

            ShowErrorDialog(errorMessage, logPath);
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
    private static void ShowErrorDialog(string errorMessage, string logPath)
    {
        var form = new Form
        {
            Text = "TruckSim Widget — Update Error",
            Width = 500,
            Height = 340,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = System.Drawing.Color.FromArgb(30, 30, 35),
            ForeColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("Segoe UI", 10)
        };

        var lblTitle = new Label
        {
            Text = "❌ Update Failed",
            Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(232, 65, 65),
            Location = new System.Drawing.Point(20, 15),
            AutoSize = true
        };

        var lblMessage = new Label
        {
            Text = $"An error occurred during the update:\n{(errorMessage.Length > 200 ? errorMessage[..200] + "..." : errorMessage)}",
            Location = new System.Drawing.Point(20, 55),
            Size = new System.Drawing.Size(440, 80),
            ForeColor = System.Drawing.Color.FromArgb(200, 200, 200)
        };

        var lblContact = new Label
        {
            Text = $"Please send the log file to {SupportEmail} for support.",
            Location = new System.Drawing.Point(20, 140),
            AutoSize = true,
            ForeColor = System.Drawing.Color.FromArgb(180, 180, 180)
        };

        var btnCopyEmail = new Button
        {
            Text = $"📋 Copy email: {SupportEmail}",
            Location = new System.Drawing.Point(20, 180),
            Size = new System.Drawing.Size(220, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(55, 55, 65),
            ForeColor = System.Drawing.Color.White,
            Cursor = Cursors.Hand
        };
        btnCopyEmail.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 80, 90);
        btnCopyEmail.Click += (s, e) =>
        {
            try
            {
                Clipboard.SetText(SupportEmail);
                btnCopyEmail.Text = "✅ Copied!";
            }
            catch { }
        };

        var btnSaveLog = new Button
        {
            Text = "💾 Save log file...",
            Location = new System.Drawing.Point(250, 180),
            Size = new System.Drawing.Size(220, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(55, 55, 65),
            ForeColor = System.Drawing.Color.White,
            Cursor = Cursors.Hand
        };
        btnSaveLog.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 80, 90);
        btnSaveLog.Click += (s, e) =>
        {
            try
            {
                using var dialog = new SaveFileDialog
                {
                    Filter = "Log files (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = "trucksim_widget_log.txt",
                    Title = "Save update log"
                };

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                    {
                        File.Copy(logPath, dialog.FileName, overwrite: true);
                        btnSaveLog.Text = "✅ Log saved!";
                    }
                    else
                    {
                        // Если лог-файл не найден, сохраняем текст ошибки
                        File.WriteAllText(dialog.FileName, errorMessage);
                        btnSaveLog.Text = "✅ Error saved!";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save log: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var btnClose = new Button
        {
            Text = "Close",
            Location = new System.Drawing.Point(170, 240),
            Size = new System.Drawing.Size(150, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(70, 70, 80),
            ForeColor = System.Drawing.Color.White,
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK
        };
        btnClose.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(100, 100, 110);

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
