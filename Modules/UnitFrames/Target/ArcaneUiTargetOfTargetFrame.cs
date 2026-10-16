using UnityEngine;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Theming.Rendering;

namespace ArcanesGroupFrames.Modules.UnitFrames.Target
{
    /// <summary>Supplemental read-only target-of-target frame. Visible only
    /// after native relationships resolve to an actual Character; there is
    /// no native HUD suppression for this optional module.</summary>
    internal sealed class ArcaneUiTargetOfTargetFrame
    {
        private readonly RectTransform _parent;
        private readonly ArcaneUiUnitFrameView _view;
        private Character _lastActor;
        internal RectTransform Rect { get { return _view.Root; } }
        internal ArcaneUiTargetOfTargetFrame(RectTransform parent)
        {
            _parent = parent;
            _view = new ArcaneUiUnitFrameView(parent, "TargetOfTarget", ArcaneUiModuleSettings.Target);
            RefreshLayout();
            _view.Root.gameObject.SetActive(false);
        }
        internal void RefreshLayout()
        {
            _view.Root.anchoredPosition = TargetFrameSettings.TargetOfTargetPosition;
            _view.SetGeometry(190f, 48f, false);
        }
        internal void Update(Character target, bool visible)
        {
            Character actor = visible && TargetFrameSettings.ShowTargetOfTarget
                ? TargetNativeReadAdapter.TryReadTargetOfTarget(target) : null;
            bool show = actor != null && actor.MyStats != null;
            if (_view.Root.gameObject.activeSelf != show) _view.Root.gameObject.SetActive(show);
            if (!show) { _lastActor = null; return; }
            Stats stats = actor.MyStats;
            if (_lastActor != actor) { _lastActor = actor; RefreshStyle(actor); }
            _view.Render(stats.MyName, "Lv " + stats.Level, "",
                stats.CurrentHP, stats.CurrentMaxHP, stats.CurrentMana, stats.GetCurrentMaxMana(),
                true, true, false, false, false);
        }
        internal void RefreshStyle(Character actor)
        {
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
            }
            bool useClass;
            Color color;
            TargetHealthColourResolver.Resolve(actor, actor != null ? actor.MyStats : null,
                actor != null && actor.MyStats != null ? actor.MyStats.MyName : null,
                out useClass, out color);
            _view.SetStyle(style, useClass, color, TargetFrameSettings.Texture, TargetFrameSettings.Font);
            _view.ApplyTextStyles();
        }
        internal void Hide() { _view.Root.gameObject.SetActive(false); _lastActor = null; }
    }
}
