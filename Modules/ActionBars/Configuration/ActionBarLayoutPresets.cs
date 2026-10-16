using System;
using System.Collections.Generic;
using UnityEngine;
using ArcanesGroupFrames.Modules.ActionBars.Native;

namespace ArcanesGroupFrames.Modules.ActionBars.Configuration
{
    /// <summary>
    /// Explicit character-scoped action-bar layout snapshots. The live bar PLAN remains
    /// the existing global setting until the player loads a preset; captured native
    /// primary hotkey contents and native keymaps are never written or restored.
    /// </summary>
    internal static class ActionBarLayoutPresets
    {
        private const string Prefix = "ArcaneUI.ActionBars.Layouts.v1.";
        internal const int Slots = 5;

        [Serializable]
        internal sealed class SlotRecord
        {
            public string Binding;
            public int NativeSource;
            public string Kind, Identity, Name;
        }

        [Serializable]
        internal sealed class BarRecord
        {
            public int Slots, Columns, X, Y, Fade, HiddenOpacity, Glow;
            public bool InheritStyle;
            public string Visibility, Preset, Border, Accent;
            public SlotRecord[] Entries;
        }

        [Serializable]
        internal sealed class StyleRecord
        {
            public string Preset, Border, Accent;
            public int IconSize, Spacing, BorderWidth, CornerSize, Glow, BackgroundOpacity;
            public bool ShowKeys, ShowCooldowns, ShowTooltips;
        }

        [Serializable]
        internal sealed class LayoutRecord
        {
            public int Version = 1;
            public string Owner;
            public BarRecord[] Bars;
            public StyleRecord Style;
        }

        private static string CurrentOwner
        {
            get
            {
                Stats player = GameData.PlayerStats;
                return player == null || string.IsNullOrEmpty(player.MyName) ? null : player.MyName.Trim();
            }
        }
        private static string Key(int preset)
        {
            string owner = CurrentOwner;
            return owner == null || preset < 1 || preset > Slots ? null :
                Prefix + Uri.EscapeDataString(owner) + "." + preset;
        }
        internal static bool HasSaved(int preset)
        {
            string key = Key(preset);
            return key != null && !string.IsNullOrEmpty(PlayerPrefs.GetString(key, ""));
        }
        internal static string Description(int preset)
        {
            string key = Key(preset);
            if (key == null) return "Log in to manage character layouts.";
            string stored = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(stored)) return "Empty layout slot";
            try
            {
                LayoutRecord layout = UnityEngine.JsonUtility.FromJson<LayoutRecord>(stored);
                if (layout != null && layout.Bars != null && layout.Owner == CurrentOwner)
                    return layout.Bars.Length + " bar(s) saved for " + layout.Owner;
            }
            catch (Exception) { }
            return "Invalid saved layout";
        }
        internal static bool Save(int preset, out string message)
        {
            string key = Key(preset);
            message = "You must be logged in to save a layout.";
            if (key == null) return false;
            var layout = new LayoutRecord { Owner = CurrentOwner, Bars = new BarRecord[ActionBarPlanSettings.Count],
                Style = new StyleRecord {
                    Preset = ActionBarStyleSettings.Preset, Border = ActionBarStyleSettings.Border,
                    Accent = ActionBarStyleSettings.AccentHex,
                    IconSize = ActionBarStyleSettings.IconSize, Spacing = ActionBarStyleSettings.Spacing,
                    BorderWidth = ActionBarStyleSettings.BorderWidth, CornerSize = ActionBarStyleSettings.CornerSize,
                    Glow = ActionBarStyleSettings.Glow, BackgroundOpacity = ActionBarStyleSettings.BackgroundOpacity,
                    ShowKeys = ActionBarStyleSettings.ShowKeyLabels,
                    ShowCooldowns = ActionBarStyleSettings.ShowCooldownNumbers,
                    ShowTooltips = ActionBarStyleSettings.ShowTooltips }
            };
            for (int bar = 0; bar < layout.Bars.Length; bar++)
            {
                var record = new BarRecord {
                    Slots = ActionBarPlanSettings.SlotCount(bar),
                    Columns = ActionBarPlanSettings.Columns(bar),
                    X = ActionBarPlanSettings.BarX(bar), Y = ActionBarPlanSettings.BarY(bar),
                    Fade = ActionBarPlanSettings.FadeMilliseconds(bar),
                    HiddenOpacity = ActionBarPlanSettings.HiddenOpacity(bar),
                    Glow = ActionBarPlanSettings.Glow(bar),
                    InheritStyle = ActionBarPlanSettings.InheritStyle(bar),
                    Visibility = ActionBarPlanSettings.Visibility(bar),
                    Preset = ActionBarPlanSettings.Preset(bar),
                    Border = ActionBarPlanSettings.Border(bar),
                    Accent = ActionBarPlanSettings.AccentHex(bar),
                    Entries = new SlotRecord[ActionBarPlanSettings.SlotCount(bar)]
                };
                for (int slot = 1; slot <= record.Entries.Length; ++slot)
                {
                    var assignment = ActionBarIndependentAssignments.Get(bar, slot);
                    record.Entries[slot - 1] = new SlotRecord {
                        Binding = ActionBarPlanSettings.Binding(bar, slot),
                        NativeSource = bar == 0 ? 0 : ActionBarPlanSettings.SourceSlot(bar, slot),
                        Kind = assignment == null ? "" : assignment.Kind,
                        Identity = assignment == null ? "" : assignment.Identity,
                        Name = assignment == null ? "" : assignment.Name
                    };
                }
                layout.Bars[bar] = record;
            }
            try
            {
                PlayerPrefs.SetString(key, UnityEngine.JsonUtility.ToJson(layout));
                PlayerPrefs.Save();
                message = "Saved layout " + preset + " for " + CurrentOwner + ". Native keybinds unchanged.";
                return true;
            }
            catch (Exception exception)
            {
                message = "Could not save layout: " + exception.Message;
                return false;
            }
        }

        internal static bool Load(int preset, out string message)
        {
            string key = Key(preset);
            message = "You must be logged in to load a layout.";
            if (key == null) return false;
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) { message = "This layout slot is empty."; return false; }
            LayoutRecord layout;
            try { layout = UnityEngine.JsonUtility.FromJson<LayoutRecord>(json); }
            catch (Exception exception) { message = "Saved layout JSON is invalid: " + exception.Message; return false; }
            if (layout == null || layout.Version != 1 || layout.Owner != CurrentOwner ||
                layout.Bars == null || layout.Bars.Length < 1 || layout.Bars.Length > ActionBarPlanSettings.MaxBars)
            { message = "Invalid layout or wrong character. No changes made."; return false; }
            // Validate everything BEFORE touching preferences. Never load a conflicting
            // custom binding, or one that might double-fire a native game hotkey.
            var bindings = new HashSet<string>(StringComparer.Ordinal);
            for (int bar = 0; bar < layout.Bars.Length; bar++)
            {
                BarRecord data = layout.Bars[bar];
                if (data == null || data.Slots < 1 || data.Slots > (bar == 0 ? 10 : ActionBarPlanSettings.MaxSlots) ||
                    data.Columns < 1 || data.Columns > data.Slots || data.Entries == null ||
                    data.Entries.Length != data.Slots)
                { message = "Invalid bar geometry in saved layout. No changes made."; return false; }
                for (int slot = 1; slot <= data.Slots; slot++)
                {
                    SlotRecord entry = data.Entries[slot - 1];
                    if (entry == null || entry.NativeSource < 0 || entry.NativeSource > 10 ||
                        (bar > 0 && !string.IsNullOrEmpty(entry.Identity) &&
                         (entry.Kind != "Spell" && entry.Kind != "Skill" && entry.Kind != "Item")))
                    { message = "Invalid action assignment. No changes made."; return false; }
                    string binding = entry.Binding ?? "";
                    if (string.IsNullOrEmpty(binding)) continue;
                    KeyCode keyCode;
                    if (!ActionBarPlanSettings.TryParseShortcut(binding, out keyCode) || !bindings.Add(binding))
                    { message = "Invalid or duplicate custom keybind: " + binding; return false; }
                    string native = NativeActionBarDiscovery.NativeBindingConflict(binding, bar, slot);
                    if (!string.IsNullOrEmpty(native))
                    { message = "Cannot load binding " + binding + ": " + native; return false; }
                }
            }
            ActionBarStyleSettings.ApplySavedStyle(layout.Style);
            ActionBarPlanSettings.ApplySavedLayout(layout);
            ActionBarIndependentAssignments.RestoreCurrentOwner(layout.Bars);
            PlayerPrefs.Save();
            message = "Loaded layout " + preset + " for " + CurrentOwner +
                ". Original Erenshor hotkeys and bindings were not modified.";
            return true;
        }
    }
}
