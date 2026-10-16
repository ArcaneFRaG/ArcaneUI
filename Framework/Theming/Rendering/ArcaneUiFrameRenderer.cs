using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames;

namespace ArcanesGroupFrames.Framework.Theming.Rendering
{
    /// <summary>
    /// Shared, presentation-only renderer used by existing party/raid units
    /// and by future player/target frames. No gameplay interactions here.
    /// </summary>
    internal static class ArcaneUiFrameRenderer
    {
        internal static void Apply(ArcaneUiFrameStyle style,
            Image root, Image background, Image resourceBackground,
            Image healthFill, Image resourceFill,
            Text name, Text percent, Text raw,
            string legacyTexture, string legacyFont, Color legacyMissingHealth, string module)
        {
            if (style.Inherit)
            {
                if (root != null) root.color = style.Border;
                if (background != null)
                {
                    background.color = style.MissingHealth;
                    // Existing bars retain their native geometry. The border
                    // is capped at 2px to avoid covering hit targets or text.
                    float inset = Mathf.Clamp(style.BorderSize, 1, 2);
                    background.rectTransform.offsetMin = new Vector2(inset, inset);
                    background.rectTransform.offsetMax = new Vector2(-inset, -inset);
                }
                if (resourceBackground != null) resourceBackground.color = style.ResourceBackground;
                if (resourceFill != null) resourceFill.color = style.Mana;
            }
            else
            {
                if (root != null) root.color = new Color(.025f, .03f, .035f, .98f);
                if (background != null)
                {
                    background.color = legacyMissingHealth;
                    background.rectTransform.offsetMin = Vector2.one;
                    background.rectTransform.offsetMax = -Vector2.one;
                }
                if (resourceBackground != null) resourceBackground.color = new Color(.035f, .04f, .055f, 1f);
                if (resourceFill != null) resourceFill.color = new Color(.18f, .42f, .85f, 1f);
            }
            string texture = !string.IsNullOrEmpty(style.BarTexture) ? style.BarTexture : legacyTexture;
            string fontName = !string.IsNullOrEmpty(style.FontName) ? style.FontName : legacyFont;
            UnitFrameAppearance.ApplyBarTexture(healthFill, texture);
            UnitFrameAppearance.ApplyBarTexture(resourceFill, texture);
            Font font = UnitFrameAppearance.ResolveFont(fontName);
            if (font != null)
            {
                if (name != null) name.font = font;
                if (percent != null) percent.font = font;
                if (raw != null) raw.font = font;
            }
            // Fine-tuning is presentation only. Keep the native frame hit regions
            // and existing health/mana layout unchanged.
            ArcaneUiFrameFineTuning fine = ArcaneUiFrameFineTuning.For(module);
            if (root != null)
            {
                ArcaneUiFrameFineTuning.ApplyAlpha(root.gameObject, fine.FrameOpacity);
                // Leave the Graphic component enabled: some unit frames use
                // it as a raycast surface for native target/click-casting.
                root.enabled = true;
                Color border = root.color;
                if (ColorUtility.TryParseHtmlString(fine.BorderColour, out border))
                    root.color = border;
                if (!fine.ShowBorder)
                {
                    Color transparent = root.color;
                    transparent.a = 0f;
                    root.color = transparent;
                }
            }
            if (background != null)
            {
                float inset = fine.ShowBorder ? fine.BorderWidth : 0f;
                background.rectTransform.offsetMin = new Vector2(inset, inset);
                background.rectTransform.offsetMax = new Vector2(-inset, -inset);
            }
            if (healthFill != null)
                ArcaneUiFrameFineTuning.ApplyAlpha(healthFill.gameObject, fine.HealthOpacity);
            if (resourceFill != null)
                ArcaneUiFrameFineTuning.ApplyAlpha(resourceFill.gameObject, fine.ManaOpacity);
            Color textOverride;
            bool overrideText = ColorUtility.TryParseHtmlString(fine.TextColour, out textOverride);
            if (name != null)
            {
                if (overrideText) name.color = textOverride;
                name.enabled = fine.ShowName;
                ArcaneUiFrameFineTuning.ApplyAlpha(name.gameObject, fine.TextOpacity);
            }
            if (percent != null)
            {
                if (overrideText) percent.color = textOverride;
                percent.enabled = fine.ShowHealthText;
                ArcaneUiFrameFineTuning.ApplyAlpha(percent.gameObject, fine.TextOpacity);
            }
            if (raw != null)
            {
                if (overrideText) raw.color = textOverride;
                raw.enabled = fine.ShowHealthText;
                ArcaneUiFrameFineTuning.ApplyAlpha(raw.gameObject, fine.TextOpacity);
            }
        }
    }
}
