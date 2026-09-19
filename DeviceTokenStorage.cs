using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ETSOverlay
{
    public static class DeviceTokenStorage
    {
        internal static string? CustomStoragePath { get; set; }

        public static string StoragePath => PrimaryStoragePath;

        public static string PrimaryStoragePath => CustomStoragePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TruckSimWidget",
            "device.dat"
        );

        public static string BackupStoragePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "device.dat");

        private static void LogMessage(string message)
        {
            try
            {
                if (System.Windows.Application.Current?.MainWindow is MainWindow main)
                {
                    main.Dispatcher.Invoke(() => main.WriteLog(message));
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine(message);
                    Console.Error.WriteLine(message);
                }
            }
            catch
            {
                System.Diagnostics.Trace.WriteLine(message);
            }
        }

        public static void SaveToken(string token)
        {
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(token);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

                // 1. Save to primary AppData location
                try
                {
                    var dir = Path.GetDirectoryName(PrimaryStoragePath);
                    if (!Directory.Exists(dir) && dir != null) Directory.CreateDirectory(dir);
                    File.WriteAllBytes(PrimaryStoragePath, encryptedBytes);
                }
                catch (Exception ex)
                {
                    LogMessage($"[DeviceTokenStorage] Failed to save primary token: {ex.Message}");
                }

                // 2. Save backup copy to BaseDirectory alongside state.dat
                try
                {
                    var dir = Path.GetDirectoryName(BackupStoragePath);
                    if (!Directory.Exists(dir) && dir != null) Directory.CreateDirectory(dir);
                    File.WriteAllBytes(BackupStoragePath, encryptedBytes);
                }
                catch (Exception ex)
                {
                    LogMessage($"[DeviceTokenStorage] Failed to save backup token: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"[DeviceTokenStorage] Failed to encrypt/save token: {ex.Message}");
            }
        }

        public static string LoadToken()
        {
            try
            {
                // 1. Check primary location
                string token = TryLoadFromFile(PrimaryStoragePath);
                if (!string.IsNullOrEmpty(token))
                {
                    // Ensure backup exists
                    EnsureMirror(BackupStoragePath, PrimaryStoragePath);
                    return token;
                }

                // 2. Check backup BaseDirectory location
                token = TryLoadFromFile(BackupStoragePath);
                if (!string.IsNullOrEmpty(token))
                {
                    LogMessage("[DeviceTokenStorage] Recovered device token from BaseDirectory backup.");
                    EnsureMirror(PrimaryStoragePath, BackupStoragePath);
                    return token;
                }

                // 3. Migration: check legacy AppData path "TruckSim Widget"
                string legacyAppData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TruckSim Widget",
                    "device.dat"
                );
                token = TryLoadFromFile(legacyAppData);
                if (!string.IsNullOrEmpty(token))
                {
                    LogMessage("[DeviceTokenStorage] Migrated device token from legacy AppData path.");
                    EnsureMirror(PrimaryStoragePath, legacyAppData);
                    EnsureMirror(BackupStoragePath, legacyAppData);
                    return token;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                LogMessage($"[DeviceTokenStorage] Failed to load token: {ex.Message}");
                return string.Empty;
            }
        }

        private static string TryLoadFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return string.Empty;
                byte[] encryptedBytes = File.ReadAllBytes(path);
                if (encryptedBytes.Length == 0) return string.Empty;
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void EnsureMirror(string destinationPath, string sourcePath)
        {
            try
            {
                if (!File.Exists(destinationPath) && File.Exists(sourcePath))
                {
                    var dir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(dir) && dir != null) Directory.CreateDirectory(dir);
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                }
            }
            catch { }
        }

        public static void DeleteToken()
        {
            try
            {
                if (File.Exists(PrimaryStoragePath)) File.Delete(PrimaryStoragePath);
            }
            catch { }

            try
            {
                if (File.Exists(BackupStoragePath)) File.Delete(BackupStoragePath);
            }
            catch { }

            try
            {
                string legacyAppData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TruckSim Widget",
                    "device.dat"
                );
                if (File.Exists(legacyAppData)) File.Delete(legacyAppData);
            }
            catch { }
        }
    }
}
