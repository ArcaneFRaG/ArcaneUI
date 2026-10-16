using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.Minimap.Configuration;
using ArcanesGroupFrames.Modules.Minimap.Native;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private NativeMinimapPresentation MinimapPresentation
        {
            get { return Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                ? Plugin.Instance.UiRuntime.Minimap : null; }
        }
        private void RefreshMinimap()
        {
            if (MinimapPresentation != null) MinimapPresentation.Refresh();
            if (_page == "minimap") RenderMinimapPreview();
        }
        private void RenderMinimap()
        {
            switch (_tab)
            {
                case "Appearance":
                    Section("MINIMAP SHAPE", "Change the native UI stencil geometry without replacing Erenshor's terrain sprite. If the game also clips the map inside its renderer, INSPECT will identify that limitation.");
                    DirectChoiceSetting("Minimap Frame Style", "Round, Square, Rounded, Ornate or Minimal. Changes a verified native stencil mesh; a circular source texture/shader may need a separate rendering fix.",
                        MinimapSettings.Style, MinimapSettings.Styles,
                        v => { MinimapSettings.SetStyle(v); RefreshMinimap(); });
                    ToggleSetting("Show Minimap Border", "Outline the actual map viewport. Native button hit areas are left clear. Disabling the border does not disable the selected map shape.",
                        () => MinimapSettings.ShowBorder, v => { MinimapSettings.SetBool("ShowBorder", v); RefreshMinimap(); });
                    ToggleSetting("Inherit Minimap Theme", "Use the current global theme accent for the border.",
                        () => MinimapSettings.InheritTheme, v => { MinimapSettings.SetBool("InheritTheme", v); RefreshMinimap(); });
                    HexSetting("Minimap Border Colour", "Independent colour used when global-theme inheritance is disabled.",
                        () => MinimapSettings.BorderHex, v => { MinimapSettings.SetHex(v); RefreshMinimap(); });
                    SliderSetting("Minimap Border Thickness", 0f, 16f,
                        () => MinimapSettings.BorderThickness, v => { MinimapSettings.SetFloat("BorderThickness", v); RefreshMinimap(); }, " px");
                    SliderSetting("Minimap Border Opacity", 0f, 100f,
                        () => MinimapSettings.BorderOpacity, v => { MinimapSettings.SetFloat("BorderOpacity", v); RefreshMinimap(); }, "%");
                    SliderSetting("Minimap Border Inset", 0f, 24f,
                        () => MinimapSettings.Inset, v => { MinimapSettings.SetFloat("Inset", v); RefreshMinimap(); }, " px");
                    break;
                case "Layout":
                    Section("MINIMAP LAYOUT", "Move and scale the genuine map in the HUD. This does not change its zoom level.");
                    ToggleSetting("Move Native Minimap", "Apply saved position offsets to the verified native minimap RectTransform.",
                        () => MinimapSettings.MoveNative, v => { MinimapSettings.SetBool("MoveNative", v); RefreshMinimap(); });
                    SliderSetting("Minimap X Offset", -1300f, 1300f,
                        () => MinimapSettings.OffsetX, v => { MinimapSettings.SetFloat("OffsetX", v); RefreshMinimap(); }, " px");
                    SliderSetting("Minimap Y Offset", -1000f, 1000f,
                        () => MinimapSettings.OffsetY, v => { MinimapSettings.SetFloat("OffsetY", v); RefreshMinimap(); }, " px");
                    SliderSetting("Minimap Scale", 50f, 200f,
                        () => MinimapSettings.Scale, v => { MinimapSettings.SetFloat("Scale", v); RefreshMinimap(); }, "%");
                    ActionButton("Open Minimap Layout Editor", "Select MINIMAP in the main editor to drag, resize, snap, save/load or undo/redo.", "OPEN EDITOR",
                        () => { if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null)
                        { Close(); Plugin.Instance.UiRuntime.LayoutEditor.Open(); } });
                    ActionButton("Reset Minimap Layout", "Return the original map position and scale without changing other layouts.", "RESET",
                        () => { MinimapSettings.ResetLayout(); RefreshMinimap(); RenderPage(); });
                    Info("Layout Compatibility", "Older saved ArcaneUI layouts retain your current minimap placement. New layout saves include its position and scale.");
                    break;
                case "Native":
                    Section("NATIVE MINIMAP DIAGNOSTICS", "Only explicit HUD minimap candidates are accepted. Uncertain native roots are left untouched.");
                    Info("Minimap Binding / Shape", MinimapPresentation != null ? MinimapPresentation.Status : "Minimap module unavailable.");
                    Info("Native Path", MinimapPresentation != null ? MinimapPresentation.BoundPath : "Unbound");
                    ActionButton("Inspect Native Minimap", "Print candidate minimap hierarchy paths, geometry and scores to the log.", "INSPECT",
                        () => { if (MinimapPresentation != null) MinimapPresentation.Inspect(); RenderPage(); });
                    ActionButton("Restore Native Minimap", "Restore the original RectTransform and remove ArcaneUI frame decoration.", "RESTORE",
                        () => { MinimapSettings.ResetLayout(); MinimapSettings.SetBool("Enabled", false);
                        if (MinimapPresentation != null) MinimapPresentation.Restore(); RenderPage(); });
                    Info("Map Backend", "ArcaneUI mode draws an independent HUD using the live native map texture and dispatches button commands back to the game. Fog, world-map data, map rotation and marker generation remain Erenshor-owned. If a command cannot be located its ArcaneUI button is disabled.");
                    break;
                default:
                    Section("ARCANEUI MINIMAP", "Customize the clipping shape and frame of Erenshor's live HUD minimap when the native stencil mask can be safely controlled.");
                    ToggleSetting("Use Native Minimap", "When off, switch to ArcaneUI replacement using the live native map feed and five forwarded controls. If the replacement cannot bind, the native minimap remains visible to prevent losing the map.",
                        () => MinimapSettings.ShowNative, v => { MinimapSettings.SetBool("ShowNative", v); RefreshMinimap(); });
                    ToggleSetting("M Toggles Enlarged Minimap", "When the ArcaneUI replacement HUD is active, press M to enlarge or restore it. Disabled while typing or using an ArcaneUI editor.",
                        () => MinimapSettings.MKeyEnlarge, v => { MinimapSettings.SetBool("MKeyEnlarge", v); RefreshMinimap(); });
                    SliderSetting("Enlarged Minimap Size", 125f, 275f,
                        () => MinimapSettings.EnlargedScale, v => { MinimapSettings.SetFloat("EnlargedScale", v); RefreshMinimap(); }, "%");
                    ToggleSetting("Enable Minimap Skin", "Enable shape and border customization for the selected minimap display.",
                        () => MinimapSettings.Enabled, v => { MinimapSettings.SetBool("Enabled", v); RefreshMinimap(); });
                    ActionButton("Customize Minimap Appearance", "Borders, colors, opacity, shapes and inset.", "APPEARANCE",
                        () => { _tab = "Appearance"; RenderPage(); });
                    ActionButton("Edit Minimap Layout", "Precise X/Y offsets, scale, layout presets and editor dragging.", "LAYOUT",
                        () => { _tab = "Layout"; RenderPage(); });
                    break;
            }
        }
        private void RenderMinimapPreview()
        {
            if (_previewHost == null) return;
            ArcaneUiControls.ClearChildren(_previewHost);
            _previewHost.localScale = Vector3.one * .575f;
            _previewHost.sizeDelta = new Vector2(814f, 640f);
            Color accent = Theme.Accent;
            if (!MinimapSettings.InheritTheme)
                ColorUtility.TryParseHtmlString(MinimapSettings.BorderHex, out accent);
            var map = ArcaneUiControls.Panel(_previewHost, "MinimapSample", 185f, 70f, 440f, 440f,
                new Color(.11f, .2f, .21f, 1f), false);
            // The preview uses the same mask silhouette as the live native
            // stencil path, so its map content actually fills the selected shape.
            Image nativePreviewMask = map.GetComponent<Image>();
            if (nativePreviewMask != null)
            {
                nativePreviewMask.sprite = MinimapShapeSprites.Preview(MinimapSettings.Style);
                nativePreviewMask.type = Image.Type.Simple;
                nativePreviewMask.preserveAspect = false;
                // Preview uses the same mesh silhouette as the live mask.
                var mesh = map.GetComponent<MinimapMaskGeometry>();
                if (mesh == null) mesh = map.gameObject.AddComponent<MinimapMaskGeometry>();
                mesh.SetShape(MinimapSettings.Style);
                Mask sampleMask = map.GetComponent<Mask>();
                if (sampleMask == null) sampleMask = map.gameObject.AddComponent<Mask>();
                sampleMask.showMaskGraphic = true;
            }
            // Stylized placeholder terrain is used in the editor preview only;
            // gameplay always uses Erenshor's native minimap content.
            for (int i = 0; i < 12; ++i)
            {
                float x = 28f + (i * 73f) % 335f;
                float y = 36f + (i * 49f) % 336f;
                var tile = ArcaneUiControls.Panel(map, "SampleTerrain" + i, x, y,
                    35f + (i % 3) * 18f, 20f + (i % 4) * 20f,
                    i % 2 == 0 ? new Color(.26f, .32f, .25f, .9f) : new Color(.19f, .31f, .35f, .9f), false);
                Image tileImage = tile.GetComponent<Image>();
                if (tileImage != null) tileImage.raycastTarget = false;
            }
            ArcaneUiControls.Label(map, "Player", "+", 204f, 196f, 32f, 32f, 29, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var borderObject = new GameObject("PreviewFrame", typeof(RectTransform));
            var borderRect = borderObject.GetComponent<RectTransform>();
            // Sibling of the mask: border itself is not clipped by that mask.
            borderRect.SetParent(_previewHost, false);
            borderRect.anchorMin = map.anchorMin; borderRect.anchorMax = map.anchorMax;
            borderRect.pivot = map.pivot; borderRect.anchoredPosition = map.anchoredPosition;
            borderRect.sizeDelta = map.sizeDelta;
            var frame = borderObject.AddComponent<MinimapFrameGraphic>();
            frame.raycastTarget = false;
            frame.Shape = MinimapSettings.Style;
            frame.Thickness = MinimapSettings.ShowBorder ? MinimapSettings.BorderThickness : 0f;
            frame.Inset = MinimapSettings.Inset;
            frame.color = new Color(accent.r, accent.g, accent.b, MinimapSettings.BorderOpacity / 100f);
            ArcaneUiControls.Label(_previewHost, "Title", "MINIMAP / " + MinimapSettings.Style.ToUpperInvariant(),
                145f, 11f, 525f, 35f, 19, Theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            var clickable = ArcaneUiControls.Panel(map, "SelectAppearance", 0f, 0f, 440f, 440f, Color.clear, false);
            Image hit = clickable.GetComponent<Image>();
            LinkPreview(hit, "Appearance", "Minimap Frame Style");
            ArcaneUiControls.Label(_previewHost, "Note", "Preview terrain is illustrative; the live map uses its native stencil mask.",
                80f, 532f, 660f, 30f, 15, Theme.Text, TextAnchor.MiddleCenter);
        }
    }
}
