using System;
using System.Windows;
using System.Windows.Media;

namespace ETSOverlay
{
    public class ThemeManager
    {
        public static ThemeManager Instance { get; } = new ThemeManager();

        private ThemeManager() { }

        public void ApplyTheme(string theme, string accent, string cardStyle, string accentMode = "standard", System.Collections.Generic.Dictionary<string, string>? customAccents = null)
        {
            var dict = new ResourceDictionary();

            // 1. Theme (Backgrounds, Borders, Text, Shadow)
            switch (theme?.ToLowerInvariant())
            {
                case "midnight":
                    dict["WindowBackgroundBrush"] = CreateBrush("#0E121B");
                    dict["CardBackgroundBrush"] = CreateBrush("#181D2A");
                    dict["CardBorderBrush"] = CreateBrush("#232A3B");
                    dict["MainTextBrush"] = CreateBrush("#E8EBEF");
                    dict["MutedTextBrush"] = CreateBrush("#7D879B");
                    dict["ShadowOpacity"] = 0.6;
                    dict["HeaderBackgroundBrush"] = CreateBrush("#0E121B");
                    dict["HeaderButtonBackgroundBrush"] = CreateBrush("#1E2330");
                    dict["HeaderButtonHoverBrush"] = CreateBrush("#2C3345");
                    dict["HeaderTextBrush"] = CreateBrush("#E8EBEF");
                    dict["HeaderIconBrush"] = CreateBrush("#B0BACD");
                    dict["InputBackgroundBrush"] = CreateBrush("#1E2330");
                    dict["InputBorderBrush"] = CreateBrush("#2C3345");
                    dict["InputHoverBrush"] = CreateBrush("#3A445C");
                    break;
                case "carbon":
                    dict["WindowBackgroundBrush"] = CreateBrush("#141414");
                    dict["CardBackgroundBrush"] = CreateBrush("#1E1E1E");
                    dict["CardBorderBrush"] = CreateBrush("#2D2D2D");
                    dict["MainTextBrush"] = CreateBrush("#E0E0E0");
                    dict["MutedTextBrush"] = CreateBrush("#757575");
                    dict["ShadowOpacity"] = 0.5;
                    dict["HeaderBackgroundBrush"] = CreateBrush("#141414");
                    dict["HeaderButtonBackgroundBrush"] = CreateBrush("#242424");
                    dict["HeaderButtonHoverBrush"] = CreateBrush("#363636");
                    dict["HeaderTextBrush"] = CreateBrush("#E0E0E0");
                    dict["HeaderIconBrush"] = CreateBrush("#A0A0A0");
                    dict["InputBackgroundBrush"] = CreateBrush("#242424");
                    dict["InputBorderBrush"] = CreateBrush("#363636");
                    dict["InputHoverBrush"] = CreateBrush("#484848");
                    break;
                case "oled":
                case "oled black":
                    dict["WindowBackgroundBrush"] = CreateBrush("#000000");
                    dict["CardBackgroundBrush"] = CreateBrush("#050505");
                    dict["CardBorderBrush"] = CreateBrush("#2C2C2C");
                    dict["MainTextBrush"] = CreateBrush("#F0F0F0");
                    dict["MutedTextBrush"] = CreateBrush("#666666");
                    dict["ShadowOpacity"] = 0.8;
                    dict["HeaderBackgroundBrush"] = CreateBrush("#000000");
                    dict["HeaderButtonBackgroundBrush"] = CreateBrush("#0A0A0A");
                    dict["HeaderButtonHoverBrush"] = CreateBrush("#1A1A1A");
                    dict["HeaderTextBrush"] = CreateBrush("#F0F0F0");
                    dict["HeaderIconBrush"] = CreateBrush("#888888");
                    dict["InputBackgroundBrush"] = CreateBrush("#0A0A0A");
                    dict["InputBorderBrush"] = CreateBrush("#303030");
                    dict["InputHoverBrush"] = CreateBrush("#2A2A2A");
                    break;
                case "classic":
                default:
                    dict["WindowBackgroundBrush"] = CreateBrush("#1A1C20");
                    dict["CardBackgroundBrush"] = CreateBrush("#252830");
                    dict["CardBorderBrush"] = CreateBrush("#353840");
                    dict["MainTextBrush"] = CreateBrush("White");
                    dict["MutedTextBrush"] = CreateBrush("#888888"); // And #A0A0A0 used in StatusValue
                    dict["ShadowOpacity"] = 0.5;
                    dict["HeaderBackgroundBrush"] = CreateBrush("#1A1C20");
                    dict["HeaderButtonBackgroundBrush"] = CreateBrush("#2A2D35");
                    dict["HeaderButtonHoverBrush"] = CreateBrush("#3A3D45");
                    dict["HeaderTextBrush"] = CreateBrush("#D0D0D0");
                    dict["HeaderIconBrush"] = CreateBrush("#B0B0B0");
                    dict["InputBackgroundBrush"] = CreateBrush("#2A2D35");
                    dict["InputBorderBrush"] = CreateBrush("#3A3D45");
                    dict["InputHoverBrush"] = CreateBrush("#353840");
                    break;
            }

            // 2. Accent
            switch (accent?.ToLowerInvariant())
            {
                case "blue":
                    dict["AccentColorBrush"] = CreateBrush("#4DA8DA");
                    break;
                case "amber":
                    dict["AccentColorBrush"] = CreateBrush("#FFC107");
                    break;
                case "violet":
                    dict["AccentColorBrush"] = CreateBrush("#9D4EDD");
                    break;
                case "red":
                    dict["AccentColorBrush"] = CreateBrush("#E63946");
                    break;
                case "teal":
                default:
                    dict["AccentColorBrush"] = CreateBrush("#7AC5CD");
                    break;
            }

            var globalAccent = (SolidColorBrush)dict["AccentColorBrush"];
            var mainText = (SolidColorBrush)dict["MainTextBrush"];
            var mutedText = (SolidColorBrush)dict["MutedTextBrush"];

            if (customAccents == null) customAccents = new System.Collections.Generic.Dictionary<string, string>();

            SolidColorBrush GetCardAccent(string cardName, SolidColorBrush standardBrush)
            {
                if (accentMode == "uniform") return globalAccent;
                if (accentMode == "custom")
                {
                    if (customAccents.TryGetValue(cardName, out string? colorNameOrHex))
                    {
                        // Backward compatibility for old string-based settings
                        var lowerStr = colorNameOrHex.ToLowerInvariant();
                        switch (lowerStr)
                        {
                            case "blue": return CreateBrush("#4DA8DA");
                            case "amber": return CreateBrush("#FFC107");
                            case "violet": return CreateBrush("#9D4EDD");
                            case "red": return CreateBrush("#E63946");
                            case "teal": return CreateBrush("#7AC5CD");
                            case "green": return CreateBrush("#4CAF50");
                        }

                        try
                        {
                            return CreateBrush(colorNameOrHex);
                        }
                        catch
                        {
                            return globalAccent;
                        }
                    }
                    return globalAccent;
                }
                return standardBrush; // "standard" mode
            }

            dict["AccentBrush_Sim"] = GetCardAccent("Sim", CreateBrush("#4CAF50"));
            dict["AccentBrush_Status"] = GetCardAccent("Status", globalAccent);
            dict["AccentBrush_Game"] = GetCardAccent("Game", CreateBrush("#4CAF50")); // Game status is #4CAF50, icon is globalAccent. We'll simplify to both using Game brush, or if standard, they differ. Wait, to keep standard exactly the same, I should make 2 brushes or let Game icon use AccentColorBrush directly. Let's make the Game icon use AccentBrush_GameIcon and Game text use AccentBrush_GameText.
            // Let's refine standard brushes:
            dict["AccentBrush_Sim"] = GetCardAccent("Sim", CreateBrush("#4CAF50"));
            dict["AccentBrush_Status"] = GetCardAccent("Status", globalAccent);
            dict["AccentBrush_GameIcon"] = GetCardAccent("Game", globalAccent);
            dict["AccentBrush_GameText"] = GetCardAccent("Game", CreateBrush("#4CAF50"));
            dict["AccentBrush_Distance"] = GetCardAccent("Distance", CreateBrush("#52C14F"));
            dict["AccentBrush_Route"] = GetCardAccent("Route", mutedText);
            dict["AccentBrush_Speed"] = GetCardAccent("Speed", mainText);
            dict["AccentBrush_Max"] = GetCardAccent("Max", CreateBrush("#F39C12"));
            dict["AccentBrush_Type"] = GetCardAccent("Type", mutedText); // Type changes dynamically, but if custom/uniform we can set a base here.

            // 3. Card Style (Padding, Radius, Gaps)
            switch (cardStyle?.ToLowerInvariant())
            {
                case "rounded":
                    dict["CardCornerRadius"] = new CornerRadius(12);
                    dict["InfoCardPadding"] = new Thickness(10, 8, 10, 8);
                    dict["GaugeCardPadding"] = new Thickness(5, 3, 5, 3);
                    dict["CardMargin"] = new Thickness(0, 0, 0, 6);
                    dict["CardGapWidth"] = new GridLength(6);
                    break;
                case "standard":
                default:
                    dict["CardCornerRadius"] = new CornerRadius(7);
                    dict["InfoCardPadding"] = new Thickness(10, 8, 10, 8);
                    dict["GaugeCardPadding"] = new Thickness(5, 3, 5, 3);
                    dict["CardMargin"] = new Thickness(0, 0, 0, 6);
                    dict["CardGapWidth"] = new GridLength(6);
                    break;
            }

            // Also expose some derivative brushes for Settings window and controls
            var windowBg = (SolidColorBrush)dict["WindowBackgroundBrush"];
            var cardBg = (SolidColorBrush)dict["CardBackgroundBrush"];
            var border = (SolidColorBrush)dict["CardBorderBrush"];

            // A slightly lighter brush for hovers
            dict["CardBackgroundHoverBrush"] = LighterBrush(cardBg, 0.05);

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        private SolidColorBrush CreateBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private SolidColorBrush LighterBrush(SolidColorBrush original, double percent)
        {
            var c = original.Color;
            var r = (byte)Math.Min(255, c.R + 255 * percent);
            var g = (byte)Math.Min(255, c.G + 255 * percent);
            var b = (byte)Math.Min(255, c.B + 255 * percent);
            var newBrush = new SolidColorBrush(Color.FromArgb(c.A, r, g, b));
            newBrush.Freeze();
            return newBrush;
        }
    }
}
