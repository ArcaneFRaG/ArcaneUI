using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Configuration.UI;
using ArcanesGroupFrames.Modules.CooldownAuras.Editor;

namespace ArcanesGroupFrames.Modules.CooldownAuras.UI
{
    /// <summary>Read-only cosmetic animation math. No game state changes, timers,
    /// coroutines, per-frame allocations or independent Unity Update behaviours.</summary>
    internal static class CustomAuraAnimation
    {
        internal struct Pose
        {
            internal float Scale;
            internal float Alpha;
            internal float GlowAlpha;
            internal Vector2 Offset;
        }

        internal static Pose Evaluate(CustomAuraDefinition aura, float clock,
            float sinceTransition, bool triggered, bool editing, string glowAction)
        {
            Pose pose = new Pose { Scale = 1f, Alpha = 1f, GlowAlpha = 0f, Offset = Vector2.zero };
            if (aura == null) return pose;
            float t = clock * Mathf.Clamp(aura.LoopSpeed, 25, 300) / 100f;
            float power = Mathf.Clamp01(aura.LoopIntensity / 100f);
            if (!editing && triggered)
            {
                if (aura.LoopAnimation == "Pulse")
                    pose.Scale *= 1f + power * .18f * Mathf.Sin(t * 6.2831853f);
                else if (aura.LoopAnimation == "Breathe")
                    pose.Alpha *= 1f - power * .55f * (.5f + .5f * Mathf.Sin(t * 3.14159265f));
                else if (aura.LoopAnimation == "Float")
                    pose.Offset.y += power * 14f * Mathf.Sin(t * 3.14159265f);
                else if (aura.LoopAnimation == "Shake")
                {
                    pose.Offset.x += power * 6f * Mathf.Sin(t * 31f);
                    pose.Offset.y += power * 3f * Mathf.Sin(t * 23f);
                }
            }
            if (!editing)
            {
                float duration = Mathf.Clamp(aura.TransitionMs, 100, 1200) / 1000f;
                float fraction = Mathf.Clamp01(Mathf.Max(0f, sinceTransition) / duration);
                float eased = fraction * (2f - fraction);
                string style = triggered ? aura.EnterAnimation : aura.ExitAnimation;
                if (style == "Fade") pose.Alpha *= triggered ? eased : 1f - eased;
                else if (style == "Pop" && triggered) pose.Scale *= .65f + .35f * eased;
                else if (style == "Shrink" && !triggered) pose.Scale *= 1f - .35f * eased;
                else if (style == "Slide Up") pose.Offset.y += triggered ? -20f * (1f - eased) : 20f * eased;
                else if (style == "Slide Down") pose.Offset.y += triggered ? 20f * (1f - eased) : -20f * eased;
                // Slide also fades on exit to avoid a suddenly cut-off frame.
                if (!triggered && style.StartsWith("Slide")) pose.Alpha *= 1f - eased;
            }
            string glow = aura.GlowStyle;
            if (glowAction == "Enable" && glow == "None") glow = "Solid";
            if (glowAction == "Disable") glow = "None";
            if (glow != "None")
            {
                float phase = .5f + .5f * Mathf.Sin(t * 6.2831853f);
                float modulation = glow == "Pulse" ? .35f + .65f * phase :
                    glow == "Flash" ? (phase > .65f ? 1f : .08f) : 1f;
                pose.GlowAlpha = aura.GlowOpacity / 100f * modulation * pose.Alpha;
            }
            return pose;
        }

        internal static bool ShouldKeepExiting(CustomAuraDefinition aura, float timeSinceExit)
        {
            return aura != null && aura.ExitAnimation != "None" &&
                timeSinceExit < Mathf.Clamp(aura.TransitionMs, 100, 1200) / 1000f;
        }

        // 4 inner and 4 outer softly layered, raycast-transparent edge strips.
        // Objects are created once per displayed aura, never in Update.
        internal static Image[] CreateGlow(RectTransform root)
        {
            var graphics = new Image[8];
            for (int i = 0; i < graphics.Length; i++)
            {
                var panel = ArcaneUiControls.Panel(root, "Glow_" + i, 0f, 0f, 1f, 1f,
                    new Color(1f, .5f, .2f, 0f), false);
                graphics[i] = panel.GetComponent<Image>();
                graphics[i].raycastTarget = false;
                panel.SetAsFirstSibling();
                panel.gameObject.SetActive(false);
            }
            return graphics;
        }

        internal static void DrawGlow(Image[] strips, CustomAuraDefinition aura,
            float width, float height, float opacity, bool layoutChanged)
        {
            if (strips == null) return;
            bool show = opacity > .001f;
            if (!show)
            {
                for (int i = 0; i < strips.Length; i++)
                    if (strips[i].gameObject.activeSelf) strips[i].gameObject.SetActive(false);
                return;
            }
            int thick = Mathf.Clamp(aura.GlowWidth, 1, 16);
            Color color;
            if (!ColorUtility.TryParseHtmlString(aura.GlowColour, out color))
                color = new Color(1f, .6f, .25f, 1f);
            for (int i = 0; i < strips.Length; i++)
            {
                Image img = strips[i];
                if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
                bool outer = i >= 4;
                int edge = i % 4;
                float thickness = outer ? thick * 2f : thick;
                float inset = outer ? thick : 0f;
                float alpha = outer ? .24f : .68f;
                color.a = opacity * alpha;
                if (img.color != color) img.color = color;
                if (!layoutChanged) continue;
                RectTransform rect = img.rectTransform;
                if (edge == 0) { rect.anchoredPosition = new Vector2(-inset, thickness * .5f - inset);
                    rect.sizeDelta = new Vector2(width + inset * 2f, thickness); }
                else if (edge == 1) { rect.anchoredPosition = new Vector2(-inset, -height + thickness * .5f + inset);
                    rect.sizeDelta = new Vector2(width + inset * 2f, thickness); }
                else if (edge == 2) { rect.anchoredPosition = new Vector2(-thickness * .5f - inset, inset);
                    rect.sizeDelta = new Vector2(thickness, height + inset * 2f); }
                else { rect.anchoredPosition = new Vector2(width - thickness * .5f + inset, inset);
                    rect.sizeDelta = new Vector2(thickness, height + inset * 2f); }
            }
        }
    }
}
