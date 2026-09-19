using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace TruckSimWidget.ElevatedHelper;

internal static class Program
{
    private const int EXIT_SUCCESS = 0;
    private const int EXIT_INVALID_ARGS = 1;
    private const int EXIT_PATH_FORBIDDEN = 2;
    private const int EXIT_NOT_FOUND = 3;
    private const int EXIT_IO_ERROR = 4;
    private const int EXIT_ACCESS_DENIED = 5;

    private static readonly Regex GameBackupRegex = new(@"^scs-telemetry\.dll\.backup_\d{8}_\d{6}\.bak$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StagingRollbackRegex = new(@"^(ETS2|ATS)_rollback_\d{8}_\d{6}\.dll$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ElevatedHelper.exe <copy|move|delete|mkdir> <path1> [path2] [--allow-root <dir>]");
                return EXIT_INVALID_ARGS;
            }

            var positionalArgs = new List<string>();
            var allowedRoots = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].Trim();
                if (arg.Equals("--allow-root", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        allowedRoots.Add(args[++i].Trim());
                    }
                }
                else if (arg.StartsWith("--allow-root=", StringComparison.OrdinalIgnoreCase))
                {
                    allowedRoots.Add(arg.Substring(13).Trim());
                }
                else
                {
                    positionalArgs.Add(arg);
                }
            }

            if (positionalArgs.Count < 2)
            {
                Console.Error.WriteLine("Error: Missing required arguments.");
                return EXIT_INVALID_ARGS;
            }

            string command = positionalArgs[0].ToLowerInvariant();
            string path1 = positionalArgs[1];
            string path2 = positionalArgs.Count > 2 ? positionalArgs[2] : string.Empty;

            switch (command)
            {
                case "copy":
                    if (string.IsNullOrEmpty(path2)) return EXIT_INVALID_ARGS;
                    return ExecuteCopy(path1, path2, allowedRoots);

                case "move":
                    if (string.IsNullOrEmpty(path2)) return EXIT_INVALID_ARGS;
                    return ExecuteMove(path1, path2, allowedRoots);

                case "delete":
                    return ExecuteDelete(path1, allowedRoots);

                case "mkdir":
                    return ExecuteMkdir(path1, allowedRoots);

                default:
                    Console.Error.WriteLine($"Unknown command: {command}");
                    return EXIT_INVALID_ARGS;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"Access denied: {ex.Message}");
            return EXIT_ACCESS_DENIED;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return EXIT_IO_ERROR;
        }
    }

    private static int ExecuteCopy(string source, string destination, List<string> allowedRoots)
    {
        if (!IsAllowedPath(source, isDirectory: false, isSourceOnly: true, allowedRoots) ||
            !IsAllowedPath(destination, isDirectory: false, isSourceOnly: false, allowedRoots))
        {
            Console.Error.WriteLine("Security violation: path not allowed for elevated copy.");
            return EXIT_PATH_FORBIDDEN;
        }

        if (!File.Exists(source))
        {
            Console.Error.WriteLine($"Source file not found: {source}");
            return EXIT_NOT_FOUND;
        }

        string? destDir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            if (!IsAllowedPath(destDir, isDirectory: true, isSourceOnly: false, allowedRoots))
            {
                Console.Error.WriteLine("Security violation: destination directory not allowed.");
                return EXIT_PATH_FORBIDDEN;
            }
            Directory.CreateDirectory(destDir);
        }

        File.Copy(source, destination, overwrite: true);
        return EXIT_SUCCESS;
    }

    private static int ExecuteMove(string source, string destination, List<string> allowedRoots)
    {
        if (!IsAllowedPath(source, isDirectory: false, isSourceOnly: false, allowedRoots) ||
            !IsAllowedPath(destination, isDirectory: false, isSourceOnly: false, allowedRoots))
        {
            Console.Error.WriteLine("Security violation: path not allowed for elevated move.");
            return EXIT_PATH_FORBIDDEN;
        }

        if (!File.Exists(source))
        {
            Console.Error.WriteLine($"Source file not found: {source}");
            return EXIT_NOT_FOUND;
        }

        string? destDir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            if (!IsAllowedPath(destDir, isDirectory: true, isSourceOnly: false, allowedRoots))
            {
                Console.Error.WriteLine("Security violation: destination directory not allowed.");
                return EXIT_PATH_FORBIDDEN;
            }
            Directory.CreateDirectory(destDir);
        }

        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        File.Move(source, destination);
        return EXIT_SUCCESS;
    }

    private static int ExecuteDelete(string target, List<string> allowedRoots)
    {
        if (!IsAllowedPath(target, isDirectory: false, isSourceOnly: false, allowedRoots))
        {
            Console.Error.WriteLine("Security violation: path not allowed for elevated delete.");
            return EXIT_PATH_FORBIDDEN;
        }

        if (File.Exists(target))
        {
            File.Delete(target);
        }

        return EXIT_SUCCESS;
    }

    private static int ExecuteMkdir(string dir, List<string> allowedRoots)
    {
        if (!IsAllowedPath(dir, isDirectory: true, isSourceOnly: false, allowedRoots))
        {
            Console.Error.WriteLine("Security violation: path not allowed for elevated mkdir.");
            return EXIT_PATH_FORBIDDEN;
        }

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return EXIT_SUCCESS;
    }

    internal static bool IsAllowedPath(string path, bool isDirectory, List<string>? allowedRoots = null)
    {
        return IsAllowedPath(path, isDirectory, isSourceOnly: false, allowedRoots);
    }

    internal static bool IsAllowedPath(string path, bool isDirectory, bool isSourceOnly, List<string>? allowedRoots)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!Path.IsPathRooted(path)) return false;
        if (path.StartsWith(@"\\")) return false; // Reject UNC
        if (path.Contains("..")) return false; // Reject directory traversal
        if (path.Contains('*') || path.Contains('?')) return false; // Reject wildcards

        string normalized;
        try
        {
            normalized = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        string lower = normalized.ToLowerInvariant();

        // Reject Windows and System directories
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToLowerInvariant();
        string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System).ToLowerInvariant();
        string sysX86 = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86).ToLowerInvariant();

        if (!string.IsNullOrEmpty(winDir) && (lower == winDir || lower.StartsWith(winDir + Path.DirectorySeparatorChar))) return false;
        if (!string.IsNullOrEmpty(sysDir) && (lower == sysDir || lower.StartsWith(sysDir + Path.DirectorySeparatorChar))) return false;
        if (!string.IsNullOrEmpty(sysX86) && (lower == sysX86 || lower.StartsWith(sysX86 + Path.DirectorySeparatorChar))) return false;

        // Reject drive roots (e.g. C:\)
        string? root = Path.GetPathRoot(normalized);
        if (string.Equals(normalized.TrimEnd('\\'), root?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return false;

        // Check reparse points across the entire existing path chain
        if (HasReparsePointInPath(normalized))
            return false;

        // Built-in Widget root: %LOCALAPPDATA%\TruckSimWidget and %LOCALAPPDATA%\Programs\TruckSimWidget
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string widgetDataDir = Path.Combine(localAppData, "TruckSimWidget").ToLowerInvariant() + Path.DirectorySeparatorChar;
        string widgetAppDir = Path.Combine(localAppData, "Programs", "TruckSimWidget").ToLowerInvariant() + Path.DirectorySeparatorChar;
        string tempDir = Path.GetTempPath().ToLowerInvariant();
        if (!tempDir.EndsWith(Path.DirectorySeparatorChar.ToString())) tempDir += Path.DirectorySeparatorChar;

        bool isWithinWidgetScope = lower.StartsWith(widgetDataDir) || lower.StartsWith(widgetAppDir);
        bool isWithinTempSource = isSourceOnly && lower.StartsWith(tempDir);

        bool isWithinGameScope = false;
        if (allowedRoots != null)
        {
            foreach (var r in allowedRoots)
            {
                if (string.IsNullOrWhiteSpace(r)) continue;
                try
                {
                    string fullRoot = Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    string allowedPluginDir = Path.Combine(fullRoot, "bin", "win_x64", "plugins").ToLowerInvariant() + Path.DirectorySeparatorChar;

                    if (isDirectory)
                    {
                        string dirTrimmed = lower.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                        if (dirTrimmed.Equals(allowedPluginDir, StringComparison.OrdinalIgnoreCase))
                        {
                            isWithinGameScope = true;
                            break;
                        }
                    }
                    else
                    {
                        if (lower.StartsWith(allowedPluginDir, StringComparison.OrdinalIgnoreCase))
                        {
                            string rel = lower.Substring(allowedPluginDir.Length);
                            if (!rel.Contains(Path.DirectorySeparatorChar) && !rel.Contains('/'))
                            {
                                isWithinGameScope = true;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore invalid roots in list
                }
            }
        }

        if (!isWithinWidgetScope && !isWithinTempSource && !isWithinGameScope)
        {
            return false;
        }

        // Exact filename whitelist
        if (!isDirectory)
        {
            string fileName = Path.GetFileName(normalized);
            if (isWithinGameScope)
            {
                bool isAllowedGameFile = fileName.Equals("scs-telemetry.dll", StringComparison.OrdinalIgnoreCase) ||
                                         fileName.Equals("scs-telemetry.dll.trucksim_backup", StringComparison.OrdinalIgnoreCase) ||
                                         GameBackupRegex.IsMatch(fileName);
                if (!isAllowedGameFile) return false;
            }
            else if (isWithinWidgetScope || isWithinTempSource)
            {
                bool isAllowedWidgetFile = fileName.Equals("scs-telemetry.dll", StringComparison.OrdinalIgnoreCase) ||
                                           fileName.Equals("transaction_journal.json", StringComparison.OrdinalIgnoreCase) ||
                                           fileName.Equals("transaction_journal.json.tmp", StringComparison.OrdinalIgnoreCase) ||
                                           fileName.Equals("install-state.json", StringComparison.OrdinalIgnoreCase) ||
                                           fileName.Equals("install-state.json.tmp", StringComparison.OrdinalIgnoreCase) ||
                                           fileName.Equals("ElevatedHelper.exe", StringComparison.OrdinalIgnoreCase) ||
                                           StagingRollbackRegex.IsMatch(fileName);
                if (!isAllowedWidgetFile) return false;
            }
        }

        return true;
    }

    private static bool HasReparsePointInPath(string path)
    {
        try
        {
            string? current = path;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(current))
                {
                    var fi = new FileInfo(current);
                    if ((fi.Attributes & FileAttributes.ReparsePoint) != 0)
                        return true;
                }
                else if (Directory.Exists(current))
                {
                    var di = new DirectoryInfo(current);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                        return true;
                }

                current = Path.GetDirectoryName(current);
            }
        }
        catch
        {
            return true; // Fail closed if unable to inspect path attributes
        }

        return false;
    }
}
