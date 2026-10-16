using System;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.Chat.Configuration
{
    /// <summary>Opt-in native chat presentation; no message routing or command changes.</summary>
    internal static class ChatSettings
    {
        private const string Key = "ArcaneUI.Chat.";
        internal static readonly string[] Presets = { "Arcane Dark", "Refined Fantasy", "Minimal Clean" };
        internal static readonly string[] Borders = { "Single", "Double", "Inset", "None" };
        internal static readonly string[] BackgroundStyles = { "Solid", "Vertical Gradient", "Horizontal Gradient", "Soft Bands" };
        internal static readonly string[] FontStyles = { "Native", "Normal", "Bold", "Italic" };
        internal static readonly string[] FontFamilies = { "Native", "Arial", "Verdana", "Tahoma", "Georgia", "Courier New", "Segoe UI", "Trebuchet MS", "Calibri", "Cambria", "Garamond", "Consolas", "Times New Roman", "Century Gothic" };
        internal static readonly string[] SkinVisibilityModes = { "Always", "Fade Outside Chat", "Hide Skin Outside Chat", "Combat Skin Only" };
        internal static bool Enabled { get; private set; }
        internal static bool InheritTheme { get; private set; }
        internal static bool Frame { get; private set; }
        internal static bool TintBackground { get; private set; }
        internal static bool StyleText { get; private set; }
        internal static string Preset { get; private set; }
        internal static float BackgroundOpacity { get; private set; }
        internal static float BorderThickness { get; private set; }
        internal static float FontSize { get; private set; }
        internal static float LineSpacing { get; private set; }
        internal static string AccentHex { get; private set; }
        internal static string BackgroundHex { get; private set; }
        internal static string TextHex { get; private set; }
        internal static string BorderStyle { get; private set; }
        internal static string BackgroundStyle { get; private set; }
        internal static string GradientHex { get; private set; }
        internal static string BorderHex { get; private set; }
        internal static bool CustomBorderColor { get; private set; }
        internal static string FontStyle { get; private set; }
        internal static string FontFamily { get; private set; }
        internal static string SkinVisibility { get; private set; }
        internal static bool ShadowText { get; private set; }
        internal static bool OutlineText { get; private set; }
        internal static bool EditLayout { get; private set; }
        internal static bool MoveNative { get; private set; }
        internal static bool ResizeNative { get; private set; }
        internal static float BorderOpacity { get; private set; }
        internal static float PaddingX { get; private set; }
        internal static float PaddingY { get; private set; }
        internal static float IdleOpacity { get; private set; }
        internal static float TextEffectStrength { get; private set; }
        internal static float OffsetX { get; private set; }
        internal static float OffsetY { get; private set; }
        internal static float Width { get; private set; }
        internal static float Height { get; private set; }

        internal static void Load()
        {
            // Default OFF until Erenshor's actual chat hierarchy has been mapped in-game.
            Enabled = PlayerPrefs.GetInt(Key + "Enabled", 0) != 0;
            InheritTheme = PlayerPrefs.GetInt(Key + "InheritTheme", 1) != 0;
            Frame = PlayerPrefs.GetInt(Key + "Frame", 1) != 0;
            TintBackground = PlayerPrefs.GetInt(Key + "TintBackground", 1) != 0;
            StyleText = PlayerPrefs.GetInt(Key + "StyleText", 1) != 0;
            Preset = PlayerPrefs.GetString(Key + "Preset", Presets[0]);
            if (Array.IndexOf(Presets, Preset) < 0) Preset = Presets[0];
            BackgroundOpacity = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "BackgroundOpacity", 65f), 0f, 100f);
            BorderThickness = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "BorderThickness", 2f), 0f, 6f);
            FontSize = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "FontSize", 14f), 9f, 24f);
            LineSpacing = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "LineSpacing", 1.05f), .8f, 2f);
            AccentHex = PlayerPrefs.GetString(Key + "AccentHex", "#CCA970");
            BackgroundHex = PlayerPrefs.GetString(Key + "BackgroundHex", "#17202D");
            TextHex = PlayerPrefs.GetString(Key + "TextHex", "#E8E5DB");
            BorderStyle = Checked(Borders, PlayerPrefs.GetString(Key + "BorderStyle", "Single"));
            BackgroundStyle = Checked(BackgroundStyles, PlayerPrefs.GetString(Key + "BackgroundStyle", "Solid"));
            GradientHex = PlayerPrefs.GetString(Key + "GradientHex", "#304354");
            BorderHex = PlayerPrefs.GetString(Key + "BorderHex", "#CCA970");
            CustomBorderColor = PlayerPrefs.GetInt(Key + "CustomBorderColor", 0) == 1;
            FontStyle = Checked(FontStyles, PlayerPrefs.GetString(Key + "FontStyle", "Native"));
            FontFamily = Checked(FontFamilies, PlayerPrefs.GetString(Key + "FontFamily", "Native"));
            SkinVisibility = Checked(SkinVisibilityModes, PlayerPrefs.GetString(Key + "SkinVisibility", "Always"));
            ShadowText = PlayerPrefs.GetInt(Key + "ShadowText", 0) == 1;
            OutlineText = PlayerPrefs.GetInt(Key + "OutlineText", 0) == 1;
            EditLayout = false; // never leave draggable overlays enabled after a restart
            MoveNative = PlayerPrefs.GetInt(Key + "MoveNative", 0) == 1;
            ResizeNative = PlayerPrefs.GetInt(Key + "ResizeNative", 0) == 1;
            BorderOpacity = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "BorderOpacity", 100f), 0f, 100f);
            PaddingX = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "PaddingX", 8f), 0f, 32f);
            PaddingY = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "PaddingY", 7f), 0f, 32f);
            IdleOpacity = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "IdleOpacity", 25f), 0f, 100f);
            TextEffectStrength = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "TextEffectStrength", 1f), .5f, 3f);
            OffsetX = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "OffsetX", 0f), -900f, 900f);
            OffsetY = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "OffsetY", 0f), -900f, 900f);
            Width = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Width", 0f), 0f, 1000f);
            Height = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Height", 0f), 0f, 750f);
        }

        private static string Checked(string[] allowed, string value)
        {
            return Array.IndexOf(allowed, value) >= 0 ? value : allowed[0];
        }
        internal static void SetChoice(string name, string value)
        {
            switch (name)
            {
                case "BorderStyle": value = Checked(Borders, value); BorderStyle = value; break;
                case "BackgroundStyle": value = Checked(BackgroundStyles, value); BackgroundStyle = value; break;
                case "FontStyle": value = Checked(FontStyles, value); FontStyle = value; break;
                case "FontFamily": value = Checked(FontFamilies, value); FontFamily = value; break;
                case "SkinVisibility": value = Checked(SkinVisibilityModes, value); SkinVisibility = value; break;
                default: return;
            }
            PlayerPrefs.SetString(Key + name, value);
            PlayerPrefs.Save();
        }
        internal static void ResetLayout()
        {
            SetBool("MoveNative", false); SetBool("ResizeNative", false);
            SetBool("EditLayout", false);
            SetFloat("OffsetX", 0f); SetFloat("OffsetY", 0f);
            SetFloat("Width", 0f); SetFloat("Height", 0f);
        }
        internal static void SetBool(string name, bool value)
        {
            switch (name)
            {
                case "Enabled": Enabled = value; break;
                case "InheritTheme": InheritTheme = value; break;
                case "Frame": Frame = value; break;
                case "TintBackground": TintBackground = value; break;
                case "StyleText": StyleText = value; break;
                case "ShadowText": ShadowText = value; break;
                case "OutlineText": OutlineText = value; break;
                case "CustomBorderColor": CustomBorderColor = value; break;
                case "EditLayout": EditLayout = value; return; // never persist unlocked mode
                case "MoveNative": MoveNative = value; break;
                case "ResizeNative": ResizeNative = value; break;
                default: return;
            }
            PlayerPrefs.SetInt(Key + name, value ? 1 : 0);
            PlayerPrefs.Save();
        }
        internal static void SetFloat(string name, float value, bool save = true)
        {
            switch (name)
            {
                case "BackgroundOpacity": BackgroundOpacity = Mathf.Clamp(value, 0f, 100f); value = BackgroundOpacity; break;
                case "BorderThickness": BorderThickness = Mathf.Clamp(value, 0f, 6f); value = BorderThickness; break;
                case "FontSize": FontSize = Mathf.Clamp(value, 9f, 24f); value = FontSize; break;
                case "LineSpacing": LineSpacing = Mathf.Clamp(value, .8f, 2f); value = LineSpacing; break;
                case "BorderOpacity": BorderOpacity = Mathf.Clamp(value, 0f, 100f); value = BorderOpacity; break;
                case "PaddingX": PaddingX = Mathf.Clamp(value, 0f, 32f); value = PaddingX; break;
                case "PaddingY": PaddingY = Mathf.Clamp(value, 0f, 32f); value = PaddingY; break;
                case "IdleOpacity": IdleOpacity = Mathf.Clamp(value, 0f, 100f); value = IdleOpacity; break;
                case "TextEffectStrength": TextEffectStrength = Mathf.Clamp(value, .5f, 3f); value = TextEffectStrength; break;
                case "OffsetX": OffsetX = Mathf.Clamp(value, -900f, 900f); value = OffsetX; break;
                case "OffsetY": OffsetY = Mathf.Clamp(value, -900f, 900f); value = OffsetY; break;
                case "Width": Width = Mathf.Clamp(value, 0f, 1000f); value = Width; break;
                case "Height": Height = Mathf.Clamp(value, 0f, 750f); value = Height; break;
                default: return;
            }
            PlayerPrefs.SetFloat(Key + name, value);
            if (save) PlayerPrefs.Save();
        }
        internal static void SetPreset(string value)
        {
            if (Array.IndexOf(Presets, value) < 0) return;
            Preset = value;
            PlayerPrefs.SetString(Key + "Preset", value);
            PlayerPrefs.Save();
        }
        internal static void SetHex(string name, string hex)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString(hex, out color)) return;
            string result = "#" + ColorUtility.ToHtmlStringRGB(color);
            switch (name)
            {
                case "AccentHex": AccentHex = result; break;
                case "BackgroundHex": BackgroundHex = result; break;
                case "TextHex": TextHex = result; break;
                case "GradientHex": GradientHex = result; break;
                case "BorderHex": BorderHex = result; break;
                default: return;
            }
            PlayerPrefs.SetString(Key + name, result);
            PlayerPrefs.Save();
        }
    }
}
