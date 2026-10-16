using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Configuration.UI;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Modules.EnemyTracker.Configuration;
using ArcanesGroupFrames.Modules.EnemyTracker.Native;
using ArcanesGroupFrames.Modules.UnitFrames.Target;

namespace ArcanesGroupFrames.Modules.EnemyTracker.UI
{
    /// <summary>
    /// A visual-only view of Erenshor's real TrackEngagedTargets rows.
    /// TrackedTarget retains selection and all party / raid command execution.
    /// The original remains available whenever this view is insufficient.
    /// </summary>
    internal sealed class ArcaneUiEnemyTracker
    {
        private sealed class RowView
        {
            internal Button Select, Party, Raid1, Raid2, Raid3, Urgent;
            internal Text Caption;
            internal Image HealthBack, HealthFill, TargetStripe;
            internal Outline Border;
            internal NativeEnemyTrackerBridge.Entry Bound;
            internal Character AnimatedEnemy;
            internal float DisplayedHealth;
        }

        private readonly ArcaneCanvasManager _canvas;
        private readonly NativeEnemyTrackerBridge _native = new NativeEnemyTrackerBridge();
        private RectTransform _layer, _panel;
        private CanvasGroup _visibility;
        private Text _title, _overflow;
        private Outline _panelBorder;
        private RowView[] _rows;
        private readonly List<NativeEnemyTrackerBridge.Entry> _display = new List<NativeEnemyTrackerBridge.Entry>(20);
        private string _lastTexture;
        private float _lastHealthUpdate;
        private float _nextRefresh, _nextFailureLog;
        private float _nextCombatProbe;
        private bool _lastCombat;

        internal string Status { get { return _native.Status; } }
        internal string NativePath { get { return _native.Path; } }
        internal RectTransform EditorRect { get { return EnemyTrackerSettings.Enabled ? _panel : null; } }
        internal ArcaneUiEnemyTracker(ArcaneCanvasManager canvas) { _canvas = canvas; }
        internal void Inspect() { _native.Inspect(); _nextRefresh = 0f; }
        internal void RefreshVisibility()
        {
            if (!EnemyTrackerSettings.Enabled || !EnemyTrackerSettings.HideNative ||
                EnemyTrackerSettings.VisibilityMode == "Hidden") _native.RestoreNative();
            _nextRefresh = 0f;
        }
        internal void RestoreNative() { _native.RestoreNative(); }

        // All visibility changes, including mouseout and leaving combat, fail
        // open immediately rather than waiting for the native polling interval.
        private bool AllowedByMode()
        {
            string mode = EnemyTrackerSettings.VisibilityMode;
            if (mode == "Hidden") return false;
            if (mode == "Mouseover Only")
                return _panel != null && RectTransformUtility.RectangleContainsScreenPoint(
                    _panel, Input.mousePosition, null);
            if (mode == "Combat Only")
            {
                // Cache the reflection-backed native combat flag; do not probe
                // each rendered frame in a busy raid.
                if (Time.unscaledTime >= _nextCombatProbe)
                {
                    _nextCombatProbe = Time.unscaledTime + .15f;
                    bool inCombat;
                    _lastCombat = GameData.PlayerStats != null &&
                        TargetNativeReadAdapter.TryReadCombat(GameData.PlayerStats, out inCombat)
                        ? inCombat : _native.ActiveNativeEnemyCount > 0;
                }
                return _lastCombat;
            }
            return true;
        }

        internal void Update()
        {
            try
            {
                if (_canvas == null || !_canvas.IsReady)
                {
                    _native.RestoreNative(); return;
                }
                if (_panel == null) Build();
                if (_panel == null) { _native.RestoreNative(); return; }
                bool gameplay = EnemyTrackerSettings.Enabled && !GameData.InCharSelect &&
                    NativeUiLayering.CanShowGameplayUi && GameData.PlayerControl != null;
                if (_layer.gameObject.activeSelf != gameplay) _layer.gameObject.SetActive(gameplay);
                if (!gameplay) { _native.RestoreNative(); return; }

                // Apply updated settings before hover hit tests (width/position
                // can be edited while the tracker is mouseover-only).
                bool modeVisible = AllowedByMode();
                if (_visibility != null)
                {
                    _visibility.alpha = modeVisible ? 1f : 0f;
                    _visibility.interactable = modeVisible;
                    _visibility.blocksRaycasts = modeVisible;
                }
                if (!modeVisible || !_panel.gameObject.activeInHierarchy)
                    _native.RestoreNative();

                if (Time.unscaledTime < _nextRefresh) return;
                _nextRefresh = Time.unscaledTime + .18f;
                _native.Update();
                ApplyLayout();
                bool raid = _native.RaidActive;
                _display.Clear();
                foreach (NativeEnemyTrackerBridge.Entry e in _native.Entries) _display.Add(e);
                if (EnemyTrackerSettings.SortingMode != "Native Order") _display.Sort(CompareEntries);
                int count = Mathf.Min(_rows.Length, EnemyTrackerSettings.MaxRows, _display.Count);
                float dt = _lastHealthUpdate > 0f ? Mathf.Clamp(Time.unscaledTime - _lastHealthUpdate, 0f, .5f) : .18f;
                _lastHealthUpdate = Time.unscaledTime;
                float width = EnemyTrackerSettings.Width;
                float h = EnemyTrackerSettings.RowHeight;
                float topStart = EnemyTrackerSettings.ShowTitle ? 41f : 9f;
                float stride = h + EnemyTrackerSettings.RowSpacing;
                Color target = Tint(EnemyTrackerSettings.TargetHex, new Color(.85f, .72f, .42f));
                Color text = Tint(EnemyTrackerSettings.TextHex, Color.white);
                Color fill = Tint(EnemyTrackerSettings.BarHex, new Color(.27f, .69f, .43f));
                Color low = Tint(EnemyTrackerSettings.LowHealthHex, new Color(.85f, .26f, .25f));
                for (int i = 0; i < _rows.Length; ++i)
                {
                    RowView view = _rows[i];
                    bool show = i < count;
                    view.Select.gameObject.SetActive(show);
                    view.Party.gameObject.SetActive(show && !raid);
                    view.Raid1.gameObject.SetActive(show && raid);
                    view.Raid2.gameObject.SetActive(show && raid);
                    view.Raid3.gameObject.SetActive(show && raid);
                    view.Urgent.gameObject.SetActive(show && raid);
                    view.Bound = show ? _display[i] : null;
                    if (!show) continue;

                    NativeEnemyTrackerBridge.Entry entry = view.Bound;
                    float nameWidth = width - (raid ? 172f : 88f);
                    float top = topStart + i * stride;
                    RectTransform selectRect = (RectTransform)view.Select.transform;
                    selectRect.anchoredPosition = new Vector2(9f, -top);
                    selectRect.sizeDelta = new Vector2(nameWidth, h - 4f);
                    string health = EnemyTrackerSettings.HealthMode == "Raw" ?
                        entry.Health + "/" + entry.MaxHealth :
                        EnemyTrackerSettings.HealthMode == "Both" ?
                        Mathf.RoundToInt(100f * entry.HealthFraction) + "% (" + entry.Health + "/" + entry.MaxHealth + ")" :
                        EnemyTrackerSettings.HealthMode == "Off" ? "" :
                        Mathf.RoundToInt(100f * entry.HealthFraction) + "%";
                    view.Caption.text = (EnemyTrackerVisualStyle.Highlight(entry.Selected) ? "» " : "") + entry.Name + "  Lv" + entry.Level +
                        (health.Length > 0 ? "   " + health : "");
                    view.Caption.color = text;
                    view.Caption.font = UnitFrameAppearance.ResolveFont(EnemyTrackerSettings.FontFamily);
                    view.Caption.fontSize = PlayerPrefs.HasKey("ArcaneUI.EnemyTracker.TextSize")
                        ? Mathf.RoundToInt(EnemyTrackerSettings.TextSize) :
                        ArcaneUiGlobalTypography.Scale(Mathf.RoundToInt(EnemyTrackerSettings.TextSize));
                    ArcaneUiGlobalTypography.ApplyTextStyle(view.Caption);
                    ArcaneUiGlobalTypography.ApplyFont(view.Caption,
                        !string.IsNullOrEmpty(EnemyTrackerSettings.FontFamily) &&
                        EnemyTrackerSettings.FontFamily != "Default");
                    view.Caption.resizeTextMaxSize = view.Caption.fontSize;
                    view.Caption.resizeTextMinSize = 9;
                    view.Caption.fontStyle = EnemyTrackerSettings.BoldNames ? FontStyle.Bold : FontStyle.Normal;
                    view.Caption.alignment = EnemyTrackerSettings.TextAlignment == "Center" ? TextAnchor.MiddleCenter :
                        EnemyTrackerSettings.TextAlignment == "Right" ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                    RectTransform captionRect = view.Caption.rectTransform;
                    captionRect.anchoredPosition = new Vector2(8f, -2f);
                    captionRect.sizeDelta = new Vector2(nameWidth - 16f,
                        Mathf.Max(15f, h - EnemyTrackerSettings.BarHeight - 9f));
                    SetShadow(view.Caption, EnemyTrackerSettings.TextShadow);

                    bool targetHighlighted = EnemyTrackerVisualStyle.Highlight(entry.Selected);
                    Image background = view.Select.GetComponent<Image>();
                    if (background != null) background.color = EnemyTrackerVisualStyle.RowColor(entry.Selected);
                    view.TargetStripe.gameObject.SetActive(targetHighlighted);
                    view.TargetStripe.color = target;
                    view.TargetStripe.rectTransform.sizeDelta = new Vector2(3f, h - 6f);
                    ApplyOutline(view.Border, targetHighlighted, target);

                    float bh = Mathf.Min(EnemyTrackerSettings.BarHeight, h - 21f);
                    RectTransform barRect = view.HealthBack.rectTransform;
                    barRect.anchoredPosition = new Vector2(5f, -(h - bh - 4f));
                    barRect.sizeDelta = new Vector2(nameWidth - 10f, bh);
                    view.HealthBack.color = new Color(.05f, .065f, .08f, .94f);
                    // Never animate across two different native enemy bindings.
                    // The colour and numeric HP always use the actual native value.
                    if (view.AnimatedEnemy != entry.Enemy || !EnemyTrackerSettings.SmoothHealth)
                        view.DisplayedHealth = entry.HealthFraction;
                    else
                        view.DisplayedHealth = Mathf.Lerp(view.DisplayedHealth, entry.HealthFraction,
                            1f - Mathf.Exp(-EnemyTrackerSettings.HealthSmoothing * dt));
                    view.AnimatedEnemy = entry.Enemy;
                    view.HealthFill.rectTransform.sizeDelta = new Vector2(
                        Mathf.Max(0f, (nameWidth - 10f) * view.DisplayedHealth), bh);
                    view.HealthFill.color = WithAlpha(entry.HealthFraction < .3f ? low : fill, .96f);

                    bool highlight = EnemyTrackerSettings.ShowAssignments;
                    SetAssignmentButton(view.Raid1, 2, highlight && (entry.RaidGroupMask & 1) != 0);
                    SetAssignmentButton(view.Raid2, 3, highlight && (entry.RaidGroupMask & 2) != 0);
                    SetAssignmentButton(view.Raid3, 4, highlight && (entry.RaidGroupMask & 4) != 0);
                    SetAssignmentButton(view.Urgent, 5, highlight && entry.Urgent);
                    SetAssignmentButton(view.Party, 1, highlight && entry.PartyAttackers > 0);
                    StyleCommandText(view.Party);
                    StyleCommandText(view.Raid1); StyleCommandText(view.Raid2);
                    StyleCommandText(view.Raid3); StyleCommandText(view.Urgent);
                    SetButton(view.Party, width - 74f, top, 64f, h - 4f);
                    SetButton(view.Raid1, width - 160f, top, 35f, h - 4f);
                    SetButton(view.Raid2, width - 122f, top, 35f, h - 4f);
                    SetButton(view.Raid3, width - 84f, top, 35f, h - 4f);
                    SetButton(view.Urgent, width - 46f, top, 36f, h - 4f);
                }
                int overflow = Mathf.Max(0, _native.ActiveNativeEnemyCount - count);
                // Reserve a footer only when required. Do not leave a permanent
                // empty band under the normal in-game tracker.
                float baseHeight = topStart + EnemyTrackerSettings.MaxRows * h +
                    Mathf.Max(0, EnemyTrackerSettings.MaxRows - 1) * EnemyTrackerSettings.RowSpacing + 4f;
                _panel.sizeDelta = new Vector2(width,
                    baseHeight + (EnemyTrackerSettings.ShowOverflow && overflow > 0 ? 20f : 0f));
                if (_overflow != null)
                {
                    _overflow.gameObject.SetActive(EnemyTrackerSettings.ShowOverflow && overflow > 0);
                    if (overflow > 0)
                    {
                        _overflow.text = "+" + overflow + " additional tracked enemies — native commands preserved";
                        _overflow.color = Tint(EnemyTrackerSettings.TextHex, Color.white);
                        _overflow.font = UnitFrameAppearance.ResolveFont(EnemyTrackerSettings.FontFamily);
                        _overflow.fontSize = Mathf.RoundToInt(Mathf.Min(EnemyTrackerSettings.TextSize, 11f));
                        _overflow.rectTransform.anchoredPosition = new Vector2(11f, -(baseHeight + 1f));
                        _overflow.rectTransform.sizeDelta = new Vector2(width - 22f, 18f);
                    }
                }
                bool panelReady = count > 0 || EnemyTrackerSettings.ShowEmpty;
                _panel.gameObject.SetActive(panelReady);
                // Never suppress native command rows when ArcaneUI is hidden,
                // clipped, empty, or cannot represent ALL native tracked enemies.
                bool mayHide = modeVisible && EnemyTrackerSettings.HideNative &&
                    _native.Bound && panelReady && _layer.gameObject.activeInHierarchy &&
                    _native.ActiveNativeEnemyCount > 0 && _native.ActiveNativeEnemyCount <= count;
                _native.SetNativeHidden(mayHide);
            }
            catch (System.Exception ex)
            {
                _native.RestoreNative();
                if (Time.unscaledTime >= _nextFailureLog)
                {
                    _nextFailureLog = Time.unscaledTime + 5f;
                    Plugin.LogWarning("[ArcaneUI Enemy Tracker] UI refresh failed; native rows restored: " + ex.Message);
                }
            }
        }

        private static Color Tint(string hex, Color fallback)
        {
            Color result;
            return ColorUtility.TryParseHtmlString(hex, out result) ? result : fallback;
        }
        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
        private static void SetShadow(Text text, bool enabled)
        {
            if (text == null) return;
            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null && enabled) shadow = text.gameObject.AddComponent<Shadow>();
            if (shadow != null)
            {
                shadow.enabled = enabled;
                shadow.effectColor = new Color(0f, 0f, 0f, .82f);
                shadow.effectDistance = new Vector2(1f, -1f);
            }
        }
        private static void ApplyOutline(Outline outline, bool selected, Color target)
        {
            if (outline == null) return;
            string style = EnemyTrackerSettings.BorderStyle;
            outline.enabled = selected || style != "None";
            outline.useGraphicAlpha = false;
            outline.effectColor = selected ? WithAlpha(target, .98f) :
                WithAlpha(Tint(EnemyTrackerSettings.BorderHex, target), style == "Thin" ? .55f : .84f);
            float size = selected ? Mathf.Max(2f, EnemyTrackerSettings.BorderWidth) :
                style == "Glow" ? Mathf.Max(2f, EnemyTrackerSettings.BorderWidth) : EnemyTrackerSettings.BorderWidth;
            outline.effectDistance = new Vector2(size, -size);
        }
        private static void SetAssignmentButton(Button button, int command, bool assigned)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = EnemyTrackerVisualStyle.CommandColor(command, assigned);
        }

        private static int CompareEntries(NativeEnemyTrackerBridge.Entry a, NativeEnemyTrackerBridge.Entry b)
        {
            int comparison = 0;
            string mode = EnemyTrackerSettings.SortingMode;
            if (mode == "Selected First") comparison = b.Selected.CompareTo(a.Selected);
            else if (mode == "Lowest Health") comparison = a.HealthFraction.CompareTo(b.HealthFraction);
            else if (mode == "Name") comparison = string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
            // Native list order is the stable tie-breaker. Sorting NEVER
            // reorders the native tracking objects or changes raid assignments.
            return comparison != 0 ? comparison : a.NativeOrder.CompareTo(b.NativeOrder);
        }
        private static void SetButton(Button b, float x, float top, float width, float height)
        {
            RectTransform rect = b.transform as RectTransform;
            if (rect != null) { rect.anchoredPosition = new Vector2(x, -top); rect.sizeDelta = new Vector2(width, height); }
        }
        private static void StyleCommandText(Button button)
        {
            if (button == null) return;
            Text label = button.GetComponentInChildren<Text>();
            if (label == null) return;
            label.font = UnitFrameAppearance.ResolveFont(EnemyTrackerSettings.FontFamily);
            label.fontSize = PlayerPrefs.HasKey("ArcaneUI.EnemyTracker.ButtonTextSize")
                ? Mathf.RoundToInt(EnemyTrackerSettings.ButtonTextSize) :
                ArcaneUiGlobalTypography.Scale(Mathf.RoundToInt(EnemyTrackerSettings.ButtonTextSize));
            ArcaneUiGlobalTypography.ApplyTextStyle(label);
            ArcaneUiGlobalTypography.ApplyFont(label,
                !string.IsNullOrEmpty(EnemyTrackerSettings.FontFamily) &&
                EnemyTrackerSettings.FontFamily != "Default");
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 8;
            label.resizeTextMaxSize = label.fontSize;
            label.color = Tint(EnemyTrackerSettings.TextHex, Color.white);
            SetShadow(label, EnemyTrackerSettings.TextShadow);
        }

        private void Execute(int index, int command)
        {
            if (_rows == null || index < 0 || index >= _rows.Length) return;
            NativeEnemyTrackerBridge.Entry bound = _rows[index].Bound;
            if (!_native.Command(bound, command))
                Plugin.LogWarning("[ArcaneUI Enemy Tracker] Native row changed or command unavailable; no order issued.");
        }

        private void Build()
        {
            _layer = _canvas.CreateLayer("ArcaneUI_EnemyTracker");
            if (_layer == null) return;
            UnityEngine.Canvas canvas = _layer.gameObject.AddComponent<UnityEngine.Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = NativeUiLayering.GameplayCanvasSortingOrder;
            _layer.gameObject.AddComponent<GraphicRaycaster>();
            _panel = ArcaneUiControls.Panel(_layer, "EnemyTracker", 0f, 0f, 420f, 365f,
                new Color(.055f, .075f, .10f, .94f));
            _panel.anchorMin = _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(1f, 1f);
            _visibility = _panel.gameObject.AddComponent<CanvasGroup>();
            _panelBorder = _panel.gameObject.AddComponent<Outline>();
            _title = ArcaneUiControls.Label(_panel, "Title", "ENEMY TRACKER", 11f, 3f, 300f, 28f,
                13, new Color(.94f, .83f, .63f), TextAnchor.MiddleLeft, FontStyle.Bold);
            _overflow = ArcaneUiControls.Label(_panel, "NativeOverflow", "", 11f, 345f,
                395f, 18f, 10, Color.white, TextAnchor.MiddleLeft);
            _overflow.gameObject.SetActive(false);
            _overflow.raycastTarget = false;
            _rows = new RowView[14];
            for (int i = 0; i < _rows.Length; ++i)
            {
                int slot = i;
                RowView row = new RowView();
                row.Select = ArcaneUiControls.Button(_panel, "Target_" + i, "", 9f, 41f+i*40f, 245f, 36f,
                    new Color(.16f,.21f,.27f,.98f), Color.white, () => Execute(slot, 0), 11);
                row.Caption = row.Select.GetComponentInChildren<Text>();
                if (row.Caption != null) { row.Caption.resizeTextForBestFit = true; row.Caption.horizontalOverflow = HorizontalWrapMode.Wrap; row.Caption.verticalOverflow = VerticalWrapMode.Truncate; }
                row.Border = row.Select.gameObject.AddComponent<Outline>();
                row.TargetStripe = ArcaneUiControls.Panel(row.Select.transform, "SelectedTargetStripe",
                    0f, 0f, 3f, 30f, Color.white, false).GetComponent<Image>();
                RectTransform health = ArcaneUiControls.Panel(row.Select.transform,
                    "HealthBack", 5f, 25f, 145f, 3f, new Color(.09f,.10f,.12f,.9f), false);
                row.HealthBack = health.GetComponent<Image>();
                row.HealthFill = ArcaneUiControls.Panel(health, "HealthFill", 0f, 0f, 145f, 3f,
                    new Color(.27f,.69f,.43f,.8f), false).GetComponent<Image>();
                row.Party = ArcaneUiControls.Button(_panel, "Attack_" + i, "ATTACK", 340f, 41f+i*40f, 64f, 36f,
                    new Color(.37f,.19f,.16f,.98f), Color.white, () => Execute(slot, 1), 10);
                row.Raid1 = ArcaneUiControls.Button(_panel, "RaidGroup1_" + i, "G1", 260f, 41f+i*40f, 35f, 36f,
                    new Color(.32f,.23f,.16f,.98f), Color.white, () => Execute(slot, 2), 10);
                row.Raid2 = ArcaneUiControls.Button(_panel, "RaidGroup2_" + i, "G2", 298f, 41f+i*40f, 35f, 36f,
                    new Color(.32f,.23f,.16f,.98f), Color.white, () => Execute(slot, 3), 10);
                row.Raid3 = ArcaneUiControls.Button(_panel, "RaidGroup3_" + i, "G3", 336f, 41f+i*40f, 35f, 36f,
                    new Color(.32f,.23f,.16f,.98f), Color.white, () => Execute(slot, 4), 10);
                row.Urgent = ArcaneUiControls.Button(_panel, "Urgent_" + i, "!", 374f, 41f+i*40f, 35f, 36f,
                    new Color(.49f,.17f,.14f,.98f), Color.white, () => Execute(slot, 5), 11);
                _rows[i] = row;
            }
            ApplyLayout();
            _layer.gameObject.SetActive(false);
        }

        internal void ApplyLayout()
        {
            if (_panel == null) return;
            float width = EnemyTrackerSettings.Width;
            float header = EnemyTrackerSettings.ShowTitle ? 41f : 9f;
            _panel.anchoredPosition = EnemyTrackerSettings.Position;
            _panel.sizeDelta = new Vector2(width, header + EnemyTrackerSettings.MaxRows *
                EnemyTrackerSettings.RowHeight + Mathf.Max(0, EnemyTrackerSettings.MaxRows - 1) *
                EnemyTrackerSettings.RowSpacing + 4f);
            Color background = Tint(EnemyTrackerSettings.BackgroundHex, new Color(.055f,.075f,.10f));
            Image image = _panel.GetComponent<Image>();
            if (image != null)
            {
                image.color = WithAlpha(background, EnemyTrackerVisualStyle.PanelAlpha);
                image.raycastTarget = EnemyTrackerVisualStyle.PanelAlpha > .001f;
            }
            if (_title != null)
            {
                _title.gameObject.SetActive(EnemyTrackerSettings.ShowTitle);
                _title.rectTransform.sizeDelta = new Vector2(width - 22f, 29f);
                _title.font = UnitFrameAppearance.ResolveFont(EnemyTrackerSettings.FontFamily);
                _title.color = Tint(EnemyTrackerSettings.BorderHex, Color.white);
                SetShadow(_title, EnemyTrackerSettings.TextShadow);
            }
            if (_panelBorder != null)
            {
                ApplyOutline(_panelBorder, false, Tint(EnemyTrackerSettings.TargetHex, Color.white));
                _panelBorder.enabled = EnemyTrackerSettings.PanelBorder && EnemyTrackerSettings.BorderStyle != "None";
            }
            // Textures are procedural: rebuild ONLY after the user changes
            // texture selection, not every native status poll in a large raid.
            if (_lastTexture != EnemyTrackerSettings.BarTexture && _rows != null)
            {
                _lastTexture = EnemyTrackerSettings.BarTexture;
                foreach (RowView row in _rows)
                    if (row != null && row.HealthFill != null)
                        UnitFrameAppearance.ApplyBarTexture(row.HealthFill, EnemyTrackerSettings.BarTexture);
            }
        }

        internal void Shutdown()
        {
            _native.RestoreNative();
            _native.Reset();
            if (_layer != null) UnityEngine.Object.Destroy(_layer.gameObject);
            _layer = null; _panel = null; _rows = null; _visibility = null;
            _title = null; _panelBorder = null; _overflow = null;
            _display.Clear(); _lastTexture = null; _lastHealthUpdate = 0f;
            _lastCombat = false; _nextCombatProbe = 0f;
        }
    }
}
