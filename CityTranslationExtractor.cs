// ---------------------------------------------------------------------------
// CityTranslationExtractor.cs — Extracts localized city names from ETS2/ATS game files
// ---------------------------------------------------------------------------
// Uses ScsArchiveReader (adapted from ts-map, MIT License) to read .scs archives.
// This tool reads ONLY static game installation files. It does NOT modify any game
// data, memory, saves, or telemetry. Safe to use with TrucksBook and TruckersMP.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ETSOverlay.ScsArchive;
using Microsoft.Win32;

namespace ETSOverlay
{
    /// <summary>
    /// Extracts localized city names from ETS2/ATS game .scs archives and generates
    /// a multi-language translation JSON file.
    /// </summary>
    public static class CityTranslationExtractor
    {
        /// <summary>
        /// Maps a game language code (e.g. "uk_ua") to the widget's UI language code (e.g. "uk").
        /// Only languages that the widget supports should be listed here.
        /// </summary>
        private static readonly Dictionary<string, string> SupportedGameLanguages = new(StringComparer.OrdinalIgnoreCase)
        {
            { "uk_uk", "uk" },
            // Future languages can be added here, e.g.:
            // { "ru_ru", "ru" },
            // { "de_de", "de" },
            // { "pl_pl", "pl" },
            // { "fr_fr", "fr" },
            // { "es_es", "es" },
        };

        /// <summary>Known ETS2 Steam App ID.</summary>
        private const int Ets2AppId = 227300;

        /// <summary>Known ATS Steam App ID.</summary>
        private const int AtsAppId = 270880;

        // Regex patterns for parsing .sii/.sui city definition files
        private static readonly Regex CityNameRegex = new(@"city_name:\s*""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex CityTokenRegex = new(@"city_name_localized:\s*""@@([^@]+)@@""", RegexOptions.Compiled);

        // Regex patterns for parsing locale .str/.sii files (key[]/val[] format)
        private static readonly Regex LocaleKeyRegex = new(@"key\[\]\s*:\s*""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex LocaleValRegex = new(@"val\[\]\s*:\s*""([^""]*?)""", RegexOptions.Compiled);

        /// <summary>
        /// Result of an extraction attempt.
        /// </summary>
        public class ExtractionResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public int Ets2CityCount { get; set; }
            public int AtsCityCount { get; set; }
            public int LanguageCount { get; set; }
            public string? OutputPath { get; set; }
        }

        /// <summary>
        /// The new multi-language translation file format.
        /// </summary>
        public class MultiLangTranslationFile
        {
            [JsonPropertyName("version")]
            public int Version { get; set; } = 1;

            [JsonPropertyName("generated_at")]
            public string GeneratedAt { get; set; } = DateTime.UtcNow.ToString("o");

            [JsonPropertyName("ets2")]
            public Dictionary<string, Dictionary<string, string>> Ets2 { get; set; } = new();

            [JsonPropertyName("ats")]
            public Dictionary<string, Dictionary<string, string>> Ats { get; set; } = new();
        }

        /// <summary>
        /// Run the full extraction process: find game installations, parse .scs archives,
        /// extract city names and translations, generate JSON.
        /// </summary>
        /// <param name="ets2Path">Optional explicit path to ETS2 installation folder.</param>
        /// <param name="atsPath">Optional explicit path to ATS installation folder.</param>
        /// <param name="outputPath">Path where the JSON file will be saved.</param>
        /// <param name="logAction">Optional callback for logging progress.</param>
        public static ExtractionResult Extract(string? ets2Path, string? atsPath, string outputPath, Action<string>? logAction = null)
        {
            void Log(string msg) => logAction?.Invoke(msg);

            try
            {
                // Auto-detect paths if not provided
                if (string.IsNullOrWhiteSpace(ets2Path))
                    ets2Path = FindGameInstallation(Ets2AppId, "Euro Truck Simulator 2");

                if (string.IsNullOrWhiteSpace(atsPath))
                    atsPath = FindGameInstallation(AtsAppId, "American Truck Simulator");

                var result = new MultiLangTranslationFile();
                int ets2Count = 0;
                int atsCount = 0;

                // Process ETS2
                if (!string.IsNullOrWhiteSpace(ets2Path) && Directory.Exists(ets2Path))
                {
                    Log($"Found ETS2 at: {ets2Path}");
                    var cities = ExtractCitiesFromGame(ets2Path, Log);
                    ets2Count = cities.Count;
                    result.Ets2 = cities;
                    Log($"Extracted {ets2Count} ETS2 cities");
                }
                else
                {
                    Log("ETS2 installation not found, skipping.");
                }

                // Process ATS
                if (!string.IsNullOrWhiteSpace(atsPath) && Directory.Exists(atsPath))
                {
                    Log($"Found ATS at: {atsPath}");
                    var cities = ExtractCitiesFromGame(atsPath, Log);
                    atsCount = cities.Count;
                    result.Ats = cities;
                    Log($"Extracted {atsCount} ATS cities");
                }
                else
                {
                    Log("ATS installation not found, skipping.");
                }

                if (ets2Count == 0 && atsCount == 0)
                {
                    return new ExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "No game installations found or no cities could be extracted."
                    };
                }

                // Calculate language count
                var allLangs = new HashSet<string>();
                foreach (var city in result.Ets2.Values)
                    foreach (var lang in city.Keys)
                        allLangs.Add(lang);
                foreach (var city in result.Ats.Values)
                    foreach (var lang in city.Keys)
                        allLangs.Add(lang);

                // Write output
                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(outputPath, json, Encoding.UTF8);

                Log($"Translations saved to: {outputPath}");

                return new ExtractionResult
                {
                    Success = true,
                    Ets2CityCount = ets2Count,
                    AtsCityCount = atsCount,
                    LanguageCount = allLangs.Count,
                    OutputPath = outputPath
                };
            }
            catch (Exception ex)
            {
                Log($"Extraction failed: {ex.Message}");
                return new ExtractionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Extract cities from a single game installation.
        /// Returns: Dictionary[EnglishCityName] -> Dictionary[langCode, translatedName]
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> ExtractCitiesFromGame(string gamePath, Action<string>? log)
        {
            // Step 1: Find all .scs archives
            var scsFiles = Directory.GetFiles(gamePath, "*.scs", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f)
                .ToList();

            log?.Invoke($"  Found {scsFiles.Count} .scs archives");

            // Step 2: Extract city definitions (english name -> localization token)
            var cityTokenMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var scsFile in scsFiles)
            {
                try
                {
                    using var archive = new ScsArchiveReader(scsFile);
                    if (!archive.IsValid) continue;

                    ExtractCityDefinitions(archive, cityTokenMap, log);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"  Warning: Could not read {Path.GetFileName(scsFile)}: {ex.Message}");
                }
            }

            log?.Invoke($"  Found {cityTokenMap.Count} city definitions");

            // Step 3: Extract translations from locale files
            var translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            // Initialize with english names
            foreach (var (englishName, token) in cityTokenMap)
            {
                translations[englishName] = new Dictionary<string, string>();
            }

            foreach (var scsFile in scsFiles)
            {
                try
                {
                    using var archive = new ScsArchiveReader(scsFile);
                    if (!archive.IsValid) continue;

                    ExtractTranslations(archive, cityTokenMap, translations, log);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"  Warning: Could not read locale from {Path.GetFileName(scsFile)}: {ex.Message}");
                }
            }

            return translations;
        }

        /// <summary>
        /// Scan an archive for city definition files and extract english name -> token mapping.
        /// </summary>
        private static void ExtractCityDefinitions(ScsArchiveReader archive, Dictionary<string, string> cityTokenMap, Action<string>? log)
        {
            // Try to list the def/city directory
            var cityDirListing = archive.GetDirectoryListing("def/city");
            if (cityDirListing == null) return;

            foreach (var entry in cityDirListing)
            {
                var trimmed = entry.Trim().TrimStart('*');
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Try both .sui and .sii extensions, and also try as a directory
                string? content = null;
                string[] candidatePaths = new[]
                {
                    $"def/city/{trimmed}",
                    $"def/city/{trimmed}.sui",
                    $"def/city/{trimmed}.sii",
                    $"def/city/{trimmed}/city_data.sii",
                    $"def/city/{trimmed}/city_data.sui",
                };

                foreach (var path in candidatePaths)
                {
                    content = archive.ExtractFileAsString(path);
                    if (content != null) break;
                }

                // If entry is a directory, try listing it
                if (content == null)
                {
                    var subListing = archive.GetDirectoryListing($"def/city/{trimmed}");
                    if (subListing != null)
                    {
                        foreach (var subEntry in subListing)
                        {
                            var subTrimmed = subEntry.Trim().TrimStart('*');
                            content = archive.ExtractFileAsString($"def/city/{trimmed}/{subTrimmed}");
                            if (content != null)
                            {
                                ParseCityDefinition(content, cityTokenMap);
                            }
                        }
                        continue;
                    }
                }

                if (content != null)
                {
                    ParseCityDefinition(content, cityTokenMap);
                }
            }
        }

        /// <summary>
        /// Parse a single city definition file content and extract name/token pairs.
        /// </summary>
        private static void ParseCityDefinition(string content, Dictionary<string, string> cityTokenMap)
        {
            var nameMatch = CityNameRegex.Match(content);
            var tokenMatch = CityTokenRegex.Match(content);

            if (nameMatch.Success && tokenMatch.Success)
            {
                var englishName = nameMatch.Groups[1].Value;
                var token = tokenMatch.Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(englishName) && !string.IsNullOrWhiteSpace(token))
                {
                    cityTokenMap[englishName] = token;
                }
            }
        }

        /// <summary>
        /// Extract translations for all supported languages from an archive's locale files.
        /// </summary>
        private static void ExtractTranslations(
            ScsArchiveReader archive,
            Dictionary<string, string> cityTokenMap,
            Dictionary<string, Dictionary<string, string>> translations,
            Action<string>? log)
        {
            // Build reverse map: token -> english name (for lookup)
            var tokenToEnglish = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (english, token) in cityTokenMap)
            {
                tokenToEnglish[token] = english;
            }

            foreach (var (gameLang, widgetLang) in SupportedGameLanguages)
            {
                // Try multiple possible locale file paths
                string[] localePaths = new[]
                {
                    $"locale/{gameLang}/local.sii",
                    $"locale/{gameLang}/local.str",
                    $"locale/{gameLang}/local.override.sii",
                    $"locale/{gameLang}/local.override.str",
                };

                foreach (var localePath in localePaths)
                {
                    var localeContent = archive.ExtractFileAsString(localePath);
                    if (localeContent == null) continue;

                    ParseLocaleFile(localeContent, tokenToEnglish, translations, widgetLang);
                }

                // Also try reading the locale directory for module files
                var localeDirListing = archive.GetDirectoryListing($"locale/{gameLang}");
                if (localeDirListing != null)
                {
                    foreach (var entry in localeDirListing)
                    {
                        var trimmed = entry.Trim().TrimStart('*');
                        if (string.IsNullOrWhiteSpace(trimmed)) continue;
                        if (trimmed == "local.sii" || trimmed == "local.str" ||
                            trimmed == "local.override.sii" || trimmed == "local.override.str") continue;

                        if (trimmed.EndsWith(".sii") || trimmed.EndsWith(".str") || trimmed.EndsWith(".sui"))
                        {
                            var content = archive.ExtractFileAsString($"locale/{gameLang}/{trimmed}");
                            if (content != null)
                            {
                                ParseLocaleFile(content, tokenToEnglish, translations, widgetLang);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Parse a locale file (key[]/val[] format) and match city tokens to translations.
        /// </summary>
        private static void ParseLocaleFile(
            string content,
            Dictionary<string, string> tokenToEnglish,
            Dictionary<string, Dictionary<string, string>> translations,
            string widgetLang)
        {
            var lines = content.Split('\n');
            string? currentKey = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                var keyMatch = LocaleKeyRegex.Match(line);
                if (keyMatch.Success)
                {
                    currentKey = keyMatch.Groups[1].Value;
                    continue;
                }

                var valMatch = LocaleValRegex.Match(line);
                if (valMatch.Success && currentKey != null)
                {
                    var value = valMatch.Groups[1].Value;

                    // Check if this key matches any city token
                    if (tokenToEnglish.TryGetValue(currentKey, out var englishName))
                    {
                        if (!string.IsNullOrWhiteSpace(value) && translations.ContainsKey(englishName))
                        {
                            translations[englishName][widgetLang] = value;
                        }
                    }

                    currentKey = null;
                }
            }
        }

        /// <summary>
        /// Try to find a Steam game installation path via registry and common paths.
        /// </summary>
        public static string? FindGameInstallation(int appId, string gameFolderName)
        {
            try
            {
                // Method 1: Check Steam registry for library folders
                var steamPaths = GetSteamLibraryPaths();
                foreach (var steamLib in steamPaths)
                {
                    var gamePath = Path.Combine(steamLib, "steamapps", "common", gameFolderName);
                    if (Directory.Exists(gamePath) && Directory.GetFiles(gamePath, "*.scs").Any())
                    {
                        return gamePath;
                    }
                }

                // Method 2: Common installation paths
                var commonPaths = new List<string>
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        "Steam", "steamapps", "common", gameFolderName),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "Steam", "steamapps", "common", gameFolderName)
                };

                foreach (var drive in System.IO.DriveInfo.GetDrives())
                {
                    if (drive.IsReady)
                    {
                        commonPaths.Add(Path.Combine(drive.Name, "Steam", "steamapps", "common", gameFolderName));
                        commonPaths.Add(Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", gameFolderName));
                        commonPaths.Add(Path.Combine(drive.Name, "Games", "SteamLibrary", "steamapps", "common", gameFolderName));
                    }
                }

                foreach (var p in commonPaths)
                {
                    if (Directory.Exists(p) && Directory.GetFiles(p, "*.scs").Any())
                    {
                        return p;
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Read Steam library folder paths from the registry and libraryfolders.vdf.
        /// </summary>
        private static List<string> GetSteamLibraryPaths()
        {
            var paths = new List<string>();

            try
            {
                // Read Steam installation path from registry
                string? steamPath = null;
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    steamPath = key?.GetValue("SteamPath") as string;
                }

                if (string.IsNullOrWhiteSpace(steamPath))
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
                    steamPath = key?.GetValue("InstallPath") as string;
                }

                if (string.IsNullOrWhiteSpace(steamPath))
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                    steamPath = key?.GetValue("InstallPath") as string;
                }

                if (!string.IsNullOrWhiteSpace(steamPath))
                {
                    paths.Add(steamPath);

                    // Parse libraryfolders.vdf for additional library paths
                    var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdfPath))
                    {
                        var vdfContent = File.ReadAllText(vdfPath);
                        var pathRegex = new Regex(@"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                        foreach (Match match in pathRegex.Matches(vdfContent))
                        {
                            var libPath = match.Groups[1].Value.Replace(@"\\", @"\");
                            if (Directory.Exists(libPath) && !paths.Contains(libPath, StringComparer.OrdinalIgnoreCase))
                            {
                                paths.Add(libPath);
                            }
                        }
                    }
                }
            }
            catch { }

            return paths;
        }

        /// <summary>
        /// Get the default output path for the generated translations file.
        /// </summary>
        public static string GetDefaultOutputPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "city_translations.json");
        }
    }
}
