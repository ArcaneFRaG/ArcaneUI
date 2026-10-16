using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Modules.ActionBars.Configuration;
using ArcanesGroupFrames.Modules.ActionBars.Native;
using ArcanesGroupFrames.Modules.UnitFrames.Target;

namespace ArcanesGroupFrames.Modules.ActionBars.UI
{
    /// <summary>
    /// Phase 7.2 native-backed hotbars with Phase 7.3 independent action views.
    /// Primary slots always invoke their original native Hotkeys.DoHotkeyTask().
    /// Extra slots either keep legacy native links or use explicitly captured
    /// independent identities; unverified independent execution is opt-in.
    /// </summary>
    internal sealed class ArcaneUiNativeBarMirror
    {
        private sealed class SlotView
        {
            internal Image Icon;
            internal Image Shade;
            internal Text Key;
            internal Text Label;
            internal Text CooldownMark;
            internal Button Click;
            internal string TooltipName;
            internal string TooltipKind;
            internal object TooltipAction;
            internal int Source;
            internal bool Independent;
            internal float LastCooldown;
            internal float MaxCooldown;
            internal float LastCooldownSample;
            internal float DisplayedCooldown;
            internal int CountdownSamples;
        }
        private sealed class BarView
        {
            internal RectTransform Root;
            internal CanvasGroup Alpha;
            internal SlotView[] Slots = new SlotView[0];
            internal string Geometry;
            internal string Mode = "Always Visible";
            internal float Fade = .16f;
            internal float HiddenOpacity;
            internal bool Combat;
        }
        private readonly ArcaneCanvasManager _canvas;
        private RectTransform _layer;
        private RectTransform _tooltipRoot;
        private Image _tooltipBackground;
        private Text _tooltipTitle;
        private Text _tooltipMeta;
        private Text _tooltipDescription;
        private int _hoverBar = -1, _hoverSlot = -1;
        private string _tooltipIdentity;
        private string _tooltipMetaText;
        private const float TooltipWidth = 292f;
        // Separate canvases sort independently of sibling order. Keep the tooltip
        // above ArcaneUI settings (32760) and the layout editor (32761).
        private const int TooltipCanvasSortingOrder = 32762;
        private readonly BarView[] _bars = new BarView[ActionBarPlanSettings.MaxBars];
        private float _nextRefresh;
        private static Font _font;
        private static Sprite _cooldownSprite;
        private static Texture2D _cooldownTexture;
        private static Sprite CooldownSprite()
        {
            if (_cooldownSprite != null) return _cooldownSprite;
            _cooldownTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            _cooldownTexture.name = "ArcaneUI_CooldownFill";
            Color[] pixels = new Color[16];
            for (int p = 0; p < pixels.Length; ++p) pixels[p] = Color.white;
            _cooldownTexture.SetPixels(pixels);
            _cooldownTexture.Apply();
            _cooldownSprite = Sprite.Create(_cooldownTexture, new Rect(0f, 0f, 4f, 4f),
                new Vector2(.5f, .5f), 100f);
            return _cooldownSprite;
        }
        private readonly Dictionary<string, float> _nativeCooldowns = new Dictionary<string, float>(StringComparer.Ordinal);
        private sealed class NativeCooldownSample
        {
            internal string Action;
            internal float Seconds;
            internal float Time;
        }
        private readonly Dictionary<int, NativeCooldownSample> _nativeSamples =
            new Dictionary<int, NativeCooldownSample>();
        private Stats _nativeSampleOwner;

        internal ArcaneUiNativeBarMirror(ArcaneCanvasManager canvas) { _canvas = canvas; }

        // The layout editor observes this RectTransform rather than duplicating
        // bar geometry. It may be absent until the player and native slots load.
        internal RectTransform GetBarRect(int index)
        {
            if (index < 0 || index >= _bars.Length) return null;
            BarView bar = _bars[index];
            return bar != null ? bar.Root : null;
        }

        internal void Update()
        {
            bool usable = ActionBarMirrorSettings.Enabled && _canvas != null && _canvas.IsReady &&
                GameData.PlayerControl != null && !GameData.InCharSelect &&
                ArcanesGroupFrames.NativeUiLayering.CanShowGameplayUi;
            if (!usable)
            {
                HideAll();
                NativeActionBarVisualBridge.Restore();
                return;
            }
            NativeActionBarDiscovery.TryBindRuntime();
            if (!NativeActionBarDiscovery.HasValidLiveSnapshot)
            {
                HideAll();
                NativeActionBarVisualBridge.Restore();
                return;
            }
            if (_layer == null) Initialize();
            if (_layer == null) return;
            NativeActionBarVisualBridge.Update(ActionBarMirrorSettings.HideNativeBar);
            NativeExtraHotkeySlots.Tick(Time.deltaTime);
            bool editing = Plugin.Instance != null && Plugin.Instance.UiRuntime != null &&
                Plugin.Instance.UiRuntime.LayoutEditor != null &&
                Plugin.Instance.UiRuntime.LayoutEditor.IsOpen;
            // Per-frame work is restricted to lightweight fades, hovering and
            // actual key-down events. No hierarchy scans or string rebuilding.
            for (int b = 0; b < _bars.Length; ++b)
            {
                BarView view = _bars[b];
                if (view == null || view.Root == null) continue;
                bool active = b < ActionBarPlanSettings.Count;
                if (view.Root.gameObject.activeSelf != active) view.Root.gameObject.SetActive(active);
                if (active)
                {
                    if (editing)
                    {
                        // Even combat-only, mouseover-only and hidden bars must
                        // be visible while their positions are being edited.
                        if (view.Alpha != null && view.Alpha.alpha != 1f) view.Alpha.alpha = 1f;
                        if (view.Alpha != null) view.Alpha.blocksRaycasts = false;
                    }
                    else Fade(view);
                }
            }
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + .20f;
                NativeActionBarDiscovery.RefreshPrimarySnapshots();
                RefreshNativeCooldowns();
                Stats player = GameData.PlayerStats;
                bool combat = false;
                if (player != null) TargetNativeReadAdapter.TryReadCombat(player, out combat);
                for (int b = 0; b < ActionBarPlanSettings.Count; ++b)
                {
                    if (_bars[b] == null) CreateBar(b);
                    var bar = _bars[b];
                    if (bar == null) continue;
                    string geometry = Fingerprint(b);
                    if (bar.Geometry != geometry) { bar.Geometry = geometry; BuildBar(b); }
                    Vector2 position = new Vector2(ActionBarPlanSettings.BarX(b), ActionBarPlanSettings.BarY(b));
                    // During layout edit the mover owns the live RectTransform.
                    // Reapplying persisted coordinates here would fight dragging.
                    if (!editing && bar.Root.anchoredPosition != position)
                        bar.Root.anchoredPosition = position;
                    bar.Combat = combat;
                    bar.Mode = ActionBarPlanSettings.Visibility(b);
                    bar.HiddenOpacity = ActionBarPlanSettings.HiddenOpacity(b) / 100f;
                    bar.Fade = ActionBarPlanSettings.FadeMilliseconds(b) / 1000f;
                    UpdateSlotData(b);
                }
            }
            AnimateCooldownSwipes();
            UpdateHoverTooltip(editing);
            ArcaneUiActionDropTarget.PollPointerDrop(editing);
            if (Input.anyKeyDown) ProcessBindings();
        }

        // A restored profile can change bar geometry and colours while the
        // action-bar mirror is already alive. Force its next 200 ms update to
        // rebuild ArcaneUI views from the restored preferences. This never
        // rewrites native slot assignments or player keybinds.


        private void HideAll()
        {
            HideTooltip();
            for (int i = 0; i < _bars.Length; i++)
                if (_bars[i] != null && _bars[i].Root != null && _bars[i].Root.gameObject.activeSelf)
                    _bars[i].Root.gameObject.SetActive(false);
            _nextRefresh = 0f;
        }

        private void Initialize()
        {
            _layer = _canvas.CreateLayer("ArcaneUI_ActionBars");
            if (_layer == null) return;
            var nested = _layer.gameObject.AddComponent<UnityEngine.Canvas>();
            nested.overrideSorting = true;
            nested.sortingOrder = ArcanesGroupFrames.NativeUiLayering.GameplayCanvasSortingOrder;
            _layer.gameObject.AddComponent<GraphicRaycaster>();
            InitializeTooltip();
        }

        private void CreateBar(int index)
        {
            BarView view = new BarView();
            view.Root = NewRect(_layer, "ActionBar_" + (index + 1));
            view.Root.anchorMin = new Vector2(.5f, 0f);
            view.Root.anchorMax = new Vector2(.5f, 0f);
            view.Root.pivot = new Vector2(.5f, 0f);
            view.Alpha = view.Root.gameObject.AddComponent<CanvasGroup>();
            view.Alpha.interactable = true;
            view.Alpha.blocksRaycasts = true;
            _bars[index] = view;
        }

        private string Fingerprint(int bar)
        {
            return ActionBarPlanSettings.SlotCount(bar) + ":" + ActionBarPlanSettings.Columns(bar) + ":" +
                ActionBarStyleSettings.IconSize + ":" + ActionBarStyleSettings.Spacing + ":" +
                ActionBarPlanSettings.Border(bar) + ":" + ActionBarPlanSettings.AccentHex(bar) + ":" +
                ActionBarStyleSettings.BorderWidth + ":" + ActionBarStyleSettings.CornerSize + ":" +
                ActionBarStyleSettings.Glow + ":" + ActionBarStyleSettings.BackgroundOpacity + ":" +
                ArcaneUiGlobalTypography.FontScale + ":" + ArcaneUiGlobalTypography.FontStyleIndex + ":" +
                ArcaneUiGlobalTypography.OutlineIndex + ":" + ArcaneUiStyleService.GlobalFont;
        }

        private void BuildBar(int barIndex)
        {
            var bar = _bars[barIndex];
            if (bar == null || bar.Root == null) return;
            // Dispose owned native HK registrations before rebuilding their GameObjects.
            NativeExtraHotkeySlots.UnregisterBar(barIndex);
            for (int i = bar.Root.childCount - 1; i >= 0; --i)
                UnityEngine.Object.Destroy(bar.Root.GetChild(i).gameObject);
            int count = ActionBarPlanSettings.SlotCount(barIndex);
            int size = ActionBarStyleSettings.IconSize;
            int gap = ActionBarStyleSettings.Spacing;
            int cols = Mathf.Clamp(ActionBarPlanSettings.Columns(barIndex), 1, count);
            int rows = Mathf.CeilToInt(count / (float)cols);
            float width = cols * size + (cols - 1) * gap + 12f;
            float height = rows * size + (rows - 1) * gap + 12f;
            bar.Root.sizeDelta = new Vector2(width, height);
            bar.Slots = new SlotView[count];
            Color accent = ActionBarPlanSettings.AccentColor(barIndex);
            string border = ActionBarPlanSettings.Border(barIndex);
            float backingAlpha = ActionBarStyleSettings.BackgroundOpacity / 100f;
            Image backing = MakeImage(bar.Root, "Backing", new Color(.055f, .073f, .096f, backingAlpha));
            Place(backing.rectTransform, 0, 0, width, height);
            for (int i = 0; i < count; ++i)
            {
                int slotIndex = i;
                int col = i % cols;
                int row = i / cols;
                float x = 6 + col * (size + gap);
                float y = 6 + (rows - 1 - row) * (size + gap);
                RectTransform slot = NewRect(bar.Root, "Slot" + (i + 1));
                Place(slot, x, y, size, size);
                // The game's Hotkeys.Start expects the slot GameObject to
                // expose a root Image (ExtendedHotbars uses this layout).
                // The real rendered background remains ArcaneUI's child Image.
                Image nativeSlotSurface = slot.gameObject.AddComponent<Image>();
                nativeSlotSurface.color = Color.clear;
                nativeSlotSurface.raycastTarget = false;
                Image slotBackground = MakeImage(slot, "Background", new Color(.11f, .13f, .17f, 1f));
                Place(slotBackground.rectTransform, 0, 0, size, size);
                // The native-backed button must be raycastable. No other graphic
                // on this slot intercepts pointer events.
                slotBackground.raycastTarget = true;
                var drop = slot.gameObject.AddComponent<ArcaneUiActionDropTarget>();
                drop.Initialize(barIndex, i + 1);
                // Refresh immediately after a right-click clear instead of waiting
                // for the normal 200 ms action-bar snapshot interval.
                drop.OnCleared = (index, number) =>
                {
                    _nextRefresh = 0f;
                    _tooltipIdentity = null;
                    if (_hoverBar == index && _hoverSlot == number)
                        _hoverBar = _hoverSlot = -1;
                    HideTooltip();
                };
                drop.OnHover = OnSlotHover;
                Button button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slotBackground;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => ClickSlot(barIndex, slotIndex));
                var view = new SlotView();
                view.Click = button;
                view.Icon = MakeImage(slot, "AbilityIcon", Color.clear);
                Place(view.Icon.rectTransform, 3, 3, size - 6, size - 6);
                view.Icon.preserveAspect = true;
                view.Label = MakeLabel(slot, "MissingIconLabel", "", 4, 4, size - 8, size - 8,
                    Mathf.Clamp(size / 4, 10, 14), Color.white, TextAnchor.MiddleCenter);
                view.Shade = MakeImage(slot, "CooldownShade", new Color(0, 0, 0, .70f));
                // Image.Type.Filled without a Sprite falls back to a plain
                // quad in Unity's Image.OnPopulateMesh, so the radial swipe
                // never renders. An owned white sprite fixes that path.
                view.Shade.sprite = CooldownSprite();
                Place(view.Shade.rectTransform, 2, 2, size - 4, size - 4);
                view.Shade.enabled = false;
                // Native fill graphics provide a clockwise radial cooldown wipe.
                view.Shade.type = Image.Type.Filled;
                view.Shade.fillMethod = Image.FillMethod.Radial360;
                view.Shade.fillOrigin = (int)Image.Origin360.Top;
                view.Shade.fillClockwise = true;
                view.Shade.fillAmount = 1f;
                view.CooldownMark = MakeLabel(slot, "CooldownIndicator", "", 2, 1, size - 4, size - 2,
                    13, new Color(1f, .86f, .66f), TextAnchor.MiddleCenter);
                if (border != "None") AddBorders(slot, size, accent, border);
                view.Key = MakeLabel(slot, "HotkeyLabel", "", 2, 1, size - 4, 13,
                    10, Color.white, TextAnchor.LowerRight);
                bar.Slots[i] = view;
                if (barIndex > 0)
                    NativeExtraHotkeySlots.Register(barIndex, i + 1, slot.gameObject, view.Icon, size);
            }
        }

        private void UpdateSlotData(int barIndex)
        {
            BarView bar = _bars[barIndex];
            if (bar == null) return;
            for (int i = 0; i < bar.Slots.Length; ++i)
            {
                var view = bar.Slots[i];
                if (view == null) continue;
                if (barIndex > 0) NativeExtraHotkeySlots.Sync(barIndex, i + 1);
                int sourceNumber = ActionBarPlanSettings.SourceSlot(barIndex, i + 1);
                view.Source = sourceNumber;
                var independent = ActionBarIndependentAssignments.Get(barIndex, i + 1);
                view.Independent = independent != null;
                NativeActionBarDiscovery.NativeSlotSnapshot native = null;
                if (!view.Independent && sourceNumber > 0)
                    NativeActionBarDiscovery.TryGetActiveSlot(sourceNumber, out native);
                object resolved = null;
                bool independentResolved = view.Independent &&
                    (ActionBarIndependentAssignments.TryResolve(independent, out resolved) ||
                     NativeExtraHotkeySlots.HasAssignment(barIndex, i + 1));
                bool assigned = view.Independent ? independentResolved : native != null && native.HasAssignment;
                Sprite sprite = null;
                string name = "";
                if (view.Independent)
                {
                    name = independent.Name;
                    // Trust the real native Hotkeys icon first, especially for
                    // inventory items (their Sprite is on the ItemIcon).
                    sprite = NativeExtraHotkeySlots.ActionIcon(barIndex, i + 1);
                    if (sprite == null && resolved != null)
                    {
                        // Native spell/skill icons are optional and field-based
                        // across the game's builds; avoid a hardcoded member ABI.
                        string member = independent.Kind == "Spell" ? "SpellIcon" : "SkillIcon";
                        var field = resolved.GetType().GetField(member,
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);
                        if (field != null) sprite = field.GetValue(resolved) as Sprite;
                    }
                }
                else if (native != null)
                {
                    sprite = native.AbilityIcon;
                    name = native.AbilityName;
                }
                view.TooltipName = assigned ? name : "";
                view.TooltipKind = view.Independent ? independent.Kind : native != null ? native.AbilityKind : "";
                view.TooltipAction = view.Independent ? resolved : null;
                if (view.Icon.sprite != sprite) view.Icon.sprite = sprite;
                if (view.Icon.enabled != (assigned && sprite != null))
                    view.Icon.enabled = assigned && sprite != null;
                Color color = assigned && sprite != null ? Color.white : Color.clear;
                if (view.Icon.color != color) view.Icon.color = color;
                string caption = view.Independent && !independentResolved ? "?" : assigned && sprite == null ? ShortName(name) : "";
                if (view.Label.text != caption) view.Label.text = caption;
                string bind = ActionBarPlanSettings.Binding(barIndex, i + 1);
                if (string.IsNullOrEmpty(bind) && barIndex == 0 && native != null && native.PrimaryKey != KeyCode.None)
                    bind = ActionBarPlanSettings.Describe(native.PrimaryKey, false, false, false);
                if (!ActionBarStyleSettings.ShowKeyLabels) bind = "";
                if (view.Key.text != bind) view.Key.text = bind;
                // Erenshor Hotkeys.Cooldown is measured at 60 units/second.
                // Convert native values exactly once for the ArcaneUI seconds UI.
                float rawCooldown = native != null && assigned ? native.RawCooldown / 60f : 0f;
                // Phase 7.3F+ slots are genuine native Hotkeys. Their live
                // cooldown is authoritative, including instant resets. Never
                // replay an estimated cast recast over a zero native timer.
                float knownMaximum = 0f;
                if (view.Independent && independentResolved)
                {
                    rawCooldown = NativeExtraHotkeySlots.CooldownSeconds(barIndex, i + 1);
                    // A matching primary HK can supply a shared cooldown when
                    // an ability is used on the original hotbar.
                    float primaryRemaining;
                    if (_nativeCooldowns.TryGetValue(independent.Kind + ":" + independent.Identity, out primaryRemaining))
                        rawCooldown = Mathf.Max(rawCooldown, primaryRemaining);
                }
                UpdateCooldown(view, rawCooldown, assigned, knownMaximum);
            }
        }

        private void RefreshNativeCooldowns()
        {
            if (!object.ReferenceEquals(_nativeSampleOwner, GameData.PlayerStats))
            {
                _nativeSampleOwner = GameData.PlayerStats;
                _nativeSamples.Clear();
            }
            _nativeCooldowns.Clear();
            var resetThisSample = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i <= 10; ++i)
            {
                string kind, identity, name;
                object nativeAction;
                NativeActionBarDiscovery.NativeSlotSnapshot slot;
                if (NativeActionBarDiscovery.TryReadNativeAssignment(i, out kind, out identity, out name, out nativeAction) &&
                    NativeActionBarDiscovery.TryGetActiveSlot(i, out slot) && slot != null)
                {
                    string key = kind + ":" + identity;
                    float seconds = Mathf.Max(0f, slot.RawCooldown / 60f);
                    NativeCooldownSample previousSample;
                    if (_nativeSamples.TryGetValue(i, out previousSample) &&
                        previousSample.Action == key && previousSample.Seconds > 1.5f &&
                        seconds <= .01f &&
                        previousSample.Seconds - (Time.time - previousSample.Time) > .5f)
                    {
                        NativeExtraHotkeySlots.ResetVisualCooldown(kind, identity);
                        resetThisSample.Add(key);
                    }
                    _nativeSamples[i] = new NativeCooldownSample
                        { Action = key, Seconds = seconds, Time = Time.time };
                    float previous;
                    if (!_nativeCooldowns.TryGetValue(key, out previous) || seconds > previous)
                        _nativeCooldowns[key] = seconds;
                }
                else _nativeSamples.Remove(i);
            }
            // If several native slots hold the same spell, one authoritative
            // early-ready event must not be masked by a stale duplicate timer.
            foreach (string key in resetThisSample)
                _nativeCooldowns[key] = 0f;
        }

        // Native timers arrive only five times per second. Maintain a
        // frame-rate-independent, continuous render clock so the radial wipe
        // and countdown never move in visible 200ms steps. No hotbar polling
        // or reflection occurs here; gameplay still owns actual cooldowns.
        private void AnimateCooldownSwipes()
        {
            for (int b = 0; b < ActionBarPlanSettings.Count; ++b)
            {
                BarView bar = _bars[b];
                if (bar == null || bar.Slots == null) continue;
                foreach (SlotView slot in bar.Slots)
                {
                    if (slot == null || slot.Shade == null || !slot.Shade.enabled ||
                        slot.MaxCooldown <= .001f) continue;
                    if (slot.CountdownSamples > 0)
                    {
                        // The same time source as native spellcast observation.
                        float elapsed = Mathf.Max(0f, Time.time - slot.LastCooldownSample);
                        float predicted = Mathf.Max(0f, slot.LastCooldown - elapsed);
                        // Smoothly reconcile minor deviations in the live timer;
                        // never animate backwards on a decreasing cooldown.
                        slot.DisplayedCooldown = Mathf.Min(slot.DisplayedCooldown,
                            Mathf.Max(0f, predicted));
                    }
                    float current = Mathf.Max(0f, slot.DisplayedCooldown);
                    slot.Shade.fillAmount = Mathf.Clamp01(current / slot.MaxCooldown);
                    if (ActionBarStyleSettings.ShowCooldownNumbers && slot.CountdownSamples > 0)
                    {
                        string label = current < 10f ?
                            current.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) :
                            Mathf.CeilToInt(current).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        if (slot.CooldownMark.text != label) slot.CooldownMark.text = label;
                    }
                    if (current <= .001f)
                    {
                        slot.Shade.enabled = false;
                        slot.CooldownMark.text = "";
                    }
                }
            }
        }

        private static void UpdateCooldown(SlotView view, float raw, bool assigned, float knownMaximum)
        {
            bool active = assigned && !float.IsNaN(raw) && !float.IsInfinity(raw) && raw > .001f;
            if (!active)
            {
                view.LastCooldown = 0f;
                view.MaxCooldown = 0f;
                view.DisplayedCooldown = 0f;
                view.LastCooldownSample = Time.time;
                view.CountdownSamples = 0;
                view.Shade.enabled = false;
                view.CooldownMark.text = "";
                return;
            }
            float now = Time.time;
            bool started = view.LastCooldown <= .001f || raw > view.LastCooldown + .5f;
            if (started)
            {
                view.MaxCooldown = Mathf.Max(raw, knownMaximum);
                view.DisplayedCooldown = raw;
                // Authored recast intervals from the accepted player cast are
                // already expressed in seconds. For a native-only timer, wait
                // for a second valid sample before ticking between updates.
                view.CountdownSamples = knownMaximum > .001f ? 2 : 0;
            }
            else
            {
                float elapsed = Mathf.Max(.001f, now - view.LastCooldownSample);
                float speed = (view.LastCooldown - raw) / elapsed;
                if (knownMaximum > .001f || (speed > .25f && speed < 3.5f))
                    view.CountdownSamples = Mathf.Max(1, view.CountdownSamples);
                // Native readings are authoritative. Small disagreements are
                // smoothed; a genuine reset/new cast starts a new swipe above.
                if (view.CountdownSamples > 0)
                {
                    if (raw < view.DisplayedCooldown - .6f) view.DisplayedCooldown = raw;
                    else view.DisplayedCooldown = Mathf.Min(view.DisplayedCooldown, raw);
                }
                else view.DisplayedCooldown = raw;
                view.MaxCooldown = Mathf.Max(view.MaxCooldown, knownMaximum, raw);
            }
            view.LastCooldown = raw;
            view.LastCooldownSample = now;
            view.Shade.enabled = true;
            view.Shade.fillAmount = Mathf.Clamp01(view.DisplayedCooldown / Mathf.Max(.001f, view.MaxCooldown));
            if (!ActionBarStyleSettings.ShowCooldownNumbers || view.CountdownSamples == 0)
                view.CooldownMark.text = "";
        }

        // One shared tooltip for every primary and extra action slot. None of
        // its graphics receive raycasts, so native dragging/clicks are untouched.
        private void InitializeTooltip()
        {
            _tooltipRoot = NewRect(_layer, "ActionTooltip");
            _tooltipRoot.sizeDelta = new Vector2(TooltipWidth, 68f);
            // A last sibling can only beat siblings on the action-bar canvas.
            // Give the tooltip its own higher-sorted nested canvas so it renders
            // over player/target/party/raid frames and other ArcaneUI overlays.
            var tooltipCanvas = _tooltipRoot.gameObject.AddComponent<UnityEngine.Canvas>();
            tooltipCanvas.overrideSorting = true;
            tooltipCanvas.sortingOrder = TooltipCanvasSortingOrder;
            // Deliberately no GraphicRaycaster: tooltip must never intercept
            // spell dragging, slot clicks, or any underlying UI interactions.
            var group = _tooltipRoot.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            _tooltipBackground = MakeImage(_tooltipRoot, "TooltipBackground", new Color(.065f, .075f, .105f, .97f));
            _tooltipTitle = MakeLabel(_tooltipRoot, "ActionName", "", 12f, 37f, TooltipWidth - 24f, 22f,
                14, Color.white, TextAnchor.MiddleLeft);
            _tooltipTitle.fontStyle = FontStyle.Bold;
            _tooltipMeta = MakeLabel(_tooltipRoot, "ActionTypeAndKey", "", 12f, 14f, TooltipWidth - 24f, 20f,
                11, new Color(.72f, .81f, .94f), TextAnchor.MiddleLeft);
            _tooltipDescription = MakeLabel(_tooltipRoot, "ActionDescription", "", 12f, 8f,
                TooltipWidth - 24f, 50f, 11, new Color(.88f, .87f, .79f), TextAnchor.UpperLeft);
            _tooltipTitle.supportRichText = false;
            _tooltipMeta.supportRichText = false;
            _tooltipDescription.supportRichText = false;
            _tooltipRoot.gameObject.SetActive(false);
        }

        private void OnSlotHover(int bar, int slot, bool entered)
        {
            if (entered)
            {
                if (_hoverBar != bar || _hoverSlot != slot)
                    _tooltipIdentity = null;
                _hoverBar = bar;
                _hoverSlot = slot;
            }
            else if (_hoverBar == bar && _hoverSlot == slot)
            {
                _hoverBar = _hoverSlot = -1;
                HideTooltip();
            }
        }

        private void HideTooltip()
        {
            if (_tooltipRoot != null && _tooltipRoot.gameObject.activeSelf)
                _tooltipRoot.gameObject.SetActive(false);
        }

        private void UpdateHoverTooltip(bool editing)
        {
            if (_tooltipRoot == null || !ActionBarStyleSettings.ShowTooltips || editing ||
                ArcaneUiModalInputGuard.IsBlocking || Input.GetMouseButton(0) || Input.GetMouseButton(1) ||
                _hoverBar < 0 || _hoverBar >= ActionBarPlanSettings.Count)
            {
                HideTooltip();
                return;
            }
            BarView bar = _bars[_hoverBar];
            if (bar == null || bar.Root == null || !bar.Root.gameObject.activeInHierarchy ||
                _hoverSlot < 1 || _hoverSlot > bar.Slots.Length ||
                (bar.Alpha != null && bar.Alpha.alpha < .1f))
            {
                HideTooltip();
                return;
            }
            SlotView slot = bar.Slots[_hoverSlot - 1];
            if (slot == null || string.IsNullOrEmpty(slot.TooltipName))
            {
                HideTooltip();
                return;
            }
            // The native slot snapshot is already refreshed every 200ms. Only
            // read optional description members when the hovered action changes.
            string identity = _hoverBar + ":" + _hoverSlot + ":" + slot.TooltipKind + ":" + slot.TooltipName + ":" +
                (slot.Independent ? "owned" : slot.Source.ToString());
            if (_tooltipIdentity != identity)
            {
                _tooltipIdentity = identity;
                object action = slot.TooltipAction;
                if (action == null && slot.Source > 0)
                {
                    string kind, id, name;
                    NativeActionBarDiscovery.TryReadNativeAssignment(slot.Source, out kind, out id, out name, out action);
                }
                string description = ReadActionDescription(action);
                bool hasDescription = !string.IsNullOrEmpty(description);
                float height = hasDescription ? 142f : 68f;
                _tooltipRoot.sizeDelta = new Vector2(TooltipWidth, height);
                Place(_tooltipBackground.rectTransform, 0f, 0f, TooltipWidth, height);
                Place(_tooltipTitle.rectTransform, 12f, height - 31f, TooltipWidth - 24f, 22f);
                Place(_tooltipMeta.rectTransform, 12f, height - 54f, TooltipWidth - 24f, 20f);
                Place(_tooltipDescription.rectTransform, 12f, 8f, TooltipWidth - 24f, 76f);
                _tooltipDescription.text = hasDescription ? description : "";
                _tooltipDescription.gameObject.SetActive(hasDescription);
                _tooltipTitle.text = slot.TooltipName;
            }
            string binding = ActionBarPlanSettings.Binding(_hoverBar, _hoverSlot);
            if (string.IsNullOrEmpty(binding) && _hoverBar == 0 && slot.Source > 0)
            {
                NativeActionBarDiscovery.NativeSlotSnapshot primary;
                if (NativeActionBarDiscovery.TryGetActiveSlot(slot.Source, out primary) &&
                    primary != null && primary.PrimaryKey != KeyCode.None)
                    binding = ActionBarPlanSettings.Describe(primary.PrimaryKey, false, false, false);
            }
            string meta = string.IsNullOrEmpty(slot.TooltipKind) ? "Action" : slot.TooltipKind;
            meta += "  •  " + (string.IsNullOrEmpty(binding) ? "Unbound" : binding);
            if (slot.DisplayedCooldown > .01f)
                meta += "  •  " + slot.DisplayedCooldown.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
            if (_tooltipMetaText != meta)
            {
                _tooltipMetaText = meta;
                _tooltipMeta.text = meta;
            }
            if (!_tooltipRoot.gameObject.activeSelf) _tooltipRoot.gameObject.SetActive(true);
            // Layering is provided by the dedicated tooltip canvas, not by
            // moving the action-bar slots or relying on sibling order.
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, Input.mousePosition, null, out local))
            {
                Vector2 panelSize = _tooltipRoot.sizeDelta;
                float px = local.x - _layer.rect.xMin;
                float py = local.y - _layer.rect.yMin;
                float x = Mathf.Clamp(px + 18f, 4f, Mathf.Max(4f, _layer.rect.width - panelSize.x - 4f));
                float y = Mathf.Clamp(py + 20f, 4f, Mathf.Max(4f, _layer.rect.height - panelSize.y - 4f));
                _tooltipRoot.anchoredPosition = new Vector2(x, y);
            }
        }

        private static string ReadActionDescription(object action)
        {
            if (action == null) return "";
            // Erenshor builds and FOTF items use different field names. Read
            // existing text only; don't calculate or invent spell properties.
            object candidate = action;
            for (int depth = 0; depth < 2 && candidate != null; ++depth)
            {
                Type type = candidate.GetType();
                foreach (string name in new[] { "Description", "SpellDescription", "SkillDescription", "ItemDescription", "SpellDesc" })
                {
                    try
                    {
                        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                        FieldInfo field = type.GetField(name, flags);
                        object value = field != null ? field.GetValue(candidate) : null;
                        if (value == null)
                        {
                            PropertyInfo property = type.GetProperty(name, flags);
                            if (property != null && property.GetIndexParameters().Length == 0)
                                value = property.GetValue(candidate, null);
                        }
                        string text = value as string;
                        if (!string.IsNullOrEmpty(text))
                        {
                            text = text.Trim().Replace('\r', ' ').Replace('\n', ' ');
                            return text.Length > 200 ? text.Substring(0, 197) + "..." : text;
                        }
                    }
                    catch (Exception) { /* Optional display-only metadata. */ }
                }
                ItemIcon icon = candidate as ItemIcon;
                try { candidate = icon != null ? (object)icon.MyItem : null; }
                catch (Exception) { candidate = null; }
            }
            return "";
        }

        private static string ShortName(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length > 3 ? text.Substring(0, 3).ToUpperInvariant() : text.ToUpperInvariant();
        }

        private static bool CanActivate()
        {
            return !GameData.InCharSelect && GameData.PlayerControl != null &&
                !GameData.PlayerTyping && !ArcaneUiModalInputGuard.IsBlocking &&
                ArcanesGroupFrames.NativeUiLayering.CanShowGameplayUi;
        }

        private void ClickSlot(int bar, int zeroBasedSlot)
        {
            if (!CanActivate() || bar >= ActionBarPlanSettings.Count) return;
            var view = _bars[bar];
            if (view == null || zeroBasedSlot >= view.Slots.Length) return;
            var slot = view.Slots[zeroBasedSlot];
            if (slot == null) return;
            // Empty slots are assigned by dragging a live spellbook or HK
            // entry onto their exact rectangle; no assignment picker.
            if (slot.Independent)
                NativeExtraHotkeySlots.Activate(bar, zeroBasedSlot + 1);
            else if (slot.Source > 0)
                NativeActionBarDiscovery.ActivatePrimarySlot(slot.Source);
        }

        private void ProcessBindings()
        {
            if (!CanActivate()) return;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            for (int bar = 0; bar < ActionBarPlanSettings.Count; ++bar)
            {
                var view = _bars[bar];
                if (view == null) continue;
                for (int i = 0; i < view.Slots.Length; ++i)
                {
                    string binding = ActionBarPlanSettings.Binding(bar, i + 1);
                    if (string.IsNullOrEmpty(binding)) continue;
                    KeyCode key;
                    if (!TryParseBinding(binding, ctrl, alt, shift, out key) ||
                        !Input.GetKeyDown(key)) continue;
                    // Native input still owns its shortcuts, including when its
                    // artwork is hidden. Never dispatch duplicates or steal keys.
                    if (NativeActionBarDiscovery.IsNativeKey(key)) continue;
                    if (view.Slots[i] != null && view.Slots[i].Independent)
                        NativeExtraHotkeySlots.Activate(bar, i + 1);
                    else if (view.Slots[i] != null && view.Slots[i].Source > 0)
                        NativeActionBarDiscovery.ActivatePrimarySlot(view.Slots[i].Source);
                    return;
                }
            }
        }

        private static bool TryParseBinding(string bind, bool ctrl, bool alt, bool shift, out KeyCode key)
        {
            key = KeyCode.None;
            if (string.IsNullOrEmpty(bind)) return false;
            string[] segments = bind.Split('+');
            bool needCtrl = false, needAlt = false, needShift = false;
            for (int i = 0; i < segments.Length - 1; ++i)
            {
                if (segments[i] == "Ctrl") needCtrl = true;
                // Existing Alt bindings remain saved for backward compatibility
                // but do not dispatch while Alt is globally reserved.
                else if (segments[i] == "Alt") return false;
                else if (segments[i] == "Shift") needShift = true;
                else return false;
            }
            if (needCtrl != ctrl || needAlt != alt || needShift != shift) return false;
            string last = segments[segments.Length - 1];
            if (last.Length == 1 && last[0] >= '0' && last[0] <= '9') last = "Alpha" + last;
            if (last.StartsWith("Num", StringComparison.Ordinal)) last = "Keypad" + last.Substring(3);
            return Enum.TryParse(last, out key) && key != KeyCode.None;
        }

        private static void Fade(BarView bar)
        {
            if (bar == null || bar.Alpha == null || bar.Root == null) return;
            bool hover = RectTransformUtility.RectangleContainsScreenPoint(bar.Root, Input.mousePosition);
            bool visible;
            switch (bar.Mode)
            {
                case "Mouseover Only": visible = hover; break;
                case "Combat Only": visible = bar.Combat; break;
                case "Combat + Mouseover": visible = bar.Combat && hover; break;
                case "Hidden": visible = false; break;
                default: visible = true; break;
            }
            float target = visible ? 1f : bar.HiddenOpacity;
            float next = bar.Fade <= 0f ? target :
                Mathf.MoveTowards(bar.Alpha.alpha, target, Time.unscaledDeltaTime / bar.Fade);
            if (!Mathf.Approximately(bar.Alpha.alpha, next)) bar.Alpha.alpha = next;
            // Hidden bars still accept keyboard bindings, but never steal mouse
            // input while invisible. This also covers fades in/out.
            bool acceptMouse = visible && bar.Alpha.alpha > .05f;
            if (bar.Alpha.blocksRaycasts != acceptMouse) bar.Alpha.blocksRaycasts = acceptMouse;
            if (bar.Alpha.interactable != acceptMouse) bar.Alpha.interactable = acceptMouse;
        }

        private static void AddBorders(RectTransform parent, int size, Color color, string style)
        {
            float thick = style == "Thin" ? 1f : Mathf.Max(1f, ActionBarStyleSettings.BorderWidth);
            Bar(parent, "TopEdge", color, 0, size - thick, size, thick);
            Bar(parent, "BottomEdge", color, 0, 0, size, thick);
            Bar(parent, "LeftEdge", color, 0, 0, thick, size);
            Bar(parent, "RightEdge", color, size - thick, 0, thick, size);
            if (style == "Ornate" || style == "Runic" || style == "Glowing")
            {
                float corner = ActionBarStyleSettings.CornerSize;
                Color bright = new Color(color.r, color.g, color.b, style == "Glowing" ? .6f : 1f);
                Bar(parent, "CornerTL", bright, 0, size - corner, corner, thick + 1);
                Bar(parent, "CornerBR", bright, size - corner, 0, corner, thick + 1);
                if (style == "Runic") Bar(parent, "RunicNotch", bright, size / 2f - 2, size - thick - 3, 4, 3);
            }
            if (style == "Bevelled")
                Bar(parent, "BevelHighlight", new Color(1, 1, 1, .25f), thick, size - thick - 2, size - thick * 2, 1);
        }
        private static void Bar(RectTransform parent, string name, Color color, float x, float y, float w, float h)
        {
            var graphic = MakeImage(parent, name, color);
            Place(graphic.rectTransform, x, y, w, h);
        }
        private static RectTransform NewRect(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            return rect;
        }
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
        }
        private static Image MakeImage(Transform parent, string name, Color color)
        {
            var rect = NewRect(parent, name);
            var graphic = rect.gameObject.AddComponent<Image>();
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic;
        }
        private static Text MakeLabel(Transform parent, string name, string value,
            float x, float y, float w, float h, int size, Color color, TextAnchor alignment)
        {
            var rect = NewRect(parent, name);
            Place(rect, x, y, w, h);
            var label = rect.gameObject.AddComponent<Text>();
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.font = _font; label.text = value; label.fontSize = size;
            label.color = color; label.alignment = alignment;
            label.raycastTarget = false;
            ArcaneUiGlobalTypography.ApplyControl(label, size);
            return label;
        }
        internal void Shutdown()
        {
            NativeActionBarVisualBridge.Restore();
            NativeExtraHotkeySlots.Shutdown();
            if (_layer != null) UnityEngine.Object.Destroy(_layer.gameObject);
            _layer = null;
            _tooltipRoot = null;
            _hoverBar = _hoverSlot = -1;
            _tooltipIdentity = _tooltipMetaText = null;
            if (_cooldownSprite != null) UnityEngine.Object.Destroy(_cooldownSprite);
            if (_cooldownTexture != null) UnityEngine.Object.Destroy(_cooldownTexture);
            _cooldownSprite = null;
            _cooldownTexture = null;
            for (int i = 0; i < _bars.Length; ++i) _bars[i] = null;
        }
    }
}
