using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Configuration.UI;
using ArcanesGroupFrames;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Modules;

namespace ArcanesGroupFrames.Framework.Theming.Rendering
{
    /// <summary>
    /// Pure view: built once; retains no Stats/Character reference and never
    /// owns game interactions. Future target frames can share this renderer.
    /// </summary>
    internal sealed class ArcaneUiUnitFrameView
    {
        internal readonly RectTransform Root;
        private readonly Image _border, _healthBackground, _health, _absorb, _manaBackground, _mana;
        private readonly Text _name, _level, _class, _healthText, _percentText, _manaText, _manaPercentText;
        private float _width, _height;
        private bool _showMana;
        private Image _castBackground, _castFill;
        private Text _castName, _castTime;
        private bool _casting;
        private float _castProgress;
        private string _castPlacement = "Overlay Mana";
        private float _castX, _castY;
        private Color _castBaseColour = new Color(.77f, .54f, .22f, 1f);
        private readonly string _moduleId;
        private readonly RectTransform _badgeRoot;
        private readonly Image _badgeBackground;
        private readonly Text _badgeText;

        internal ArcaneUiUnitFrameView(RectTransform parent, string id, string moduleId = ArcaneUiModuleSettings.Player)
        {
            _moduleId = moduleId;
            Root = ArcaneUiControls.Panel(parent, id, 0f, 0f, 250f, 70f, Color.black, false);
            _border = Root.GetComponent<Image>();
            var background = ArcaneUiControls.Panel(Root, "HealthBackground", 0, 0, 0, 0, Color.black, false);
            _healthBackground = background.GetComponent<Image>();
            _health = ArcaneUiControls.Panel(background, "HealthFill", 0, 0, 0, 0,
                new Color(.2f, .7f, .5f, 1f), false).GetComponent<Image>();
            // Overlay on HP only. Labels and cast/mana remain unobstructed.
            _absorb = ArcaneUiControls.Panel(background, "LightbringerAbsorb", 0, 0, 0, 0,
                new Color(1f, .82f, .19f, .43f), false).GetComponent<Image>();
            _absorb.raycastTarget = false;
            _absorb.gameObject.SetActive(false);
            var resource = ArcaneUiControls.Panel(Root, "ManaBackground", 0, 0, 0, 0, Color.black, false);
            _manaBackground = resource.GetComponent<Image>();
            _mana = ArcaneUiControls.Panel(resource, "ManaFill", 0, 0, 0, 0,
                new Color(.3f, .4f, .9f, 1f), false).GetComponent<Image>();
            _name = ArcaneUiControls.Label(Root, "Name", "", 9, 2, 160, 24, 13,
                Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            _level = ArcaneUiControls.Label(Root, "Level", "", 112, 2, 47, 24, 11,
                Color.white, TextAnchor.MiddleRight);
            _class = ArcaneUiControls.Label(Root, "Class", "", 159, 2, 83, 24, 11,
                Color.white, TextAnchor.MiddleRight);
            _healthText = ArcaneUiControls.Label(Root, "HealthNumbers", "", 9, 29, 180, 25, 12,
                Color.white, TextAnchor.MiddleLeft);
            _percentText = ArcaneUiControls.Label(Root, "HealthPercent", "", 176, 29, 67, 25, 12,
                Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            // Mana values use separate columns rather than both tags competing
            // for the same rect. Both retain independent Text-tab offsets.
            _manaPercentText = ArcaneUiControls.Label(Root, "ManaPercent", "", 7, 0, 75, 14, 10,
                Color.white, TextAnchor.MiddleLeft);
            _manaText = ArcaneUiControls.Label(Root, "ManaNumbers", "", 90, 0, 150, 14, 10,
                Color.white, TextAnchor.MiddleRight);
            var cast = ArcaneUiControls.Panel(Root, "CastBackground", 0, 0, 0, 0,
                new Color(.09f, .11f, .14f, 1f), false);
            _castBackground = cast.GetComponent<Image>();
            _castFill = ArcaneUiControls.Panel(cast, "CastFill", 0, 0, 0, 0,
                new Color(.77f, .54f, .22f, 1f), false).GetComponent<Image>();
            _castName = ArcaneUiControls.Label(cast, "CastName", "", 4, 0, 175, 19, 10,
                Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            _castTime = ArcaneUiControls.Label(cast, "CastTime", "", 175, 0, 60, 19, 10,
                Color.white, TextAnchor.MiddleRight);
            cast.gameObject.SetActive(false);
            _badgeRoot = ArcaneUiControls.Panel(Root, "ClassLevelBadge", 0f, 0f, 32f, 32f,
                new Color(.4f, .28f, .16f, 1f), false);
            _badgeBackground = _badgeRoot.GetComponent<Image>();
            _badgeBackground.raycastTarget = false;
            _badgeText = ArcaneUiControls.Label(_badgeRoot, "BadgeText", "", 0f, 0f, 32f, 32f,
                15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _badgeText.raycastTarget = false;
            _badgeRoot.gameObject.SetActive(false);
            SetGeometry(250, 70, true);
        }

        private static void Place(RectTransform r, float left, float top, float width, float height)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(left, -top);
            r.sizeDelta = new Vector2(width, height);
        }
        private static void SetFill(Image image, float percent, int direction)
        {
            var r = image.rectTransform;
            float p = Mathf.Clamp01(percent);
            r.anchorMin = direction == 1 ? new Vector2(1f - p, 0f) :
                direction == 2 ? new Vector2(0f, 0f) :
                direction == 3 ? new Vector2(0f, 1f - p) : new Vector2(0f, 0f);
            r.anchorMax = direction == 1 ? new Vector2(1f, 1f) :
                direction == 2 ? new Vector2(1f, p) :
                direction == 3 ? new Vector2(1f, 1f) : new Vector2(p, 1f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }
        internal void SetGeometry(float width, float height, bool showMana)
        {
            _width = width;
            _height = height;
            _showMana = showMana;
            Root.sizeDelta = new Vector2(width, height);
            ArcaneUiFrameFineTuning fine = ArcaneUiFrameFineTuning.For(_moduleId);
            float inset = fine.ShowBorder ? fine.BorderWidth : 0f;
            float usable = Mathf.Max(1f, height - inset * 2f - (showMana ? 2f : 0f));
            float resourceHeight = ArcaneUiBarLayout.ManaHeight(_moduleId, usable, showMana);
            float healthHeight = Mathf.Max(1f, usable - resourceHeight);
            Place(_healthBackground.rectTransform, inset, inset, width - inset * 2f, healthHeight);
            Place(_manaBackground.rectTransform, inset, inset + healthHeight + 2f,
                width - inset * 2f, resourceHeight);
            _manaBackground.gameObject.SetActive(showMana);
            LayoutCast();
            Place(_name.rectTransform, 10, 3, Mathf.Max(36f, width * .43f), 23);
            Place(_level.rectTransform, width - 120f, 3, 41f, 23);
            Place(_class.rectTransform, width - 77f, 3, 70f, 23);
            Place(_healthText.rectTransform, 10, Mathf.Max(22, healthHeight - 23), width * .70f, 23);
            Place(_percentText.rectTransform, width - 75, Mathf.Max(22, healthHeight - 23), 65, 23);
            // Keep percentage on the left and numerical mana on the right.
            // A minimum text rect height allows readable glyphs even when a
            // user chooses a very narrow mana bar; user tag offsets still work.
            float manaTop = inset + healthHeight + 2f;
            float textHeight = Mathf.Max(18f, resourceHeight);
            float manaPctWidth = Mathf.Max(35f, (width - 18f) * .31f);
            Place(_manaPercentText.rectTransform, 7f, manaTop, manaPctWidth, textHeight);
            float valueLeft = 9f + manaPctWidth;
            Place(_manaText.rectTransform, valueLeft, manaTop,
                Mathf.Max(20f, width - valueLeft - 8f), textHeight);
            ApplyFineTuning();
        }
        internal void SetStyle(ArcaneUiFrameStyle style, bool useClass, Color classColor,
            string texture, string font)
        {
            _border.color = style.Border;
            _healthBackground.color = style.MissingHealth;
            _manaBackground.color = style.ResourceBackground;
            _health.color = useClass ? classColor : style.Health;
            // White Lightbringer fills otherwise obscure white HP text.
            LightHealthBarContrast.HealthText(_healthText, _health.color, false);
            LightHealthBarContrast.HealthText(_percentText, _health.color, false);
            _mana.color = style.Mana;
            if (_castBackground != null)
            {
                _castBackground.color = style.ResourceBackground;
                Color castColour = ArcaneUiStyleService.Theme.Accent;
                Color userColour;
                string castKey = _moduleId == ArcaneUiModuleSettings.Target
                    ? "ArcaneUI.TargetFrame.CastColour" :
                    _moduleId == ArcaneUiModuleSettings.Player
                    ? "ArcaneUI.PlayerFrame.CastColour" : null;
                if (castKey != null &&
                    ColorUtility.TryParseHtmlString(PlayerPrefs.GetString(castKey, ""), out userColour))
                    castColour = userColour;
                _castBaseColour = castColour;
                _castFill.color = castColour;
                _castName.color = style.Text;
                _castTime.color = style.Text;
            }
            string chosenTexture = !string.IsNullOrEmpty(texture) ? texture : style.BarTexture;
            string chosenFont = !string.IsNullOrEmpty(font) ? font : style.FontName;
            UnitFrameAppearance.ApplyBarTexture(_health, chosenTexture);
            UnitFrameAppearance.ApplyBarTexture(_mana, chosenTexture);
            if (_castFill != null) UnitFrameAppearance.ApplyBarTexture(_castFill, chosenTexture);
            Font resolved = UnitFrameAppearance.ResolveFont(chosenFont);
            if (resolved != null)
            {
                _name.font = resolved;
                _level.font = resolved;
                _class.font = resolved;
                _healthText.font = resolved;
                _percentText.font = resolved;
                _manaText.font = resolved;
                _manaPercentText.font = resolved;
                if (_castName != null) _castName.font = resolved;
                if (_castTime != null) _castTime.font = resolved;
            }
            Color text = style.Text;
            _name.color = text;
            _level.color = text;
            _class.color = text;
            _healthText.color = text;
            _percentText.color = text;
            _manaText.color = text;
            _manaPercentText.color = text;
            ApplyFineTuning();
        }
        internal void ApplyFineTuning()
        {
            ArcaneUiFrameFineTuning fine = ArcaneUiFrameFineTuning.For(_moduleId);
            ArcaneUiFrameFineTuning.ApplyAlpha(Root.gameObject, fine.FrameOpacity);
            ArcaneUiFrameFineTuning.ApplyAlpha(_health.gameObject, fine.HealthOpacity);
            ArcaneUiFrameFineTuning.ApplyAlpha(_mana.gameObject, fine.ManaOpacity);
            _border.enabled = fine.ShowBorder;
            Color tint;
            if (ColorUtility.TryParseHtmlString(fine.BorderColour, out tint))
                _border.color = tint;
            if (ColorUtility.TryParseHtmlString(fine.TextColour, out tint))
            {
                _name.color = tint;
                _level.color = tint;
                _class.color = tint;
                _healthText.color = tint;
                _percentText.color = tint;
                _manaText.color = tint;
                _manaPercentText.color = tint;
            }
            _name.enabled = fine.ShowName;
            _healthText.enabled = fine.ShowHealthText;
            _percentText.enabled = fine.ShowHealthText;
            _manaText.enabled = fine.ShowManaText;
            _manaPercentText.enabled = fine.ShowManaText;
            ArcaneUiFrameFineTuning.ApplyAlpha(_name.gameObject, fine.TextOpacity);
            ArcaneUiFrameFineTuning.ApplyAlpha(_level.gameObject, fine.TextOpacity);
            ArcaneUiFrameFineTuning.ApplyAlpha(_class.gameObject, fine.TextOpacity);
            ArcaneUiFrameFineTuning.ApplyAlpha(_healthText.gameObject, fine.TextOpacity);
            ArcaneUiFrameFineTuning.ApplyAlpha(_percentText.gameObject, fine.TextOpacity);
            ArcaneUiFrameFineTuning.ApplyAlpha(_manaText.gameObject, fine.TextOpacity);
            ArcaneUiFrameFineTuning.ApplyAlpha(_manaPercentText.gameObject, fine.TextOpacity);
            ArcaneUiFrameFineTuning.ApplyAlpha(_castName.gameObject, fine.TextOpacity);
            ArcaneUiFrameFineTuning.ApplyAlpha(_castTime.gameObject, fine.TextOpacity);
            bool hasBadge = fine.BadgeMode != "None";
            _badgeRoot.gameObject.SetActive(hasBadge);
            if (hasBadge)
            {
                float size = fine.BadgeSize;
                float x = fine.BadgePlacement == "Right" ? _width + 4f : -size - 4f;
                Place(_badgeRoot, x + fine.BadgeX, 3f + fine.BadgeY, size, size);
                Place(_badgeText.rectTransform, 0f, 0f, size, size);
                _badgeText.fontSize = Mathf.Clamp(Mathf.RoundToInt(size * .48f), 10, 34);
                _badgeText.color = Color.white;
                if (ColorUtility.TryParseHtmlString(fine.BadgeColour, out tint))
                    _badgeBackground.color = tint;
            }
        }

        internal void ApplyTextStyles()
        {
            string module = _moduleId;
            ArcaneUiTextTagStyle.Apply(_name, module, "name", 13);
            ArcaneUiTextTagStyle.Apply(_level, module, "level", 11);
            ArcaneUiTextTagStyle.Apply(_class, module, "class", 11);
            ArcaneUiTextTagStyle.Apply(_healthText, module, "health", 12);
            ArcaneUiTextTagStyle.Apply(_percentText, module, "percent", 12);
            ArcaneUiTextTagStyle.Apply(_manaText, module, "mana", 10);
            ArcaneUiTextTagStyle.Apply(_manaPercentText, module, "mana-percent", 10);
            if (_castName != null) ArcaneUiTextTagStyle.Apply(_castName, module, "cast-name", 10);
            if (_castTime != null) ArcaneUiTextTagStyle.Apply(_castTime, module, "cast-time", 10);
        }

        internal void SetCasting(bool active, string name, float progress, float seconds,
            string placement = "Overlay Mana", float offsetX = 0f, float offsetY = 0f)
        {
            if (_castBackground == null) return;
            bool changed = _casting != active || _castPlacement != placement ||
                _castX != offsetX || _castY != offsetY;
            _casting = active;
            _castPlacement = placement ?? "Overlay Mana";
            _castX = offsetX;
            _castY = offsetY;
            _castProgress = progress < 0f ? -1f : Mathf.Clamp01(progress);
            if (changed) LayoutCast();
            _castBackground.gameObject.SetActive(active);
            if (!active) return;
            bool interrupted = string.Equals(name, "Interrupted", System.StringComparison.Ordinal);
            _castName.text = ArcaneUiNameFormatting.Fit(_castName, name, _moduleId);
            _castName.color = interrupted ? new Color(1f, .45f, .42f, 1f) : _castTime.color;
            _castTime.text = seconds >= 0f ? seconds.ToString("0.0") + "s" : "";
            // Native casting can be confirmed before we know the timer fields.
            // In that case display a subtle full-width activity strip, not a
            // fabricated percentage. Hide the time until it is verified.
            SetFill(_castFill, _castProgress < 0f ? 1f : _castProgress, 0);
            Color fill = string.Equals(name, "Interrupted", System.StringComparison.Ordinal)
                ? new Color(.88f, .19f, .19f, 1f) : _castBaseColour;
            fill.a = _castProgress < 0f ? 0.28f : 1f;
            _castFill.color = fill;
        }

        private void LayoutCast()
        {
            if (_castBackground == null) return;
            float h = Mathf.Max(14f, _manaBackground.rectTransform.rect.height);
            float w = Mathf.Max(30f, _width - 4f);
            float top;
            switch (_castPlacement)
            {
                case "Above Health": top = -h - 3f; break;
                case "Below Frame": top = _height + 3f; break;
                case "Detached": top = _height + 25f - _castY; break;
                default:
                    top = _manaBackground.rectTransform.anchoredPosition.y * -1f;
                    if (h <= 14.5f && !_showMana) top = _height - h - 2f;
                    break;
            }
            Place(_castBackground.rectTransform, 2f + _castX, top, w, h);
            Place(_castFill.rectTransform, 0f, 0f, w, h);
            Place(_castName.rectTransform, 5f, 0f, Mathf.Max(20f, w - 71f), h);
            Place(_castTime.rectTransform, Mathf.Max(5f, w - 64f), 0f, 59f, h);
            if (_castPlacement == "Overlay Mana") _manaBackground.gameObject.SetActive(_showMana && !_casting);
            else _manaBackground.gameObject.SetActive(_showMana);
        }

        internal void Render(string name, string level, string className, int health, int maxHealth,
            int mana, int maxMana, bool showNumbers, bool showPercent,
            bool showLevel, bool showClass, bool showManaPercent, int absorb = 0)
        {
            _name.text = ArcaneUiNameFormatting.Fit(_name, name, _moduleId);
            _level.text = showLevel ? (level ?? "") : "";
            _class.text = showClass ? (className ?? "") : "";
            ArcaneUiFrameFineTuning fine = ArcaneUiFrameFineTuning.For(_moduleId);
            if (fine.BadgeMode == "Class")
                _badgeText.text = string.IsNullOrEmpty(className) ? "?" : className.Substring(0,1).ToUpperInvariant();
            else if (fine.BadgeMode == "Level")
                _badgeText.text = string.IsNullOrEmpty(level) ? "?" : level.Replace("Lv ", "");
            health = Mathf.Max(0, health);
            maxHealth = Mathf.Max(1, maxHealth);
            mana = Mathf.Max(0, mana);
            maxMana = Mathf.Max(1, maxMana);
            int healthDirection = ArcaneUiBarDirection.Health(_moduleId);
            int manaDirection = ArcaneUiBarDirection.Mana(_moduleId);
            SetFill(_health, (float)health / maxHealth, healthDirection);
            FOTFAbsorbOverlay.Render(_absorb, health, maxHealth, absorb, healthDirection,
                Vector2.zero, Vector2.zero, _health.color);
            SetFill(_mana, (float)mana / maxMana, manaDirection);
            _healthText.text = showNumbers ? ArcaneUiValueFormatting.Format(health) + " / " +
                ArcaneUiValueFormatting.Format(maxHealth) : "";
            _percentText.text = showPercent ? Mathf.RoundToInt(health * 100f / maxHealth) + "%" : "";
            _manaText.text = _showMana && showNumbers ? ArcaneUiValueFormatting.Format(mana) +
                " / " + ArcaneUiValueFormatting.Format(maxMana) : "";
            _manaPercentText.text = _showMana && showManaPercent
                ? Mathf.RoundToInt(mana * 100f / maxMana) + "%" : "";
        }
    }
}
