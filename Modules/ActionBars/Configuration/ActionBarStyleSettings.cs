using UnityEngine;

namespace ArcanesGroupFrames.Modules.ActionBars.Configuration
{
    // Phase 7.1: these are persisted APPEARANCE PREVIEW settings, not live actionbar overrides.
    // Native hotbars, spell slots, keybinds and cooldowns remain entirely untouched.
    internal static class ActionBarStyleSettings
    {
        private const string Prefix = "ArcaneUI.ActionBars.Appearance.";
        private static readonly string[] Presets =
        {
            "Arcane Dark", "Gilded Reliquary", "Runebound", "Emberforged", "Voidglass", "Minimal", "Custom"
        };
        private static readonly string[] Borders =
        {
            "Ornate", "Runic", "Bevelled", "Glowing", "Thin", "None"
        };
        internal static string Preset { get { return GetChoice("Preset", Presets, "Arcane Dark"); } }
        internal static string Border { get { return GetChoice("Border", Borders, "Ornate"); } }
        internal static int IconSize { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "IconSize", 42), 24, 72); } }
        internal static int Spacing { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Spacing", 6), 0, 18); } }
        internal static int BorderWidth { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "BorderWidth", 3), 1, 8); } }
        internal static int CornerSize { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "CornerSize", 11), 3, 16); } }
        internal static int Glow { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Glow", 35), 0, 100); } }
        internal static int BackgroundOpacity { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "BackgroundOpacity", 75), 0, 100); } }
        internal static bool ShowKeyLabels { get { return PlayerPrefs.GetInt(Prefix + "ShowKeyLabels", 1) == 1; } }
        internal static bool ShowCooldownNumbers { get { return PlayerPrefs.GetInt(Prefix + "ShowCooldownNumbers", 1) == 1; } }
        internal static bool ShowTooltips { get { return PlayerPrefs.GetInt(Prefix + "ShowTooltips", 1) == 1; } }

        internal static void NextPreset() { NextChoice("Preset", Presets, "Arcane Dark"); }
        internal static void NextBorder() { NextChoice("Border", Borders, "Ornate"); }
        internal static void SetIconSize(float value) { SaveInt("IconSize", Mathf.Clamp(Mathf.RoundToInt(value), 24, 72)); }
        internal static void SetSpacing(float value) { SaveInt("Spacing", Mathf.Clamp(Mathf.RoundToInt(value), 0, 18)); }
        internal static void SetBorderWidth(float value) { SaveInt("BorderWidth", Mathf.Clamp(Mathf.RoundToInt(value), 1, 8)); }
        internal static void SetCornerSize(float value) { SaveInt("CornerSize", Mathf.Clamp(Mathf.RoundToInt(value), 3, 16)); }
        internal static void SetGlow(float value) { SaveInt("Glow", Mathf.Clamp(Mathf.RoundToInt(value), 0, 100)); }
        internal static void SetBackgroundOpacity(float value) { SaveInt("BackgroundOpacity", Mathf.Clamp(Mathf.RoundToInt(value), 0, 100)); }
        internal static void SetKeyLabels(bool value) { SaveInt("ShowKeyLabels", value ? 1 : 0); }
        internal static void SetCooldownNumbers(bool value) { SaveInt("ShowCooldownNumbers", value ? 1 : 0); }
        internal static void SetShowTooltips(bool value) { SaveInt("ShowTooltips", value ? 1 : 0); }

        internal static string AccentHex
        {
            get
            {
                string custom = PlayerPrefs.GetString(Prefix + "AccentHex", "");
                return string.IsNullOrEmpty(custom) ? PresetAccentHex : custom;
            }
        }
        internal static string PresetAccentHex
        {
            get
            {
                switch (Preset)
                {
                    case "Gilded Reliquary": return "#D5AD5E";
                    case "Runebound": return "#48B9DB";
                    case "Emberforged": return "#E76F35";
                    case "Voidglass": return "#9E72E0";
                    case "Minimal": return "#91A1B2";
                    case "Custom": return "#B88AEB";
                    default: return "#798ED5";
                }
            }
        }
        internal static Color AccentColor
        {
            get
            {
                Color color;
                return ColorUtility.TryParseHtmlString(AccentHex, out color) ? color : Color.white;
            }
        }
        internal static void SetAccentHex(string hex)
        {
            Color parsed;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out parsed))
            {
                PlayerPrefs.SetString(Prefix + "AccentHex", "#" + ColorUtility.ToHtmlStringRGB(parsed));
                PlayerPrefs.Save();
            }
        }
        internal static void ResetAccent()
        {
            PlayerPrefs.DeleteKey(Prefix + "AccentHex");
            PlayerPrefs.Save();
        }
        internal static void ResetStyle()
        {
            foreach (string key in new [] { "Preset", "Border", "IconSize", "Spacing", "BorderWidth", "CornerSize", "Glow", "BackgroundOpacity", "ShowKeyLabels", "ShowCooldownNumbers", "ShowTooltips", "AccentHex" })
                PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.Save();
        }

        // A layout contains a snapshot of the global inherited appearance,
        // otherwise loading a saved bar with InheritStyle enabled changes its look.
        internal static void ApplySavedStyle(ActionBarLayoutPresets.StyleRecord style)
        {
            if (style == null) return;
            if (System.Array.IndexOf(Presets, style.Preset) >= 0)
                PlayerPrefs.SetString(Prefix + "Preset", style.Preset);
            if (System.Array.IndexOf(Borders, style.Border) >= 0)
                PlayerPrefs.SetString(Prefix + "Border", style.Border);
            Color parsed;
            if (!string.IsNullOrEmpty(style.Accent) && ColorUtility.TryParseHtmlString(style.Accent, out parsed))
                PlayerPrefs.SetString(Prefix + "AccentHex", "#" + ColorUtility.ToHtmlStringRGB(parsed));
            PlayerPrefs.SetInt(Prefix + "IconSize", Mathf.Clamp(style.IconSize, 24, 72));
            PlayerPrefs.SetInt(Prefix + "Spacing", Mathf.Clamp(style.Spacing, 0, 18));
            PlayerPrefs.SetInt(Prefix + "BorderWidth", Mathf.Clamp(style.BorderWidth, 1, 8));
            PlayerPrefs.SetInt(Prefix + "CornerSize", Mathf.Clamp(style.CornerSize, 3, 16));
            PlayerPrefs.SetInt(Prefix + "Glow", Mathf.Clamp(style.Glow, 0, 100));
            PlayerPrefs.SetInt(Prefix + "BackgroundOpacity", Mathf.Clamp(style.BackgroundOpacity, 0, 100));
            PlayerPrefs.SetInt(Prefix + "ShowKeyLabels", style.ShowKeys ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "ShowCooldownNumbers", style.ShowCooldowns ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "ShowTooltips", style.ShowTooltips ? 1 : 0);
        }

        private static string GetChoice(string key, string[] options, string fallback)
        {
            string value = PlayerPrefs.GetString(Prefix + key, fallback);
            foreach (string option in options) if (option == value) return value;
            return fallback;
        }
        private static void NextChoice(string key, string[] options, string fallback)
        {
            string current = GetChoice(key, options, fallback);
            for (int i = 0; i < options.Length; i++)
                if (options[i] == current)
                {
                    PlayerPrefs.SetString(Prefix + key, options[(i + 1) % options.Length]);
                    PlayerPrefs.Save();
                    return;
                }
        }
        private static void SaveInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            PlayerPrefs.Save();
        }
    }
}
