using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.Theming.Rendering
{
    /// <summary>
    /// Readability helpers for very light class-coloured health fills (notably
    /// the FOTF Lightbringer's pure white). These affect presentation only;
    /// they never alter shield amounts or the user's saved absorb colour.
    /// </summary>
    internal static class LightHealthBarContrast
    {
        private static float Luminance(Color colour)
        {
            return .2126f * colour.r + .7152f * colour.g + .0722f * colour.b;
        }

        internal static bool IsVeryLight(Color healthFill)
        {
            // Restrict contrast overrides to genuinely pale/near-white fills.
            return Luminance(healthFill) >= .88f &&
                Mathf.Min(healthFill.r, Mathf.Min(healthFill.g, healthFill.b)) >= .78f;
        }

        internal static Color AbsorbAgainst(Color healthFill, Color configuredAbsorb)
        {
            if (!IsVeryLight(healthFill)) return configuredAbsorb;

            // Compare the actual composited result, not merely the configured
            // overlay RGB. A low-alpha gold shield disappears against white.
            Color composite = Color.Lerp(healthFill, configuredAbsorb,
                Mathf.Clamp01(configuredAbsorb.a));
            if (Luminance(composite) < .78f) return configuredAbsorb;

            // Display-only contrast blue. Keep the configured colour saved so
            // non-white class bars retain their existing absorb appearance.
            return new Color(.12f, .42f, .85f, Mathf.Max(.74f, configuredAbsorb.a));
        }

        internal static void HealthText(Text text, Color healthFill, bool hasPermanentOutline)
        {
            if (text == null) return;
            bool light = IsVeryLight(healthFill);
            Outline outline = text.GetComponent<Outline>();
            // Explicit per-tag borders/shadows supersede automatic readability
            // outlines; using both produces a thick, inconsistent HP outline
            // compared with the mana 100% Soft Shadow shown in the UI.
            ArcaneUiTextEffects custom = text.GetComponent<ArcaneUiTextEffects>();
            if (custom != null && custom.Mode != ArcaneUiTextTagStyle.None)
            {
                if (outline != null) outline.enabled = false;
                return;
            }

            if (hasPermanentOutline)
            {
                // Party/raid labels already use an Outline. Strengthen that
                // existing one only when necessary; don't add a second pass.
                if (outline != null)
                {
                    outline.enabled = true;
                    outline.effectColor = new Color(0f, 0f, 0f, light ? .98f : .85f);
                }
                return;
            }

            // The shared player/target renderer has no fixed Outline on its
            // health labels. Add one lazily and toggle it with the bar colour.
            if (outline == null && light)
            {
                outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, .95f);
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = true;
            }
            if (outline != null) outline.enabled = light;
        }
    }
}
