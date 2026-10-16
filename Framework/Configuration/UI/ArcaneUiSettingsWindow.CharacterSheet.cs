using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames.Modules.CharacterSheet.Configuration;
using ArcanesGroupFrames.Modules.CharacterSheet.Native;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private NativeCharacterSheetDecorator CharacterDecorator
        {
            get { return Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                ? Plugin.Instance.UiRuntime.CharacterSheet : null; }
        }

        private void RefreshCharacterSheet()
        {
            NativeCharacterSheetDecorator decorator = CharacterDecorator;
            if (decorator != null) decorator.ApplyStyle();
            if (_page == "character") RenderCharacterSheetPreview();
        }

        private void RenderCharacterSheet()
        {
            switch (_tab)
            {
                case "Appearance":
                    Section("CHARACTER SHEET PRESETS", "Choose the proportions and visual style independently. Presets use only real native equipment controls.");
                    DirectChoiceSetting("Character Layout", "Choose between wider stats, a balanced split, or larger equipment spacing.",
                        CharacterSheetSettings.LayoutPreset, CharacterSheetSettings.LayoutPresets,
                        v => { CharacterSheetSettings.SetChoice("LayoutPreset", v); RefreshCharacterSheet(); });
                    DirectChoiceSetting("Character Theme", "Three persistent character-window visual identities, independent of action-bar themes.",
                        CharacterSheetSettings.VisualTheme, CharacterSheetSettings.VisualThemes,
                        v => { CharacterSheetSettings.SetChoice("VisualTheme", v); RefreshCharacterSheet(); });
                    DirectChoiceSetting("Section Heading Alignment", "Centre or left-align section names and the character header.",
                        CharacterSheetSettings.HeaderAlignment, CharacterSheetSettings.HeaderAlignments,
                        v => { CharacterSheetSettings.SetChoice("HeaderAlignment", v); RefreshCharacterSheet(); });
                    SliderSetting("Character Stat Font Size", 11f, 15f,
                        () => CharacterSheetSettings.StatFontSize,
                        v => { CharacterSheetSettings.SetFloat("StatFontSize", v); RefreshCharacterSheet(); }, " px");
                    ToggleSetting("Show Section Dividers", "Use fine accent rules under Vitals, Attributes, Combat, Regeneration, and Resistances.",
                        () => CharacterSheetSettings.SectionDividers,
                        v => { CharacterSheetSettings.SetBool("SectionDividers", v); RefreshCharacterSheet(); });
                    Section("ASCENSION SKILL WEB", "Applies to both General and Class; all purchases still use Erenshor's native Add buttons.");
                    SliderSetting("Ascension Node Diameter", 24f, 38f,
                        () => CharacterSheetSettings.AscensionNodeSize,
                        v => { CharacterSheetSettings.SetFloat("AscensionNodeSize", v); RefreshCharacterSheet(); }, " px");
                    SliderSetting("Ascension Connection Width", .6f, 3f,
                        () => CharacterSheetSettings.AscensionLinkWidth,
                        v => { CharacterSheetSettings.SetFloat("AscensionLinkWidth", v); RefreshCharacterSheet(); }, " px");
                    ToggleSetting("Show Skill Web Labels", "Hide crowded branch labels; full skill names and native effects remain in the details panel.",
                        () => CharacterSheetSettings.AscensionShowLabels,
                        v => { CharacterSheetSettings.SetBool("AscensionShowLabels", v); RefreshCharacterSheet(); });
                    ToggleSetting("Inspect Skills on Hover", "Hover over a rank to display native effect details without spending points.",
                        () => CharacterSheetSettings.AscensionHoverInspect,
                        v => { CharacterSheetSettings.SetBool("AscensionHoverInspect", v); RefreshCharacterSheet(); });
                    Section("COSMETIC WARDROBE", "Changes the arrangement of the real native cosmetic slot controls only.");
                    DirectChoiceSetting("Wardrobe Layout", "Gallery provides larger slot spacing; compact and minimal fit more cosmetic controls.",
                        CharacterSheetSettings.WardrobeLayout, CharacterSheetSettings.WardrobeLayouts,
                        v => { CharacterSheetSettings.SetChoice("WardrobeLayout", v); RefreshCharacterSheet(); });
                    Section("NATIVE-SAFE WINDOW", "Erenshor's equipment slots, item colliders and page button callbacks remain original.");
                    ToggleSetting("Use Native Character Sheet Fallback",
                        "Keeps the original native character sheet. Turn off to permit the bounded two-column layout when all original interactive slots and tabs are verified.",
                        () => CharacterSheetSettings.SafeNativeMode,
                        v => { CharacterSheetSettings.SetBool("SafeNativeMode", v); RefreshCharacterSheet(); });
                    Info("Verified Preset Layout", "All presets stay within PlayerInv. If native equipment or tabs cannot be mapped, the native Erenshor sheet remains visible.");
                    Info("Layout Ratios", "Classic Split: 42/58, Wide Stats: 37/63, Equipment Focus: 46/54. The 3D model remains retired.");
                    ToggleSetting("Enable Themed Character Sheet", "Use real native equipment controls and tabs inside the native character-sheet bounds; falls back to native when controls are not verified.",
                        () => CharacterSheetSettings.ShowOverview,
                        v => { CharacterSheetSettings.SetBool("ShowOverview", v); RefreshCharacterSheet(); });
                    // Companion sizing remains in saved preferences for older
                    // profiles, but is no longer advertised as a native-sheet option.
                    Section("NATIVE WINDOW STYLING", "Change the original InvPar frame without disabling any native item controls.");
                    ToggleSetting("Override Character Palette", "Use the character-specific accent, background and text colours below instead of the preset defaults.",
                        () => CharacterSheetSettings.UseCustomPalette,
                        v => { CharacterSheetSettings.SetBool("UseCustomPalette", v); RefreshCharacterSheet(); });
                    ToggleSetting("Inherit Global Accent and Text", "Global text/accent colours take precedence over custom colour overrides; preset panel surfaces remain.",
                        () => CharacterSheetSettings.InheritTheme, v => { CharacterSheetSettings.SetBool("InheritTheme", v); RefreshCharacterSheet(); });
                    HexSetting("Custom Character Accent", "Used when Override Character Palette is on and global accent inheritance is off.",
                        () => CharacterSheetSettings.AccentHex, v => { CharacterSheetSettings.SetHex("AccentHex", v); RefreshCharacterSheet(); });
                    HexSetting("Custom Character Surface", "Tint the theme's layered backgrounds when Override Character Palette is on.",
                        () => CharacterSheetSettings.BackgroundHex, v => { CharacterSheetSettings.SetHex("BackgroundHex", v); RefreshCharacterSheet(); });
                    HexSetting("Custom Character Text", "Used when Override Character Palette is on and global text inheritance is off.",
                        () => CharacterSheetSettings.TextHex, v => { CharacterSheetSettings.SetHex("TextHex", v); RefreshCharacterSheet(); });
                    ToggleSetting("Show Character Frame Border", "Draw a four-sided, non-interactive native-window outline.",
                        () => CharacterSheetSettings.DrawFrame, v => { CharacterSheetSettings.SetBool("DrawFrame", v); RefreshCharacterSheet(); });
                    SliderSetting("Character Border Thickness", 0f, 8f,
                        () => CharacterSheetSettings.BorderThickness,
                        v => { CharacterSheetSettings.SetFloat("BorderThickness", v); RefreshCharacterSheet(); }, " px");
                    break;
                case "Equipment":
                    Section("EQUIPMENT SLOT PRESENTATION", "Native equipping, item tooltips, dragging, item effects and FOTF equipment remain unchanged.");
                    ToggleSetting("Highlight Character Equipment Slots", "Outline native equipment slots when their transform names can be identified safely.",
                        () => CharacterSheetSettings.FrameEquipment,
                        v => { CharacterSheetSettings.SetBool("FrameEquipment", v); RefreshCharacterSheet(); });
                    SliderSetting("Character Slot Border Thickness", 0f, 6f,
                        () => CharacterSheetSettings.SlotThickness,
                        v => { CharacterSheetSettings.SetFloat("SlotThickness", v); RefreshCharacterSheet(); }, " px");
                    Info("Native Gameplay Preserved", "The bounded sheet uses original item controls and restores their native parents when closed. If any slot is unverified, the original sheet remains visible; no imitation slots.");
                    Info("FOTF Compatibility", "Native equipment continues to resolve custom item IDs, class eligibility and equipped Aura items through the game and FOTF.");
                    break;
                case "Native":
                    Section("NATIVE CHARACTER WINDOW DETECTION", "No game UI is hidden or replaced during discovery. Select the correct panel explicitly.");
                    {
                        NativeCharacterSheetDecorator decorator = CharacterDecorator;
                        string status = decorator != null ? decorator.Status : "Character sheet bridge not ready.";
                        Info("Character Sheet Detection", status);
                        Info("Known Hierarchy", "InvPar = shared character + inventory root; PlayerInv = inventory. CosmeticPanel belongs to PlayerInv.");
                        Info("Selected Native Path", string.IsNullOrEmpty(CharacterSheetSettings.NativePath)
                            ? "Automatic: verified InvPar / PlayerInv hierarchy only."
                            : CharacterSheetSettings.NativePath);
                        Info("Verified Character Root", decorator != null && !string.IsNullOrEmpty(decorator.KnownCharacterPath)
                            ? decorator.KnownCharacterPath : "Not currently found (open the native character/inventory window).");
                        ActionButton("Inspect Native Equipment Controls", "Logs the exact slot/interaction components used by Erenshor. Share the results if the left column is empty.", "INSPECT SLOTS",
                            () => { if (decorator != null) decorator.InspectNativeSlotControls(); RenderPage(); });
                        ActionButton("Inspect Native Character Values", "Logs the exact current native character label text for diagnosing unmatched values; no game UI is changed.", "INSPECT STATS",
                            () => { if (decorator != null) decorator.InspectNativeStats(); RenderPage(); });
                        ActionButton("Use Known Character Root", "Bind the identified InvPar shared window, not PlayerInv or CosmeticPanel.", "USE INVP",
                            () => { if (decorator != null) decorator.SelectKnownCharacterRoot(); RenderPage(); });
                        ActionButton("Scan Character Panels", "Find named/semantic character panels, including inactive panels.", "SCAN",
                            () => { if (decorator != null) decorator.ScanCandidates(); RenderPage(); });
                        ActionButton("Browse All Large UI Panels", "Fallback for generic native UI names. Only choose the actual character window.", "ALL PANELS",
                            () => { if (decorator != null) decorator.ScanCandidates(true); RenderPage(); });
                        ActionButton("Capture a Character Window Opening", "Closes ArcaneUI settings for 45 seconds so you can open Erenshor's character sheet. Newly opened panels are remembered.", "ARM CAPTURE",
                            () => { if (decorator != null) { decorator.ArmCapture(); Close(); } });
                        if (decorator != null && decorator.CapturedCount > 0)
                            ActionButton("Review Captured Panels", "View native panels that appeared after arming capture.", "REVIEW",
                                () => { decorator.ReviewCapture(); RenderPage(); });
                        if (decorator != null && decorator.CandidateCount > 0)
                        {
                            Info("Candidate " + decorator.CandidateIndex + " / " + decorator.CandidateCount, decorator.CandidateName);
                            ActionButton("Next Character Candidate", "Browse the discovered window hierarchy paths.", "NEXT",
                                () => { decorator.NextCandidate(); RenderPage(); });
                            ActionButton("Use This Character Window", "Save this exact panel. Only this selected native panel receives cosmetic styling.", "SELECT",
                                () => { decorator.SelectCandidate(); RenderPage(); });
                        }
                        ActionButton("Restore Native Character Sheet", "Disable ArcaneUI character styling and clear any manual window binding. Native gameplay remains untouched.", "RESTORE",
                            () => { ArcaneUiModuleSettings.SetEnabled(ArcaneUiModuleSettings.Character, false);
                                if (decorator != null) decorator.SetSelectedPath(""); RenderPage(); });
                    }
                    break;
                default:
                    Section("CHARACTER SHEET", "Phase 10.5 native-safe appearance layer. The original character sheet remains fully interactive.");
                    ToggleSetting("Enable ArcaneUI Character Sheet", "Opt in to styling the selected native character window. Native appearance returns when disabled.",
                        () => ArcaneUiModuleSettings.CharacterSheetEnabled,
                        v => ArcaneUiModuleSettings.SetEnabled(ArcaneUiModuleSettings.Character, v));
                    Info("Native Layout", "The custom sheet uses exactly the bounds of InvPar, while PlayerInv remains the same attached native inventory. Unverified controls keep the original Erenshor sheet.");
                    ActionButton("Configure Inventory", "Open independent inventory styling in the same native window.", "INVENTORY",
                        () => SelectPage("inventory"));
                    Info("Character Sheet Information", "The bounded Equipment page now restores the native class icon and a live Proficiencies section alongside the 19 existing stats. Icons are mirrored from native/FOTF class artwork; proficiency entries come only from learned skills or exposed class metadata. Missing values display as unavailable.");
                    Info("Native Gameplay Preserved", "With Safe Native mode enabled, all original Erenshor equipment slots, Ascension/Cosmetic tabs, drag-and-drop, tooltips and FOTF items remain functional.");
                    ActionButton("Character Window Compatibility", "Use only if the character sheet does not appear on your game version.", "ADVANCED",
                        () => { _tab = "Native"; RenderPage(); });
                    Info("Character Sheet Preview", "The selected layout and theme apply to the class icon, Proficiencies, 17 native equipment slots, 19 live stats and three page tabs. Inventory appearance is configured independently.");
                    break;
            }
        }

        private void RenderCharacterSheetPreview()
        {
            if (_previewHost == null) return;
            _previewHost.localScale = Vector3.one * .59f;
            _previewHost.sizeDelta = new Vector2(960f, 650f);
            ArcaneUiControls.ClearChildren(_previewHost);
            string visual = CharacterSheetSettings.VisualTheme;
            Color accent = CharacterSheetSettings.Parse(visual == "Refined Fantasy" ? "#DFB76D" :
                visual == "Minimal Clean" ? "#8FCDCD" : "#D3AD70", Theme.Accent);
            Color surface = CharacterSheetSettings.Parse(visual == "Refined Fantasy" ? "#181720" :
                visual == "Minimal Clean" ? "#151B22" : "#141F2B", Theme.Panel);
            Color panel = CharacterSheetSettings.Parse(visual == "Refined Fantasy" ? "#24212A" :
                visual == "Minimal Clean" ? "#1B2630" : "#182B38", Theme.Panel);
            Color section = CharacterSheetSettings.Parse(visual == "Refined Fantasy" ? "#3A2930" :
                visual == "Minimal Clean" ? "#23323B" : "#1A3947", Theme.Panel);
            Color text = CharacterSheetSettings.Parse(visual == "Refined Fantasy" ? "#EEE3CC" :
                visual == "Minimal Clean" ? "#E8F1F4" : "#EBF0F2", Theme.Text);
            if (CharacterSheetSettings.UseCustomPalette)
            {
                accent = CharacterSheetSettings.Parse(CharacterSheetSettings.AccentHex, accent);
                surface = CharacterSheetSettings.Parse(CharacterSheetSettings.BackgroundHex, surface);
                text = CharacterSheetSettings.Parse(CharacterSheetSettings.TextHex, text);
            }
            if (CharacterSheetSettings.InheritTheme)
            {
                accent = Theme.Accent;
                text = Theme.Text;
            }
            ArcaneUiControls.Label(_previewHost, "Mode",
                "CHARACTER  •  " + CharacterSheetSettings.LayoutPreset.ToUpperInvariant() + "  •  " + visual.ToUpperInvariant(),
                18, 9, 920, 36, 21, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform page = ArcaneUiControls.Panel(_previewHost, "CharacterLayoutPreview", 10, 50, 940, 590,
                surface, false);
            ArcaneUiControls.Panel(page, "HeaderStripe", 7, 5, 926, 47, panel, false);
            RectTransform classIconPreview = ArcaneUiControls.Panel(page,
                "ClassIconPreview", 17, 10, 38, 36, section, false);
            ArcaneUiControls.Label(classIconPreview, "IconFallbackPreview", "C",
                0, 0, 38, 36, 23, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            ArcaneUiControls.Label(page, "Name", "CHARACTER NAME   ·   CLASS   ·   LEVEL 35",
                64, 8, 856, 40, 18, text, TextAnchor.MiddleLeft, FontStyle.Bold);
            float split = 940f * CharacterSheetSettings.EquipmentWidth;
            ArcaneUiControls.Panel(page, "EquipmentSurface", 10, 58, split - 16, 464, panel, false);
            ArcaneUiControls.Panel(page, "StatsSurface", split + 5, 58, 925 - split, 464, panel, false);
            ArcaneUiControls.Label(page, "ItemsTitle", "EQUIPPED ITEMS", 16, 60, split - 28, 29, 17,
                accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            // Schematic only: never duplicate or move genuine native item controls in settings.
            string[] slots = { "Head", "Neck", "Chest", "Back", "Hands", "Arms", "L Wrist", "R Wrist",
                "Legs", "Waist", "Feet", "Charm", "Ring L", "Ring R", "Main", "Offhand", "Aura" };
            float gap = CharacterSheetSettings.LayoutPreset == "Equipment Focus" ? 95f : 76f;
            float origin = 10f + (split - gap) / 2f - 27f;
            for (int i = 0; i < slots.Length; i++)
            {
                float x = origin + (i % 2) * gap;
                if (i == 16) x = origin + gap / 2f;
                PreviewEquipmentSlot(page, slots[i], x, 97 + (i / 2) * 46, accent, panel, text);
            }
            float statLeft = split + 14;
            float statWidth = 910 - split;
            ArcaneUiControls.Label(page, "StatsTitle", "CHARACTER STATISTICS", statLeft, 60, statWidth - 8, 29, 17,
                accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            string[] sectionTitles = { "VITALS", "ATTRIBUTES", "COMBAT", "REGENERATION", "RESISTANCES" };
            string[][] labels = {
                new [] { "HP", "AC", "MP" },
                new [] { "Strength", "Endurance", "Dexterity", "Agility", "Intelligence", "Wisdom", "Charisma", "Resilience" },
                new [] { "Haste", "Attack Bonus" },
                new [] { "HP Regen", "MP Regen" },
                new [] { "Magic", "Poison", "Elemental", "Void" }
            };
            float y = 97f;
            for (int group = 0; group < labels.Length; group++)
            {
                ArcaneUiControls.Panel(page, "Heading" + group, statLeft, y, statWidth - 6, 17, section, false);
                ArcaneUiControls.Label(page, "HeadingLabel" + group, sectionTitles[group], statLeft + 5, y,
                    statWidth - 16, 17, 13, accent,
                    CharacterSheetSettings.HeaderAlignment == "Left" ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                y += 18;
                foreach (string label in labels[group])
                {
                    ArcaneUiControls.Label(page, "Key" + group + label, label, statLeft + 7, y,
                        statWidth * .56f, 13, 12, text, TextAnchor.MiddleLeft);
                    ArcaneUiControls.Label(page, "Value" + group + label, "—", statLeft + statWidth * .66f, y,
                        statWidth * .29f, 13, 12, accent, TextAnchor.MiddleRight);
                    y += 14;
                }
                y += 2;
            }
            ArcaneUiControls.Panel(page, "ProficiencyTitle", statLeft, y + 2,
                statWidth - 6, 18, section, false);
            ArcaneUiControls.Label(page, "ProficiencyHeading", "PROFICIENCIES",
                statLeft + 5, y + 2, statWidth - 16, 18, 13,
                accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            ArcaneUiControls.Label(page, "ProficiencyPlaceholder",
                "Live native proficiency skills / class data",
                statLeft + 6, y + 21, statWidth - 18, 30, 11,
                text, TextAnchor.MiddleCenter);
            RectTransform tabs = ArcaneUiControls.Panel(page, "NativeTabs", 7, 527, 926, 55,
                panel, false);
            string[] tabLabels = { "EQUIPMENT", "ASCENSION", "COSMETIC" };
            for (int i = 0; i < 3; i++)
            {
                RectTransform cell = ArcaneUiControls.Panel(tabs, "Tab" + i, 3 + i * 307, 3, 300, 47,
                    i == 0 ? section : panel, false);
                ArcaneUiControls.Label(cell, "TabText", tabLabels[i], 3, 3, 294, 41,
                    15, i == 0 ? accent : text, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        private void PreviewEquipmentSlot(RectTransform parent, string label, float x, float y,
            Color accent, Color surface, Color text)
        {
            RectTransform slot = ArcaneUiControls.Panel(parent, "Slot_" + label, x, y, 54f, 43f,
                ArcaneUiControls.Multiply(surface, 1.3f), false);
            if (CharacterSheetSettings.FrameEquipment)
            {
                var outline = slot.gameObject.AddComponent<Outline>();
                outline.effectColor = accent;
                float size = Mathf.Max(1f, CharacterSheetSettings.SlotThickness);
                outline.effectDistance = new Vector2(size, -size);
            }
            ArcaneUiControls.Label(slot, "Name", label, 3f, 0f, 48f, 43f, 10, text,
                TextAnchor.MiddleCenter, FontStyle.Bold);
        }
    }
}
