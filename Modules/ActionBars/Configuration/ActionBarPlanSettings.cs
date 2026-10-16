using System;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.ActionBars.Configuration
{
    /// <summary>
    /// Persisted action-bar plans. Phase 7.2 activates native-backed links;
    /// Phase 7.3 adds a separate character-scoped independent assignment layer.
    /// This class continues to own layout, visibility and custom keybinds.
    /// </summary>
    internal static class ActionBarPlanSettings
    {
        private const string Prefix = "ArcaneUI.ActionBars.Plan.";
        internal const int MaxBars = 8;
        internal const int MaxSlots = 24;
        internal const int MinSlots = 1;
        private static readonly string[] Presets = { "Arcane Dark", "Gilded Reliquary", "Runebound", "Emberforged", "Voidglass", "Minimal", "Custom" };
        private static readonly string[] Borders = { "Ornate", "Runic", "Bevelled", "Glowing", "Thin", "None" };
        private static readonly string[] VisibilityModes =
        {
            "Always Visible", "Mouseover Only", "Combat Only", "Combat + Mouseover", "Hidden"
        };

        // These are persisted design choices only until native slot execution and
        // custom actionbar rendering are implemented. In particular, a Hidden
        // design does NOT disable any native action or registered shortcut.
        internal static string Visibility(int bar)
        {
            return GetOption(bar, "Visibility", VisibilityModes, "Always Visible");
        }
        internal static void NextVisibility(int bar)
        {
            NextOptionRaw(bar, "Visibility", VisibilityModes, "Always Visible");
        }
        internal static int FadeMilliseconds(int bar)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(Key(bar, "FadeMilliseconds"), 160), 0, 600);
        }
        internal static void SetFadeMilliseconds(int bar, float milliseconds)
        {
            if (!ValidBar(bar)) return;
            PlayerPrefs.SetInt(Key(bar, "FadeMilliseconds"), Mathf.Clamp(Mathf.RoundToInt(milliseconds), 0, 600));
            PlayerPrefs.Save();
        }
        internal static int HiddenOpacity(int bar)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(Key(bar, "HiddenOpacity"), 0), 0, 30);
        }
        internal static void SetHiddenOpacity(int bar, float opacity)
        {
            if (!ValidBar(bar)) return;
            PlayerPrefs.SetInt(Key(bar, "HiddenOpacity"), Mathf.Clamp(Mathf.RoundToInt(opacity), 0, 30));
            PlayerPrefs.Save();
        }
        internal static bool IsVisibleInSituation(int bar, bool inCombat, bool hovered)
        {
            switch (Visibility(bar))
            {
                case "Mouseover Only": return hovered;
                case "Combat Only": return inCombat;
                case "Combat + Mouseover": return inCombat && hovered;
                case "Hidden": return false;
                default: return true;
            }
        }

        internal static int Count { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Count", 1), 1, MaxBars); } }
        internal static int Selected
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Selected", 0), 0, Count - 1); }
            set { PlayerPrefs.SetInt(Prefix + "Selected", Mathf.Clamp(value, 0, Count - 1)); PlayerPrefs.Save(); }
        }
        internal static int SelectedSlot
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "SelectedSlot", 1), 1, SlotCount(Selected)); }
            set { PlayerPrefs.SetInt(Prefix + "SelectedSlot", Mathf.Clamp(value, 1, SlotCount(Selected))); PlayerPrefs.Save(); }
        }
        internal static string Name(int bar) { return bar == 0 ? "Primary Bar" : "Additional Bar " + (bar + 1); }
        // Extra bars link to an existing native slot. The native Hotkeys component
        // remains the authority for casting, targeting, cooldowns and item usage.
        // Unlinked custom slots NEVER attempt to call an unverified spell API.
        internal static int SourceSlot(int bar, int slot)
        {
            if (bar == 0) return slot >= 1 && slot <= 10 ? slot : 0;
            if (!ValidBar(bar) || slot < 1 || slot > SlotCount(bar)) return 0;
            return Mathf.Clamp(PlayerPrefs.GetInt(Key(bar, "Source." + slot), 0), 0, 10);
        }
        internal static void SetSourceSlot(int bar, int slot, int nativeSlot)
        {
            if (bar <= 0 || !ValidBar(bar) || slot < 1 || slot > SlotCount(bar)) return;
            PlayerPrefs.SetInt(Key(bar, "Source." + slot), Mathf.Clamp(nativeSlot, 0, 10));
            PlayerPrefs.Save();
        }
        internal static void NextSourceSlot(int bar, int slot)
        {
            if (bar <= 0 || !ValidBar(bar) || slot < 1 || slot > SlotCount(bar)) return;
            PlayerPrefs.SetInt(Key(bar, "Source." + slot), (SourceSlot(bar, slot) + 1) % 11);
            PlayerPrefs.Save();
        }
        internal static int BarX(int bar)
        {
            return bar == 0 ? ActionBarMirrorSettings.X :
                Mathf.Clamp(PlayerPrefs.GetInt(Key(bar, "X"), 0), -800, 800);
        }
        internal static int BarY(int bar)
        {
            return bar == 0 ? ActionBarMirrorSettings.Y :
                Mathf.Clamp(PlayerPrefs.GetInt(Key(bar, "Y"), 245 + (bar - 1) * 65), 0, 900);
        }
        internal static void SetBarPosition(int bar, Vector2 position)
        {
            if (!ValidBar(bar)) return;
            if (bar == 0)
            {
                ActionBarMirrorSettings.SetPosition(position.x, position.y);
                return;
            }
            PlayerPrefs.SetInt(Key(bar, "X"), Mathf.Clamp(Mathf.RoundToInt(position.x), -800, 800));
            PlayerPrefs.SetInt(Key(bar, "Y"), Mathf.Clamp(Mathf.RoundToInt(position.y), 0, 900));
            PlayerPrefs.Save();
        }
        internal static void SetBarX(int bar, float x)
        {
            if (!ValidBar(bar)) return;
            if (bar == 0) { ActionBarMirrorSettings.SetX(x); return; }
            PlayerPrefs.SetInt(Key(bar, "X"), Mathf.Clamp(Mathf.RoundToInt(x), -800, 800));
            PlayerPrefs.Save();
        }
        internal static void SetBarY(int bar, float y)
        {
            if (!ValidBar(bar)) return;
            if (bar == 0) { ActionBarMirrorSettings.SetY(y); return; }
            PlayerPrefs.SetInt(Key(bar, "Y"), Mathf.Clamp(Mathf.RoundToInt(y), 0, 900));
            PlayerPrefs.Save();
        }

        internal static int SlotCount(int bar)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(Key(bar, "Slots"), bar == 0 ? 10 : 8), MinSlots, bar == 0 ? 10 : MaxSlots);
        }
        internal static int Columns(int bar)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(Key(bar, "Columns"), bar == 0 ? 10 : 8), 1, SlotCount(bar));
        }
        internal static int Rows(int bar)
        {
            return Mathf.CeilToInt(SlotCount(bar) / (float)Columns(bar));
        }
        internal static void SetSlotCount(int bar, int count)
        {
            if (!ValidBar(bar)) return;
            int value = Mathf.Clamp(count, MinSlots, bar == 0 ? 10 : MaxSlots);
            int oldCount = SlotCount(bar);
            int oldColumns = Columns(bar);
            for (int slot = value + 1; slot <= oldCount; slot++)
            {
                PlayerPrefs.DeleteKey(Key(bar, "Binding." + slot));
                PlayerPrefs.DeleteKey(Key(bar, "Source." + slot));
                ActionBarIndependentAssignments.RemoveStoredSlot(bar, slot);
            }
            PlayerPrefs.SetInt(Key(bar, "Slots"), value);
            if (oldColumns > value) PlayerPrefs.SetInt(Key(bar, "Columns"), value);
            if (SelectedSlot > value) PlayerPrefs.SetInt(Prefix + "SelectedSlot", value);
            PlayerPrefs.Save();
        }
        internal static void SetColumns(int bar, int columns)
        {
            if (!ValidBar(bar)) return;
            PlayerPrefs.SetInt(Key(bar, "Columns"), Mathf.Clamp(columns, 1, SlotCount(bar)));
            PlayerPrefs.Save();
        }
        internal static bool AddBar()
        {
            if (Count >= MaxBars) return false;
            int newBar = Count;
            DeleteStoredBar(newBar);
            PlayerPrefs.SetInt(Prefix + "Count", newBar + 1);
            PlayerPrefs.SetInt(Prefix + "Selected", newBar);
            PlayerPrefs.SetInt(Prefix + "SelectedSlot", 1);
            PlayerPrefs.Save();
            return true;
        }
        internal static bool RemoveBar(int bar)
        {
            // Never delete the original primary bar plan.
            if (bar < 1 || bar >= Count) return false;
            int oldCount = Count;
            for (int source = bar + 1; source < oldCount; source++) CopyBar(source, source - 1);
            DeleteStoredBar(oldCount - 1);
            PlayerPrefs.SetInt(Prefix + "Count", oldCount - 1);
            PlayerPrefs.SetInt(Prefix + "Selected", Mathf.Clamp(bar - 1, 0, oldCount - 2));
            PlayerPrefs.SetInt(Prefix + "SelectedSlot", 1);
            PlayerPrefs.Save();
            return true;
        }
        internal static void NextBar()
        {
            Selected = (Selected + 1) % Count;
            SelectedSlot = 1;
        }
        internal static void NextSlot()
        {
            SelectedSlot = SelectedSlot >= SlotCount(Selected) ? 1 : SelectedSlot + 1;
        }
        internal static bool InheritStyle(int bar)
        {
            return PlayerPrefs.GetInt(Key(bar, "InheritStyle"), 1) != 0;
        }
        internal static void SetInheritStyle(int bar, bool inherit)
        {
            if (!ValidBar(bar)) return;
            if (!inherit && InheritStyle(bar))
            {
                PlayerPrefs.SetString(Key(bar, "Preset"), ActionBarStyleSettings.Preset);
                PlayerPrefs.SetString(Key(bar, "Border"), ActionBarStyleSettings.Border);
                PlayerPrefs.SetString(Key(bar, "AccentHex"), ActionBarStyleSettings.AccentHex);
                PlayerPrefs.SetInt(Key(bar, "Glow"), ActionBarStyleSettings.Glow);
            }
            PlayerPrefs.SetInt(Key(bar, "InheritStyle"), inherit ? 1 : 0);
            PlayerPrefs.Save();
        }
        internal static string Preset(int bar)
        {
            return InheritStyle(bar) ? ActionBarStyleSettings.Preset : GetOption(bar, "Preset", Presets, ActionBarStyleSettings.Preset);
        }
        internal static string Border(int bar)
        {
            return InheritStyle(bar) ? ActionBarStyleSettings.Border : GetOption(bar, "Border", Borders, ActionBarStyleSettings.Border);
        }
        internal static void NextPreset(int bar) { NextOption(bar, "Preset", Presets, Preset(bar)); }
        internal static void NextBorder(int bar) { NextOption(bar, "Border", Borders, Border(bar)); }
        internal static string AccentHex(int bar)
        {
            if (InheritStyle(bar)) return ActionBarStyleSettings.AccentHex;
            string hex = PlayerPrefs.GetString(Key(bar, "AccentHex"), "");
            return string.IsNullOrEmpty(hex) ? PresetAccentHex(Preset(bar)) : hex;
        }
        internal static Color AccentColor(int bar)
        {
            Color parsed;
            return ColorUtility.TryParseHtmlString(AccentHex(bar), out parsed) ? parsed : Color.white;
        }
        internal static void SetAccentHex(int bar, string hex)
        {
            if (!ValidBar(bar) || InheritStyle(bar)) return;
            Color parsed;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out parsed))
            {
                PlayerPrefs.SetString(Key(bar, "AccentHex"), "#" + ColorUtility.ToHtmlStringRGB(parsed));
                PlayerPrefs.Save();
            }
        }
        internal static void ResetAccent(int bar)
        {
            if (!ValidBar(bar) || InheritStyle(bar)) return;
            PlayerPrefs.DeleteKey(Key(bar, "AccentHex"));
            PlayerPrefs.Save();
        }
        internal static int Glow(int bar)
        {
            return InheritStyle(bar) ? ActionBarStyleSettings.Glow :
                Mathf.Clamp(PlayerPrefs.GetInt(Key(bar, "Glow"), ActionBarStyleSettings.Glow), 0, 100);
        }
        internal static void SetGlow(int bar, float value)
        {
            if (!ValidBar(bar) || InheritStyle(bar)) return;
            PlayerPrefs.SetInt(Key(bar, "Glow"), Mathf.Clamp(Mathf.RoundToInt(value), 0, 100));
            PlayerPrefs.Save();
        }
        private static string PresetAccentHex(string preset)
        {
            switch (preset)
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

        internal static string Binding(int bar, int slot)
        {
            return ValidBar(bar) && slot >= 1 && slot <= SlotCount(bar) ?
                PlayerPrefs.GetString(Key(bar, "Binding." + slot), "") : "";
        }
        internal static void ClearBinding(int bar, int slot)
        {
            if (!ValidBar(bar) || slot < 1 || slot > SlotCount(bar)) return;
            PlayerPrefs.DeleteKey(Key(bar, "Binding." + slot));
            PlayerPrefs.Save();
        }
        internal static bool TrySetBinding(int bar, int slot, string shortcut, out string error)
        {
            error = null;
            if (!ValidBar(bar) || slot < 1 || slot > SlotCount(bar))
            {
                error = "Selected action-bar slot is invalid.";
                return false;
            }
            if (string.IsNullOrEmpty(shortcut))
            {
                ClearBinding(bar, slot);
                return true;
            }
            // Do not create any new Alt-modified ArcaneUI actions. Existing
            // stored Alt bindings are ignored at execution and preserved so
            // older player layouts can still be loaded without data loss.
            if (Array.Exists(shortcut.Split('+'), part => part == "Alt"))
            {
                error = "Alt is disabled as an in-game action modifier. Use Ctrl or Shift.";
                return false;
            }
            for (int b = 0; b < Count; b++)
                for (int s = 1; s <= SlotCount(b); s++)
                    if ((b != bar || s != slot) && string.Equals(Binding(b, s), shortcut, StringComparison.Ordinal))
                    {
                        error = shortcut + " is already planned for " + Name(b) + ", slot " + s + ". Clear that binding first.";
                        return false;
                    }
            string nativeConflict = ArcanesGroupFrames.Modules.ActionBars.Native.NativeActionBarDiscovery.NativeBindingConflict(shortcut, bar, slot);
            if (!string.IsNullOrEmpty(nativeConflict))
            {
                error = nativeConflict + " Choose a different key or rebind it in Erenshor first.";
                return false;
            }
            PlayerPrefs.SetString(Key(bar, "Binding." + slot), shortcut);
            PlayerPrefs.Save();
            return true;
        }
        // Confirmed replacement of another ArcaneUI-only shortcut. Native game
        // bindings are never cleared, replaced or modified.
        internal static bool TryGetCustomConflict(int bar, int slot, string shortcut,
            out int conflictBar, out int conflictSlot)
        {
            conflictBar = -1; conflictSlot = -1;
            if (string.IsNullOrEmpty(shortcut)) return false;
            for (int b = 0; b < Count; ++b)
                for (int s = 1; s <= SlotCount(b); ++s)
                    if ((b != bar || s != slot) &&
                        string.Equals(Binding(b, s), shortcut, StringComparison.Ordinal))
                    { conflictBar = b; conflictSlot = s; return true; }
            return false;
        }
        internal static bool TryReplaceCustomConflict(int bar, int slot, string shortcut,
            int oldBar, int oldSlot, out string error)
        {
            error = "The conflict has changed. Capture the shortcut again.";
            if (!ValidBar(bar) || slot < 1 || slot > SlotCount(bar)) return false;
            int foundBar, foundSlot;
            if (!TryGetCustomConflict(bar, slot, shortcut, out foundBar, out foundSlot) ||
                foundBar != oldBar || foundSlot != oldSlot) return false;
            string native = ArcanesGroupFrames.Modules.ActionBars.Native.NativeActionBarDiscovery.NativeBindingConflict(shortcut, bar, slot);
            if (!string.IsNullOrEmpty(native)) { error = native; return false; }
            PlayerPrefs.DeleteKey(Key(oldBar, "Binding." + oldSlot));
            PlayerPrefs.SetString(Key(bar, "Binding." + slot), shortcut);
            PlayerPrefs.Save(); error = null; return true;
        }
        internal static bool TryParseShortcut(string binding, out KeyCode key)
        {
            key = KeyCode.None;
            if (string.IsNullOrEmpty(binding)) return false;
            string[] parts = binding.Split('+');
            if (parts.Length < 1 || parts.Length > 4) return false;
            bool ctrl = false, alt = false, shift = false;
            for (int i = 0; i < parts.Length - 1; ++i)
            {
                if (parts[i] == "Ctrl" && !ctrl) ctrl = true;
                else if (parts[i] == "Alt" && !alt) alt = true;
                else if (parts[i] == "Shift" && !shift) shift = true;
                else return false;
            }
            string raw = parts[parts.Length - 1];
            if (raw.Length == 1 && raw[0] >= '0' && raw[0] <= '9') raw = "Alpha" + raw;
            if (raw.StartsWith("Num", StringComparison.Ordinal)) raw = "Keypad" + raw.Substring(3);
            if (!Enum.TryParse(raw, out key) || key == KeyCode.None || IsModifier(key) ||
                key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) return false;
            return string.Equals(binding, Describe(key, ctrl, alt, shift), StringComparison.Ordinal);
        }
        // Apply a validated preset directly; the usual resize/removal APIs would
        // erase ALL characters' identities and must not be used for per-character presets.
        internal static void ApplySavedLayout(ActionBarLayoutPresets.LayoutRecord layout)
        {
            for (int b = 0; b < MaxBars; ++b)
            {
                foreach (string field in new[] { "Slots", "Columns", "InheritStyle", "Preset", "Border",
                    "Visibility", "FadeMilliseconds", "HiddenOpacity", "AccentHex", "Glow", "X", "Y" })
                    PlayerPrefs.DeleteKey(Key(b, field));
                for (int slot = 1; slot <= MaxSlots; ++slot)
                {
                    PlayerPrefs.DeleteKey(Key(b, "Binding." + slot));
                    PlayerPrefs.DeleteKey(Key(b, "Source." + slot));
                }
            }
            for (int bar = 0; bar < layout.Bars.Length; ++bar)
            {
                var data = layout.Bars[bar];
                PlayerPrefs.SetInt(Key(bar, "Slots"), data.Slots);
                PlayerPrefs.SetInt(Key(bar, "Columns"), data.Columns);
                PlayerPrefs.SetInt(Key(bar, "X"), Mathf.Clamp(data.X, -800, 800));
                PlayerPrefs.SetInt(Key(bar, "Y"), Mathf.Clamp(data.Y, 0, 900));
                PlayerPrefs.SetInt(Key(bar, "FadeMilliseconds"), Mathf.Clamp(data.Fade, 0, 600));
                PlayerPrefs.SetInt(Key(bar, "HiddenOpacity"), Mathf.Clamp(data.HiddenOpacity, 0, 30));
                PlayerPrefs.SetInt(Key(bar, "Glow"), Mathf.Clamp(data.Glow, 0, 100));
                PlayerPrefs.SetInt(Key(bar, "InheritStyle"), data.InheritStyle ? 1 : 0);
                if (!string.IsNullOrEmpty(data.Visibility)) PlayerPrefs.SetString(Key(bar, "Visibility"), data.Visibility);
                if (!string.IsNullOrEmpty(data.Preset)) PlayerPrefs.SetString(Key(bar, "Preset"), data.Preset);
                if (!string.IsNullOrEmpty(data.Border)) PlayerPrefs.SetString(Key(bar, "Border"), data.Border);
                if (!string.IsNullOrEmpty(data.Accent)) PlayerPrefs.SetString(Key(bar, "AccentHex"), data.Accent);
                for (int slot = 1; slot <= data.Slots; ++slot)
                {
                    var action = data.Entries[slot - 1];
                    if (!string.IsNullOrEmpty(action.Binding))
                        PlayerPrefs.SetString(Key(bar, "Binding." + slot), action.Binding);
                    if (bar > 0 && action.NativeSource > 0 && string.IsNullOrEmpty(action.Identity))
                        PlayerPrefs.SetInt(Key(bar, "Source." + slot), action.NativeSource);
                }
            }
            // The primary bar's position uses a separate original mirror key.
            ActionBarMirrorSettings.SetPosition(layout.Bars[0].X, layout.Bars[0].Y);
            PlayerPrefs.SetInt(Prefix + "Count", layout.Bars.Length);
            PlayerPrefs.SetInt(Prefix + "Selected", 0);
            PlayerPrefs.SetInt(Prefix + "SelectedSlot", 1);
        }
        internal static string Describe(KeyCode key, bool ctrl, bool alt, bool shift)
        {
            string name = key.ToString();
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) name = ((int)key - (int)KeyCode.Alpha0).ToString();
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) name = "Num" + ((int)key - (int)KeyCode.Keypad0);
            if (key == KeyCode.Space) name = "Space";
            return (ctrl ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "") + name;
        }
        internal static bool IsModifier(KeyCode key)
        {
            return key == KeyCode.LeftShift || key == KeyCode.RightShift ||
                key == KeyCode.LeftControl || key == KeyCode.RightControl ||
                key == KeyCode.LeftAlt || key == KeyCode.RightAlt ||
                key == KeyCode.LeftCommand || key == KeyCode.RightCommand;
        }
        private static bool ValidBar(int bar) { return bar >= 0 && bar < Count; }
        private static string Key(int bar, string field) { return Prefix + "Bar." + bar + "." + field; }
        private static string GetOption(int bar, string field, string[] options, string fallback)
        {
            string value = PlayerPrefs.GetString(Key(bar, field), fallback);
            foreach (var candidate in options) if (value == candidate) return value;
            return fallback;
        }
        private static void NextOption(int bar, string field, string[] options, string fallback)
        {
            if (!ValidBar(bar) || InheritStyle(bar)) return;
            NextOptionRaw(bar, field, options, fallback);
        }
        private static void NextOptionRaw(int bar, string field, string[] options, string fallback)
        {
            if (!ValidBar(bar)) return;
            string current = GetOption(bar, field, options, fallback);
            for (int i = 0; i < options.Length; i++) if (options[i] == current)
            {
                PlayerPrefs.SetString(Key(bar, field), options[(i + 1) % options.Length]);
                PlayerPrefs.Save();
                return;
            }
        }
        private static void CopyBar(int source, int destination)
        {
            // Snapshot first, then overwrite destination, including empty bindings.
            int slots = SlotCount(source), columns = Columns(source);
            int inherit = InheritStyle(source) ? 1 : 0;
            string visibility = Visibility(source);
            int fadeMilliseconds = FadeMilliseconds(source);
            int hiddenOpacity = HiddenOpacity(source);
            int glow = Glow(source);
            string accentHex = PlayerPrefs.GetString(Key(source, "AccentHex"), "");
            string preset = PlayerPrefs.GetString(Key(source, "Preset"), ActionBarStyleSettings.Preset);
            string border = PlayerPrefs.GetString(Key(source, "Border"), ActionBarStyleSettings.Border);
            int x = BarX(source), y = BarY(source);
            string[] bindings = new string[MaxSlots];
            int[] sources = new int[MaxSlots];
            for (int i = 1; i <= MaxSlots; ++i) sources[i - 1] = SourceSlot(source, i);
            for (int i = 1; i <= MaxSlots; i++) bindings[i - 1] = PlayerPrefs.GetString(Key(source, "Binding." + i), "");
            DeleteStoredBar(destination);
            PlayerPrefs.SetInt(Key(destination, "Slots"), slots);
            PlayerPrefs.SetInt(Key(destination, "Columns"), columns);
            PlayerPrefs.SetInt(Key(destination, "InheritStyle"), inherit);
            PlayerPrefs.SetString(Key(destination, "Visibility"), visibility);
            PlayerPrefs.SetInt(Key(destination, "FadeMilliseconds"), fadeMilliseconds);
            PlayerPrefs.SetInt(Key(destination, "HiddenOpacity"), hiddenOpacity);
            PlayerPrefs.SetInt(Key(destination, "Glow"), glow);
            PlayerPrefs.SetInt(Key(destination, "X"), x);
            PlayerPrefs.SetInt(Key(destination, "Y"), y);
            if (!string.IsNullOrEmpty(accentHex)) PlayerPrefs.SetString(Key(destination, "AccentHex"), accentHex);
            PlayerPrefs.SetString(Key(destination, "Preset"), preset);
            PlayerPrefs.SetString(Key(destination, "Border"), border);
            for (int i = 1; i <= MaxSlots; i++) if (!string.IsNullOrEmpty(bindings[i - 1]))
                PlayerPrefs.SetString(Key(destination, "Binding." + i), bindings[i - 1]);
            for (int i = 1; i <= MaxSlots; i++) if (sources[i - 1] > 0)
                PlayerPrefs.SetInt(Key(destination, "Source." + i), sources[i - 1]);
            for (int i = 1; i <= MaxSlots; i++)
                ActionBarIndependentAssignments.CopyStoredSlot(source, destination, i);
        }
        private static void DeleteStoredBar(int bar)
        {
            foreach (string field in new [] { "Slots", "Columns", "InheritStyle", "Preset", "Border",
                "Visibility", "FadeMilliseconds", "HiddenOpacity", "AccentHex", "Glow", "X", "Y" })
                PlayerPrefs.DeleteKey(Key(bar, field));
            for (int i = 1; i <= MaxSlots; i++)
            {
                PlayerPrefs.DeleteKey(Key(bar, "Binding." + i));
                PlayerPrefs.DeleteKey(Key(bar, "Source." + i));
                ActionBarIndependentAssignments.RemoveStoredSlot(bar, i);
            }
        }
    }
}
