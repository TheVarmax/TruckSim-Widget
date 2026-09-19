using System.Security.Principal;
using System.Text.RegularExpressions;
using TruckSimWidgetSetup.Common;

namespace TruckSimWidgetSetup.Diagnostics;

public static class InstallerLogger
{
    private static readonly object LockObj = new();
    private static string? _customLogPath;

    public static void SetCustomLogPath(string path)
    {
        lock (LockObj)
        {
            _customLogPath = path;
        }
    }

    public static string LogFilePath => _customLogPath ?? Constants.GetInstallerLogFilePath();

    public static void InitSession(string version, string commandArgs)
    {
        LogInfo("================================================================");
        LogInfo("TruckSim Widget Custom Installer Session Started");
        LogInfo($"Installer Version : {version}");
        LogInfo($"Command Args      : {commandArgs}");
        LogInfo($"Is Admin / Elevated: {IsAdministrator()}");
        LogInfo($"Is 64-Bit OS      : {Environment.Is64BitOperatingSystem}");
        LogInfo($"AppData Directory : {Constants.GetUserDataDir()}");
        LogInfo("================================================================");
    }

    public static void LogInfo(string message) => Log("INFO", message);
    public static void LogWarn(string message) => Log("WARN", message);
    public static void LogErr(string message, Exception? ex = null)
    {
        if (ex != null)
        {
            Log("ERROR", $"{message}: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }
        else
        {
            Log("ERROR", message);
        }
    }
    public static void LogDebug(string message) => Log("DEBUG", message);

    public static void LogIgnoredUserFile(string path)
    {
        Log("INFO", $"[IGNORED USER FILE] {path}");
    }

    private static void Log(string level, string message)
    {
        try
        {
            string cleanMessage = SanitizeMessage(message);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string line = $"[{timestamp}] [{level}] {cleanMessage}{Environment.NewLine}";

            lock (LockObj)
            {
                string targetPath = LogFilePath;
                string? dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(targetPath, line);
            }
        }
        catch
        {
            // Logging failure must never crash the installer
        }
    }

    internal static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;

        string sanitized = message;

        try
        {
            string userName = Environment.UserName;
            if (!string.IsNullOrEmpty(userName) && userName.Length > 1)
            {
                sanitized = sanitized.Replace($@"\Users\{userName}", @"\Users\<redacted>", StringComparison.OrdinalIgnoreCase);
                sanitized = sanitized.Replace($@"/Users/{userName}", @"/Users/<redacted>", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }

        sanitized = RedactKeyword(sanitized, "token=");
        sanitized = RedactKeyword(sanitized, "password=");
        sanitized = RedactKeyword(sanitized, "secret=");
        sanitized = RedactKeyword(sanitized, "key=");
        sanitized = RedactKeyword(sanitized, "bearer ");

        return sanitized;
    }

    private static string RedactKeyword(string input, string keyword)
    {
        int idx = input.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            int start = idx + keyword.Length;
            int end = start;
            while (end < input.Length && input[end] != ' ' && input[end] != '&' && input[end] != ';' && input[end] != '"' && input[end] != '\'')
            {
                end++;
            }

            if (end > start)
            {
                input = input.Substring(0, start) + "[REDACTED]" + input.Substring(end);
            }

            idx = input.IndexOf(keyword, start + "[REDACTED]".Length, StringComparison.OrdinalIgnoreCase);
        }

        return input;
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
