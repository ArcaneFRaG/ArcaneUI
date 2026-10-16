using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using ArcanesGroupFrames.Modules.ActionBars.Configuration;
using ArcanesGroupFrames.Modules.ActionBars.Native;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private RectTransform _actionPreviewHost;
        // Diagnostic preview situation, not a gameplay combat/mouse state watcher.
        private int _actionPreviewSituation = 0;

        // Phase 7.2: native-backed primary bar and linked extra bars.
        // Native discovery/diagnostics remain available on the Native tab.
        private void RenderActionBars()
        {
            if (_tab == "Native")
            {
                Section("NATIVE ACTION BAR INVESTIGATION",
                    "Diagnostic only: lists native slots and shortcuts. Gameplay actions are delegated to Hotkeys.DoHotkeyTask().");
                Info("Native bar state", NativeActionBarDiscovery.Status);
                if (NativeActionBarDiscovery.HasSlotSnapshot)
                {
                    Info("Confirmed native slots", NativeActionBarDiscovery.ActiveSlotCount +
                        " primary and " + NativeActionBarDiscovery.AlternateSlotCount +
                        " alternate objects. The latter are NOT additional simultaneous action bars.");
                    for (int offset = 1; offset <= 10; offset += 2)
                    {
                        NativeActionBarDiscovery.NativeSlotSnapshot first, second;
                        NativeActionBarDiscovery.TryGetActiveSlot(offset, out first);
                        NativeActionBarDiscovery.TryGetActiveSlot(offset + 1, out second);
                        Info("Native hotkeys " + offset + "–" + (offset + 1),
                            NativeActionBarDiscovery.DescribeSlot(first) + "     |     " +
                            NativeActionBarDiscovery.DescribeSlot(second));
                    }
                    Info("Preview source", "Action Bars → General now previews real native spell/skill icons " +
                        "for the primary bar when native hotkeys are available.");
                }
                ActionButton("Native Action Bar Scan",
                    "Scan actionbar UI paths, component names and likely slot/ability types. Results go to the mod log.",
                    "SCAN & LOG", () => { NativeActionBarDiscovery.ScanAndLog(); RenderPage(); });
                Info("How to test",
                    "Run SCAN & LOG with the native hotbars visible. Send the ArcaneUI ActionBar log entries. " +
                    "We will bind to actual native slot data before replacing any artwork.");
                Info("No gameplay changes",
                    "Custom clicks use the game's native Hotkeys action. The optional native-art hide does not disable native input.");
                return;
            }

            if (_tab == "Bars")
            {
                RenderActionBarPlans();
                return;
            }
            if (_tab == "Keybinds")
            {
                RenderActionBarKeybinds();
                return;
            }
            if (_tab == "Layouts")
            {
                RenderActionBarLayouts();
                return;
            }
            if (_tab == "General")
            {
                Section("ACTION BARS", "Enable the ArcaneUI bars and choose whether to hide Erenshor's original bar.");
                ToggleSetting("Enable ArcaneUI Action Bars", "Show the custom, native-backed action bars.",
                    () => ActionBarMirrorSettings.Enabled, ActionBarMirrorSettings.SetEnabled);
                ToggleSetting("Hide Original Erenshor Hotbar",
                    "Hide the original artwork without disabling native actions or shortcuts.",
                    () => ActionBarMirrorSettings.HideNativeBar, ActionBarMirrorSettings.SetHideNativeBar);
                ChoiceSetting("Action Bar Preset", "Appearance for bars that inherit the global style.",
                    ActionBarStyleSettings.Preset, ActionBarStyleSettings.NextPreset);
            }
            else if (_tab == "Themes")
            {
                ChoiceSetting("Action Bar Preset",
                    "Arcane Dark / Gilded Reliquary / Runebound / Emberforged / Voidglass / Minimal / Custom.",
                    ActionBarStyleSettings.Preset, ActionBarStyleSettings.NextPreset);
                HexSetting("Action Bar Accent Colour", "Customise frame trim, ornamental details and highlights.",
                    () => ActionBarStyleSettings.AccentHex, ActionBarStyleSettings.SetAccentHex);
                ActionButton("Reset Action Bar Accent", "Use the selected preset's original trim colour.",
                    "PRESET COLOUR", () => { ActionBarStyleSettings.ResetAccent(); RenderPage(); });
                SliderSetting("Action Bar Background Opacity", 0, 100,
                    () => ActionBarStyleSettings.BackgroundOpacity,
                    ActionBarStyleSettings.SetBackgroundOpacity, "%");
            }
            else if (_tab == "Borders")
            {
                ChoiceSetting("Action Bar Border Style",
                    "Ornate / Runic / Bevelled / Glowing / Thin / None.",
                    ActionBarStyleSettings.Border, ActionBarStyleSettings.NextBorder);
                SliderSetting("Action Bar Border Thickness", 1, 8,
                    () => ActionBarStyleSettings.BorderWidth,
                    ActionBarStyleSettings.SetBorderWidth, " px");
                SliderSetting("Action Bar Corner Size", 3, 16,
                    () => ActionBarStyleSettings.CornerSize,
                    ActionBarStyleSettings.SetCornerSize, " px");
                SliderSetting("Action Bar Glow Intensity", 0, 100,
                    () => ActionBarStyleSettings.Glow,
                    ActionBarStyleSettings.SetGlow, "%");
                Info("Per-bar overrides", "Bars → Selected Bar lets every bar use a distinct border, accent colour and glow.");
            }
            else if (_tab == "Icons & Text")
            {
                SliderSetting("Action Bar Icon Size", 24, 72,
                    () => ActionBarStyleSettings.IconSize,
                    ActionBarStyleSettings.SetIconSize, " px");
                SliderSetting("Action Bar Icon Spacing", 0, 18,
                    () => ActionBarStyleSettings.Spacing,
                    ActionBarStyleSettings.SetSpacing, " px");
                ToggleSetting("Show Action Bar Hotkeys", "Display key labels in the bottom-right of preview buttons.",
                    () => ActionBarStyleSettings.ShowKeyLabels,
                    ActionBarStyleSettings.SetKeyLabels);
                ToggleSetting("Show Action Bar Cooldown Numbers", "Preview a remaining cooldown label.",
                    () => ActionBarStyleSettings.ShowCooldownNumbers,
                    ActionBarStyleSettings.SetCooldownNumbers);
                ToggleSetting("Show Action Tooltips", "Show the assigned action when hovering an occupied slot.",
                    () => ActionBarStyleSettings.ShowTooltips,
                    ActionBarStyleSettings.SetShowTooltips);
            }
            if (_tab != "General")
                ActionButton("Reset Action Bar Style",
                    "Restore the appearance design defaults. Does not change native game slots or bindings.",
                    "RESET STYLE", () => { ActionBarStyleSettings.ResetStyle(); RenderPage(); });
        }

        private void RenderActionBarPlans()
        {
            int bar = ActionBarPlanSettings.Selected;
            Section("ACTION BAR LAYOUT", "Choose a bar to edit its size, position, appearance and visibility.");
            string[] barNames = new string[ActionBarPlanSettings.Count];
            for (int i = 0; i < barNames.Length; i++) barNames[i] = ActionBarPlanSettings.Name(i);
            DirectChoiceSetting("Select Action Bar",
                "Choose the bar to configure; this doesn't change any slot assignments.",
                ActionBarPlanSettings.Name(bar), barNames, selected =>
                {
                    for (int i = 0; i < barNames.Length; i++)
                        if (selected == barNames[i])
                        {
                            ActionBarPlanSettings.Selected = i;
                            ActionBarPlanSettings.SelectedSlot = 1;
                            break;
                        }
                });
            if (ActionBarPlanSettings.Count < ActionBarPlanSettings.MaxBars)
                ActionButton("Create Additional Action Bar",
                    "Create a new bar with its own native Hotkeys slots. Assign from the game spellbook, skills or inventory.",
                    "ADD BAR", () => { ActionBarPlanSettings.AddBar(); RenderPage(); });
            if (bar > 0)
                ActionButton("Remove Selected Action Bar",
                    "Delete this extra bar plan and its saved keybinds; the primary bar cannot be removed.",
                    "REMOVE", () => { ActionBarPlanSettings.RemoveBar(bar); RenderPage(); });
            Section("SELECTED BAR LAYOUT", "Changes to slots and columns rebuild enabled ArcaneUI bars. Click a slot in the right-hand preview to edit its binding.");
            SliderSetting("Action Bar Slots", 1, bar == 0 ? 10 : ActionBarPlanSettings.MaxSlots,
                () => ActionBarPlanSettings.SlotCount(bar),
                value => ActionBarPlanSettings.SetSlotCount(bar, Mathf.RoundToInt(value)), " slots");
            SliderSetting("Action Bar Columns", 1, ActionBarPlanSettings.SlotCount(bar),
                () => ActionBarPlanSettings.Columns(bar),
                value => ActionBarPlanSettings.SetColumns(bar, Mathf.RoundToInt(value)), " cols");
            SliderSetting("Selected Bar X", -800, 800,
                () => ActionBarPlanSettings.BarX(bar), value => ActionBarPlanSettings.SetBarX(bar, value), " px");
            SliderSetting("Selected Bar Y", 0, 900,
                () => ActionBarPlanSettings.BarY(bar), value => ActionBarPlanSettings.SetBarY(bar, value), " px");
            Section("PER-BAR STYLING", "Use one global look, or override the theme and border for this bar.");
            ToggleSetting("Inherit Action Bar Style", "ON uses global Action Bar style; OFF allows independent preset and border.",
                () => ActionBarPlanSettings.InheritStyle(bar),
                value => ActionBarPlanSettings.SetInheritStyle(bar, value));
            if (!ActionBarPlanSettings.InheritStyle(bar))
            {
                ChoiceSetting("Selected Bar Theme", "Independent fantasy theme for this bar.",
                    ActionBarPlanSettings.Preset(bar), () => ActionBarPlanSettings.NextPreset(bar));
                ChoiceSetting("Selected Bar Border", "Independent slot ornament/border style.",
                    ActionBarPlanSettings.Border(bar), () => ActionBarPlanSettings.NextBorder(bar));
                HexSetting("Selected Bar Accent Colour", "Override the trim and ornament colour for this bar.",
                    () => ActionBarPlanSettings.AccentHex(bar), hex => ActionBarPlanSettings.SetAccentHex(bar, hex));
                ActionButton("Reset Selected Bar Accent", "Return to this bar's preset accent.",
                    "RESET COLOUR", () => { ActionBarPlanSettings.ResetAccent(bar); RenderPage(); });
                SliderSetting("Selected Bar Glow", 0, 100,
                    () => ActionBarPlanSettings.Glow(bar),
                    value => ActionBarPlanSettings.SetGlow(bar, value), "%");
            }
            Section("PER-BAR VISIBILITY", "Every ArcaneUI bar can fade, show only on mouseover or combat, or be hidden.");
            ChoiceSetting("Selected Bar Visibility", "Always Visible / Mouseover Only / Combat Only / Combat + Mouseover / Hidden.",
                ActionBarPlanSettings.Visibility(bar), () => ActionBarPlanSettings.NextVisibility(bar));
            SliderSetting("Selected Bar Fade Duration", 0, 600,
                () => ActionBarPlanSettings.FadeMilliseconds(bar),
                value => ActionBarPlanSettings.SetFadeMilliseconds(bar, value), " ms");
            SliderSetting("Selected Bar Hidden Opacity", 0, 30,
                () => ActionBarPlanSettings.HiddenOpacity(bar),
                value => ActionBarPlanSettings.SetHiddenOpacity(bar, value), "%");
            // Preview remains available, but developer combat-state simulation
            // no longer appears as a user-facing action-bar setting.

        }

        private void RenderActionBarKeybinds()
        {
            int bar = ActionBarPlanSettings.Selected;
            int slot = ActionBarPlanSettings.SelectedSlot;
            Section("SLOT KEYBINDS", "Assign actions by dragging onto the bar. Right-click a slot to clear it.");
            string[] barNames = new string[ActionBarPlanSettings.Count];
            for (int i = 0; i < barNames.Length; i++) barNames[i] = ActionBarPlanSettings.Name(i);
            DirectChoiceSetting("Select Keybind Bar", "Choose which bar owns the shortcut.",
                ActionBarPlanSettings.Name(bar), barNames, selected =>
                {
                    for (int i = 0; i < barNames.Length; i++)
                        if (selected == barNames[i]) { ActionBarPlanSettings.Selected = i;
                            ActionBarPlanSettings.SelectedSlot = 1; break; }
                    _actionBindingFeedback = "";
                });
            string[] slotNames = new string[ActionBarPlanSettings.SlotCount(bar)];
            for (int i = 0; i < slotNames.Length; i++) slotNames[i] = "Slot " + (i + 1);
            DirectChoiceSetting("Select Action Slot", "Choose the exact slot to bind.",
                "Slot " + slot, slotNames, selected =>
                {
                    int index = Array.IndexOf(slotNames, selected);
                    if (index >= 0) ActionBarPlanSettings.SelectedSlot = index + 1;
                    _actionBindingFeedback = "";
                });
            if (bar > 0)
            {
                var action = ActionBarIndependentAssignments.Get(bar, slot);
                if (action != null)
                    Info("Assigned Action", action.Name + " (" + action.Kind + ")");
                else
                {
                    int source = ActionBarPlanSettings.SourceSlot(bar, slot);
                    Info("Assigned Action", source > 0 ? "Native HK" + source + " (linked)" : "Empty");
                }
            }
            else
                Info("Assigned Action", "Native hotkey " + slot);
            string current = ActionBarPlanSettings.Binding(bar, slot);
            ActionButton("Capture Slot Keybind",
                _capturingActionBinding ? "Press a key, optionally holding Ctrl/Shift. Alt is disabled. Esc cancels." :
                "ArcaneUI shortcut: " + (string.IsNullOrEmpty(current) ? "Unbound" : current),
                _capturingActionBinding ? "PRESS KEY" : "SET KEY", () =>
                {
                    _actionBindingBar = bar;
                    _actionBindingSlot = slot;
                    _actionBindingFeedback = "";
                    _pendingActionShortcut = "";
                    _capturingActionBinding = true;
                    RenderPage();
                });
            ActionButton("Clear Slot Keybind", "Remove only the ArcaneUI binding for this slot.", "CLEAR KEY", () =>
            {
                ActionBarPlanSettings.ClearBinding(bar, slot);
                _pendingActionShortcut = "";
                _capturingActionBinding = false;
                _actionBindingFeedback = "ArcaneUI shortcut cleared. Native binding unchanged.";
                RenderPage();
            });
            if (!string.IsNullOrEmpty(_actionBindingFeedback)) Info("Keybind result", _actionBindingFeedback);
            if (!string.IsNullOrEmpty(_pendingActionShortcut) &&
                _actionBindingBar == bar && _actionBindingSlot == slot)
            {
                string waiting = _pendingActionShortcut;
                int fromBar = _pendingConflictBar, fromSlot = _pendingConflictSlot;
                ActionButton("Replace Conflicting ArcaneUI Shortcut",
                    "Move " + waiting + " from " + ActionBarPlanSettings.Name(fromBar) +
                    " slot " + fromSlot + " to this slot. The old ArcaneUI shortcut is removed; native game shortcuts are untouched.",
                    "CONFIRM REBIND", () =>
                    {
                        string error;
                        if (ActionBarPlanSettings.TryReplaceCustomConflict(bar, slot, waiting,
                            fromBar, fromSlot, out error))
                            _actionBindingFeedback = "Moved " + waiting + " to " +
                                ActionBarPlanSettings.Name(bar) + " slot " + slot + ".";
                        else _actionBindingFeedback = error;
                        _pendingActionShortcut = "";
                        RenderPage();
                    });
                ActionButton("Cancel Conflicting Shortcut", "Keep both existing bindings unchanged.",
                    "CANCEL", () => { _pendingActionShortcut = ""; RenderPage(); });
            }
            if (bar > 0) RenderActionSlotManagement(bar, slot);
            Section("BINDINGS ON THIS BAR");
            var lines = new List<string>();
            for (int i = 1; i <= ActionBarPlanSettings.SlotCount(bar); i++)
            {
                string bind = ActionBarPlanSettings.Binding(bar, i);
                lines.Add(i + ": " + (string.IsNullOrEmpty(bind) ? "—" : bind));
                if (lines.Count == 6 || i == ActionBarPlanSettings.SlotCount(bar))
                {
                    Info("ArcaneUI Slot Keybinds " + (i - lines.Count + 1) + "–" + i,
                        string.Join("    |    ", lines.ToArray()));
                    lines.Clear();
                }
            }
            string nativeCollision = NativeActionBarDiscovery.NativeBindingConflict(current, bar, slot);
            if (!string.IsNullOrEmpty(nativeCollision))
                Info("Native key conflict", nativeCollision);
        }

        private void RenderActionSlotManagement(int bar, int slot)
        {
            Section("SLOT ASSIGNMENTS", "Drag live spells, skills or items onto extra bars. Drag an extra slot onto " +
                "another to swap; hold Ctrl while dragging to copy into an empty destination.");
            ActionButton("Clear Selected Extra Slot", "Clear only the selected extra Hotkeys slot. Original native HK1–HK10 stay unchanged.",
                "CLEAR SLOT", () =>
                {
                    NativeExtraHotkeySlots.Clear(bar, slot);
                    _actionBindingFeedback = "Extra slot cleared. Custom keybind left intact.";
                    RenderPage();
                });
            string[] primary = new string[10];
            for (int i = 0; i < primary.Length; i++) primary[i] = "HK" + (i + 1);
            DirectChoiceSetting("Native Hotkey to Copy", "Copies an actually assigned native spell/skill/item to this extra slot.",
                "HK" + _nativeSourceSlot, primary, choice =>
                { int index = Array.IndexOf(primary, choice); if (index >= 0) _nativeSourceSlot = index + 1; });
            ActionButton("Copy Native Assignment", "Capture only the action actually present in the chosen Erenshor hotkey.",
                "COPY FROM HK", () =>
                {
                    string result;
                    ActionBarIndependentAssignments.CaptureNative(bar, slot, _nativeSourceSlot, out result);
                    _actionBindingFeedback = result;
                    RenderPage();
                });
            if (ActionBarPlanSettings.Count < 2) return;
            _actionSourceBar = Mathf.Clamp(_actionSourceBar, 1, ActionBarPlanSettings.Count - 1);
            string[] sourceBars = new string[ActionBarPlanSettings.Count - 1];
            for (int i = 1; i < ActionBarPlanSettings.Count; i++) sourceBars[i - 1] = ActionBarPlanSettings.Name(i);
            DirectChoiceSetting("Action Source Bar", "Choose an independent extra-bar source for copying or swapping.",
                ActionBarPlanSettings.Name(_actionSourceBar), sourceBars, chosen =>
                {
                    int index = Array.IndexOf(sourceBars, chosen);
                    if (index >= 0) { _actionSourceBar = index + 1; _actionSourceSlot = 1; }
                });
            _actionSourceSlot = Mathf.Clamp(_actionSourceSlot, 1, ActionBarPlanSettings.SlotCount(_actionSourceBar));
            string[] slots = new string[ActionBarPlanSettings.SlotCount(_actionSourceBar)];
            for (int i = 0; i < slots.Length; i++) slots[i] = "Slot " + (i + 1);
            DirectChoiceSetting("Action Source Slot", "Select an occupied independent slot.",
                "Slot " + _actionSourceSlot, slots, chosen =>
                { int index = Array.IndexOf(slots, chosen); if (index >= 0) _actionSourceSlot = index + 1; });
            ActionButton("Copy Independent Assignment", "Copy into an EMPTY extra slot without changing the source.",
                "COPY", () =>
                {
                    string result;
                    ActionBarIndependentAssignments.Copy(_actionSourceBar, _actionSourceSlot, bar, slot, out result);
                    _actionBindingFeedback = result;
                    RenderPage();
                });
            ActionButton("Swap Independent Assignments", "Move or swap two independently assigned extra slots. Does not change either keybind.",
                "SWAP", () =>
                {
                    string result;
                    ActionBarIndependentAssignments.Move(_actionSourceBar, _actionSourceSlot, bar, slot, out result);
                    _actionBindingFeedback = result;
                    RenderPage();
                });
        }

        private void RenderActionBarLayouts()
        {
            Section("CHARACTER ACTION BAR LAYOUTS",
                "Five manually saved layouts per character. Store bar positions, columns, appearance, " +
                "ArcaneUI keybinds, and independent extra-slot spell/skill/item identities.");
            string[] presets = new string[ActionBarLayoutPresets.Slots];
            for (int i = 0; i < presets.Length; ++i) presets[i] = "Layout " + (i + 1);
            DirectChoiceSetting("Character Layout Slot", "Choose one of the five slots owned by the current character.",
                "Layout " + _actionLayoutSlot, presets, choice =>
                {
                    int index = Array.IndexOf(presets, choice);
                    if (index >= 0) _actionLayoutSlot = index + 1;
                    _confirmActionLayoutLoad = _confirmActionLayoutSave = false;
                    _actionLayoutFeedback = "";
                });
            Info("Current Layout Slot", ActionBarLayoutPresets.Description(_actionLayoutSlot));
            ActionButton("Save Action Bar Layout", "Save the CURRENT bars for this character. Overwrites only this named layout slot.",
                _confirmActionLayoutSave ? "CONFIRM SAVE" : "SAVE LAYOUT", () =>
                {
                    if (!_confirmActionLayoutSave) { _confirmActionLayoutSave = true; _confirmActionLayoutLoad = false; }
                    else
                    {
                        _confirmActionLayoutSave = false;
                        ActionBarLayoutPresets.Save(_actionLayoutSlot, out _actionLayoutFeedback);
                    }
                    RenderPage();
                });
            if (ActionBarLayoutPresets.HasSaved(_actionLayoutSlot))
                ActionButton("Load Action Bar Layout", "Restores selected ArcaneUI layout and extra-slot assignments for this character only. " +
                    "Native assignments and native keybindings are never changed.",
                    _confirmActionLayoutLoad ? "CONFIRM LOAD" : "LOAD LAYOUT", () =>
                    {
                        if (!_confirmActionLayoutLoad) { _confirmActionLayoutLoad = true; _confirmActionLayoutSave = false; }
                        else
                        {
                            _confirmActionLayoutLoad = false;
                            ActionBarLayoutPresets.Load(_actionLayoutSlot, out _actionLayoutFeedback);
                        }
                        RenderPage();
                    });
            if (_confirmActionLayoutLoad || _confirmActionLayoutSave)
                ActionButton("Cancel Layout Operation", "Keep the current live configuration and stored layout unchanged.",
                    "CANCEL", () => { _confirmActionLayoutLoad = _confirmActionLayoutSave = false; RenderPage(); });
            if (!string.IsNullOrEmpty(_actionLayoutFeedback)) Info("Layout result", _actionLayoutFeedback);
            Section("SAFETY AND SCOPE");
            Info("Character isolation", "Saved layouts are keyed by character name. Loading changes the currently active ArcaneUI bar arrangement. " +
                "Only the current character's independent slot assignments are restored.");
            Info("Erenshor-native actions", "The original ten native slots and Erenshor's native shortcut settings are never modified by loading a layout.");
            Info("Keybind conflicts", "A load is rejected if a custom shortcut duplicates another in the layout or collides with a discovered native key.");
        }

        private void RenderActionBarPreview()
        {
            if (_previewHost == null) return;
            ArcaneUiControls.ClearChildren(_previewHost);
            // The same preview now lives only in the shared right panel.
            _actionPreviewHost = ArcaneUiControls.Rect(_previewHost, "ActionBarPreview", 34, 248, 747, 92);
            DrawActionPreview();
        }

        private string PreviewSituationName()
        {
            switch (_actionPreviewSituation)
            {
                case 1: return "Mouseover, out of combat";
                case 2: return "Combat, not mouseover";
                case 3: return "Combat + mouseover";
                default: return "Idle, out of combat";
            }
        }

        private void DrawActionPreview()
        {
            if (_actionPreviewHost == null || _page != "action") return;
            ArcaneUiControls.ClearChildren(_actionPreviewHost);
            int currentBar = ActionBarPlanSettings.Selected;
            string preset = ActionBarPlanSettings.Preset(currentBar);
            bool inCombat = _actionPreviewSituation >= 2;
            bool hovered = _actionPreviewSituation == 1 || _actionPreviewSituation == 3;
            bool visible = ActionBarPlanSettings.IsVisibleInSituation(currentBar, inCombat, hovered);
            float previewAlpha = visible ? 1f : ActionBarPlanSettings.HiddenOpacity(currentBar) / 100f;
            Color trim = ActionBarPlanSettings.AccentColor(currentBar);
            int size = ActionBarStyleSettings.IconSize;
            int space = ActionBarStyleSettings.Spacing;
            int count = Mathf.Min(ActionBarPlanSettings.SlotCount(currentBar),
                Mathf.Min(10, Mathf.Max(1, Mathf.FloorToInt(730f /
                    (ActionBarStyleSettings.IconSize + ActionBarStyleSettings.Spacing)))));
            bool nativePreview = currentBar == 0 && NativeActionBarDiscovery.HasSlotSnapshot;
            float start = 8f;
            float y = 10f + Mathf.Max(0, (72 - size) / 2f);
            string border = ActionBarPlanSettings.Border(currentBar);
            float bgAlpha = ActionBarStyleSettings.BackgroundOpacity / 100f;
            ArcaneUiControls.Panel(_actionPreviewHost, "BarSurface", 0, 3, 745, 82,
                new Color(.08f, .095f, .12f, bgAlpha * .65f), false);
            // A transparent content group simulates hidden/mouseover/combat states
            // without affecting the native bars or consuming any action keys.
            var previewSlots = ArcaneUiControls.Rect(_actionPreviewHost, "VisibilityPreviewSlots", 0, 0, 745, 82);
            var previewCanvasGroup = previewSlots.gameObject.AddComponent<CanvasGroup>();
            previewCanvasGroup.alpha = previewAlpha;
            previewCanvasGroup.blocksRaycasts = true;
            previewCanvasGroup.interactable = true;
            for (int i = 0; i < count; i++)
            {
                float x = start + i * (size + space);
                Color interior = i % 3 == 0 ? new Color(.24f, .15f, .14f, 1f) :
                    i % 3 == 1 ? new Color(.15f, .21f, .31f, 1f) : new Color(.21f, .16f, .29f, 1f);
                int selectedSlot = i + 1;
                var slot = ArcaneUiControls.Panel(previewSlots, "StyleSlot" + i,
                    x, y, size, size, interior, true);
                var selectSlot = slot.gameObject.AddComponent<Button>();
                selectSlot.targetGraphic = slot.GetComponent<Image>();
                selectSlot.transition = Selectable.Transition.None;
                selectSlot.onClick.AddListener(() =>
                {
                    ActionBarPlanSettings.SelectedSlot = selectedSlot;
                    _tab = "Keybinds";
                    _actionBindingFeedback = "Selected slot " + selectedSlot + " from the bar preview.";
                    RenderPage();
                });
                if (ActionBarPlanSettings.SelectedSlot == selectedSlot)
                    ArcaneUiControls.Panel(slot, "SelectedEdge", 0, 0, size, 3,
                        new Color(.32f, .77f, 1f, 1f), false);
                NativeActionBarDiscovery.NativeSlotSnapshot real = null;
                if (nativePreview) NativeActionBarDiscovery.TryGetActiveSlot(i + 1, out real);
                if (real != null && real.AbilityIcon != null)
                {
                    var icon = ArcaneUiControls.Panel(slot, "NativeSpellIcon", 2, 2,
                        size - 4, size - 4, Color.white, false);
                    icon.GetComponent<Image>().sprite = real.AbilityIcon;
                    icon.GetComponent<Image>().preserveAspect = true;
                }
                else if (!nativePreview)
                {
                    ArcaneUiControls.Label(slot, "Glyph", (i + 1).ToString(),
                        0, 3, size, size - 6, Mathf.Clamp(size / 3, 10, 20),
                        new Color(trim.r, trim.g, trim.b, .75f), TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                else if (real != null && real.HasAssignment)
                {
                    // Skill/item sprites are not guaranteed to be present on
                    // every build. Label the assignment instead of faking art.
                    string abbreviation = real.AbilityName.Length > 3 ? real.AbilityName.Substring(0, 3) : real.AbilityName;
                    ArcaneUiControls.Label(slot, "AbilityAbbrev", abbreviation,
                        1, 4, size - 2, size - 8, 11, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                DrawSlotBorder(slot, size, border, trim, ActionBarPlanSettings.Glow(currentBar));
                if (ActionBarStyleSettings.ShowKeyLabels)
                {
                    string planned = ActionBarPlanSettings.Binding(currentBar, i + 1);
                    string actual = real != null && real.PrimaryKey != KeyCode.None ?
                        ActionBarPlanSettings.Describe(real.PrimaryKey, false, false, false) : (i + 1).ToString();
                    string key = string.IsNullOrEmpty(planned) ? actual : planned;
                    ArcaneUiControls.Label(slot, "Hotkey", key,
                        size - 28, size - 16, 26, 14, 9, Color.white, TextAnchor.MiddleRight);
                }
                // Real native Cooldown is a raw slot field whose timer units
                // have not been verified. Do not display fabricated seconds.
                if (!nativePreview && ActionBarStyleSettings.ShowCooldownNumbers && i == 2)
                {
                    ArcaneUiControls.Panel(slot, "CooldownShade", 1, 1, size - 2,
                        size - 2, new Color(0f, 0f, 0f, .40f), false);
                    ArcaneUiControls.Label(slot, "Cooldown", "8.2", 0, 0, size, size,
                        Mathf.Clamp(size / 4, 10, 16), Color.white,
                        TextAnchor.MiddleCenter, FontStyle.Bold);
                }
            }
            ArcaneUiControls.Label(_actionPreviewHost, "PresetInfo",
                ActionBarPlanSettings.Name(currentBar) + "  •  " + preset + " / " + border,
                470, 58, 263, 22, 11, Muted, TextAnchor.MiddleRight);
            if (!visible)
                ArcaneUiControls.Label(_actionPreviewHost, "VisibilityInfo", "HIDDEN in " + PreviewSituationName(),
                    100, 32, 520, 25, 12, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private static Color AccentForPreset(string preset)
        {
            string hex;
            switch (preset)
            {
                case "Gilded Reliquary": hex = "#D5AD5E"; break;
                case "Runebound": hex = "#48B9DB"; break;
                case "Emberforged": hex = "#E76F35"; break;
                case "Voidglass": hex = "#9E72E0"; break;
                case "Minimal": hex = "#91A1B2"; break;
                case "Custom": hex = "#B88AEB"; break;
                default: hex = "#798ED5"; break;
            }
            Color colour;
            return ColorUtility.TryParseHtmlString(hex, out colour) ? colour : Color.white;
        }

        private void DrawSlotBorder(RectTransform slot, int size, string style, Color colour, int glowAmount)
        {
            if (style == "None") return;
            float b = style == "Thin" ? 1f : ActionBarStyleSettings.BorderWidth;
            var glow = glowAmount / 100f;
            if (style == "Glowing" && glow > 0)
            {
                var aura = new Color(colour.r, colour.g, colour.b, .12f + glow * .32f);
                ArcaneUiControls.Panel(slot, "AuraT", 0, 0, size, 5, aura, false);
                ArcaneUiControls.Panel(slot, "AuraB", 0, size - 5, size, 5, aura, false);
                ArcaneUiControls.Panel(slot, "AuraL", 0, 0, 5, size, aura, false);
                ArcaneUiControls.Panel(slot, "AuraR", size - 5, 0, 5, size, aura, false);
            }
            ArcaneUiControls.Panel(slot, "TrimT", 0, 0, size, b, colour, false);
            ArcaneUiControls.Panel(slot, "TrimB", 0, size - b, size, b, colour, false);
            ArcaneUiControls.Panel(slot, "TrimL", 0, 0, b, size, colour, false);
            ArcaneUiControls.Panel(slot, "TrimR", size - b, 0, b, size, colour, false);
            if (style == "Bevelled")
            {
                Color light = new Color(1f, 1f, 1f, .33f);
                ArcaneUiControls.Panel(slot, "BevelTop", b, b, size - 2 * b, 1, light, false);
                ArcaneUiControls.Panel(slot, "BevelLeft", b, b, 1, size - 2 * b, light, false);
            }
            if (style == "Ornate" || style == "Runic")
            {
                int corner = ActionBarStyleSettings.CornerSize;
                Color detail = new Color(colour.r, colour.g, colour.b, .96f);
                for (int side = 0; side < 2; side++)
                    for (int top = 0; top < 2; top++)
                    {
                        float px = side == 0 ? 0 : size - corner;
                        float py = top == 0 ? 0 : size - b;
                        ArcaneUiControls.Panel(slot, "CornerH" + side + top,
                            px, py, corner, b + 1, detail, false);
                        ArcaneUiControls.Panel(slot, "CornerV" + side + top,
                            side == 0 ? 0 : size - b, top == 0 ? 0 : size - corner,
                            b + 1, corner, detail, false);
                    }
                if (style == "Runic")
                {
                    for (int tick = 1; tick <= 3; tick++)
                        ArcaneUiControls.Panel(slot, "Rune" + tick,
                            size * tick / 4f - 1f, b + 2f, 2, 3, detail, false);
                }
                else
                {
                    ArcaneUiControls.Panel(slot, "Jewel", size / 2f - 3, 0, 6, b + 3,
                        new Color(1f, .86f, .58f, 1f), false);
                }
            }
        }
    }
}
