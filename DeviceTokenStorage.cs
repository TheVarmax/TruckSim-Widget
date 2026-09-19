using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ETSOverlay
{
    public static class DeviceTokenStorage
    {
        internal static string? CustomStoragePath { get; set; }

        public static string StoragePath => CustomStoragePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TruckSimWidget",
            "device.dat"
        );

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
                var directory = Path.GetDirectoryName(StoragePath);
                if (!Directory.Exists(directory) && directory != null)
                {
                    Directory.CreateDirectory(directory);
                }

                byte[] plainBytes = Encoding.UTF8.GetBytes(token);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(StoragePath, encryptedBytes);
            }
            catch (Exception ex)
            {
                LogMessage($"[DeviceTokenStorage] Failed to save token: {ex.Message}");
            }
        }

        public static string LoadToken()
        {
            try
            {
                string targetPath = StoragePath;

                // Migration: check legacy locations if canonical storage doesn't exist
                if (!File.Exists(targetPath) && CustomStoragePath == null)
                {
                    string legacyAppData = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "TruckSim Widget",
                        "device.dat"
                    );
                    string legacyBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "device.dat");

                    if (File.Exists(legacyAppData))
                    {
                        try
                        {
                            var dir = Path.GetDirectoryName(targetPath);
                            if (!Directory.Exists(dir) && dir != null) Directory.CreateDirectory(dir);
                            File.Copy(legacyAppData, targetPath, overwrite: false);
                            LogMessage("[DeviceTokenStorage] Migrated device token from legacy AppData path.");
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"[DeviceTokenStorage] Failed to copy legacy AppData token: {ex.Message}");
                        }
                    }
                    else if (File.Exists(legacyBaseDir))
                    {
                        try
                        {
                            var dir = Path.GetDirectoryName(targetPath);
                            if (!Directory.Exists(dir) && dir != null) Directory.CreateDirectory(dir);
                            File.Copy(legacyBaseDir, targetPath, overwrite: false);
                            LogMessage("[DeviceTokenStorage] Migrated device token from legacy BaseDirectory path.");
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"[DeviceTokenStorage] Failed to copy legacy BaseDirectory token: {ex.Message}");
                        }
                    }
                }

                if (!File.Exists(targetPath))
                {
                    return string.Empty;
                }

                byte[] encryptedBytes = File.ReadAllBytes(targetPath);
                if (encryptedBytes.Length == 0)
                {
                    return string.Empty;
                }

                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                LogMessage($"[DeviceTokenStorage] Failed to load token: {ex.Message}");
                return string.Empty;
            }
        }

        public static void DeleteToken()
        {
            try
            {
                if (File.Exists(StoragePath))
                {
                    File.Delete(StoragePath);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"[DeviceTokenStorage] Failed to delete token: {ex.Message}");
            }
        }
    }
}
