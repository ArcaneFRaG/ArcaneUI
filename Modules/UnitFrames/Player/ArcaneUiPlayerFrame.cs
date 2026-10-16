using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Theming.Rendering;
using ArcanesGroupFrames.Framework.NativeUI.Player;
using ArcanesGroupFrames.Modules.UnitFrames.Target;

namespace ArcanesGroupFrames.Modules.UnitFrames.Player
{
    /// <summary>Non-interactive visual representation of GameData.PlayerStats.</summary>
    internal sealed class ArcaneUiPlayerFrame
    {
        private readonly ArcaneCanvasManager _canvas;
        private RectTransform _layer;
        private ArcaneUiUnitFrameView _view;
        private ArcaneUiTargetAuras _auras;
        private ArcaneUiFOTFResources _fotfResources;
        private bool _aurasWereEnabled;
        private float _nextRefresh;
        private string _lastClass;
        private Stats _lastPlayer;
        private bool _wasCasting;
        private float _interruptUntil;

        internal RectTransform Rect { get { return _view == null ? null : _view.Root; } }
        internal RectTransform FOTFPrimaryRect { get { return _fotfResources == null ? null : _fotfResources.PrimaryRect; } }
        internal RectTransform FOTFSecondaryRect { get { return _fotfResources == null ? null : _fotfResources.SecondaryRect; } }
        internal RectTransform FOTFMassRect { get { return _fotfResources == null ? null : _fotfResources.MassRect; } }
        internal ArcaneUiPlayerFrame(ArcaneCanvasManager canvas) { _canvas = canvas; }

        internal void Initialize()
        {
            if (_layer != null || !_canvas.IsReady) return;
            _layer = _canvas.CreateLayer("ArcaneUI_PlayerFrame");
            // Keep the HUD below settings and the layout editor.
            var sorting = _layer.gameObject.AddComponent<UnityEngine.Canvas>();
            sorting.overrideSorting = true;
            // Match Party/Raid HUD layering. A HUD sorted above the native
            // loading canvas remains visible even when scene-name heuristics
            // don't recognize Erenshor's transition overlay.
            sorting.sortingOrder = NativeUiLayering.GameplayCanvasSortingOrder;
            _view = new ArcaneUiUnitFrameView(_layer, "PlayerFrame");
            _auras = new ArcaneUiTargetAuras(_view.Root, ArcaneUiModuleSettings.Player);
            _fotfResources = new ArcaneUiFOTFResources(_layer, _view.Root);
            RefreshLayout();
            RefreshStyle();
            _view.Root.gameObject.SetActive(false);
        }
        internal void Update()
        {
            if (_layer == null && _canvas.IsReady) Initialize();
            Stats player = GameData.PlayerStats;
            bool show = ArcaneUiModuleSettings.PlayerFrameEnabled && player != null &&
                GameData.PlayerControl != null && !GameData.InCharSelect &&
                NativeUiLayering.CanShowGameplayUi;
            if (show && PlayerFrameSettings.ShowOnlyInCombat)
            {
                bool combat;
                if (ArcanesGroupFrames.Modules.UnitFrames.Target.TargetNativeReadAdapter.TryReadCombat(player, out combat) && !combat)
                    show = false;
            }
            if (_view != null && _view.Root.gameObject.activeSelf != show)
            {
                _view.Root.gameObject.SetActive(show);
                _nextRefresh = 0f;
            }
            // Only suppress the exact confirmed PlayerLifePar native visual.
            NativePlayerHudBridge.Update(show);
            // A separate native cast visual must not overlap ArcaneUI's cast strip.
            NativeCastBarBridge.Update(show && PlayerFrameSettings.ShowCastBar);
            if (!show || _view == null)
            {
                // Reset the mana-strip overlay immediately when gameplay ends
                // or this module is disabled, even before a new character loads.
                if (_view != null) _view.SetCasting(false, "", -1f, -1f);
                if (_auras != null) _auras.Hide();
                if (_fotfResources != null) _fotfResources.Hide();
                _aurasWereEnabled = false;
                _wasCasting = false;
                _interruptUntil = 0f;
                _lastPlayer = null;
                return;
            }
            if (player != _lastPlayer)
            {
                _lastPlayer = player;
                _wasCasting = false;
                _interruptUntil = 0f;
            }
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .05f;
            string className = RaidClassColors.ResolveClassNameForActor(player, player.MyName);
            if (_lastClass != className)
            {
                _lastClass = className;
                RefreshStyle();
            }
            _view.Render(player.MyName, "Lv " + player.Level, className,
                player.CurrentHP, player.CurrentMaxHP,
                player.CurrentMana, player.GetCurrentMaxMana(),
                PlayerFrameSettings.ShowNumbers, PlayerFrameSettings.ShowPercent,
                PlayerFrameSettings.ShowLevel, PlayerFrameSettings.ShowClass,
                PlayerFrameSettings.ShowManaPercent, FOTFAbsorbOverlay.Read(player));
            if (_fotfResources != null) _fotfResources.Update(show, PlayerFrameSettings.Width, PlayerFrameSettings.Height);
            if (_auras != null)
            {
                var fine = ArcaneUiFrameFineTuning.For(ArcaneUiModuleSettings.Player);
                bool enabled = fine.ShowBuffIcons || fine.ShowDebuffIcons;
                if (enabled) _auras.Update(player, PlayerFrameSettings.Width, PlayerFrameSettings.Height);
                else if (_aurasWereEnabled) _auras.Hide();
                _aurasWereEnabled = enabled;
            }
            string castName = "";
            float castProgress = -1f, remaining = -1f;
            bool casting = PlayerFrameSettings.ShowCastBar &&
                TargetNativeReadAdapter.TryReadCast(player, out castName, out castProgress, out remaining);
            if (!casting && _wasCasting && TargetNativeReadAdapter.WasInterrupted(player))
                _interruptUntil = Time.unscaledTime + .45f;
            _wasCasting = casting;
            if (!casting && PlayerFrameSettings.ShowCastBar && Time.unscaledTime < _interruptUntil)
            {
                casting = true;
                castName = "Interrupted";
                castProgress = 0f;
                remaining = -1f;
            }
            _view.SetCasting(casting, casting ? castName : "", casting ? castProgress : -1f,
                casting ? remaining : -1f, PlayerFrameSettings.CastPlacement,
                PlayerFrameSettings.CastOffsetX, PlayerFrameSettings.CastOffsetY);
        }
        internal void RefreshLayout()
        {
            if (_view == null) return;
            _view.Root.anchoredPosition = PlayerFrameSettings.Position;
            _view.SetGeometry(PlayerFrameSettings.Width, PlayerFrameSettings.Height, PlayerFrameSettings.ShowMana);
            if (_fotfResources != null) _fotfResources.RefreshPositions(PlayerFrameSettings.Width, PlayerFrameSettings.Height);
        }
        internal void RefreshStyle()
        {
            if (_view == null) return;
            ArcaneUiFrameStyle style = ArcaneUiStyleService.FrameStyle(ArcaneUiModuleSettings.Player);
            // Blank without a palette, or an independent Player Frame theme,
            // uses the saved player colours, not a hidden preset palette.
            if (!PlayerFrameSettings.InheritTheme ||
                (ArcaneUiStyleService.IsBlankTheme && !ArcaneUiStyleService.BlankPaletteEnabled))
            {
                style.Health = UnitFrameAppearance.ParseColorOrDefault(PlayerFrameSettings.HealthHex, new Color(.36f,.61f,.52f));
                style.Mana = UnitFrameAppearance.ParseColorOrDefault(PlayerFrameSettings.ManaHex, new Color(.32f,.49f,.71f));
                style.MissingHealth = UnitFrameAppearance.ParseColorOrDefault(PlayerFrameSettings.BackgroundHex, new Color(.10f,.14f,.16f));
                style.ResourceBackground = style.MissingHealth;
                style.Border = style.MissingHealth;
                style.BarTexture = "Flat";
                style.FontName = "Default";
            }
            Stats stats = GameData.PlayerStats;
            Color classColor = RaidClassColors.GetColorForActor(
                stats, stats != null ? stats.MyName : null);
            _view.SetStyle(style, PlayerFrameSettings.UseClassColours, classColor,
                PlayerFrameSettings.Texture, PlayerFrameSettings.Font);
            _view.ApplyTextStyles();
        }
        internal void Shutdown()
        {
            NativePlayerHudBridge.Restore();
            NativeCastBarBridge.Restore();
            if (_fotfResources != null) _fotfResources.Shutdown();
            if (_layer != null) UnityEngine.Object.Destroy(_layer.gameObject);
            _layer = null;
            _view = null;
            _auras = null;
            _fotfResources = null;
        }
    }
}
