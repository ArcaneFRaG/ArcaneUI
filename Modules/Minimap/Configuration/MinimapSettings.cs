using System;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.Minimap.Configuration
{
    /// <summary>Only minimap presentation; never manipulates world-map, zoom, navigation or map markers.</summary>
    internal static class MinimapSettings
    {
        private const string Key = "ArcaneUI.Minimap.";
        internal static readonly string[] Styles = { "Round", "Square", "Rounded", "Ornate", "Minimal" };
        internal static bool Enabled { get; private set; }
        internal static bool ShowNative { get; private set; }
        internal static bool ShowCustom { get { return !ShowNative; } }
        internal static bool MoveNative { get; private set; }
        internal static bool MKeyEnlarge { get; private set; }
        internal static float EnlargedScale { get; private set; }
        internal static bool ShowBorder { get; private set; }
        internal static bool InheritTheme { get; private set; }
        internal static string Style { get; private set; }
        internal static string BorderHex { get; private set; }
        internal static float BorderThickness { get; private set; }
        internal static float BorderOpacity { get; private set; }
        internal static float Inset { get; private set; }
        internal static float Scale { get; private set; }
        internal static float OffsetX { get; private set; }
        internal static float OffsetY { get; private set; }
        internal static void Load()
        {
            Enabled = PlayerPrefs.GetInt(Key + "Enabled", 0) == 1;
            ShowNative = PlayerPrefs.GetInt(Key + "ShowNative", 1) != 0;
            MoveNative = PlayerPrefs.GetInt(Key + "MoveNative", 0) == 1;
            MKeyEnlarge = PlayerPrefs.GetInt(Key + "MKeyEnlarge", 1) == 1;
            EnlargedScale = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "EnlargedScale", 190f), 125f, 275f);
            ShowBorder = PlayerPrefs.GetInt(Key + "ShowBorder", 1) == 1;
            InheritTheme = PlayerPrefs.GetInt(Key + "InheritTheme", 1) == 1;
            Style = PlayerPrefs.GetString(Key + "Style", "Round");
            if (Array.IndexOf(Styles, Style) < 0) Style = "Round";
            BorderHex = PlayerPrefs.GetString(Key + "BorderHex", "#D4B77C");
            BorderThickness = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "BorderThickness", 4f), 0f, 16f);
            BorderOpacity = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "BorderOpacity", 100f), 0f, 100f);
            Inset = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Inset", 1f), 0f, 24f);
            Scale = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Scale", 100f), 50f, 200f);
            OffsetX = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "OffsetX", 0f), -1300f, 1300f);
            OffsetY = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "OffsetY", 0f), -1000f, 1000f);
            // Phase 11.3.2 applied offsets/scale to the 800x800 renderer itself.
            // Discard those potentially corrupting transforms once on upgrade;
            // only a verified compact HUD container may be edited from now on.
            if (PlayerPrefs.GetInt(Key + "HudContainerMigrationV1", 0) == 0)
            {
                MoveNative = false;
                OffsetX = 0f;
                OffsetY = 0f;
                Scale = 100f;
                PlayerPrefs.SetInt(Key + "MoveNative", 0);
                PlayerPrefs.SetFloat(Key + "OffsetX", 0f);
                PlayerPrefs.SetFloat(Key + "OffsetY", 0f);
                PlayerPrefs.SetFloat(Key + "Scale", 100f);
                PlayerPrefs.SetInt(Key + "HudContainerMigrationV1", 1);
                PlayerPrefs.Save();
            }
        }
        internal static void SetBool(string key, bool value)
        {
            switch (key)
            {
                case "Enabled": Enabled = value; break;
                case "ShowNative": ShowNative = value; break;
                case "MoveNative": MoveNative = value; break;
                case "MKeyEnlarge": MKeyEnlarge = value; break;
                case "ShowBorder": ShowBorder = value; break;
                case "InheritTheme": InheritTheme = value; break;
                default: return;
            }
            PlayerPrefs.SetInt(Key + key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
        internal static void SetFloat(string key, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            switch (key)
            {
                case "BorderThickness": BorderThickness = Mathf.Clamp(value, 0f, 16f); value = BorderThickness; break;
                case "BorderOpacity": BorderOpacity = Mathf.Clamp(value, 0f, 100f); value = BorderOpacity; break;
                case "Inset": Inset = Mathf.Clamp(value, 0f, 24f); value = Inset; break;
                case "Scale": Scale = Mathf.Clamp(value, 50f, 200f); value = Scale; break;
                case "EnlargedScale": EnlargedScale = Mathf.Clamp(value, 125f, 275f); value = EnlargedScale; break;
                case "OffsetX": OffsetX = Mathf.Clamp(value, -1300f, 1300f); value = OffsetX; break;
                case "OffsetY": OffsetY = Mathf.Clamp(value, -1000f, 1000f); value = OffsetY; break;
                default: return;
            }
            PlayerPrefs.SetFloat(Key + key, value);
            PlayerPrefs.Save();
        }
        internal static void SetStyle(string value)
        {
            if (Array.IndexOf(Styles, value) < 0) return;
            Style = value;
            PlayerPrefs.SetString(Key + "Style", value); PlayerPrefs.Save();
        }
        internal static void SetHex(string value)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(value, out c)) return;
            BorderHex = "#" + ColorUtility.ToHtmlStringRGB(c);
            PlayerPrefs.SetString(Key + "BorderHex", BorderHex); PlayerPrefs.Save();
        }
        internal static void ResetLayout()
        {
            SetFloat("OffsetX", 0f);
            SetFloat("OffsetY", 0f);
            SetFloat("Scale", 100f);
            SetBool("MoveNative", false);
        }
    }
}
