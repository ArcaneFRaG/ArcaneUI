using System;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.EnemyTracker.Configuration
{
    // All keys remain under the original prefix; existing tracker preferences
    // retain their value when moving from Phase 11.5B3 to Phase 11.5C.
    internal static class EnemyTrackerSettings
    {
        private const string Prefix = "ArcaneUI.EnemyTracker.";
        internal static readonly string[] VisibilityModes = { "Always", "Combat Only", "Mouseover Only", "Hidden" };
        internal static readonly string[] HealthModes = { "Percent", "Raw", "Both", "Off" };
        internal static readonly string[] Alignments = { "Left", "Center", "Right" };
        internal static readonly string[] BorderStyles = { "None", "Thin", "Accent", "Glow" };
        internal static readonly string[] SortingModes = { "Native Order", "Selected First", "Lowest Health", "Name" };
        internal static readonly string[] BackgroundStyles = { "Solid", "Soft", "Clear" };

        internal static bool Enabled { get; private set; }
        internal static int MaxRows { get; private set; }
        internal static float Width { get; private set; }
        internal static float RowHeight { get; private set; }
        internal static float Opacity { get; private set; }
        internal static Vector2 Position { get; private set; }
        internal static bool ShowEmpty { get; private set; }
        internal static bool ShowAssignments { get; private set; }
        internal static bool HideNative { get; private set; }
        internal static string SortingMode { get; private set; }
        internal static string BackgroundStyle { get; private set; }
        internal static bool HighlightTarget { get; private set; }
        internal static bool ShowOverflow { get; private set; }
        internal static bool SmoothHealth { get; private set; }
        internal static float HealthSmoothing { get; private set; }
        internal static bool PanelBorder { get; private set; }
        internal static float CommandOpacity { get; private set; }
        internal static string PartyActiveHex { get; private set; }
        internal static string G1ActiveHex { get; private set; }
        internal static string G2ActiveHex { get; private set; }
        internal static string G3ActiveHex { get; private set; }
        internal static string UrgentActiveHex { get; private set; }
        internal static string UrgentIdleHex { get; private set; }

        internal static string VisibilityMode { get; private set; }
        internal static string HealthMode { get; private set; }
        internal static string TextAlignment { get; private set; }
        internal static string BarTexture { get; private set; }
        internal static string FontFamily { get; private set; }
        internal static string BorderStyle { get; private set; }
        internal static float BarHeight { get; private set; }
        internal static float RowSpacing { get; private set; }
        internal static float TextSize { get; private set; }
        internal static float ButtonTextSize { get; private set; }
        internal static float BorderWidth { get; private set; }
        internal static bool TextShadow { get; private set; }
        internal static bool BoldNames { get; private set; }
        internal static bool ShowTitle { get; private set; }
        internal static string BarHex { get; private set; }
        internal static string LowHealthHex { get; private set; }
        internal static string TargetHex { get; private set; }
        internal static string BorderHex { get; private set; }
        internal static string TextHex { get; private set; }
        internal static string CommandHex { get; private set; }
        internal static string ActiveCommandHex { get; private set; }
        internal static string BackgroundHex { get; private set; }

        private static string Checked(string[] values, string input, string fallback)
        {
            foreach (string value in values) if (string.Equals(value, input, StringComparison.Ordinal)) return value;
            return fallback;
        }
        private static string CheckedHex(string key, string fallback)
        {
            string saved = PlayerPrefs.GetString(Prefix + key, fallback);
            Color parsed;
            return ColorUtility.TryParseHtmlString(saved, out parsed) ? saved : fallback;
        }
        private static void SaveBool(string key, bool value) { PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0); PlayerPrefs.Save(); }
        private static void SaveFloat(string key, float value) { PlayerPrefs.SetFloat(Prefix + key, value); PlayerPrefs.Save(); }
        private static void SaveString(string key, string value) { PlayerPrefs.SetString(Prefix + key, value); PlayerPrefs.Save(); }

        internal static void Load()
        {
            Enabled = PlayerPrefs.GetInt(Prefix + "Enabled", 0) != 0;
            MaxRows = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "MaxRows", 8), 2, 14);
            Width = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Width", 420f), 320f, 650f);
            RowHeight = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "RowHeight", 40f), 32f, 58f);
            Opacity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Opacity", .92f), 0f, 1f);
            Position = new Vector2(PlayerPrefs.GetFloat(Prefix + "PositionX", -306f),
                PlayerPrefs.GetFloat(Prefix + "PositionY", -315f));
            ShowEmpty = PlayerPrefs.GetInt(Prefix + "ShowEmpty", 1) != 0;
            ShowAssignments = PlayerPrefs.GetInt(Prefix + "ShowAssignments", 1) != 0;
            HideNative = PlayerPrefs.GetInt(Prefix + "HideNative", 0) != 0;
            SortingMode = Checked(SortingModes, PlayerPrefs.GetString(Prefix + "SortingMode", "Native Order"), "Native Order");
            BackgroundStyle = Checked(BackgroundStyles, PlayerPrefs.GetString(Prefix + "BackgroundStyle", "Solid"), "Solid");
            HighlightTarget = PlayerPrefs.GetInt(Prefix + "HighlightTarget", 1) != 0;
            ShowOverflow = PlayerPrefs.GetInt(Prefix + "ShowOverflow", 1) != 0;
            SmoothHealth = PlayerPrefs.GetInt(Prefix + "SmoothHealth", 1) != 0;
            HealthSmoothing = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "HealthSmoothing", 9f), 2f, 25f);
            PanelBorder = PlayerPrefs.GetInt(Prefix + "PanelBorder", 0) != 0;
            CommandOpacity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "CommandOpacity", 1f), .2f, 1f);

            VisibilityMode = Checked(VisibilityModes, PlayerPrefs.GetString(Prefix + "VisibilityMode", "Always"), "Always");
            HealthMode = Checked(HealthModes, PlayerPrefs.GetString(Prefix + "HealthMode", "Percent"), "Percent");
            TextAlignment = Checked(Alignments, PlayerPrefs.GetString(Prefix + "TextAlignment", "Left"), "Left");
            BarTexture = Checked(UnitFrameAppearance.TextureNames,
                PlayerPrefs.GetString(Prefix + "BarTexture", "Flat"), "Flat");
            FontFamily = Checked(UnitFrameAppearance.FontNames,
                PlayerPrefs.GetString(Prefix + "FontFamily", "Default"), "Default");
            BorderStyle = Checked(BorderStyles, PlayerPrefs.GetString(Prefix + "BorderStyle", "None"), "None");
            BarHeight = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "BarHeight", 3f), 2f, 14f);
            RowSpacing = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "RowSpacing", 0f), 0f, 12f);
            TextSize = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "TextSize", 11f), 9f, 22f);
            ButtonTextSize = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "ButtonTextSize", 10f), 8f, 17f);
            BorderWidth = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "BorderWidth", 1f), 1f, 4f);
            TextShadow = PlayerPrefs.GetInt(Prefix + "TextShadow", 0) != 0;
            BoldNames = PlayerPrefs.GetInt(Prefix + "BoldNames", 1) != 0;
            ShowTitle = PlayerPrefs.GetInt(Prefix + "ShowTitle", 1) != 0;
            BarHex = CheckedHex("BarHex", "#45B06E");
            LowHealthHex = CheckedHex("LowHealthHex", "#D94340");
            TargetHex = CheckedHex("TargetHex", "#D9BA6C");
            BorderHex = CheckedHex("BorderHex", "#C8A86E");
            TextHex = CheckedHex("TextHex", "#F0EDE4");
            CommandHex = CheckedHex("CommandHex", "#523B29");
            ActiveCommandHex = CheckedHex("ActiveCommandHex", "#218F82");
            BackgroundHex = CheckedHex("BackgroundHex", "#101720");
            PartyActiveHex = CheckedHex("PartyActiveHex", ActiveCommandHex);
            G1ActiveHex = CheckedHex("G1ActiveHex", ActiveCommandHex);
            G2ActiveHex = CheckedHex("G2ActiveHex", ActiveCommandHex);
            G3ActiveHex = CheckedHex("G3ActiveHex", ActiveCommandHex);
            UrgentActiveHex = CheckedHex("UrgentActiveHex", "#CC3D2E");
            UrgentIdleHex = CheckedHex("UrgentIdleHex", "#853329");
        }
        internal static void SetSortingMode(string value) { SortingMode = Checked(SortingModes, value, "Native Order"); SaveString("SortingMode", SortingMode); }
        internal static void SetBackgroundStyle(string value) { BackgroundStyle = Checked(BackgroundStyles, value, "Solid"); SaveString("BackgroundStyle", BackgroundStyle); }
        internal static void SetHighlightTarget(bool value) { HighlightTarget = value; SaveBool("HighlightTarget", value); }
        internal static void SetShowOverflow(bool value) { ShowOverflow = value; SaveBool("ShowOverflow", value); }
        internal static void SetSmoothHealth(bool value) { SmoothHealth = value; SaveBool("SmoothHealth", value); }
        internal static void SetHealthSmoothing(float value) { HealthSmoothing = Mathf.Clamp(value, 2f, 25f); SaveFloat("HealthSmoothing", HealthSmoothing); }
        internal static void SetPanelBorder(bool value) { PanelBorder = value; SaveBool("PanelBorder", value); }
        internal static void SetCommandOpacity(float value) { CommandOpacity = Mathf.Clamp(value / 100f, .2f, 1f); SaveFloat("CommandOpacity", CommandOpacity); }
        internal static void SetShowAssignments(bool value) { ShowAssignments = value; SaveBool("ShowAssignments", value); }
        internal static void SetHideNative(bool value) { HideNative = value; SaveBool("HideNative", value); }
        internal static void SetEnabled(bool value) { Enabled = value; SaveBool("Enabled", value); }
        internal static void SetShowEmpty(bool value) { ShowEmpty = value; SaveBool("ShowEmpty", value); }
        internal static void SetRows(float value) { MaxRows = Mathf.Clamp(Mathf.RoundToInt(value), 2, 14); PlayerPrefs.SetInt(Prefix + "MaxRows", MaxRows); PlayerPrefs.Save(); }
        internal static void SetWidth(float value) { Width = Mathf.Clamp(value, 320f, 650f); SaveFloat("Width", Width); }
        internal static void SetHeight(float value) { RowHeight = Mathf.Clamp(value, 32f, 58f); SaveFloat("RowHeight", RowHeight); }
        internal static void SetOpacity(float value) { Opacity = Mathf.Clamp(value / 100f, 0f, 1f); SaveFloat("Opacity", Opacity); }
        internal static void SetVisibilityMode(string v) { VisibilityMode = Checked(VisibilityModes, v, "Always"); SaveString("VisibilityMode", VisibilityMode); }
        internal static void SetHealthMode(string v) { HealthMode = Checked(HealthModes, v, "Percent"); SaveString("HealthMode", HealthMode); }
        internal static void SetTextAlignment(string v) { TextAlignment = Checked(Alignments, v, "Left"); SaveString("TextAlignment", TextAlignment); }
        internal static void SetBarTexture(string v) { BarTexture = Checked(UnitFrameAppearance.TextureNames, v, "Flat"); SaveString("BarTexture", BarTexture); }
        internal static void SetFontFamily(string v) { FontFamily = Checked(UnitFrameAppearance.FontNames, v, "Default"); SaveString("FontFamily", FontFamily); }
        internal static void SetBorderStyle(string v) { BorderStyle = Checked(BorderStyles, v, "None"); SaveString("BorderStyle", BorderStyle); }
        internal static void SetBarHeight(float v) { BarHeight = Mathf.Clamp(v, 2f, 14f); SaveFloat("BarHeight", BarHeight); }
        internal static void SetRowSpacing(float v) { RowSpacing = Mathf.Clamp(v, 0f, 12f); SaveFloat("RowSpacing", RowSpacing); }
        internal static void SetTextSize(float v) { TextSize = Mathf.Clamp(v, 9f, 22f); SaveFloat("TextSize", TextSize); }
        internal static void SetButtonTextSize(float v) { ButtonTextSize = Mathf.Clamp(v, 8f, 17f); SaveFloat("ButtonTextSize", ButtonTextSize); }
        internal static void SetBorderWidth(float v) { BorderWidth = Mathf.Clamp(v, 1f, 4f); SaveFloat("BorderWidth", BorderWidth); }
        internal static void SetTextShadow(bool v) { TextShadow = v; SaveBool("TextShadow", v); }
        internal static void SetBoldNames(bool v) { BoldNames = v; SaveBool("BoldNames", v); }
        internal static void SetShowTitle(bool v) { ShowTitle = v; SaveBool("ShowTitle", v); }
        internal static void SetHex(string field, string value)
        {
            Color test;
            if (string.IsNullOrEmpty(value) || !ColorUtility.TryParseHtmlString(value, out test)) return;
            switch (field)
            {
                case "BarHex": BarHex = value; break;
                case "LowHealthHex": LowHealthHex = value; break;
                case "TargetHex": TargetHex = value; break;
                case "BorderHex": BorderHex = value; break;
                case "TextHex": TextHex = value; break;
                case "CommandHex": CommandHex = value; break;
                case "ActiveCommandHex": ActiveCommandHex = value; break;
                case "BackgroundHex": BackgroundHex = value; break;
                case "PartyActiveHex": PartyActiveHex = value; break;
                case "G1ActiveHex": G1ActiveHex = value; break;
                case "G2ActiveHex": G2ActiveHex = value; break;
                case "G3ActiveHex": G3ActiveHex = value; break;
                case "UrgentActiveHex": UrgentActiveHex = value; break;
                case "UrgentIdleHex": UrgentIdleHex = value; break;
                default: return;
            }
            SaveString(field, value);
            // Retain the original shared colour control as a useful global
            // default. Independently customized buttons stay independent.
            if (field == "ActiveCommandHex")
            {
                if (!PlayerPrefs.HasKey(Prefix + "PartyActiveHex")) PartyActiveHex = value;
                if (!PlayerPrefs.HasKey(Prefix + "G1ActiveHex")) G1ActiveHex = value;
                if (!PlayerPrefs.HasKey(Prefix + "G2ActiveHex")) G2ActiveHex = value;
                if (!PlayerPrefs.HasKey(Prefix + "G3ActiveHex")) G3ActiveHex = value;
            }
        }
        internal static void ResetStyle()
        {
            foreach (string key in new[] { "HealthMode", "TextAlignment", "BarTexture", "FontFamily",
                "BorderStyle", "BarHeight", "RowSpacing", "TextSize", "ButtonTextSize", "BorderWidth",
                "TextShadow", "BoldNames", "ShowTitle", "BarHex", "LowHealthHex", "TargetHex",
                "BorderHex", "TextHex", "CommandHex", "ActiveCommandHex", "BackgroundHex",
                "PartyActiveHex", "G1ActiveHex", "G2ActiveHex", "G3ActiveHex",
                "UrgentActiveHex", "UrgentIdleHex", "BackgroundStyle", "PanelBorder", "CommandOpacity",
                "HighlightTarget", "SmoothHealth", "HealthSmoothing" })
                PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.Save();
            Load();
        }
        internal static void SetPosition(Vector2 position)
        {
            SetDragPosition(position);
            PlayerPrefs.SetFloat(Prefix + "PositionX", Position.x);
            PlayerPrefs.SetFloat(Prefix + "PositionY", Position.y);
            PlayerPrefs.Save();
        }
        internal static void SetDragPosition(Vector2 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.x) || float.IsInfinity(position.y)) return;
            Position = new Vector2(Mathf.Clamp(position.x, -1920f, 0f), Mathf.Clamp(position.y, -1100f, 0f));
        }
    }
}
