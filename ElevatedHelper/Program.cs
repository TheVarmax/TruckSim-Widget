using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace TruckSimWidget.ElevatedHelper;

internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);
    private const uint LoadLibrarySearchApplicationDir = 0x00000200;
    private const uint LoadLibrarySearchSystem32 = 0x00000800;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const int FileAttributeTagInfoClass = 9;
    private const int EXIT_SUCCESS = 0;
    private const int EXIT_INVALID_ARGS = 1;
    private const int EXIT_PATH_FORBIDDEN = 2;
    private const int EXIT_NOT_FOUND = 3;
    private const int EXIT_IO_ERROR = 4;
    private const int EXIT_ACCESS_DENIED = 5;

    private static readonly Regex GameBackupRegex = new(@"^scs-telemetry\.dll\.backup_\d{8}_\d{6}\.bak$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StagingRollbackRegex = new(@"^((ETS2|ATS)_(rollback_\d{8}_\d{6}|new_plugin)\.dll|state_rollback_\d{8}_\d{6}\.json)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFileForNoFollowRead(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        out FileAttributeTagInfo fileInformation,
        uint bufferSize);

    public static int Main(string[] args)
    {
        try
        {
            if (!SetDefaultDllDirectories(LoadLibrarySearchApplicationDir | LoadLibrarySearchSystem32))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ElevatedHelper.exe <copy|move|delete|mkdir|rmdir> <path1> [path2] [--allow-root <dir>]");
                return EXIT_INVALID_ARGS;
            }

            var positionalArgs = new List<string>();
            var allowedRoots = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].Trim();
                if (arg.Equals("--allow-root", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || allowedRoots.Count != 0 || string.IsNullOrWhiteSpace(args[i + 1]))
                        return EXIT_INVALID_ARGS;
                    allowedRoots.Add(args[++i].Trim());
                }
                else if (arg.StartsWith("--allow-root=", StringComparison.OrdinalIgnoreCase))
                {
                    if (allowedRoots.Count != 0 || string.IsNullOrWhiteSpace(arg.Substring(13))) return EXIT_INVALID_ARGS;
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
            int expectedArguments = command is "copy" or "move" ? 3 : 2;
            if (positionalArgs.Count != expectedArguments) return EXIT_INVALID_ARGS;

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

                case "rmdir":
                    return ExecuteRmdir(path1, allowedRoots);

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
            if (!TryPerformMutation(
                    () => IsAllowedPath(destDir, isDirectory: true, isSourceOnly: false, allowedRoots),
                    () => Directory.CreateDirectory(destDir)))
                return EXIT_PATH_FORBIDDEN;
        }

        if (!TryPerformMutation(
                () => IsAllowedPath(source, isDirectory: false, isSourceOnly: true, allowedRoots) &&
                      IsAllowedPath(destination, isDirectory: false, isSourceOnly: false, allowedRoots),
                () => CopySourceWithoutFollowingReparsePoint(source, destination)))
            return EXIT_PATH_FORBIDDEN;
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
            if (!TryPerformMutation(
                    () => IsAllowedPath(destDir, isDirectory: true, isSourceOnly: false, allowedRoots),
                    () => Directory.CreateDirectory(destDir)))
                return EXIT_PATH_FORBIDDEN;
        }

        if (!TryPerformMutation(
                () => IsAllowedPath(source, isDirectory: false, isSourceOnly: false, allowedRoots) &&
                      IsAllowedPath(destination, isDirectory: false, isSourceOnly: false, allowedRoots),
                () => File.Move(source, destination, overwrite: true)))
            return EXIT_PATH_FORBIDDEN;
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
            if (!TryPerformMutation(
                    () => IsAllowedPath(target, isDirectory: false, isSourceOnly: false, allowedRoots),
                    () => File.Delete(target)))
                return EXIT_PATH_FORBIDDEN;
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
            if (!TryPerformMutation(
                    () => IsAllowedPath(dir, isDirectory: true, isSourceOnly: false, allowedRoots),
                    () => Directory.CreateDirectory(dir)))
                return EXIT_PATH_FORBIDDEN;
        }

        return EXIT_SUCCESS;
    }

    private static int ExecuteRmdir(string dir, List<string> allowedRoots)
    {
        if (!IsAllowedPath(dir, isDirectory: true, isSourceOnly: false, allowedRoots))
        {
            Console.Error.WriteLine("Security violation: path not allowed for elevated rmdir.");
            return EXIT_PATH_FORBIDDEN;
        }

        if (!Directory.Exists(dir))
        {
            return EXIT_SUCCESS;
        }

        if (Directory.EnumerateFileSystemEntries(dir).Any())
        {
            Console.Error.WriteLine($"Directory is not empty: {dir}");
            return EXIT_IO_ERROR;
        }

        if (!TryPerformMutation(
                () => IsAllowedPath(dir, isDirectory: true, isSourceOnly: false, allowedRoots),
                () => Directory.Delete(dir, recursive: false)))
            return EXIT_PATH_FORBIDDEN;
        return EXIT_SUCCESS;
    }

    private static bool TryPerformMutation(Func<bool> pathStillAllowed, Action mutation)
    {
        // Each operation repeats the complete containment, ACL, and reparse-point
        // check at the last possible point before touching the filesystem.
        if (!pathStillAllowed()) return false;
        mutation();
        return true;
    }

    private static void CopySourceWithoutFollowingReparsePoint(string source, string destination)
    {
        using SafeFileHandle sourceHandle = CreateFileForNoFollowRead(
            source,
            GenericRead,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagSequentialScan,
            IntPtr.Zero);
        if (sourceHandle.IsInvalid)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        if (!GetFileInformationByHandleEx(
                sourceHandle,
                FileAttributeTagInfoClass,
                out FileAttributeTagInfo sourceInfo,
                (uint)Marshal.SizeOf<FileAttributeTagInfo>()))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        if ((sourceInfo.FileAttributes & (FileAttributeReparsePoint | (uint)FileAttributes.Directory)) != 0)
            throw new UnauthorizedAccessException("Elevated copy source must be a regular, non-reparse file.");

        using var sourceStream = new FileStream(sourceHandle, FileAccess.Read);
        using var destinationStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        sourceStream.CopyTo(destinationStream);
        destinationStream.Flush(flushToDisk: true);
    }

    private static bool IsAllowedPath(string path, bool isDirectory, List<string>? allowedRoots = null)
    {
        return IsAllowedPath(path, isDirectory, isSourceOnly: false, allowedRoots);
    }

    private static bool IsAllowedPath(string path, bool isDirectory, bool isSourceOnly, List<string>? allowedRoots)
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

        string tempDir = Path.GetTempPath().ToLowerInvariant();
        if (!tempDir.EndsWith(Path.DirectorySeparatorChar.ToString())) tempDir += Path.DirectorySeparatorChar;

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
                    if (!IsTrustedGameRoot(fullRoot)) continue;
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

        if (!isWithinTempSource && !isWithinGameScope)
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
            else if (isWithinTempSource)
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

    private static bool IsTrustedGameRoot(string root)
    {
        if (!OperatingSystem.IsWindows() || !Directory.Exists(root) || HasReparsePointInPath(root))
            return false;

        string[] gameExecutables =
        {
            Path.Combine(root, "bin", "win_x64", "eurotrucks2.exe"),
            Path.Combine(root, "bin", "win_x64", "amtrucks.exe")
        };

        if (!gameExecutables.Any(File.Exists)) return false;
        if (HasLowPrivilegeWriteAccessInPath(root)) return false;
        return gameExecutables.Where(File.Exists).All(executable =>
            !HasReparsePointInPath(executable) && !HasLowPrivilegeWriteAccessInPath(executable));
    }

    private static bool HasLowPrivilegeWriteAccessInPath(string path)
    {
        if (!OperatingSystem.IsWindows()) return true;

        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            if (identity.User == null) return true;

            var writableSids = new HashSet<SecurityIdentifier>(identity.Groups?.OfType<SecurityIdentifier>() ?? Enumerable.Empty<SecurityIdentifier>())
            {
                identity.User,
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
                new SecurityIdentifier(WellKnownSidType.LocalSid, null)
            };

            // The elevated helper's Administrators SID is intentionally excluded:
            // an ordinary process with the same user token cannot use that SID.
            writableSids.Remove(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));

            FileSystemRights writeRights = FileSystemRights.Write | FileSystemRights.Modify |
                FileSystemRights.FullControl | FileSystemRights.Delete |
                FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions |
                FileSystemRights.TakeOwnership | FileSystemRights.CreateFiles |
                FileSystemRights.CreateDirectories | FileSystemRights.WriteAttributes |
                FileSystemRights.WriteExtendedAttributes;

            string? current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                FileSystemSecurity? security = null;
                if (Directory.Exists(current))
                    security = new DirectoryInfo(current).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
                else if (File.Exists(current))
                    security = new FileInfo(current).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);

                if (security != null)
                {
                    if (security.GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner && owner == identity.User)
                        return true;

                    AuthorizationRuleCollection rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
                    foreach (FileSystemAccessRule rule in rules)
                    {
                        if (rule.AccessControlType == AccessControlType.Allow &&
                            rule.IdentityReference is SecurityIdentifier sid && writableSids.Contains(sid) &&
                            (rule.FileSystemRights & writeRights) != 0)
                            return true;
                    }
                }

                string? parent = Path.GetDirectoryName(current);
                if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
                current = parent;
            }

            return false;
        }
        catch
        {
            return true; // Fail closed when owner/ACL state cannot be established.
        }
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
