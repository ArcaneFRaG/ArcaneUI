using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames.Modules.Inventory.Configuration;
using ArcanesGroupFrames.Modules.CharacterSheet.Configuration;
using ArcanesGroupFrames.Modules.Inventory.Native;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private NativeInventoryDecorator InventoryDecorator
        {
            get { return Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                ? Plugin.Instance.UiRuntime.Inventory : null; }
        }

        private void RefreshInventory()
        {
            NativeInventoryDecorator decorator = InventoryDecorator;
            if (decorator != null) decorator.ApplyStyle();
            if (_page == "inventory") RenderInventoryPreview();
        }

        private void RenderInventory()
        {
            switch (_tab)
            {
                case "Appearance":
                    Section("NATIVE INVENTORY APPEARANCE", "Custom inventory presentation for the verified InvPar / PlayerInv / InvWindow bag panel, using real item controls.");
                    DirectChoiceSetting("Inventory Visual Preset", "Framed, ornate or minimalist styling without moving native item colliders.",
                        InventorySettings.VisualPreset, InventorySettings.VisualPresets,
                        v => { InventorySettings.SetPreset(v); RefreshInventory(); });
                    ToggleSetting("Use Global Inventory Theme", "Inherit the active ArcaneUI theme colours.",
                        () => InventorySettings.InheritTheme, v => { InventorySettings.SetBool("InheritTheme", v); RefreshInventory(); });
                    HexSetting("Inventory Accent Colour", "Custom border/accent colour when theme inheritance is disabled.",
                        () => InventorySettings.AccentHex, v => { InventorySettings.SetHex("AccentHex", v); RefreshInventory(); });
                    HexSetting("Inventory Background Colour", "Header/tint colour when theme inheritance is disabled.",
                        () => InventorySettings.BackgroundHex, v => { InventorySettings.SetHex("BackgroundHex", v); RefreshInventory(); });
                    ToggleSetting("Show Inventory Frame Border", "Draw a themed border around the live InvWindow region without changing item controls.",
                        () => InventorySettings.DrawFrame, v => { InventorySettings.SetBool("DrawFrame", v); RefreshInventory(); });
                    SliderSetting("Inventory Border Thickness", 0f, 8f,
                        () => InventorySettings.BorderThickness,
                        v => { InventorySettings.SetFloat("BorderThickness", v); RefreshInventory(); }, " px");
                    ToggleSetting("Show Inventory Title Header", "Decorative title layered behind native currency and item controls.",
                        () => InventorySettings.ShowHeader, v => { InventorySettings.SetBool("ShowHeader", v); RefreshInventory(); });
                    SliderSetting("Inventory Header Height", 14f, 55f,
                        () => InventorySettings.HeaderHeight,
                        v => { InventorySettings.SetFloat("HeaderHeight", v); RefreshInventory(); }, " px");
                    SliderSetting("Inventory Header Opacity", 0f, 100f,
                        () => InventorySettings.HeaderOpacity,
                        v => { InventorySettings.SetFloat("HeaderOpacity", v); RefreshInventory(); }, "%");
                    ToggleSetting("Tint Native Inventory Background", "Optional tint of the underlying native InvWindow Image; original is restored on disable.",
                        () => InventorySettings.TintNativeBackground,
                        v => { InventorySettings.SetBool("TintNativeBackground", v); RefreshInventory(); });
                    SliderSetting("Inventory Tint Strength", 0f, 100f,
                        () => InventorySettings.NativeTintStrength,
                        v => { InventorySettings.SetFloat("NativeTintStrength", v); RefreshInventory(); }, "%");
                    break;
                case "Slots":
                    Section("INVENTORY ITEM SLOTS", "Custom themed slot plates behind verified InvWindow / General (n) native slots. ItemIcon and drag/drop remain untouched.");
                    ToggleSetting("Highlight Native Inventory Slots", "Render theme-matched slot backgrounds and borders beneath real inventory item icons.",
                        () => InventorySettings.FrameSlots,
                        v => { InventorySettings.SetBool("FrameSlots", v); RefreshInventory(); });
                    SliderSetting("Inventory Slot Border Thickness", 0f, 6f,
                        () => InventorySettings.SlotThickness,
                        v => { InventorySettings.SetFloat("SlotThickness", v); RefreshInventory(); }, " px");
                    SliderSetting("Slot Plate Inset", 0f, 10f,
                        () => InventorySettings.SlotInset,
                        v => { InventorySettings.SetFloat("SlotInset", v); RefreshInventory(); }, " px");
                    SliderSetting("Slot Background Opacity", 0f, 100f,
                        () => InventorySettings.SlotFillOpacity,
                        v => { InventorySettings.SetFloat("SlotFillOpacity", v); RefreshInventory(); }, "%");
                    SliderSetting("Maximum Slot Borders", 0f, 160f,
                        () => InventorySettings.MaxSlotBorders,
                        v => { InventorySettings.SetFloat("MaxSlotBorders", v); RefreshInventory(); }, " slots");
                    Info("Native Gameplay Unchanged", "Native ItemIcon handling, clicking, drag/drop, split stacks, equip/unequip, selling and FOTF items remain game-owned.");
                    Info("Slot Detection", "Verified slots are PlayerInv/InvWindow/General (n). If no slots are found, use Inventory > Native > INSPECT.");
                    break;
                case "Native":
                    Section("CHARACTER + INVENTORY NATIVE HIERARCHY", "Detected from your Erenshor runtime log. Scan without replacing any native controller.");
                    Info("Shared Window", "DontDestroyOnLoad:UI/UIElements/InvPar");
                    Info("Inventory Content", "DontDestroyOnLoad:UI/UIElements/InvPar/PlayerInv/InvWindow");
                    Info("Cosmetics", "DontDestroyOnLoad:UI/UIElements/InvPar/PlayerInv/CosmeticPanel");
                    {
                        NativeInventoryDecorator decorator = InventoryDecorator;
                        Info("Inventory Binding", decorator != null ? decorator.Status : "Native inventory bridge not initialized.");
                        Info("Bound Inventory Path", decorator != null ? decorator.NativePath : "Not initialized.");
                        Info("Slot Borders", decorator != null ? decorator.SlotBorders.ToString() : "0");
                        ActionButton("Inspect Native Inventory Hierarchy", "Log up to 140 real inventory transforms, native component names and sizes to diagnose exact item-slot names.", "INSPECT",
                            () => { if (decorator != null) decorator.InspectNativeHierarchy(); RenderPage(); });
                        ActionButton("Refresh Native Inventory Styling", "Re-detect native item slots and reapply appearance without modifying gameplay state.", "REFRESH",
                            () => { if (decorator != null) { decorator.Restore(); decorator.Update(); } RenderPage(); });
                    }
                    Info("No Native UI Suppression", "Inventory targets only the verified InvPar > PlayerInv > InvWindow hierarchy. No other panels are styled.");
                    break;
                default:
                    Section("INVENTORY", "ArcaneUI applies a themed inventory surface directly to Erenshor's real InvWindow.");
                    ToggleSetting("Enable ArcaneUI Inventory", "Enable the themed inventory over the real native bag slots. Defaults on when Character Sheet is enabled, unless explicitly disabled.",
                        () => ArcaneUiModuleSettings.InventoryEnabled,
                        v => { ArcaneUiModuleSettings.SetEnabled(ArcaneUiModuleSettings.Inventory, v); RefreshInventory(); });
                    Info("Shared Character + Inventory UI", "InvPar contains PlayerInv; its InvWindow child owns the real inventory item grid. Position and drag/drop remain native.");
                    Info("Binding", InventoryDecorator != null ? InventoryDecorator.Status : "Inventory decorator not initialized.");
                    ActionButton("Inspect Native Inventory", "Identify item slot controls and sizes in the actual InvWindow hierarchy.", "INSPECT",
                        () => { _tab = "Native"; RenderPage(); });
                    ActionButton("Character Sheet Options", "Open the parent character sheet styling settings.", "CHARACTER",
                        () => SelectPage("character"));
                    Info("Native-safe Custom Inventory", "Themed panel and actual General slot plates are implemented. Native dragging, stacking, selling and FOTF items remain functional. Sorting/search and bag consolidation are not implemented.");
                    break;
            }
        }

        private void RenderInventoryPreview()
        {
            if (_previewHost == null) return;
            _previewHost.localScale = Vector3.one * .575f;
            _previewHost.sizeDelta = new Vector2(814f, 640f);
            ArcaneUiControls.ClearChildren(_previewHost);
            Color accent = InventorySettings.InheritTheme ? Theme.Accent :
                CharacterSheetSettings.Parse(InventorySettings.AccentHex, Theme.Accent);
            Color surface = InventorySettings.InheritTheme ? Theme.Panel :
                CharacterSheetSettings.Parse(InventorySettings.BackgroundHex, Theme.Panel);
            Color ink = Theme.Text; // Preview text uses the same unmodified native UI text policy.
            bool enabled = ArcaneUiModuleSettings.InventoryEnabled;
            ArcaneUiControls.Label(_previewHost, "Mode", enabled ? "THEMED INVENTORY PREVIEW" : "INVENTORY (CUSTOM DISABLED)",
                30f, 16f, 760f, 30f, 18, enabled ? accent : Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform panel = ArcaneUiControls.Panel(_previewHost, "InventorySample", 145f, 62f, 520f, 520f,
                ArcaneUiControls.Multiply(surface, 1.1f), false);
            if (InventorySettings.DrawFrame && enabled)
            {
                var outline = panel.gameObject.AddComponent<Outline>();
                outline.effectColor = accent;
                float px = Mathf.Max(1f, InventorySettings.BorderThickness);
                outline.effectDistance = new Vector2(px, -px);
            }
            ArcaneUiControls.Label(panel, "Title", "INVENTORY", 20f, 20f, 480f, 34f,
                22, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            ArcaneUiControls.Label(panel, "Detail", "INVWINDOW  |  REAL GENERAL SLOTS", 20f, 62f, 480f, 26f,
                14, accent, TextAnchor.MiddleCenter);
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    float x = 168f + col * 65f;
                    float y = 95f + row * 47f;
                    RectTransform slot = ArcaneUiControls.Panel(panel, "InventoryPreviewSlot_" + row + "_" + col,
                        x, y, 46f, 46f, ArcaneUiControls.Multiply(surface, 1.5f), false);
                    if (InventorySettings.FrameSlots && enabled && row * 3 + col < InventorySettings.MaxSlotBorders)
                    {
                        var outline = slot.gameObject.AddComponent<Outline>();
                        outline.effectColor = accent;
                        float px = Mathf.Max(1f, InventorySettings.SlotThickness);
                        outline.effectDistance = new Vector2(px, -px);
                    }
                    if ((row + col * 3) % 5 == 0)
                        ArcaneUiControls.Label(slot, "Icon", "◆", 0f, 0f, 46f, 46f, 24,
                            accent, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
            }
            ArcaneUiControls.Label(panel, "Note", "ILLUSTRATIVE — GAME ITEM CONTROLS PRESERVED", 20, 475, 480, 25,
                12, ink, TextAnchor.MiddleCenter);
        }
    }
}
