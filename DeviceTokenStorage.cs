using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ETSOverlay
{
    public static class DeviceTokenStorage
    {
        private static readonly string StoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"TruckSim Widget\device.dat"
        );

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
                if (System.Windows.Application.Current?.MainWindow is MainWindow main)
                {
                    main.Dispatcher.Invoke(() => main.WriteLog($"[DeviceTokenStorage] Failed to save token: {ex.Message}"));
                }
            }
        }

        public static string LoadToken()
        {
            try
            {
                if (!File.Exists(StoragePath))
                {
                    return string.Empty;
                }

                byte[] encryptedBytes = File.ReadAllBytes(StoragePath);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                if (System.Windows.Application.Current?.MainWindow is MainWindow main)
                {
                    main.Dispatcher.Invoke(() => main.WriteLog($"[DeviceTokenStorage] Failed to load token: {ex.Message}"));
                }
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
                if (System.Windows.Application.Current?.MainWindow is MainWindow main)
                {
                    main.Dispatcher.Invoke(() => main.WriteLog($"[DeviceTokenStorage] Failed to delete token: {ex.Message}"));
                }
            }
        }
    }
}
