using System;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Modules;

namespace ArcanesGroupFrames.Framework.Theming.Rendering
{
    /// <summary>Character and pixel-width truncation for the shared name tag.
    /// Kept separate from Stats/target resolution so no gameplay names change.</summary>
    internal static class ArcaneUiNameFormatting
    {
        private const string Key = "ArcaneUI.NameMaxChars.";
        internal static int Limit(string module)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(Key + module, 18), 4, 60);
        }
        internal static void SetLimit(string module, float length)
        {
            PlayerPrefs.SetInt(Key + module, Mathf.Clamp(Mathf.RoundToInt(length), 4, 60));
            PlayerPrefs.Save();
        }
        internal static string Fit(Text label, string name, string module)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int limit = Limit(module);
            string candidate = name.Length > limit ? name.Substring(0, limit - 1).TrimEnd() + "…" : name;
            if (label == null || label.font == null) return candidate;
            // Restrict names to their own tag area, even when an enlarged font
            // or custom alignment would otherwise run over level/HP text.
            float available = label.rectTransform.rect.width;
            if (available <= 1f) return candidate;
            try
            {
                label.text = candidate;
                while (candidate.Length > 2 && label.preferredWidth > available)
                {
                    // The Text component's preferred width is calculated from its text.
                    // Keep the full source name untouched; only the displayed tag changes.
                    candidate = candidate.Substring(0, candidate.Length - (candidate.EndsWith("…") ? 2 : 1)).TrimEnd() + "…";
                    label.text = candidate;
                }
            }
            catch (Exception) { /* A font atlas can be unavailable during scene transition. */ }
            return candidate;
        }
    }

    /// <summary>
    /// Single persisted number-format option for Player, Target, Party and Raid.
    /// Older builds stored a separate value under ArcaneUI.ValueFormat.{module}.
    /// Migrate only once, enabling the global option if any old frame had it on.
    /// Once a global choice exists, legacy module values never override it.
    /// </summary>
    internal static class ArcaneUiValueFormatting
    {
        private const string LegacyPrefix = "ArcaneUI.ValueFormat.";
        private const string GlobalKey = "ArcaneUI.ValueFormat.Global";
        private static bool _loaded;
        private static bool _abbreviate;

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            if (PlayerPrefs.HasKey(GlobalKey))
            {
                _abbreviate = PlayerPrefs.GetInt(GlobalKey, 0) != 0;
                return;
            }

            bool foundLegacySetting = false;
            bool anyLegacyEnabled = false;
            string[] frameModules =
            {
                ArcaneUiModuleSettings.Player, ArcaneUiModuleSettings.Target,
                ArcaneUiModuleSettings.Party, ArcaneUiModuleSettings.Raid
            };
            foreach (string module in frameModules)
            {
                string oldKey = LegacyPrefix + module;
                if (!PlayerPrefs.HasKey(oldKey)) continue;
                foundLegacySetting = true;
                if (PlayerPrefs.GetInt(oldKey, 0) != 0) anyLegacyEnabled = true;
            }
            _abbreviate = anyLegacyEnabled;
            if (foundLegacySetting)
            {
                PlayerPrefs.SetInt(GlobalKey, anyLegacyEnabled ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        internal static bool Abbreviate()
        {
            Load();
            return _abbreviate;
        }

        internal static void SetAbbreviate(bool enabled)
        {
            Load();
            if (_abbreviate == enabled && PlayerPrefs.HasKey(GlobalKey)) return;
            _abbreviate = enabled;
            PlayerPrefs.SetInt(GlobalKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        internal static string Format(int value)
        {
            if (!Abbreviate()) return value.ToString();
            float v = value;
            if (Mathf.Abs(v) >= 1000000000f) return (v / 1000000000f).ToString("0.#") + "B";
            if (Mathf.Abs(v) >= 1000000f) return (v / 1000000f).ToString("0.#") + "M";
            if (Mathf.Abs(v) >= 1000f) return (v / 1000f).ToString("0.#") + "K";
            return value.ToString();
        }
    }

    internal static class ArcaneUiBarDirection
    {
        private const string Key = "ArcaneUI.BarDirection.";
        private static string HealthKey(string module) { return Key + module + ".Health"; }
        private static string ManaKey(string module) { return Key + module + ".Mana"; }
        private static int Read(string key, string module)
        {
            // Old installs used a combined bar direction. Preserve that choice
            // for both health and mana until either is changed individually.
            return Mathf.Clamp(PlayerPrefs.GetInt(key,
                PlayerPrefs.GetInt(Key + module, 0)), 0, 3);
        }
        internal static int Get(string module) { return Health(module); } // legacy readers
        internal static int Health(string module) { return Read(HealthKey(module), module); }
        internal static int Mana(string module) { return Read(ManaKey(module), module); }
        private static string Label(int value)
        {
            switch (value)
            {
                case 1: return "Right to Left";
                case 2: return "Bottom to Top";
                case 3: return "Top to Bottom";
                default: return "Left to Right";
            }
        }
        internal static string Description(string module) { return Label(Health(module)); }
        internal static string HealthDescription(string module) { return Label(Health(module)); }
        internal static string ManaDescription(string module) { return Label(Mana(module)); }
        internal static void Cycle(string module) { CycleHealth(module); }
        internal static void CycleHealth(string module)
        {
            PlayerPrefs.SetInt(HealthKey(module), (Health(module) + 1) % 4);
            PlayerPrefs.Save();
        }
        internal static void CycleMana(string module)
        {
            PlayerPrefs.SetInt(ManaKey(module), (Mana(module) + 1) % 4);
            PlayerPrefs.Save();
        }
    }


    /// <summary>
    /// Positions a directional segment inside an inset frame bar, in local
    /// pixels. Normalized start/end apply to the bar bounds, not the card.
    /// Used by both party/raid health and their FOTF absorb overlays.
    /// </summary>
    internal static class ArcaneUiBarFillGeometry
    {
        internal static void Apply(RectTransform rect, Rect bounds, int direction,
            float start, float end)
        {
            if (rect == null) return;
            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);
            if (end < start) { float tmp = start; start = end; end = tmp; }
            float xMin = bounds.xMin;
            float xMax = bounds.xMax;
            float yMin = bounds.yMin;
            float yMax = bounds.yMax;
            if (direction == 1) // Right to left
            {
                xMin = bounds.xMax - bounds.width * end;
                xMax = bounds.xMax - bounds.width * start;
            }
            else if (direction == 2) // Bottom to top
            {
                yMin = bounds.yMin + bounds.height * start;
                yMax = bounds.yMin + bounds.height * end;
            }
            else if (direction == 3) // Top to bottom
            {
                yMin = bounds.yMax - bounds.height * end;
                yMax = bounds.yMax - bounds.height * start;
            }
            else // Left to right
            {
                xMin = bounds.xMin + bounds.width * start;
                xMax = bounds.xMin + bounds.width * end;
            }
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.offsetMin = new Vector2(xMin, yMin);
            rect.offsetMax = new Vector2(xMax, yMax);
        }
    }
}
