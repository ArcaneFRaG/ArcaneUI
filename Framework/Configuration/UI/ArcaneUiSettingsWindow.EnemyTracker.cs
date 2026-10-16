using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.EnemyTracker.Configuration;
using ArcanesGroupFrames.Modules.EnemyTracker.UI;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        // This is the SHARED right-hand live-preview host, never a second preview
        // created in the scrolling settings column.
        private RectTransform _enemyTrackerPreview;
        private bool _enemyTrackerPreviewRaid;

        private ArcaneUiEnemyTracker EnemyTrackerUi
        {
            get { return Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                ? Plugin.Instance.UiRuntime.EnemyTracker : null; }
        }
        private void RenderEnemyTracker()
        {
            if (_tab == "Appearance")
            {
                Section("ENEMY TRACKER STYLE", "Edit the native-backed tracker here. The interactive sample is in the shared Live Preview on the right; sample controls never issue game commands.");
                SliderSetting("Tracker Width", 320f, 650f, () => EnemyTrackerSettings.Width,
                    v => { EnemyTrackerSettings.SetWidth(v); RefreshEnemyTracker(); }, " px");
                SliderSetting("Enemy Row Height", 32f, 58f, () => EnemyTrackerSettings.RowHeight,
                    v => { EnemyTrackerSettings.SetHeight(v); RefreshEnemyTracker(); }, " px");
                SliderSetting("Enemy Row Spacing", 0f, 12f, () => EnemyTrackerSettings.RowSpacing,
                    v => { EnemyTrackerSettings.SetRowSpacing(v); RefreshEnemyTracker(); }, " px");
                SliderSetting("Maximum Rows", 2f, 14f, () => EnemyTrackerSettings.MaxRows,
                    v => { EnemyTrackerSettings.SetRows(v); RefreshEnemyTracker(); });
                DirectChoiceSetting("Enemy Sorting", "Native order is unchanged internally. Sort only ArcaneUI visual rows by selection, health, or name.",
                    EnemyTrackerSettings.SortingMode, EnemyTrackerSettings.SortingModes,
                    v => { EnemyTrackerSettings.SetSortingMode(v); RefreshEnemyTracker(); });
                SliderSetting("Health Bar Height", 2f, 14f, () => EnemyTrackerSettings.BarHeight,
                    v => { EnemyTrackerSettings.SetBarHeight(v); RefreshEnemyTracker(); }, " px");
                DirectChoiceSetting("Health Bar Texture", "Use the same procedural bar textures available for ArcaneUI unit frames.",
                    EnemyTrackerSettings.BarTexture, UnitFrameAppearance.TextureNames,
                    v => { EnemyTrackerSettings.SetBarTexture(v); RefreshEnemyTracker(); });
                DirectChoiceSetting("Border Style", "None / Thin / Accent / Glow; selected targets always receive an outline and stripe.",
                    EnemyTrackerSettings.BorderStyle, EnemyTrackerSettings.BorderStyles,
                    v => { EnemyTrackerSettings.SetBorderStyle(v); RefreshEnemyTracker(); });
                SliderSetting("Border Width", 1f, 4f, () => EnemyTrackerSettings.BorderWidth,
                    v => { EnemyTrackerSettings.SetBorderWidth(v); RefreshEnemyTracker(); }, " px");
                DirectChoiceSetting("Panel Background Style", "Solid follows background opacity; Soft is a subtle translucent backing; Clear shows only rows.",
                    EnemyTrackerSettings.BackgroundStyle, EnemyTrackerSettings.BackgroundStyles,
                    v => { EnemyTrackerSettings.SetBackgroundStyle(v); RefreshEnemyTracker(); });
                SliderSetting("Background Opacity", 0f, 100f, () => EnemyTrackerSettings.Opacity * 100f,
                    v => { EnemyTrackerSettings.SetOpacity(v); RefreshEnemyTracker(); }, " %");
                ToggleSetting("Show Panel Border", "Draw an optional outline around the entire tracker panel.",
                    () => EnemyTrackerSettings.PanelBorder, v => { EnemyTrackerSettings.SetPanelBorder(v); RefreshEnemyTracker(); });
                ToggleSetting("Highlight Current Target", "Outline and accent the real currently targeted enemy independently of command assignment highlighting.",
                    () => EnemyTrackerSettings.HighlightTarget, v => { EnemyTrackerSettings.SetHighlightTarget(v); RefreshEnemyTracker(); });
                ToggleSetting("Smooth Health Bars", "Animate health fill changes; values and low-health warnings remain authoritative native values.",
                    () => EnemyTrackerSettings.SmoothHealth, v => { EnemyTrackerSettings.SetSmoothHealth(v); RefreshEnemyTracker(); });
                SliderSetting("Health Animation Speed", 2f, 25f, () => EnemyTrackerSettings.HealthSmoothing,
                    v => { EnemyTrackerSettings.SetHealthSmoothing(v); RefreshEnemyTracker(); });
                ToggleSetting("Show Tracker Heading", "Toggle the heading to compact the tracker vertically.",
                    () => EnemyTrackerSettings.ShowTitle, v => { EnemyTrackerSettings.SetShowTitle(v); RefreshEnemyTracker(); });
                ActionButton("Reset Tracker Style", "Reset added visual, text, and colour overrides; preserve geometry, opacity, native hiding, and enablement.",
                    "RESET STYLE", () => { EnemyTrackerSettings.ResetStyle(); RefreshEnemyTracker(); RenderPage(); });
            }
            else if (_tab == "Text")
            {
                Section("ENEMY TRACKER TEXT", "All text settings apply to actual enemy names and command labels. Long names are truncated inside their row.");
                DirectChoiceSetting("Enemy Font", "Choose a font from ArcaneUI's existing font catalogue.",
                    EnemyTrackerSettings.FontFamily, UnitFrameAppearance.FontNames,
                    v => { EnemyTrackerSettings.SetFontFamily(v); RefreshEnemyTracker(); });
                SliderSetting("Enemy Name Font Size", 9f, 22f, () => EnemyTrackerSettings.TextSize,
                    v => { EnemyTrackerSettings.SetTextSize(v); RefreshEnemyTracker(); }, " px");
                SliderSetting("Command Font Size", 8f, 17f, () => EnemyTrackerSettings.ButtonTextSize,
                    v => { EnemyTrackerSettings.SetButtonTextSize(v); RefreshEnemyTracker(); }, " px");
                DirectChoiceSetting("Name Alignment", "Left / Center / Right alignment for the target button text.",
                    EnemyTrackerSettings.TextAlignment, EnemyTrackerSettings.Alignments,
                    v => { EnemyTrackerSettings.SetTextAlignment(v); RefreshEnemyTracker(); });
                DirectChoiceSetting("Health Number Display", "Percent / Raw / Both / Off. The health strip remains visible in every mode.",
                    EnemyTrackerSettings.HealthMode, EnemyTrackerSettings.HealthModes,
                    v => { EnemyTrackerSettings.SetHealthMode(v); RefreshEnemyTracker(); });
                ToggleSetting("Bold Enemy Names", "Apply bold styling to enemy labels.",
                    () => EnemyTrackerSettings.BoldNames, v => { EnemyTrackerSettings.SetBoldNames(v); RefreshEnemyTracker(); });
                ToggleSetting("Enemy Tracker Text Shadows", "Add contrast behind text when using a transparent panel.",
                    () => EnemyTrackerSettings.TextShadow, v => { EnemyTrackerSettings.SetTextShadow(v); RefreshEnemyTracker(); });
                Info("Live Preview", "The preview stays available at the right on every Enemy Tracker tab. Hover to highlight an element and click it to jump to its setting.");
            }
            else if (_tab == "Colours")
            {
                Section("ENEMY TRACKER COLOURS", "Use the colour picker for each independent tracker accent. The native party/raid state isn't modified.");
                TrackerHex("Tracker Background Colour", "BackgroundHex");
                TrackerHex("Normal Health Colour", "BarHex");
                TrackerHex("Low Health Colour", "LowHealthHex");
                TrackerHex("Selected Target Colour", "TargetHex");
                TrackerHex("Tracker Border Colour", "BorderHex");
                TrackerHex("Enemy Text Colour", "TextHex");
                TrackerHex("Inactive Command Colour", "CommandHex");
                TrackerHex("Active Command Colour", "ActiveCommandHex");
                TrackerHex("Party ATTACK Active Colour", "PartyActiveHex");
                TrackerHex("Raid G1 Active Colour", "G1ActiveHex");
                TrackerHex("Raid G2 Active Colour", "G2ActiveHex");
                TrackerHex("Raid G3 Active Colour", "G3ActiveHex");
                TrackerHex("Urgent Active Colour", "UrgentActiveHex");
                TrackerHex("Urgent Inactive Colour", "UrgentIdleHex");
                SliderSetting("Command Button Opacity", 20f, 100f, () => EnemyTrackerSettings.CommandOpacity * 100f,
                    v => { EnemyTrackerSettings.SetCommandOpacity(v); RefreshEnemyTracker(); }, " %");
                Info("Native Assignments", "These colours only indicate real native party aggression and raid group targets. The urgent button has its own default red styling.");
            }
            else if (_tab == "Visibility")
            {
                Section("ENEMY TRACKER VISIBILITY", "Changing visibility never disables native tracking or leaves Erenshor command rows suppressed.");
                DirectChoiceSetting("Tracker Visibility Mode", "Always / Combat Only / Mouseover Only / Hidden. Hidden keeps settings and layout saved.",
                    EnemyTrackerSettings.VisibilityMode, EnemyTrackerSettings.VisibilityModes,
                    v => { EnemyTrackerSettings.SetVisibilityMode(v); RefreshEnemyTracker(); });
                ToggleSetting("Show Tracker When Empty", "Keep the empty frame visible for layout editing and presentation.",
                    () => EnemyTrackerSettings.ShowEmpty, v => { EnemyTrackerSettings.SetShowEmpty(v); RefreshEnemyTracker(); });
                ToggleSetting("Show Native Overflow Notice", "Display a small count when the native tracker has more enemies than ArcaneUI can show. Native buttons remain available.",
                    () => EnemyTrackerSettings.ShowOverflow, v => { EnemyTrackerSettings.SetShowOverflow(v); RefreshEnemyTracker(); });
                Info("Native Tracker Safety", "When the custom tracker is hidden by any visibility mode, Erenshor's original enemy command rows are restored automatically. Mouseover detection uses screen geometry even when the backing is fully transparent.");
            }
            else if (_tab == "Layout")
            {
                Section("ENEMY TRACKER LAYOUT", "Position the panel via direct selection in Layout Editor, or set its X/Y from the top-right anchor.");
                SliderSetting("Horizontal Offset", -1920f, 0f, () => EnemyTrackerSettings.Position.x,
                    v => { EnemyTrackerSettings.SetPosition(new Vector2(v, EnemyTrackerSettings.Position.y)); RefreshEnemyTracker(); }, " px");
                SliderSetting("Vertical Offset", -1100f, 0f, () => EnemyTrackerSettings.Position.y,
                    v => { EnemyTrackerSettings.SetPosition(new Vector2(EnemyTrackerSettings.Position.x, v)); RefreshEnemyTracker(); }, " px");
                ActionButton("Edit Tracker Position", "Click the Enemy Tracker outline and use the attached X/Y fields.", "UNLOCK UI",
                    () => { Close(); Plugin.Instance.UiRuntime.LayoutEditor.Open(); });
                Info("Anchor", "Currently anchored to the upper-right corner; existing saved positions and Layout Editor behaviour are preserved.");
            }
            else if (_tab == "Native")
            {
                Section("NATIVE ENEMY TRACKER DISCOVERY", "Inspect in combat with Erenshor's original tracker available. Combat logic remains owned by native TrackedTarget.");
                Info("Binding", EnemyTrackerUi != null ? EnemyTrackerUi.Status : "Module unavailable");
                Info("Native Path", EnemyTrackerUi != null ? EnemyTrackerUi.NativePath : "Unbound");
                ActionButton("Inspect Native Enemy Tracker", "Log the native controller, real rows and associated enemies.", "INSPECT",
                    () => { if (EnemyTrackerUi != null) EnemyTrackerUi.Inspect(); RenderPage(); });
                Info("Fail-safe", "All combat commands remain native. The game rows are hidden only when ArcaneUI shows all active enemies; otherwise they are restored.");
            }
            else
            {
                Section("ENEMY TRACKER — NATIVE COMBAT INTEGRATION", "Real tracked enemies, native selection, party attack and raid G1/G2/G3/urgent orders. UI configuration never changes Sim AI.");
                ToggleSetting("Enable ArcaneUI Enemy Tracker", "Enable the visually customizable native-backed tracker.",
                    () => EnemyTrackerSettings.Enabled, v => { EnemyTrackerSettings.SetEnabled(v); RefreshEnemyTracker(); });
                ToggleSetting("Highlight Active Group Buttons", "Colour active G1/G2/G3/urgent and ATTACK command buttons. No extra labels are drawn above health bars.",
                    () => EnemyTrackerSettings.ShowAssignments, v => { EnemyTrackerSettings.SetShowAssignments(v); RefreshEnemyTracker(); });
                ToggleSetting("Hide Original Enemy Tracker", "Hide only original native row artwork while the ArcaneUI replacement is visible and complete.",
                    () => EnemyTrackerSettings.HideNative, v => { EnemyTrackerSettings.SetHideNative(v); RefreshEnemyTracker(); });
                Info("Native Binding", EnemyTrackerUi != null ? EnemyTrackerUi.Status : "Module unavailable");
                ActionButton("Inspect Original Enemy Tracker", "Log original TrackEngagedTargets and assigned rows in the current scene.", "INSPECT",
                    () => { if (EnemyTrackerUi != null) EnemyTrackerUi.Inspect(); _tab = "Native"; RenderPage(); });
                Info("Native Fallback", "The original tracker controller stays alive for commands and discovery. If ArcaneUI is hidden, incomplete or unbound, its row visual suppression is reversed.");
            }
        }

        private void TrackerHex(string label, string key)
        {
            HexSetting(label, "Independent tracker colour; stored with your existing ArcaneUI preferences.",
                () => TrackerHexValue(key), v => { EnemyTrackerSettings.SetHex(key, v); RefreshEnemyTracker(); });
        }
        private static string TrackerHexValue(string key)
        {
            switch (key)
            {
                case "BackgroundHex": return EnemyTrackerSettings.BackgroundHex;
                case "BarHex": return EnemyTrackerSettings.BarHex;
                case "LowHealthHex": return EnemyTrackerSettings.LowHealthHex;
                case "TargetHex": return EnemyTrackerSettings.TargetHex;
                case "BorderHex": return EnemyTrackerSettings.BorderHex;
                case "TextHex": return EnemyTrackerSettings.TextHex;
                case "CommandHex": return EnemyTrackerSettings.CommandHex;
                case "ActiveCommandHex": return EnemyTrackerSettings.ActiveCommandHex;
                case "PartyActiveHex": return EnemyTrackerSettings.PartyActiveHex;
                case "G1ActiveHex": return EnemyTrackerSettings.G1ActiveHex;
                case "G2ActiveHex": return EnemyTrackerSettings.G2ActiveHex;
                case "G3ActiveHex": return EnemyTrackerSettings.G3ActiveHex;
                case "UrgentActiveHex": return EnemyTrackerSettings.UrgentActiveHex;
                case "UrgentIdleHex": return EnemyTrackerSettings.UrgentIdleHex;
                default: return "#FFFFFF";
            }
        }
        private static Color TrackerColor(string hex, Color fallback)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : fallback;
        }
        // Visual-only sample placed in the shared right-hand LIVE PREVIEW host.
        // All editable parts use the same LinkPreview hover/click contract as
        // player, target, nameplates and FOTF resources. NO native callbacks.
        private void RenderEnemyTrackerPreview()
        {
            if (_previewHost == null) return;
            _previewHost.localScale = Vector3.one;
            _previewHost.sizeDelta = new Vector2(470f, 372f);
            _enemyTrackerPreview = _previewHost;
            RepaintEnemyTrackerPreview();
        }

        private void RepaintEnemyTrackerPreview()
        {
            if (_page != "enemytracker" || _previewHost == null ||
                _enemyTrackerPreview == null || !_previewHost.gameObject.activeSelf) return;

            // Destroyed preview objects may still have a hover subscription
            // until Unity's end of frame. Clear it before recreating hit areas.
            ClearPreviewHover();
            ArcaneUiControls.ClearChildren(_enemyTrackerPreview);
            Color text = TrackerColor(EnemyTrackerSettings.TextHex, Color.white);
            Color background = TrackerColor(EnemyTrackerSettings.BackgroundHex, new Color(.055f, .075f, .10f));
            Color border = TrackerColor(EnemyTrackerSettings.BorderHex, Color.white);
            Color target = TrackerColor(EnemyTrackerSettings.TargetHex, Color.yellow);
            Color normal = TrackerColor(EnemyTrackerSettings.BarHex, Color.green);
            Color low = TrackerColor(EnemyTrackerSettings.LowHealthHex, Color.red);

            ArcaneUiControls.Label(_enemyTrackerPreview, "PreviewNotice", "VISUAL-ONLY SAMPLE  •  NO COMBAT COMMANDS", 13f, 7f,
                446f, 22f, 11, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Button(_enemyTrackerPreview, "SamplePartyMode", "PARTY",
                13f, 37f, 217f, 32f, _enemyTrackerPreviewRaid ? Card : Theme.Accent,
                Color.white, () => { _enemyTrackerPreviewRaid = false; RepaintEnemyTrackerPreview(); }, 11);
            ArcaneUiControls.Button(_enemyTrackerPreview, "SampleRaidMode", "RAID",
                240f, 37f, 217f, 32f, _enemyTrackerPreviewRaid ? Theme.Accent : Card,
                Color.white, () => { _enemyTrackerPreviewRaid = true; RepaintEnemyTrackerPreview(); }, 11);

            // Preserve the actual tracker geometry, scaled only as required to
            // fit the 470px masked shared-preview viewport. Do not change the
            // in-game panel position or configured width to display the sample.
            float width = EnemyTrackerSettings.Width;
            float scale = Mathf.Min(1.10f, 438f / Mathf.Max(width, 1f));
            float height = EnemyTrackerSettings.RowHeight;
            float stride = height + EnemyTrackerSettings.RowSpacing;
            float firstTop = EnemyTrackerSettings.ShowTitle ? 41f : 9f;
            float frameHeight = firstTop + height + stride + 15f;
            var backing = ArcaneUiControls.Panel(_enemyTrackerPreview, "EnemyTrackerSample",
                (470f - width * scale) * .5f, 83f, width, frameHeight,
                EnemyTrackerVisualStyle.Alpha(background, EnemyTrackerVisualStyle.PanelAlpha), false);
            backing.localScale = Vector3.one * scale;
            LinkPreview(backing.GetComponent<Image>(), "Appearance", "Panel Background Style");
            if (EnemyTrackerSettings.PanelBorder && EnemyTrackerSettings.BorderStyle != "None")
            {
                var frameOutline = backing.gameObject.AddComponent<Outline>();
                frameOutline.useGraphicAlpha = false;
                frameOutline.effectColor = border;
                frameOutline.effectDistance = new Vector2(EnemyTrackerSettings.BorderWidth,
                    -EnemyTrackerSettings.BorderWidth);
            }
            if (EnemyTrackerSettings.ShowTitle)
            {
                var heading = ArcaneUiControls.Label(backing, "SampleHeading", "ENEMY TRACKER",
                    12f, 2f, width - 24f, 29f, 12, border, TextAnchor.MiddleLeft, FontStyle.Bold);
                heading.font = UnitFrameAppearance.ResolveFont(EnemyTrackerSettings.FontFamily);
                heading.raycastTarget = false;
                LinkPreview(heading, "Appearance", "Show Tracker Heading");
            }

            for (int i = 0; i < 2; i++)
            {
                // Preview follows the chosen sort for the two sample enemies.
                bool reverse = EnemyTrackerSettings.SortingMode == "Lowest Health" ||
                    EnemyTrackerSettings.SortingMode == "Name";
                bool selected = reverse ? i == 1 : i == 0;
                bool marked = EnemyTrackerVisualStyle.Highlight(selected);
                float hp = selected ? .73f : .22f;
                float top = firstTop + i * stride;
                float nameWidth = width - (_enemyTrackerPreviewRaid ? 172f : 88f);
                var row = ArcaneUiControls.Panel(backing, "EnemySelect_" + i,
                    9f, top, nameWidth, height - 4f,
                    EnemyTrackerVisualStyle.RowColor(selected), false);
                LinkPreview(row.GetComponent<Image>(), selected ? "Colours" : "Appearance",
                    selected ? "Selected Target Colour" : "Enemy Row Height");
                var outline = row.gameObject.AddComponent<Outline>();
                outline.useGraphicAlpha = false;
                outline.enabled = marked || EnemyTrackerSettings.BorderStyle != "None";
                outline.effectColor = marked ? target : border;
                float stroke = marked ? Mathf.Max(2f, EnemyTrackerSettings.BorderWidth) :
                    EnemyTrackerSettings.BorderStyle == "Glow" ? Mathf.Max(2f, EnemyTrackerSettings.BorderWidth) :
                    EnemyTrackerSettings.BorderWidth;
                outline.effectDistance = new Vector2(stroke, -stroke);

                if (marked)
                {
                    var stripe = ArcaneUiControls.Panel(row, "SelectedTargetStripe", 0f, 0f,
                        3f, height - 6f, target, false);
                    LinkPreview(stripe.GetComponent<Image>(), "Colours", "Selected Target Colour");
                }
                string hpText = EnemyTrackerSettings.HealthMode == "Off" ? "" :
                    EnemyTrackerSettings.HealthMode == "Raw" ? (selected ? "730/1000" : "220/1000") :
                    EnemyTrackerSettings.HealthMode == "Both" ? (selected ? "73% (730/1000)" : "22% (220/1000)") :
                    (selected ? "73%" : "22%");
                float barHeight = Mathf.Min(EnemyTrackerSettings.BarHeight, height - 21f);
                var name = ArcaneUiControls.Label(row, "EnemyName", (selected ? (marked ? "» Training Dummy  Lv35" : "Training Dummy  Lv35") :
                    "Ancient Arcanist  Lv36") + (hpText.Length > 0 ? "   " + hpText : ""),
                    8f, 2f, nameWidth - 16f, Mathf.Max(15f, height - barHeight - 9f),
                    Mathf.RoundToInt(EnemyTrackerSettings.TextSize), text,
                    EnemyTrackerSettings.TextAlignment == "Center" ? TextAnchor.MiddleCenter :
                    EnemyTrackerSettings.TextAlignment == "Right" ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                    EnemyTrackerSettings.BoldNames ? FontStyle.Bold : FontStyle.Normal);
                name.font = UnitFrameAppearance.ResolveFont(EnemyTrackerSettings.FontFamily);
                name.raycastTarget = false;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 9;
                name.resizeTextMaxSize = Mathf.RoundToInt(EnemyTrackerSettings.TextSize);
                if (EnemyTrackerSettings.TextShadow)
                {
                    var shadow = name.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0f, 0f, 0f, .8f);
                    shadow.effectDistance = new Vector2(1f, -1f);
                }
                LinkPreview(name, "Text", "Enemy Name Font Size");

                var healthBack = ArcaneUiControls.Panel(row, "HealthBackground", 5f,
                    height - barHeight - 4f, nameWidth - 10f, barHeight,
                    new Color(.05f, .065f, .08f, .94f), false);
                LinkPreview(healthBack.GetComponent<Image>(), "Appearance", "Health Bar Height");
                var healthFill = ArcaneUiControls.Panel(healthBack, "HealthFill", 0f, 0f,
                    (nameWidth - 10f) * hp, barHeight, hp < .3f ? low : normal, false);
                Image fillImage = healthFill.GetComponent<Image>();
                UnitFrameAppearance.ApplyBarTexture(fillImage, EnemyTrackerSettings.BarTexture);
                LinkPreview(fillImage, "Colours", hp < .3f ? "Low Health Colour" : "Normal Health Colour");

                if (_enemyTrackerPreviewRaid)
                {
                    for (int group = 0; group < 4; group++)
                    {
                        bool urgent = group == 3;
                        bool active = EnemyTrackerSettings.ShowAssignments &&
                            (selected ? group == 0 : group == 1 || group == 3);
                        Color buttonColor = EnemyTrackerVisualStyle.CommandColor(group + 2, active);
                        string label = urgent ? "!" : "G" + (group + 1);
                        float x = width - 160f + group * 38f;
                        float buttonWidth = urgent ? 36f : 35f;
                        var command = ArcaneUiControls.Panel(backing,
                            "RaidCommand_" + i + "_" + group, x, top,
                            buttonWidth, height - 4f, buttonColor, false);
                        var caption = ArcaneUiControls.Label(command, "CommandLabel", label,
                            0f, 0f, buttonWidth, height - 4f,
                            Mathf.RoundToInt(EnemyTrackerSettings.ButtonTextSize), text,
                            TextAnchor.MiddleCenter, FontStyle.Bold);
                        caption.font = UnitFrameAppearance.ResolveFont(EnemyTrackerSettings.FontFamily);
                        caption.raycastTarget = false;
                        string colourSetting = urgent ? (active ? "Urgent Active Colour" : "Urgent Inactive Colour") :
                            active ? "Raid G" + (group + 1) + " Active Colour" : "Inactive Command Colour";
                        LinkPreview(command.GetComponent<Image>(), "Colours", colourSetting);
                    }
                }
                else
                {
                    bool active = EnemyTrackerSettings.ShowAssignments && selected;
                    var command = ArcaneUiControls.Panel(backing, "PartyCommand_" + i,
                        width - 74f, top, 64f, height - 4f,
                        EnemyTrackerVisualStyle.CommandColor(1, active), false);
                    var caption = ArcaneUiControls.Label(command, "CommandLabel", "ATTACK",
                        0f, 0f, 64f, height - 4f,
                        Mathf.RoundToInt(EnemyTrackerSettings.ButtonTextSize), text,
                        TextAnchor.MiddleCenter, FontStyle.Bold);
                    caption.font = UnitFrameAppearance.ResolveFont(EnemyTrackerSettings.FontFamily);
                    caption.raycastTarget = false;
                    LinkPreview(command.GetComponent<Image>(), "Colours", active ?
                        "Party ATTACK Active Colour" : "Inactive Command Colour");
                }
            }

            // Small settings shortcuts cover options (such as texture) that
            // have no separate hit area inside the same compact health bar.
            string[] quickNames = { "SORT", "HP NUMBERS", "BORDERS", "VISIBILITY" };
            string[] quickTabs = { "Appearance", "Text", "Appearance", "Visibility" };
            string[] quickSettings = { "Enemy Sorting", "Health Number Display",
                "Border Style", "Tracker Visibility Mode" };
            for (int q = 0; q < quickNames.Length; ++q)
            {
                var quick = ArcaneUiControls.Panel(_enemyTrackerPreview, "QuickSetting_" + q,
                    13f + q * 113f, 292f, 105f, 25f, Card, false);
                ArcaneUiControls.Label(quick, "Label", quickNames[q], 2f, 0f, 101f, 25f,
                    10, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                LinkPreview(quick.GetComponent<Image>(), quickTabs[q], quickSettings[q]);
            }
            // Texture/smoothing controls remain reachable without making the
            // sample health fill send commands or take over its colour hotspot.
            var textureLink = ArcaneUiControls.Panel(_enemyTrackerPreview, "TextureShortcut",
                13f, 322f, 217f, 22f, Card, false);
            ArcaneUiControls.Label(textureLink, "Label", "BAR TEXTURE / ANIMATION", 2f, 0f, 213f,
                22f, 10, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold).raycastTarget = false;
            LinkPreview(textureLink.GetComponent<Image>(), "Appearance", "Health Bar Texture");
            var nativeOverflowLink = ArcaneUiControls.Panel(_enemyTrackerPreview, "OverflowShortcut",
                240f, 322f, 217f, 22f, Card, false);
            ArcaneUiControls.Label(nativeOverflowLink, "Label", "ROW LIMIT / OVERFLOW", 2f, 0f, 213f,
                22f, 10, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold).raycastTarget = false;
            LinkPreview(nativeOverflowLink.GetComponent<Image>(), "Appearance", "Maximum Rows");
            ArcaneUiControls.Label(_enemyTrackerPreview, "SampleHint",
                "Hover to highlight  •  Click an element or shortcut to edit its setting",
                14f, 345f, 442f, 21f, 10, Muted, TextAnchor.MiddleCenter);
            SetPreviewHint(_contextStatusLabel,
                "Preview is visual-only. Hover to highlight; click a tracker element to edit.");
        }
        private void RefreshEnemyTracker()
        {
            if (EnemyTrackerUi != null) { EnemyTrackerUi.ApplyLayout(); EnemyTrackerUi.RefreshVisibility(); }
            RepaintEnemyTrackerPreview();
        }
    }
}
