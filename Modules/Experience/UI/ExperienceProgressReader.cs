using System;
using System.Collections.Generic;
using System.Reflection;

namespace ArcanesGroupFrames.Modules.Experience.UI
{
    /// <summary>Read-only native XP adapter. Unknown fields are never fabricated.</summary>
    internal static class ExperienceProgressReader
    {
        internal struct Progress
        {
            internal bool Ascension, Known;
            internal int Level;
            internal double Current, Needed;
            internal float Fraction;
            internal string Source;
        }
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;
        private static readonly string[] AscCurrent = { "AscensionXP", "AscensionExp", "AscensionExperience", "CurrentAscensionXP", "CurrentAscensionExp", "AscXP", "AscExp", "AscensionXp" };
        private static readonly string[] AscNeeded = { "AscensionXPRequired", "AscensionXPNeeded", "AscensionXPToNext", "AscensionRequiredXP", "AscensionXPGoal" };
        private static readonly string[] NormalCurrent = { "CurrentXP", "CurrentExp", "CurrentExperience", "XP", "Exp", "Experience", "PlayerXP", "PlayerExp" };
        private static readonly string[] NormalNeeded = { "ExperienceToLevelUp", "XPToNextLevel", "XPNeeded", "ExpNeeded", "ExperienceToLevel", "XPRequired", "NextLevelXP", "RequiredXP", "NeededXP" };
        private static readonly string[] ProgressNames = { "XPPercent", "ExperiencePercent", "ExpPercent", "XPProgress", "ExperienceProgress" };
        private static readonly string[] LevelCaps = { "MaxLevel", "LevelCap", "MaximumLevel", "MaxPlayerLevel" };

        internal static Progress Read(Stats stats)
        {
            Progress result = new Progress();
            if (stats == null) return result;
            result.Level = stats.Level;
            double capValue;
            int cap = TryNumber(stats, LevelCaps, out capValue) && capValue >= 20 && capValue <= 200 ? (int)capValue : 35;
            result.Ascension = stats.Level >= cap;
            double current, needed, v;
            object secondary = GameData.PlayerControl;
            if (result.Ascension)
            {
                // Baseline 38,000 XP is the documented native ascension point cost.
                // Prefer actual game-provided threshold when available.
                result.Needed = 38000d;
                if ((TryNumber(stats, AscNeeded, out v) || TryNumber(secondary, AscNeeded, out v)) && v > 0) result.Needed = v;
                if (TryNumber(stats, AscCurrent, out current) || TryNumber(secondary, AscCurrent, out current))
                {
                    result.Current = current >= result.Needed ? current % result.Needed : current;
                    result.Known = true;
                }
                result.Source = result.Known ? "Ascension XP" : "Ascension XP field unavailable";
            }
            else
            {
                bool hasCurrent = TryNumber(stats, NormalCurrent, out current) || TryNumber(secondary, NormalCurrent, out current);
                bool hasNeeded = TryNumber(stats, NormalNeeded, out needed) || TryNumber(secondary, NormalNeeded, out needed);
                if (hasCurrent && hasNeeded && needed > 0)
                {
                    result.Current = current;
                    result.Needed = needed;
                    result.Known = true;
                    result.Source = "XP numeric fields";
                }
                else if (TryNumber(stats, ProgressNames, out v) || TryNumber(secondary, ProgressNames, out v))
                {
                    result.Known = true;
                    result.Fraction = UnityEngine.Mathf.Clamp01((float)(v > 1 ? v / 100d : v));
                    result.Source = "XP percentage field";
                }
                else result.Source = "Normal XP fields unavailable";
            }
            if (result.Known && result.Needed > 0)
                result.Fraction = UnityEngine.Mathf.Clamp01((float)(result.Current / result.Needed));
            return result;
        }

        private static bool TryNumber(object obj, string[] names, out double number)
        {
            number = 0;
            if (obj == null) return false;
            Type type = obj.GetType();
            foreach (string name in names)
            {
                try
                {
                    FieldInfo field = type.GetField(name, Flags);
                    if (field != null && ConvertNumber(field.GetValue(obj), out number)) return true;
                    PropertyInfo prop = type.GetProperty(name, Flags);
                    if (prop != null && prop.GetIndexParameters().Length == 0 && ConvertNumber(prop.GetValue(obj, null), out number)) return true;
                }
                catch { }
            }
            return false;
        }
        private static bool ConvertNumber(object obj, out double number)
        {
            number = 0;
            if (obj == null || obj is bool) return false;
            try
            {
                if (obj is int || obj is float || obj is double || obj is long || obj is uint || obj is short || obj is byte)
                {
                    number = Convert.ToDouble(obj);
                    return !double.IsInfinity(number) && !double.IsNaN(number) && number >= 0;
                }
            }
            catch { }
            return false;
        }
        internal static string Diagnostics(Stats player)
        {
            if (player == null) return "No PlayerStats";
            var entries = new List<string>();
            foreach (FieldInfo field in player.GetType().GetFields(Flags))
            {
                string s = field.Name.ToLowerInvariant();
                if (!(s.Contains("xp") || s.Contains("exp") || s.Contains("ascen") || s.Contains("level"))) continue;
                try { entries.Add(field.Name + "=" + field.GetValue(player)); } catch { }
                if (entries.Count >= 48) break;
            }
            return entries.Count == 0 ? "No relevant Stats fields found" : string.Join("; ",entries.ToArray());
        }
    }
}
