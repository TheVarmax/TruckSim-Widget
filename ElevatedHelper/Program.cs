using System;
using System.IO;

namespace TruckSimWidget.ElevatedHelper;

internal static class Program
{
    private const int EXIT_SUCCESS = 0;
    private const int EXIT_INVALID_ARGS = 1;
    private const int EXIT_PATH_FORBIDDEN = 2;
    private const int EXIT_NOT_FOUND = 3;
    private const int EXIT_IO_ERROR = 4;
    private const int EXIT_ACCESS_DENIED = 5;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ElevatedHelper.exe <copy|move|delete|mkdir> <path1> [path2]");
                return EXIT_INVALID_ARGS;
            }

            string command = args[0].ToLowerInvariant().Trim();
            string path1 = args[1].Trim();
            string path2 = args.Length > 2 ? args[2].Trim() : string.Empty;

            switch (command)
            {
                case "copy":
                    if (string.IsNullOrEmpty(path2)) return EXIT_INVALID_ARGS;
                    return ExecuteCopy(path1, path2);

                case "move":
                    if (string.IsNullOrEmpty(path2)) return EXIT_INVALID_ARGS;
                    return ExecuteMove(path1, path2);

                case "delete":
                    return ExecuteDelete(path1);

                case "mkdir":
                    return ExecuteMkdir(path1);

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

    private static int ExecuteCopy(string source, string destination)
    {
        if (!IsAllowedPath(source, false) || !IsAllowedPath(destination, false))
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
            if (!IsAllowedPath(destDir, true))
            {
                Console.Error.WriteLine("Security violation: destination directory not allowed.");
                return EXIT_PATH_FORBIDDEN;
            }
            Directory.CreateDirectory(destDir);
        }

        File.Copy(source, destination, overwrite: true);
        return EXIT_SUCCESS;
    }

    private static int ExecuteMove(string source, string destination)
    {
        if (!IsAllowedPath(source, false) || !IsAllowedPath(destination, false))
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
            if (!IsAllowedPath(destDir, true))
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

    private static int ExecuteDelete(string target)
    {
        if (!IsAllowedPath(target, false))
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

    private static int ExecuteMkdir(string dir)
    {
        if (!IsAllowedPath(dir, true))
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

    internal static bool IsAllowedPath(string path, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!Path.IsPathRooted(path)) return false;
        if (path.StartsWith(@"\\")) return false; // Reject UNC
        if (path.Contains("..")) return false; // Reject traversal
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

        if (!string.IsNullOrEmpty(winDir) && lower.StartsWith(winDir)) return false;
        if (!string.IsNullOrEmpty(sysDir) && lower.StartsWith(sysDir)) return false;
        if (!string.IsNullOrEmpty(sysX86) && lower.StartsWith(sysX86)) return false;

        // Reject drive roots (e.g. C:\)
        string? root = Path.GetPathRoot(normalized);
        if (string.Equals(normalized.TrimEnd('\\'), root?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return false;

        // Verify target scope:
        // 1. Game telemetry plugin directories: must contain \bin\win_x64\plugins
        // 2. Widget data or installation directories: contains \trucksimwidget\ or \trucksim widget\
        // 3. Staging / temp area
        bool isGamePluginDir = lower.Contains(@"\bin\win_x64\plugins") || lower.Contains(@"/bin/win_x64/plugins");
        bool isWidgetDir = lower.Contains(@"\trucksimwidget") || lower.Contains(@"\trucksim widget");
        bool isTempOrStaging = lower.Contains(@"\temp\") || lower.Contains(@"\staging\") || lower.Contains(@"\tmp\");

        if (!isGamePluginDir && !isWidgetDir && !isTempOrStaging)
        {
            return false;
        }

        if (!isDirectory)
        {
            string fileName = Path.GetFileName(lower);
            bool isAllowedFile = fileName.StartsWith("scs-telemetry.dll") ||
                                 fileName.EndsWith(".dll") ||
                                 fileName.EndsWith(".trucksim_backup") ||
                                 fileName.EndsWith(".bak") ||
                                 fileName.EndsWith(".json");
            if (!isAllowedFile)
                return false;
        }

        return true;
    }
}
