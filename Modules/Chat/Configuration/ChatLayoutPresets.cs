using System;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.Chat.Configuration
{
    /// <summary>
    /// Three character-scoped chat appearance/layout presets. Persisted data
    /// affects presentation only; never contains chat logs or native chat binds.
    /// </summary>
    internal static class ChatLayoutPresets
    {
        private const string Prefix = "ArcaneUI.Chat.Profile.v1.";
        internal const int Slots = 3;
        [Serializable]
        private sealed class Record
        {
            public int Version = 2;
            public string Owner;
            public bool Enabled, InheritTheme, Frame, TintBackground, StyleText;
            public bool ShadowText, OutlineText, MoveNative, ResizeNative;
            public string Preset, BorderStyle, FontStyle, FontFamily, SkinVisibility;
            public string AccentHex, BackgroundHex, TextHex;
            public string BackgroundStyle, GradientHex, BorderHex;
            public bool CustomBorderColor;
            public float BackgroundOpacity, BorderThickness, FontSize, LineSpacing;
            public float BorderOpacity, PaddingX, PaddingY, IdleOpacity, TextEffectStrength;
            public float OffsetX, OffsetY, Width, Height;
        }
        private static string Owner
        {
            get
            {
                Stats player = GameData.PlayerStats;
                return player == null || string.IsNullOrEmpty(player.MyName) ? null : player.MyName.Trim();
            }
        }
        private static string Key(int slot)
        {
            var owner = Owner;
            return owner == null || slot < 1 || slot > Slots ? null : Prefix + Uri.EscapeDataString(owner) + "." + slot;
        }
        internal static string Description(int slot)
        {
            string key = Key(slot);
            if (key == null) return "Requires a logged-in character";
            return string.IsNullOrEmpty(PlayerPrefs.GetString(key, "")) ? "Empty" : "Saved";
        }
        internal static bool Save(int slot, out string message)
        {
            string key = Key(slot);
            if (key == null) { message = "Log in to save character chat presets."; return false; }
            var data = new Record {
                Owner = Owner,
                Enabled = ChatSettings.Enabled, InheritTheme = ChatSettings.InheritTheme,
                Frame = ChatSettings.Frame, TintBackground = ChatSettings.TintBackground,
                StyleText = ChatSettings.StyleText, ShadowText = ChatSettings.ShadowText,
                OutlineText = ChatSettings.OutlineText, MoveNative = ChatSettings.MoveNative,
                ResizeNative = ChatSettings.ResizeNative,
                Preset = ChatSettings.Preset, BorderStyle = ChatSettings.BorderStyle,
                FontStyle = ChatSettings.FontStyle, FontFamily = ChatSettings.FontFamily,
                SkinVisibility = ChatSettings.SkinVisibility,
                AccentHex = ChatSettings.AccentHex, BackgroundHex = ChatSettings.BackgroundHex,
                TextHex = ChatSettings.TextHex,
                BackgroundStyle = ChatSettings.BackgroundStyle,
                GradientHex = ChatSettings.GradientHex,
                BorderHex = ChatSettings.BorderHex,
                CustomBorderColor = ChatSettings.CustomBorderColor,
                BackgroundOpacity = ChatSettings.BackgroundOpacity,
                BorderThickness = ChatSettings.BorderThickness, FontSize = ChatSettings.FontSize,
                LineSpacing = ChatSettings.LineSpacing, BorderOpacity = ChatSettings.BorderOpacity,
                PaddingX = ChatSettings.PaddingX, PaddingY = ChatSettings.PaddingY,
                IdleOpacity = ChatSettings.IdleOpacity, TextEffectStrength = ChatSettings.TextEffectStrength,
                OffsetX = ChatSettings.OffsetX, OffsetY = ChatSettings.OffsetY,
                Width = ChatSettings.Width, Height = ChatSettings.Height
            };
            try
            {
                PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
                PlayerPrefs.Save();
                message = "Saved chat preset " + slot + " for " + Owner + ".";
                return true;
            }
            catch (Exception e)
            {
                message = "Failed saving chat preset: " + e.Message;
                return false;
            }
        }
        private static bool Finite(float value)
        { return !float.IsNaN(value) && !float.IsInfinity(value); }
        internal static bool Load(int slot, out string message)
        {
            string key = Key(slot);
            if (key == null) { message = "Log in to load character chat presets."; return false; }
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) { message = "Chat preset " + slot + " is empty."; return false; }
            Record data;
            try { data = JsonUtility.FromJson<Record>(json); }
            catch (Exception e) { message = "Invalid saved chat preset: " + e.Message; return false; }
            if (data == null || (data.Version != 1 && data.Version != 2) || data.Owner != Owner ||
                !Finite(data.BackgroundOpacity) || !Finite(data.BorderThickness) ||
                !Finite(data.FontSize) || !Finite(data.LineSpacing) ||
                !Finite(data.BorderOpacity) || !Finite(data.PaddingX) || !Finite(data.PaddingY) ||
                !Finite(data.IdleOpacity) || !Finite(data.TextEffectStrength) ||
                !Finite(data.OffsetX) || !Finite(data.OffsetY) ||
                !Finite(data.Width) || !Finite(data.Height))
            { message = "Invalid or foreign chat preset; settings were not changed."; return false; }
            // Apply via normal, range-clamped setters so hand-edited JSON cannot
            // bypass controls, and never restore an unlocked edit overlay.
            ChatSettings.SetBool("Enabled", data.Enabled);
            ChatSettings.SetBool("InheritTheme", data.InheritTheme);
            ChatSettings.SetBool("Frame", data.Frame);
            ChatSettings.SetBool("TintBackground", data.TintBackground);
            ChatSettings.SetBool("StyleText", data.StyleText);
            ChatSettings.SetBool("ShadowText", data.ShadowText);
            ChatSettings.SetBool("OutlineText", data.OutlineText);
            ChatSettings.SetBool("MoveNative", data.MoveNative);
            ChatSettings.SetBool("ResizeNative", data.ResizeNative);
            ChatSettings.SetBool("EditLayout", false);
            ChatSettings.SetPreset(data.Preset);
            ChatSettings.SetChoice("BorderStyle", data.BorderStyle);
            ChatSettings.SetChoice("FontStyle", data.FontStyle);
            ChatSettings.SetChoice("FontFamily", data.FontFamily);
            ChatSettings.SetChoice("SkinVisibility", data.SkinVisibility);
            ChatSettings.SetHex("AccentHex", data.AccentHex);
            ChatSettings.SetHex("BackgroundHex", data.BackgroundHex);
            ChatSettings.SetHex("TextHex", data.TextHex);
            if (data.Version >= 2)
            {
                ChatSettings.SetChoice("BackgroundStyle", data.BackgroundStyle);
                ChatSettings.SetHex("GradientHex", data.GradientHex);
                ChatSettings.SetHex("BorderHex", data.BorderHex);
                ChatSettings.SetBool("CustomBorderColor", data.CustomBorderColor);
            }
            ChatSettings.SetFloat("BackgroundOpacity", data.BackgroundOpacity);
            ChatSettings.SetFloat("BorderThickness", data.BorderThickness);
            ChatSettings.SetFloat("FontSize", data.FontSize);
            ChatSettings.SetFloat("LineSpacing", data.LineSpacing);
            ChatSettings.SetFloat("BorderOpacity", data.BorderOpacity);
            ChatSettings.SetFloat("PaddingX", data.PaddingX);
            ChatSettings.SetFloat("PaddingY", data.PaddingY);
            ChatSettings.SetFloat("IdleOpacity", data.IdleOpacity);
            ChatSettings.SetFloat("TextEffectStrength", data.TextEffectStrength);
            ChatSettings.SetFloat("OffsetX", data.OffsetX);
            ChatSettings.SetFloat("OffsetY", data.OffsetY);
            ChatSettings.SetFloat("Width", data.Width);
            ChatSettings.SetFloat("Height", data.Height);
            message = "Loaded chat preset " + slot + " for " + Owner + ". Native chat messages and commands unchanged.";
            return true;
        }
    }
}
