using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.Nameplates.Configuration;
using ArcanesGroupFrames.Modules.Nameplates.UI;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    /// <summary>
    /// A lightweight, always-visible mock nameplate in the settings window.
    /// It uses the same health-fill calculations and saved preferences as the
    /// world-space renderer, but cannot alter a real Character or the native UI.
    /// </summary>
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private RectTransform _nameplateSampleRoot;
        private RectTransform _nameplateSampleFill;
        private Image _nameplateSampleBackground;
        private Image _nameplateSampleHealth;
        private Outline _nameplateSampleTargetOutline;
        private Text _nameplateSampleName;
        private Text _nameplateSamplePercent;
        private Text _nameplateSampleStatus;

        private void RenderNameplatePreview()
        {
            if (_previewHost == null) return;
            // The shared preview normally uses a fractional (.575) scale for
            // legacy frame samples. Native-size nameplate controls avoid its
            // fractional glyph rasterisation and look sharper at screen pixels.
            _previewHost.localScale = Vector3.one;
            _previewHost.sizeDelta = new Vector2(470f, 372f);
            // The parent is already masked; only one sample is constructed.
            // Changing a setting updates these existing components in place.
            ArcaneUiControls.ClearChildren(_previewHost);

            string[] modes = { "HOSTILE", "FRIENDLY", "SIMPLAYER", "TARGET" };
            for (int i = 0; i < modes.Length; ++i)
            {
                int selected = i;
                ArcaneUiControls.Button(_previewHost, "PreviewMode_" + modes[i], modes[i],
                    9f + i * 114f, 13f, 105f, 44f,
                    _nameplatePreviewCategory == i ? Theme.Accent : Theme.Panel,
                    Theme.Text, () =>
                    {
                        _nameplatePreviewCategory = selected;
                        RenderNameplatePreview();
                    }, 14);
            }

            _nameplateSampleRoot = ArcaneUiControls.Rect(_previewHost, "NameplateSample", 0f, 0f, 150f, 50f);
            // Centre anchoring keeps all supported nameplate widths centred.
            _nameplateSampleRoot.anchorMin = new Vector2(.5f, 1f);
            _nameplateSampleRoot.anchorMax = new Vector2(.5f, 1f);
            _nameplateSampleRoot.pivot = new Vector2(.5f, .5f);

            _nameplateSampleName = ArcaneUiControls.Label(_nameplateSampleRoot, "Name", "",
                0f, 0f, 150f, 22f, 12, Color.white, TextAnchor.MiddleCenter);
            _nameplateSampleName.horizontalOverflow = HorizontalWrapMode.Overflow;
            var nameOutline = _nameplateSampleName.gameObject.AddComponent<Outline>();
            nameOutline.effectColor = Color.black;
            nameOutline.effectDistance = new Vector2(1f, -1f);
            nameOutline.useGraphicAlpha = false;

            RectTransform bg = ArcaneUiControls.Panel(_nameplateSampleRoot, "HealthBackground",
                0f, 23f, 150f, 13f, Color.black, false);
            _nameplateSampleBackground = bg.GetComponent<Image>();
            _nameplateSampleTargetOutline = bg.gameObject.AddComponent<Outline>();
            _nameplateSampleTargetOutline.useGraphicAlpha = false;
            _nameplateSampleTargetOutline.enabled = false;

            _nameplateSampleFill = ArcaneUiControls.Panel(bg, "HealthFill", 0f, 0f, 150f, 13f,
                Color.red, false);
            _nameplateSampleHealth = _nameplateSampleFill.GetComponent<Image>();
            _nameplateSampleFill.anchorMin = Vector2.zero;
            _nameplateSampleFill.anchorMax = Vector2.one;
            _nameplateSampleFill.offsetMin = Vector2.zero;
            _nameplateSampleFill.offsetMax = Vector2.zero;
            _nameplateSampleFill.pivot = new Vector2(0f, .5f);

            _nameplateSamplePercent = ArcaneUiControls.Label(bg, "HealthPercent", "63%",
                0f, 0f, 150f, 13f, 11, Color.white, TextAnchor.MiddleCenter);
            var percentOutline = _nameplateSamplePercent.gameObject.AddComponent<Outline>();
            percentOutline.effectColor = Color.black;
            percentOutline.effectDistance = new Vector2(1f, -1f);
            percentOutline.useGraphicAlpha = false;

            _nameplateSampleStatus = ArcaneUiControls.Label(_previewHost, "VisibilityStatus", "",
                28f, 311f, 414f, 30f, 15, Muted, TextAnchor.MiddleCenter);
            UpdateNameplatePreview();
        }

        private void UpdateNameplatePreview()
        {
            if (_page != "nameplates" || _previewHost == null || !_previewHost.gameObject.activeSelf ||
                _nameplateSampleRoot == null || _nameplateSampleFill == null) return;

            bool sim = _nameplatePreviewCategory == 2;
            bool friendly = _nameplatePreviewCategory == 1;
            bool targeted = _nameplatePreviewCategory == 3;
            string sampleName = sim ? "Emberweaver SimPlayer" : friendly ? "Friendly Wanderer" :
                targeted ? "Targeted Enemy" : "Blighted Marauder";
            int limit = NameplateSettings.NameCharacterLimit;
            if (sampleName.Length > limit) sampleName = sampleName.Substring(0, limit - 1) + "…";
            Color fill = sim
                ? (NameplateSettings.SimClassColours ? RaidClassColors.GetColor("Emberweaver") :
                    ArcaneUiOverheadNameplates.ParseColour(NameplateSettings.SimColour, Color.blue))
                : friendly
                    ? ArcaneUiOverheadNameplates.ParseColour(NameplateSettings.FriendlyColour, Color.green)
                    : ArcaneUiOverheadNameplates.ParseColour(NameplateSettings.HostileColour, Color.red);

            float width = NameplateSettings.Width;
            float height = NameplateSettings.Height;
            // Render expanded geometry at its actual resolution instead of
            // enlarging a tiny font/image hierarchy with localScale. This
            // keeps the UI Text glyphs crisp and sample buttons readable.
            float targetZoom = targeted ? NameplateSettings.TargetScale / 100f : 1f;
            float zoom = Mathf.Min(2.35f, 410f / Mathf.Max(1f, width * targetZoom));
            float displayWidth = width * zoom * targetZoom;
            float displayHeight = height * zoom * targetZoom;
            float nameHeight = 27f * zoom * targetZoom;
            _nameplateSampleRoot.localScale = Vector3.one;
            _nameplateSampleRoot.sizeDelta = new Vector2(displayWidth, displayHeight + nameHeight);
            _nameplateSampleRoot.anchoredPosition = new Vector2(0f,
                -181f + NameplateSettings.HeightOffset * 9f);
            _nameplateSampleName.rectTransform.sizeDelta = new Vector2(displayWidth, nameHeight);
            _nameplateSampleName.text = sampleName;
            _nameplateSampleName.fontSize = Mathf.Clamp(
                Mathf.RoundToInt(NameplateSettings.NameFontSize * zoom * targetZoom), 16, 52);
            _nameplateSampleName.color = targeted ? Color.white : sim && NameplateSettings.SimClassColours
                ? fill : ArcaneUiOverheadNameplates.ParseColour(NameplateSettings.NameColour, Color.white);
            _nameplateSampleName.gameObject.SetActive(NameplateSettings.ShowNames);

            var backgroundRect = _nameplateSampleBackground.rectTransform;
            backgroundRect.anchoredPosition = new Vector2(0f, -nameHeight);
            backgroundRect.sizeDelta = new Vector2(displayWidth, displayHeight);
            _nameplateSampleBackground.color = new Color(.065f, .075f, .09f,
                NameplateSettings.BackgroundOpacity / 100f);
            _nameplateSampleHealth.color = fill;
            ArcaneUiOverheadNameplates.SetHealthFill(_nameplateSampleFill, .63f,
                NameplateSettings.HealthDirection);
            _nameplateSamplePercent.gameObject.SetActive(NameplateSettings.ShowHealthPercent);
            _nameplateSamplePercent.rectTransform.sizeDelta = new Vector2(displayWidth, displayHeight);
            _nameplateSamplePercent.fontSize = Mathf.Clamp(
                Mathf.RoundToInt(NameplateSettings.PercentFontSize * zoom * targetZoom), 16, 52);
            _nameplateSamplePercent.text = "63%";

            _nameplateSampleTargetOutline.enabled = targeted && NameplateSettings.ShowTargetOutline;
            if (_nameplateSampleTargetOutline.enabled)
            {
                _nameplateSampleTargetOutline.effectColor = ArcaneUiOverheadNameplates.ParseColour(
                    NameplateSettings.TargetColour, Color.yellow);
                float thickness = NameplateSettings.TargetOutlineSize;
                _nameplateSampleTargetOutline.effectDistance =
                    new Vector2(thickness * zoom * targetZoom, -thickness * zoom * targetZoom);
            }

            // The simulated sample always remains visible in settings, even
            // when game-world nameplates are disabled. The status explains
            // whether its category would render in game.
            string hidden = !NameplateSettings.Enabled ? "Nameplates disabled in-game" :
                sim && !NameplateSettings.ShowSims ? "SimPlayers hidden in-game" :
                !sim && !NameplateSettings.ShowNpcs ? "NPCs hidden in-game" :
                friendly && !NameplateSettings.ShowFriendly ? "Friendlies hidden in-game" :
                "Visible in-game";
            _nameplateSampleStatus.text = hidden;
        }
    }
}
