using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Configuration.UI;

namespace ArcanesGroupFrames.Modules.UnitFrames.Target
{
    /// <summary>Read-only status effect presentation. Never consumes gameplay clicks
    /// or alters Stats.StatusEffects. Short hover captions are a fallback when
    /// native spell tooltips cannot be attached safely.</summary>
    internal sealed class ArcaneUiTargetAuras
    {
        private const int MaxAuras = 8;
        private readonly RectTransform _root;
        private readonly string _module;
        private readonly AuraIcon[] _buffs = new AuraIcon[MaxAuras];
        private readonly AuraIcon[] _debuffs = new AuraIcon[MaxAuras];
        private readonly Text _tooltip;
        private readonly List<StatusEffect> _buffBuffer = new List<StatusEffect>(16);
        private readonly List<StatusEffect> _debuffBuffer = new List<StatusEffect>(16);
        private sealed class AuraIcon
        {
            internal RectTransform Rect;
            internal Image Image;
            internal Text Fallback, Duration, Stacks;
            internal AuraHover Hover;
        }
        internal ArcaneUiTargetAuras(RectTransform parent, string module = "target-frame")
        {
            _module = module;
            _root = ArcaneUiControls.Rect(parent, "TargetAuras", 0, 0, 1, 1);
            for (int i = 0; i < MaxAuras; i++)
            {
                _buffs[i] = BuildIcon("Buff", i, false);
                _debuffs[i] = BuildIcon("Debuff", i, true);
            }
            _tooltip = ArcaneUiControls.Label(_root, "AuraTooltip", "", 0, 0, 340, 22,
                11, Color.white, TextAnchor.MiddleLeft);
            _tooltip.raycastTarget = false;
            _tooltip.gameObject.SetActive(false);
        }
        private AuraIcon BuildIcon(string prefix, int index, bool debuff)
        {
            var rt = ArcaneUiControls.Panel(_root, prefix + index, 0, 0, 20, 20,
                debuff ? new Color(.27f, .12f, .13f) : new Color(.1f, .25f, .13f));
            var icon = ArcaneUiControls.Panel(rt, "Icon", 1, 1, 18, 18, Color.white, false).GetComponent<Image>();
            var fallback = ArcaneUiControls.Label(rt, "Initial", "", 0, 0, 20, 18, 12,
                Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            var duration = ArcaneUiControls.Label(rt, "Duration", "", 0, 12, 20, 10, 8,
                Color.white, TextAnchor.MiddleRight);
            var stacks = ArcaneUiControls.Label(rt, "Stacks", "", 0, 0, 20, 10, 8,
                Color.white, TextAnchor.MiddleRight);
            var hover = rt.gameObject.AddComponent<AuraHover>();
            hover.TextChanged = text =>
            {
                _tooltip.text = text ?? "";
                _tooltip.gameObject.SetActive(!string.IsNullOrEmpty(text));
            };
            rt.gameObject.SetActive(false);
            return new AuraIcon { Rect = rt, Image = icon, Fallback = fallback,
                Duration = duration, Stacks = stacks, Hover = hover };
        }
        internal void Update(Stats stats, float frameWidth, float frameHeight)
        {
            _buffBuffer.Clear(); _debuffBuffer.Clear();
            var buffs = _buffBuffer;
            var debuffs = _debuffBuffer;
            if (stats != null && stats.StatusEffects != null && stats.CurrentHP > 0)
            {
                foreach (var status in stats.StatusEffects)
                {
                    if (status == null || status.Effect == null || status.Duration <= 0f) continue;
                    Spell spell = status.Effect;
                    bool beneficial = spell.Type == Spell.SpellType.Beneficial ||
                        spell.TargetHealing > 0 || spell.ShieldingAmt > 0;
                    bool nativeAllowed = _module != "target-frame" ||
                        (beneficial ? TargetFrameSettings.ShowBuffs : TargetFrameSettings.ShowDebuffs);
                    if (!nativeAllowed) continue;
                    if (beneficial) buffs.Add(status);
                    else debuffs.Add(status);
                }
            }
            // These choices only filter presentation: Stats.StatusEffects is never modified.
            var fine = ArcanesGroupFrames.Framework.Theming.Rendering.ArcaneUiFrameFineTuning.For(
                _module);
            if (!fine.ShowBuffIcons) buffs.Clear();
            if (!fine.ShowDebuffIcons) debuffs.Clear();
            buffs.Sort((a,b) => b.Duration.CompareTo(a.Duration));
            debuffs.Sort((a,b) => b.Duration.CompareTo(a.Duration));
            if (fine.StatusSort == "Shortest First") { buffs.Reverse(); debuffs.Reverse(); }
            Apply(_buffs, buffs, false, frameWidth, frameHeight);
            Apply(_debuffs, debuffs, true, frameWidth, frameHeight);
        }
        private void Apply(AuraIcon[] icons, List<StatusEffect> items, bool debuff,
            float frameWidth, float frameHeight)
        {
            var fine = ArcanesGroupFrames.Framework.Theming.Rendering.ArcaneUiFrameFineTuning.For(_module);
            bool isTarget = _module == "target-frame";
            float size = isTarget ? TargetFrameSettings.AuraSize : fine.AuraSize;
            float gap = isTarget ? TargetFrameSettings.AuraSpacing : fine.AuraSpacing;
            string placement = isTarget ? TargetFrameSettings.AuraPlacement : fine.AuraPlacement;
            for (int i = 0; i < icons.Length; i++)
            {
                var icon = icons[i];
                bool visible = i < items.Count && i < fine.MaxStatusIcons;
                icon.Rect.gameObject.SetActive(visible);
                if (!visible) continue;
                StatusEffect status = items[i];
                Spell spell = status.Effect;
                ArcanesGroupFrames.Framework.Theming.Rendering.ArcaneUiFrameFineTuning.ApplyAlpha(icon.Rect.gameObject,
                    fine.StatusOpacity);
                icon.Rect.sizeDelta = new Vector2(size, size);
                float x, y;
                switch (placement)
                {
                    case "Above Frame": x = (i * (size + gap)); y = -(size + gap) - (debuff ? size + gap : 0); break;
                    case "Left": x = -size - gap - (debuff ? size + gap : 0); y = i * (size + gap); break;
                    case "Right": x = frameWidth + gap + (debuff ? size + gap : 0); y = i * (size + gap); break;
                    default: x = i * (size + gap); y = frameHeight + gap + (debuff ? size + gap : 0); break;
                }
                icon.Rect.anchoredPosition = new Vector2(x + fine.AuraX, -y - fine.AuraY);
                icon.Image.rectTransform.sizeDelta = new Vector2(size - 2f, size - 2f);
                Sprite sprite = TargetNativeReadAdapter.ReadSpellIcon(spell);
                icon.Image.sprite = sprite;
                icon.Image.gameObject.SetActive(sprite != null);
                icon.Fallback.gameObject.SetActive(sprite == null);
                icon.Fallback.text = !string.IsNullOrEmpty(spell.SpellName) ? spell.SpellName.Substring(0,1) : (debuff ? "D" : "B");
                icon.Duration.text = Mathf.CeilToInt(status.Duration).ToString();
                int stacks = TargetNativeReadAdapter.ReadStacks(status);
                icon.Stacks.text = stacks > 1 ? stacks.ToString() : "";
                icon.Hover.Caption = (spell.SpellName ?? "Status Effect") + "   " +
                    status.Duration.ToString("0.0") + "s" + (stacks > 1 ? "  x" + stacks : "");
            }
            var tooltipRect = _tooltip.rectTransform;
            tooltipRect.anchoredPosition = new Vector2(0f, -(frameHeight + 2f * (size + gap) + 4f));
        }
        internal void Hide()
        {
            foreach (var icon in _buffs) icon.Rect.gameObject.SetActive(false);
            foreach (var icon in _debuffs) icon.Rect.gameObject.SetActive(false);
            _tooltip.gameObject.SetActive(false);
        }
    }

    internal sealed class AuraHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal string Caption;
        internal Action<string> TextChanged;
        public void OnPointerEnter(PointerEventData data)
        { if (TextChanged != null) TextChanged(Caption); }
        public void OnPointerExit(PointerEventData data)
        { if (TextChanged != null) TextChanged(null); }
    }
}
