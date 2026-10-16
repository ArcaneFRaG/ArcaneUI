using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.Theming.Rendering
{
    /// <summary>
    /// Per-module presentation-only overrides. The original gameplay and frame
    /// interaction systems remain responsible for their own data and click handling.
    /// Defaults are deliberately neutral, preserving pre-10.3 rendering.
    /// </summary>
    internal sealed class ArcaneUiFrameFineTuning
    {
        private const string Prefix = "ArcaneUI.FineFrame.v1.";
        private static readonly Dictionary<string, ArcaneUiFrameFineTuning> Cache =
            new Dictionary<string, ArcaneUiFrameFineTuning>(StringComparer.Ordinal);
        internal readonly string Module;
        internal float FrameOpacity, HealthOpacity, ManaOpacity, TextOpacity, StatusOpacity;
        internal float BorderWidth;
        internal string BorderColour, BadgeColour, BadgeMode, BadgePlacement, TextColour, StatusSort;
        internal float BadgeSize, BadgeX, BadgeY;
        internal float AuraSize, AuraSpacing, AuraX, AuraY;
        internal string AuraPlacement;
        internal bool ShowBorder, ShowName, ShowHealthText, ShowManaText, ShowBuffIcons, ShowDebuffIcons;
        internal int MaxStatusIcons;

        private ArcaneUiFrameFineTuning(string module)
        {
            Module = module;
            FrameOpacity = LoadFloat("FrameOpacity", 100, 20, 100);
            HealthOpacity = LoadFloat("HealthOpacity", 100, 0, 100);
            ManaOpacity = LoadFloat("ManaOpacity", 100, 0, 100);
            TextOpacity = LoadFloat("TextOpacity", 100, 0, 100);
            StatusOpacity = LoadFloat("StatusOpacity", 100, 0, 100);
            BorderWidth = LoadFloat("BorderWidth", 2, 0, 8);
            BorderColour = PlayerPrefs.GetString(Key("BorderColour"), "");
            BadgeColour = PlayerPrefs.GetString(Key("BadgeColour"), "#C5A66A");
            TextColour = PlayerPrefs.GetString(Key("TextColour"), "");
            StatusSort = PlayerPrefs.GetString(Key("StatusSort"), "Native");
            BadgeMode = PlayerPrefs.GetString(Key("BadgeMode"), "None");
            BadgePlacement = PlayerPrefs.GetString(Key("BadgePlacement"), "Left");
            BadgeSize = LoadFloat("BadgeSize", 32, 18, 80);
            BadgeX = LoadFloat("BadgeX", 0, -100, 100);
            BadgeY = LoadFloat("BadgeY", 0, -100, 100);
            AuraSize = LoadFloat("AuraSize", 24, 14, 48);
            AuraSpacing = LoadFloat("AuraSpacing", 4, 0, 16);
            AuraX = LoadFloat("AuraX", 0, -250, 250);
            AuraY = LoadFloat("AuraY", 0, -250, 250);
            AuraPlacement = PlayerPrefs.GetString(Key("AuraPlacement"), "Below Frame");
            ShowBorder = LoadBool("ShowBorder", true);
            ShowName = LoadBool("ShowName", true);
            ShowHealthText = LoadBool("ShowHealthText", true);
            ShowManaText = LoadBool("ShowManaText", true);
            bool player = module == "player-frame";
            ShowBuffIcons = LoadBool("ShowBuffIcons", !player);
            ShowDebuffIcons = LoadBool("ShowDebuffIcons", !player);
            MaxStatusIcons = Mathf.Clamp(PlayerPrefs.GetInt(Key("MaxStatusIcons"), player ? 4 : 8), 0, 8);
        }
        private string Key(string name) { return Prefix + Module + "." + name; }
        private float LoadFloat(string name, float fallback, float min, float max)
        { return Mathf.Clamp(PlayerPrefs.GetFloat(Key(name), fallback), min, max); }
        private bool LoadBool(string name, bool fallback)
        { return PlayerPrefs.GetInt(Key(name), fallback ? 1 : 0) != 0; }
        internal static ArcaneUiFrameFineTuning For(string module)
        {
            ArcaneUiFrameFineTuning settings;
            if (!Cache.TryGetValue(module, out settings))
            {
                settings = new ArcaneUiFrameFineTuning(module);
                Cache.Add(module, settings);
            }
            return settings;
        }
        internal void SetFloat(string key, float value)
        {
            switch (key)
            {
                case "FrameOpacity": FrameOpacity = Mathf.Clamp(value, 20, 100); value = FrameOpacity; break;
                case "HealthOpacity": HealthOpacity = Mathf.Clamp(value, 0, 100); value = HealthOpacity; break;
                case "ManaOpacity": ManaOpacity = Mathf.Clamp(value, 0, 100); value = ManaOpacity; break;
                case "TextOpacity": TextOpacity = Mathf.Clamp(value, 0, 100); value = TextOpacity; break;
                case "StatusOpacity": StatusOpacity = Mathf.Clamp(value, 0, 100); value = StatusOpacity; break;
                case "BorderWidth": BorderWidth = Mathf.Clamp(value, 0, 8); value = BorderWidth; break;
                case "BadgeSize": BadgeSize = Mathf.Clamp(value, 18, 80); value = BadgeSize; break;
                case "BadgeX": BadgeX = Mathf.Clamp(value, -100, 100); value = BadgeX; break;
                case "BadgeY": BadgeY = Mathf.Clamp(value, -100, 100); value = BadgeY; break;
                case "AuraSize": AuraSize = Mathf.Clamp(value, 14, 48); value = AuraSize; break;
                case "AuraSpacing": AuraSpacing = Mathf.Clamp(value, 0, 16); value = AuraSpacing; break;
                case "AuraX": AuraX = Mathf.Clamp(value, -250, 250); value = AuraX; break;
                case "AuraY": AuraY = Mathf.Clamp(value, -250, 250); value = AuraY; break;
                default: return;
            }
            PlayerPrefs.SetFloat(Key(key), value); PlayerPrefs.Save();
        }
        internal void SetBool(string key, bool value)
        {
            switch (key)
            {
                case "ShowBorder": ShowBorder = value; break;
                case "ShowName": ShowName = value; break;
                case "ShowHealthText": ShowHealthText = value; break;
                case "ShowManaText": ShowManaText = value; break;
                case "ShowBuffIcons": ShowBuffIcons = value; break;
                case "ShowDebuffIcons": ShowDebuffIcons = value; break;
                default: return;
            }
            PlayerPrefs.SetInt(Key(key), value ? 1 : 0); PlayerPrefs.Save();
        }
        internal void SetStatusSort(string value)
        {
            if (value != "Native" && value != "Longest First" && value != "Shortest First") return;
            StatusSort = value; PlayerPrefs.SetString(Key("StatusSort"), value); PlayerPrefs.Save();
        }
        internal void SetMaxStatus(int limit)
        {
            MaxStatusIcons = Mathf.Clamp(limit, 0, 8);
            PlayerPrefs.SetInt(Key("MaxStatusIcons"), MaxStatusIcons); PlayerPrefs.Save();
        }
        internal void SetBadgeMode(string value)
        {
            if (value != "None" && value != "Class" && value != "Level") return;
            BadgeMode = value; PlayerPrefs.SetString(Key("BadgeMode"), value); PlayerPrefs.Save();
        }
        internal void SetAuraPlacement(string value)
        {
            if (value != "Below Frame" && value != "Above Frame" &&
                value != "Left" && value != "Right") return;
            AuraPlacement = value;
            PlayerPrefs.SetString(Key("AuraPlacement"), value); PlayerPrefs.Save();
        }
        internal void SetBadgePlacement(string value)
        {
            if (value != "Left" && value != "Right") return;
            BadgePlacement = value; PlayerPrefs.SetString(Key("BadgePlacement"), value); PlayerPrefs.Save();
        }
        internal void SetColour(string key, string value)
        {
            Color candidate;
            if (!ColorUtility.TryParseHtmlString(value, out candidate)) return;
            string hex = "#" + ColorUtility.ToHtmlStringRGB(candidate);
            if (key == "BorderColour") BorderColour = hex;
            else if (key == "BadgeColour") BadgeColour = hex;
            else if (key == "TextColour") TextColour = hex;
            else return;
            PlayerPrefs.SetString(Key(key), hex); PlayerPrefs.Save();
        }
        internal void ClearColour(string key)
        {
            if (key == "BorderColour") BorderColour = "";
            else if (key == "TextColour") TextColour = "";
            else return;
            PlayerPrefs.DeleteKey(Key(key)); PlayerPrefs.Save();
        }
        // Profile application changes PlayerPrefs directly rather than going
        // through every fine-tuning setter. Drop cached instances so the next
        // redraw reads the just-restored values instead of stale pre-load ones.
        internal static void InvalidateCache()
        {
            Cache.Clear();
        }

        internal static void Reset(string module)
        {
            string[] floats = { "FrameOpacity", "HealthOpacity", "ManaOpacity", "TextOpacity",
                "StatusOpacity", "BorderWidth", "BadgeSize", "BadgeX", "BadgeY",
                "AuraSize", "AuraSpacing", "AuraX", "AuraY" };
            string[] strings = { "BorderColour", "BadgeColour", "TextColour", "BadgeMode",
                "BadgePlacement", "StatusSort", "AuraPlacement" };
            string[] booleans = { "ShowBorder", "ShowName", "ShowHealthText", "ShowManaText",
                "ShowBuffIcons", "ShowDebuffIcons" };
            string prefix = Prefix + module + ".";
            foreach (string key in floats) PlayerPrefs.DeleteKey(prefix + key);
            foreach (string key in strings) PlayerPrefs.DeleteKey(prefix + key);
            foreach (string key in booleans) PlayerPrefs.DeleteKey(prefix + key);
            PlayerPrefs.DeleteKey(prefix + "MaxStatusIcons");
            PlayerPrefs.Save(); Cache.Remove(module);
        }
        internal static void ApplyAlpha(GameObject obj, float percentage)
        {
            if (obj == null) return;
            CanvasGroup alpha = obj.GetComponent<CanvasGroup>();
            if (alpha == null) alpha = obj.AddComponent<CanvasGroup>();
            alpha.alpha = Mathf.Clamp01(percentage / 100f);
            // Never alter interactions, blocking or raycast behaviour.
        }
    }
}
