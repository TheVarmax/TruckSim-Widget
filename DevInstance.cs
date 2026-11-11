using System;
using System.IO;

namespace ETSOverlay
{
    /// <summary>
    /// Where the widget keeps its data and which cloud it talks to.
    /// Development builds only: TSW_DEV_LOCALAPPDATA runs an isolated test instance (its own data
    /// folder, its own single-instance names, so it never meets the installed widget or the
    /// user's data) and TSW_DEV_API_URL sends its cloud requests to another server.
    /// Release builds ignore both variables.
    /// </summary>
    internal static class DevInstance
    {
        private const string ProductionApi = "https://api.trucksim.uk";

#if DEBUG
        private static readonly string? DataRoot = Clean(Environment.GetEnvironmentVariable("TSW_DEV_LOCALAPPDATA"));
        private static readonly string? ApiOverride = Clean(Environment.GetEnvironmentVariable("TSW_DEV_API_URL"));
#else
        private static readonly string? DataRoot = null;
        private static readonly string? ApiOverride = null;
#endif

        /// <summary>True for an isolated test instance.</summary>
        public static bool IsIsolated => DataRoot != null;

        /// <summary>Replaces LocalApplicationData for everything the widget reads and writes.</summary>
        public static string LocalAppData => DataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static string ApiBaseUrl => ApiOverride ?? ProductionApi;

        /// <summary>Suffix for mutex and event names, so a test instance runs beside the installed one.</summary>
        public static string InstanceSuffix => DataRoot == null ? "" : "_Dev_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DataRoot.ToLowerInvariant())))[..12];

        private static string? Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim().TrimEnd('/', '\\');
            return value;
        }
    }
}
