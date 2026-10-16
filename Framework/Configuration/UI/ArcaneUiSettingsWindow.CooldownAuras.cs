using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.CooldownAuras.Configuration;
using ArcanesGroupFrames.Modules.CooldownAuras.UI;
using ArcanesGroupFrames.Framework.NativeUI.Status;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private Text _cooldownAuraPreviewState;
        private int _cooldownAuraSelectedGroup;
        private readonly RectTransform[] _cooldownAuraSamples = new RectTransform[3];
        private readonly Text[] _cooldownAuraSampleLabels = new Text[3];

        private void RenderCooldownAuraPreview()
        {
            if (_previewHost == null) return;
            if (IsAuraEditorTab(_tab)) { RenderCustomAuraPreview(); return; }
            ArcaneUiControls.ClearChildren(_previewHost);
            string[] captions = { "Player Buffs", "Target Debuffs", "Cooldowns" };
            Color[] shades = {
                new Color(.17f,.57f,.30f), new Color(.67f,.25f,.29f), new Color(.28f,.48f,.78f)
            };
            for (int i = 0; i < 3; i++)
            {
                var row = ArcaneUiControls.Rect(_previewHost,"PreviewTrack_"+i, 78, 220+i*48, 620, 32);
                _cooldownAuraSamples[i] = row;
                _cooldownAuraSampleLabels[i] = ArcaneUiControls.Label(row,"Caption",captions[i],0,0,112,24,
                    10, Theme.Text,TextAnchor.MiddleLeft);
                for (int j=0;j<4;j++)
                {
                    var icon = ArcaneUiControls.Panel(row,"Sample_"+j,116+j*30,1,23,23,
                        shades[i],false);
                    var initial = ArcaneUiControls.Label(icon,"Letter",new [] {"A","B","C","D"}[j],
                        0,0,23,23,12,Color.white,TextAnchor.MiddleCenter,FontStyle.Bold);
                    initial.raycastTarget = false;
                }
            }
            _cooldownAuraPreviewState = ArcaneUiControls.Label(_previewHost,"Hint","",20,107,
                760,20,10,Muted,TextAnchor.MiddleLeft);
            UpdateCooldownAuraPreview();
        }
        private void UpdateCooldownAuraPreview()
        {
            if (_page != "auras" || _previewHost == null || !_previewHost.gameObject.activeSelf) return;
            if (IsAuraEditorTab(_tab)) { UpdateCustomAuraPreview(); return; }
            var frame = CooldownAuraSettings.Frame(_cooldownAuraSelectedGroup);
            int size = frame.IconSize;
            int step = size + frame.Spacing;
            for (int i=0;i<3;i++)
            {
                var row = _cooldownAuraSamples[i];
                if (row == null) continue;
                row.anchoredPosition = new Vector2(78f, -(200f+i*(size+18f)));
                if (_cooldownAuraSampleLabels[i] != null)
                    _cooldownAuraSampleLabels[i].text = i == 0 ? CooldownAuraSettings.FrameNames[_cooldownAuraSelectedGroup] : (i == 1 ? "OTHER FRAMES INDEPENDENT" : "LIVE NATIVE DATA");
                for (int j=0;j<4;j++)
                {
                    var child = row.Find("Sample_" + j) as RectTransform;
                    if (child == null) continue;
                    child.sizeDelta = new Vector2(size,size);
                    child.anchoredPosition = new Vector2(116f+j*step, 0f);
                    var text = child.GetComponentInChildren<Text>();
                    if (text != null) text.rectTransform.sizeDelta = new Vector2(size,size);
                }
            }
            if (_cooldownAuraPreviewState != null)
                _cooldownAuraPreviewState.text = CooldownAuraSettings.Enabled
                    ? "Enabled — native status effects and hotkey cooldowns are shown during gameplay."
                    : "Preview only — tracker is disabled in-game until you enable it.";
        }

        private void RenderCooldownAuras()
        {
            if (IsAuraEditorTab(_tab)) { RenderAuraEditor(); return; }
            if (_tab == "Appearance" || _tab == "Position")
            {
                int id = _cooldownAuraSelectedGroup;
                var frame = CooldownAuraSettings.Frame(id);
                Section("INDEPENDENT EFFECT FRAMES", "Configure each native-backed effect category separately. The original Erenshor effects remain untouched.");
                ActionButton("Selected Effect Frame", "Editing: " + CooldownAuraSettings.FrameNames[id] + ". Change frame without resetting other frames.",
                    "NEXT FRAME", () => { _cooldownAuraSelectedGroup = (_cooldownAuraSelectedGroup + 1) % 5; RenderPage(); });
                if (_tab == "Appearance")
                {
                    Section("FRAME APPEARANCE", "These options affect only " + CooldownAuraSettings.FrameNames[id] + ".");
                    SliderSetting("Icon Size", 20f, 64f, () => frame.IconSize,
                        v => CooldownAuraSettings.SetFrameSize(id, v), " px");
                    SliderSetting("Icon Spacing", 0f, 24f, () => frame.Spacing,
                        v => CooldownAuraSettings.SetFrameSpacing(id, v), " px");
                    SliderSetting("Maximum Icons", 1f, 12f, () => frame.MaxIcons,
                        v => CooldownAuraSettings.SetFrameMax(id, v));
                    SliderSetting("Icons Per Line", 1f, 12f, () => frame.Columns,
                        v => CooldownAuraSettings.SetFrameColumns(id, v));
                    ActionButton("Growth Direction", "Current: " + CooldownAuraSettings.GrowthNames[frame.Growth],
                        "CHANGE", () => { CooldownAuraSettings.CycleFrameGrowth(id); RenderPage(); });
                    ActionButton("Effect Sorting", "Current: " + CooldownAuraSettings.SortNames[frame.Sort],
                        "CHANGE", () => { CooldownAuraSettings.CycleFrameSort(id); RenderPage(); });
                    Section("VISUAL THEME", "Independent appearance without modifying native buff icons.");
                    ActionButton("Frame Style", "Current: " + CooldownAuraSettings.StyleNames[frame.Style],
                        "CHANGE", () => { CooldownAuraSettings.CycleFrameStyle(id); RenderPage(); });
                    ActionButton("Accent Colour", "Current: " + CooldownAuraSettings.PaletteNames[frame.Palette],
                        "CHANGE", () => { CooldownAuraSettings.CycleFramePalette(id); RenderPage(); });
                    SliderSetting("Frame Opacity", 20f, 100f, () => frame.Opacity * 100f,
                        v => CooldownAuraSettings.SetFrameOpacity(id, v), " %");
                    ToggleSetting("Show Frame Label", "Show the effect category title above the icons.",
                        () => frame.ShowTitle, v => CooldownAuraSettings.SetFrameTitle(id, v));
                    ToggleSetting("Show Frame Timers", "Display remaining native duration on each icon.",
                        () => frame.ShowDuration, v => CooldownAuraSettings.SetFrameDuration(id, v));
                    ToggleSetting("Show Frame Stacks", "Show native effect stack counts when available.",
                        () => frame.ShowStacks, v => CooldownAuraSettings.SetFrameStacks(id, v));
                }
                else
                {
                    Section("FRAME POSITION", "Each group is draggable in the compact Layout Editor; X/Y settings are in reference-canvas pixels.");
                    SliderSetting("Frame X Position", 0f, 1920f, () => frame.X,
                        v => CooldownAuraSettings.SetFramePosition(id, new Vector2(v, -frame.Y)), " px");
                    SliderSetting("Frame Y Position", 0f, 1080f, () => frame.Y,
                        v => CooldownAuraSettings.SetFramePosition(id, new Vector2(frame.X, -v)), " px");
                    ActionButton("Open Layout Editor", "Select any of the five aura/cooldown outlines, drag to position, and edit X/Y beside its frame.",
                        "UNLOCK UI", () => { Close(); Plugin.Instance.UiRuntime.LayoutEditor.Open(); });
                    Info("Empty Frames", "Frames can be selected in Layout Editor even when no matching effects are active. Lock UI saves their exact positions.");
                }
            }
            else
            {
                Section("COOLDOWN & AURA TRACKER", "Separate, read-only views of Erenshor's native effects and cooldowns.");
                ToggleSetting("Enable Cooldown & Aura Tracker", "Show read-only native buffs, debuffs and assigned-ability cooldowns.",
                    () => CooldownAuraSettings.Enabled, CooldownAuraSettings.SetEnabled);
                Section("AURA CATEGORIES", "Choose which effect groups should be displayed.");
                ToggleSetting("Show Player Buffs", "Active beneficial effects on your character.",
                    () => CooldownAuraSettings.PlayerBuffs, CooldownAuraSettings.SetPlayerBuffs);
                ToggleSetting("Show Player Debuffs", "Active harmful effects on your character.",
                    () => CooldownAuraSettings.PlayerDebuffs, CooldownAuraSettings.SetPlayerDebuffs);
                ToggleSetting("Show Target Buffs", "Active beneficial effects on your selected target.",
                    () => CooldownAuraSettings.TargetBuffs, CooldownAuraSettings.SetTargetBuffs);
                ToggleSetting("Show Target Debuffs", "Active harmful effects on your selected target.",
                    () => CooldownAuraSettings.TargetDebuffs, CooldownAuraSettings.SetTargetDebuffs);
                Section("NATIVE BUFF / DEBUFF VISIBILITY", "Hide original Erenshor effect icons independently of the custom aura tracker. The native effects continue to function normally.");
                ToggleSetting("Hide Native Player Buffs", "Fail-open suppression for the player's original buff tray.",
                    () => NativeBuffDebuffVisibility.GetSuppress(NativeBuffDebuffVisibility.Channel.PlayerBuffs),
                    v => NativeBuffDebuffVisibility.SetSuppress(NativeBuffDebuffVisibility.Channel.PlayerBuffs, v));
                ToggleSetting("Hide Native Player Debuffs", "Fail-open suppression for the player's original debuff tray.",
                    () => NativeBuffDebuffVisibility.GetSuppress(NativeBuffDebuffVisibility.Channel.PlayerDebuffs),
                    v => NativeBuffDebuffVisibility.SetSuppress(NativeBuffDebuffVisibility.Channel.PlayerDebuffs, v));
                ToggleSetting("Hide Native Target Buffs", "Fail-open suppression for the target's original buff tray.",
                    () => NativeBuffDebuffVisibility.GetSuppress(NativeBuffDebuffVisibility.Channel.TargetBuffs),
                    v => NativeBuffDebuffVisibility.SetSuppress(NativeBuffDebuffVisibility.Channel.TargetBuffs, v));
                ToggleSetting("Hide Native Target Debuffs", "Fail-open suppression for the target's original debuff tray.",
                    () => NativeBuffDebuffVisibility.GetSuppress(NativeBuffDebuffVisibility.Channel.TargetDebuffs),
                    v => NativeBuffDebuffVisibility.SetSuppress(NativeBuffDebuffVisibility.Channel.TargetDebuffs, v));
                Section("ABILITY COOLDOWNS", "Tracks assigned native primary and additional hotkeys.");
                ToggleSetting("Show Ability Cooldowns", "Show abilities currently on native cooldown; instant resets remove them.",
                    () => CooldownAuraSettings.Cooldowns, CooldownAuraSettings.SetCooldowns);
                SliderSetting("Minimum Cooldown Shown",0f,30f,() => CooldownAuraSettings.MinimumCooldown,
                    CooldownAuraSettings.SetMinimumCooldown," sec");
            }
        }
    }
}
