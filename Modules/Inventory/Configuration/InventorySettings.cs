using System;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.Inventory.Configuration
{
    /// <summary>Native-safe inventory appearance settings. No item or gameplay state is cached here.</summary>
    internal static class InventorySettings
    {
        private const string Key = "ArcaneUI.Inventory.";
        internal static readonly string[] VisualPresets = { "Arcane Framed", "Refined Fantasy", "Minimal Clean" };
        internal static string VisualPreset { get; private set; }
        internal static float SlotInset { get; private set; }
        internal static float SlotFillOpacity { get; private set; }
        internal static bool InheritTheme { get; private set; }
        internal static bool DrawFrame { get; private set; }
        internal static bool FrameSlots { get; private set; }
        internal static bool ShowHeader { get; private set; }
        internal static bool TintNativeBackground { get; private set; }
        internal static float BorderThickness { get; private set; }
        internal static float SlotThickness { get; private set; }
        internal static float HeaderHeight { get; private set; }
        internal static float HeaderOpacity { get; private set; }
        internal static float NativeTintStrength { get; private set; }
        internal static float MaxSlotBorders { get; private set; }
        internal static string AccentHex { get; private set; }
        internal static string BackgroundHex { get; private set; }
        internal static string TextHex { get; private set; }

        internal static void Load()
        {
            InheritTheme = PlayerPrefs.GetInt(Key + "InheritTheme", 1) != 0;
            VisualPreset = PlayerPrefs.GetString(Key + "VisualPreset", VisualPresets[0]);
            if (Array.IndexOf(VisualPresets, VisualPreset) < 0) VisualPreset = VisualPresets[0];
            SlotInset = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "SlotInset", 3f), 0f, 10f);
            SlotFillOpacity = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "SlotFillOpacity", 96f), 0f, 100f);
            DrawFrame = PlayerPrefs.GetInt(Key + "DrawFrame", 1) != 0;
            FrameSlots = PlayerPrefs.GetInt(Key + "FrameSlots", 1) != 0;
            ShowHeader = PlayerPrefs.GetInt(Key + "ShowHeader", 1) != 0;
            TintNativeBackground = PlayerPrefs.GetInt(Key + "TintNativeBackground", 0) != 0;
            BorderThickness = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "BorderThickness", 2f), 0f, 8f);
            SlotThickness = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "SlotThickness", 2f), 0f, 6f);
            HeaderHeight = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "HeaderHeight", 24f), 14f, 55f);
            HeaderOpacity = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "HeaderOpacity", 70f), 0f, 100f);
            NativeTintStrength = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "NativeTintStrength", 25f), 0f, 100f);
            MaxSlotBorders = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "MaxSlotBorders", 80f), 0f, 160f);
            AccentHex = PlayerPrefs.GetString(Key + "AccentHex", "#CEAC6B");
            BackgroundHex = PlayerPrefs.GetString(Key + "BackgroundHex", "#1D2231");
            TextHex = PlayerPrefs.GetString(Key + "TextHex", "#EDE6D5");
        }
        internal static void SetBool(string name, bool value)
        {
            switch(name)
            {
                case "InheritTheme": InheritTheme = value; break;
                case "DrawFrame": DrawFrame = value; break;
                case "FrameSlots": FrameSlots = value; break;
                case "ShowHeader": ShowHeader = value; break;
                case "TintNativeBackground": TintNativeBackground = value; break;
                default: return;
            }
            PlayerPrefs.SetInt(Key + name, value ? 1 : 0);
            PlayerPrefs.Save();
        }
        internal static void SetFloat(string name, float value)
        {
            switch (name)
            {
                case "SlotInset": SlotInset = Mathf.Clamp(value, 0f, 10f); value = SlotInset; break;
                case "SlotFillOpacity": SlotFillOpacity = Mathf.Clamp(value, 0f, 100f); value = SlotFillOpacity; break;
                case "BorderThickness": BorderThickness = Mathf.Clamp(value, 0f, 8f); value = BorderThickness; break;
                case "SlotThickness": SlotThickness = Mathf.Clamp(value, 0f, 6f); value = SlotThickness; break;
                case "HeaderHeight": HeaderHeight = Mathf.Clamp(value, 14f, 55f); value = HeaderHeight; break;
                case "HeaderOpacity": HeaderOpacity = Mathf.Clamp(value, 0f, 100f); value = HeaderOpacity; break;
                case "NativeTintStrength": NativeTintStrength = Mathf.Clamp(value, 0f, 100f); value = NativeTintStrength; break;
                case "MaxSlotBorders": MaxSlotBorders = Mathf.Clamp(value, 0f, 160f); value = MaxSlotBorders; break;
                default: return;
            }
            PlayerPrefs.SetFloat(Key + name, value);
            PlayerPrefs.Save();
        }
        internal static void SetPreset(string selected)
        {
            if (Array.IndexOf(VisualPresets, selected) < 0) return;
            VisualPreset = selected;
            PlayerPrefs.SetString(Key + "VisualPreset", selected);
            PlayerPrefs.Save();
        }
        internal static void SetHex(string name, string hex)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(hex, out c)) return;
            string normalized = "#" + ColorUtility.ToHtmlStringRGB(c);
            switch (name)
            {
                case "AccentHex": AccentHex = normalized; break;
                case "BackgroundHex": BackgroundHex = normalized; break;
                case "TextHex": TextHex = normalized; break;
                default: return;
            }
            PlayerPrefs.SetString(Key + name, normalized);
            PlayerPrefs.Save();
        }
    }
}
