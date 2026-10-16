using System;
using UnityEngine;
using ArcanesGroupFrames.Framework.Theming.Rendering;
using ArcanesGroupFrames.Framework.Modules;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private void RefreshFineFrame(string module)
        {
            if (module == ArcaneUiModuleSettings.Player)
            {
                RefreshPlayerLayout(); RefreshPlayerStyle();
                if (_playerPreview != null) _playerPreview.ApplyFineTuning();
            }
            else if (module == ArcaneUiModuleSettings.Target)
            {
                RefreshTargetLayout(); RefreshTargetStyle();
                if (_targetPreview != null) _targetPreview.ApplyFineTuning();
            }
            else if (module == ArcaneUiModuleSettings.Party) PartyFramesUI.RefreshTheme();
            else if (module == ArcaneUiModuleSettings.Raid) RaidFramesUI.RefreshTheme();
            UpdatePreview();
        }
        private void RenderAdvancedFrame()
        {
            string module = _page == "player" ? ArcaneUiModuleSettings.Player :
                _page == "target" ? ArcaneUiModuleSettings.Target :
                _page == "party" ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid;
            bool individual = _page == "player" || _page == "target";
            bool effects = true;
            ArcaneUiFrameFineTuning fine = ArcaneUiFrameFineTuning.For(module);
            Section("ADVANCED FRAME APPEARANCE", "Independent visual controls for this module. Native spells, targets and click casting are unchanged.");
            SliderSetting("Frame Opacity", 20f, 100f, () => fine.FrameOpacity,
                v => { fine.SetFloat("FrameOpacity", v); RefreshFineFrame(module); }, "%");
            SliderSetting("Health Fill Opacity", 0f, 100f, () => fine.HealthOpacity,
                v => { fine.SetFloat("HealthOpacity", v); RefreshFineFrame(module); }, "%");
            SliderSetting("Mana Fill Opacity", 0f, 100f, () => fine.ManaOpacity,
                v => { fine.SetFloat("ManaOpacity", v); RefreshFineFrame(module); }, "%");
            SliderSetting("Text Opacity", 0f, 100f, () => fine.TextOpacity,
                v => { fine.SetFloat("TextOpacity", v); RefreshFineFrame(module); }, "%");
            ToggleSetting("Show Frame Border", "Keep an independently styled frame border.",
                () => fine.ShowBorder, v => { fine.SetBool("ShowBorder", v); RefreshFineFrame(module); });
            SliderSetting("Frame Border Thickness", 0f, 8f, () => fine.BorderWidth,
                v => { fine.SetFloat("BorderWidth", v); RefreshFineFrame(module); }, " px");
            HexSetting("Frame Border Colour", "Blank override uses the selected module theme border.",
                () => string.IsNullOrEmpty(fine.BorderColour) ? "#78694F" : fine.BorderColour,
                v => { fine.SetColour("BorderColour", v); RefreshFineFrame(module); });
            ActionButton("Use Theme Border Colour", "Remove this module's custom border colour override.",
                "RESET COLOUR", () => { fine.ClearColour("BorderColour"); RefreshFineFrame(module); RenderPage(); });
            Section("TEXT PRESENTATION", "Show or hide individual labels without changing the game data.");
            ToggleSetting("Show Unit Name", "Hide the unit name text without hiding health or casting.",
                () => fine.ShowName, v => { fine.SetBool("ShowName", v); RefreshFineFrame(module); });
            ToggleSetting("Show Health Text", "Hide health numbers and percentage together.",
                () => fine.ShowHealthText,
                v => { fine.SetBool("ShowHealthText", v); RefreshFineFrame(module); });
            if (individual)
                ToggleSetting("Show Mana Text", "Keep the mana strip, but independently hide its numbers and percentage.",
                    () => fine.ShowManaText,
                    v => { fine.SetBool("ShowManaText", v); RefreshFineFrame(module); });
            HexSetting("Frame Text Colour", "Independent text colour. Reset to inherit readable theme colours.",
                () => string.IsNullOrEmpty(fine.TextColour) ? "#F2E9D9" : fine.TextColour,
                v => { fine.SetColour("TextColour", v); RefreshFineFrame(module); });
            ActionButton("Use Theme Text Colour", "Remove the manual text-colour override.", "RESET COLOUR",
                () => { fine.ClearColour("TextColour"); RefreshFineFrame(module); RenderPage(); });
            if (individual)
            {
                Section("CLASS / LEVEL BADGE", "Optional compact decorative badge. No character portrait asset is required.");
                DirectChoiceSetting("Badge Content", "Off, class initial, or character level.",
                    fine.BadgeMode, new[] { "None", "Class", "Level" },
                    v => { fine.SetBadgeMode(v); RefreshFineFrame(module); });
                DirectChoiceSetting("Badge Placement", "Position left or right of the frame without shrinking the health bar.",
                    fine.BadgePlacement, new[] { "Left", "Right" },
                    v => { fine.SetBadgePlacement(v); RefreshFineFrame(module); });
                SliderSetting("Badge Size", 18f, 80f, () => fine.BadgeSize,
                    v => { fine.SetFloat("BadgeSize", v); RefreshFineFrame(module); }, " px");
                SliderSetting("Badge X Offset", -100f, 100f, () => fine.BadgeX,
                    v => { fine.SetFloat("BadgeX", v); RefreshFineFrame(module); }, " px");
                SliderSetting("Badge Y Offset", -100f, 100f, () => fine.BadgeY,
                    v => { fine.SetFloat("BadgeY", v); RefreshFineFrame(module); }, " px");
                HexSetting("Badge Background Colour", "Accent colour behind the class or level text.",
                    () => fine.BadgeColour,
                    v => { fine.SetColour("BadgeColour", v); RefreshFineFrame(module); });
            }
            if (effects)
            {
                Section("STATUS EFFECT INDICATORS", "These settings only filter and restyle already known native status effects.");
                ToggleSetting("Display Buff Icons", "Show beneficial effects when the base frame enables effects.",
                    () => fine.ShowBuffIcons,
                    v => { fine.SetBool("ShowBuffIcons", v); RefreshFineFrame(module); });
                ToggleSetting("Display Debuff Icons", "Show harmful effects when the base frame enables effects.",
                    () => fine.ShowDebuffIcons,
                    v => { fine.SetBool("ShowDebuffIcons", v); RefreshFineFrame(module); });
                SliderSetting("Status Icon Opacity", 0f, 100f, () => fine.StatusOpacity,
                    v => { fine.SetFloat("StatusOpacity", v); RefreshFineFrame(module); }, "%");
                int limit = individual ? 8 : 2;
                SliderSetting("Maximum Status Icons", 0f, limit, () => Mathf.Min(limit, fine.MaxStatusIcons),
                    v => { fine.SetMaxStatus(Mathf.RoundToInt(v)); RefreshFineFrame(module); }, "");
                DirectChoiceSetting("Status Sort Order", "Preserve the original display order or explicitly sort by duration.",
                    fine.StatusSort, new[] { "Native", "Longest First", "Shortest First" },
                    v => { fine.SetStatusSort(v); RefreshFineFrame(module); });
                if (individual)
                {
                    Section("STATUS ICON POSITION", "Player effects are opt-in. Target effect size and placement are also available on its Effects tab.");
                    if (_page == "player")
                    {
                        SliderSetting("Status Icon Size", 14f, 48f, () => fine.AuraSize,
                            v => { fine.SetFloat("AuraSize", v); RefreshFineFrame(module); }, " px");
                        SliderSetting("Status Icon Spacing", 0f, 16f, () => fine.AuraSpacing,
                            v => { fine.SetFloat("AuraSpacing", v); RefreshFineFrame(module); }, " px");
                        DirectChoiceSetting("Status Icon Placement", "Above, below or beside the frame.",
                            fine.AuraPlacement, new[] { "Below Frame", "Above Frame", "Left", "Right" },
                            v => { fine.SetAuraPlacement(v); RefreshFineFrame(module); });
                    }
                    SliderSetting("Status Icon X Offset", -250f, 250f, () => fine.AuraX,
                        v => { fine.SetFloat("AuraX", v); RefreshFineFrame(module); }, " px");
                    SliderSetting("Status Icon Y Offset", -250f, 250f, () => fine.AuraY,
                        v => { fine.SetFloat("AuraY", v); RefreshFineFrame(module); }, " px");
                }
            }
            Section("RESTORE", "Resets only these advanced appearance overrides for the selected frame module.");
            ActionButton("Reset Advanced Frame Styling", "Does not reset the original unit-frame sizing, colours or click bindings.",
                "RESET ADVANCED", () => {
                    ArcaneUiFrameFineTuning.Reset(module);
                    RefreshFineFrame(module); RenderPage();
                });
        }
    }
}
