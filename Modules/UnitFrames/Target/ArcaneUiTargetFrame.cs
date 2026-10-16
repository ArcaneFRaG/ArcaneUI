using UnityEngine;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Theming.Rendering;
using ArcanesGroupFrames.Framework.NativeUI.Target;

namespace ArcanesGroupFrames.Modules.UnitFrames.Target
{
    /// <summary>
    /// Visual-only target representation. Reads CurrentTarget without changing
    /// native target selection or any gameplay component. Native suppression
    /// occurs only while the custom view has valid live target Stats.
    /// </summary>
    internal sealed class ArcaneUiTargetFrame
    {
        private readonly ArcaneCanvasManager _canvas;
        private RectTransform _layer;
        private ArcaneUiUnitFrameView _view;
        private ArcaneUiTargetAuras _auras;
        private float _nextRefresh;
        private bool _wasCasting;
        private float _interruptUntil;
        private Character _lastTarget;
        private string _lastClass;
        private TargetHealthColourResolver.Kind _lastColourKind;

        internal RectTransform Rect { get { return _view == null ? null : _view.Root; } }
        internal ArcaneUiTargetFrame(ArcaneCanvasManager canvas) { _canvas = canvas; }

        internal void Initialize()
        {
            if (_layer != null || !_canvas.IsReady) return;
            _layer = _canvas.CreateLayer("ArcaneUI_TargetFrame");
            var sorting = _layer.gameObject.AddComponent<UnityEngine.Canvas>();
            sorting.overrideSorting = true;
            sorting.sortingOrder = NativeUiLayering.GameplayCanvasSortingOrder;
            _view = new ArcaneUiUnitFrameView(_layer, "TargetFrame", ArcaneUiModuleSettings.Target);
            _auras = new ArcaneUiTargetAuras(_view.Root);
            RefreshLayout();
            RefreshStyle();
            _view.Root.gameObject.SetActive(false);
        }

        internal void Update()
        {
            if (_layer == null && _canvas.IsReady) Initialize();
            Character target = GameData.PlayerControl != null ? GameData.PlayerControl.CurrentTarget : null;
            Stats stats = target != null ? target.MyStats : null;
            // Only active after explicitly pressing Cast Diagnostics.
            TargetNativeReadAdapter.TickCastTimingProbe(target);
            bool show = _view != null && ArcaneUiModuleSettings.TargetFrameEnabled &&
                target != null && stats != null && !GameData.InCharSelect &&
                NativeUiLayering.CanShowGameplayUi;
            bool emptyPreview = _view != null && ArcaneUiModuleSettings.TargetFrameEnabled &&
                target == null && !TargetFrameSettings.HideWhenNoTarget &&
                !GameData.InCharSelect && NativeUiLayering.CanShowGameplayUi;
            if (show && TargetFrameSettings.ShowOnlyInCombat)
            {
                bool combat;
                // Unknown native builds must remain visible rather than vanish.
                if (TargetNativeReadAdapter.TryReadCombat(target, out combat) && !combat)
                    show = false;
            }

            if (_view != null && _view.Root.gameObject.activeSelf != (show || emptyPreview))
            {
                _view.Root.gameObject.SetActive(show || emptyPreview);
                _nextRefresh = 0f;
            }
            // Never suppress when there is no target or no readable native Stats.
            NativeTargetHudBridge.Update(show);
            if (!show)
            {
                _wasCasting = false;
                _interruptUntil = 0f;
                if (emptyPreview && _view != null)
                {
                    _view.Render("No Target", "", "", 0, 1, 0, 1,
                        false, false, false, false, false);
                }
                _lastTarget = null; _lastClass = null;
                if (_auras != null) _auras.Hide();
                if (_view != null) _view.SetCasting(false, "", 0f, -1f);
                return;
            }
            if (Time.unscaledTime < _nextRefresh && target == _lastTarget) return;
            _nextRefresh = Time.unscaledTime + .08f;

            string name = !string.IsNullOrWhiteSpace(stats.MyName) ? stats.MyName :
                (target.MyNPC != null && !string.IsNullOrWhiteSpace(target.MyNPC.NPCName)
                    ? target.MyNPC.NPCName : target.name);
            // NPCs do not have player classes; do not guess one for them.
            string className = target.isNPC && (target.MyNPC == null || !target.MyNPC.SimPlayer)
                ? "" : RaidClassColors.ResolveClassNameForActor(stats, name);
            var kind = TargetHealthColourResolver.Classify(target);
            if (target != _lastTarget || className != _lastClass || kind != _lastColourKind)
            {
                // An interrupt state belongs to the previous target only.
                if (target != _lastTarget)
                {
                    _wasCasting = false;
                    _interruptUntil = 0f;
                }
                _lastTarget = target;
                _lastClass = className;
                _lastColourKind = kind;
                RefreshStyle();
            }
            _view.Render(name, "Lv " + stats.Level, className,
                stats.CurrentHP, stats.CurrentMaxHP,
                stats.CurrentMana, stats.GetCurrentMaxMana(),
                TargetFrameSettings.ShowNumbers, TargetFrameSettings.ShowPercent,
                TargetFrameSettings.ShowLevel, TargetFrameSettings.ShowClass,
                TargetFrameSettings.ShowManaPercent, FOTFAbsorbOverlay.Read(stats));
            if (_auras != null) _auras.Update(stats, TargetFrameSettings.Width, TargetFrameSettings.Height);
            string castName = "";
            float castProgress = 0f, remaining = -1f;
            bool casting = TargetFrameSettings.ShowCastBar &&
                TargetNativeReadAdapter.TryReadCast(target, out castName, out castProgress, out remaining);
            if (!casting && _wasCasting && TargetNativeReadAdapter.WasInterrupted(target))
                _interruptUntil = Time.unscaledTime + .45f;
            _wasCasting = casting;
            if (!casting && TargetFrameSettings.ShowCastBar && Time.unscaledTime < _interruptUntil)
            {
                casting = true;
                castName = "Interrupted";
                castProgress = 0f;
                remaining = -1f;
            }
            _view.SetCasting(casting, castName, castProgress, remaining,
                TargetFrameSettings.CastPlacement, TargetFrameSettings.CastOffsetX, TargetFrameSettings.CastOffsetY);
        }

        internal void RefreshLayout()
        {
            if (_view == null) return;
            _view.Root.anchoredPosition = TargetFrameSettings.Position;
            _view.SetGeometry(TargetFrameSettings.Width, TargetFrameSettings.Height, TargetFrameSettings.ShowMana);
        }

        internal void RefreshStyle()
        {
            if (_view == null) return;
            ArcaneUiFrameStyle style = ArcaneUiStyleService.FrameStyle(ArcaneUiModuleSettings.Target);
            if (!TargetFrameSettings.InheritTheme ||
                (ArcaneUiStyleService.IsBlankTheme && !ArcaneUiStyleService.BlankPaletteEnabled))
            {
                style.Health = UnitFrameAppearance.ParseColorOrDefault(TargetFrameSettings.HealthHex,
                    new Color(.36f, .61f, .52f));
                style.Mana = UnitFrameAppearance.ParseColorOrDefault(TargetFrameSettings.ManaHex,
                    new Color(.32f, .49f, .71f));
                style.MissingHealth = UnitFrameAppearance.ParseColorOrDefault(TargetFrameSettings.BackgroundHex,
                    new Color(.10f, .14f, .16f));
                style.ResourceBackground = style.MissingHealth;
                style.Border = style.MissingHealth;
                style.BarTexture = "Flat";
                style.FontName = "Default";
            }
            Character target = GameData.PlayerControl != null ? GameData.PlayerControl.CurrentTarget : null;
            Stats stats = target != null ? target.MyStats : null;
            string name = stats != null ? stats.MyName : null;
            bool overrideHealth;
            Color overrideColour;
            TargetHealthColourResolver.Resolve(target, stats, name,
                out overrideHealth, out overrideColour);
            _view.SetStyle(style, overrideHealth, overrideColour,
                TargetFrameSettings.Texture, TargetFrameSettings.Font);
            _view.ApplyTextStyles();
        }

        internal void Shutdown()
        {
            NativeTargetHudBridge.Restore();
            if (_layer != null) Object.Destroy(_layer.gameObject);
            _layer = null;
            _view = null;
            _auras = null;
        }
    }
}
