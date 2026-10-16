using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.Theming.Rendering
{
    /// <summary>
    /// Optional Fate of the Fallen Lightbringer ward presentation. ArcaneUI
    /// remains fully functional when FOTF is not installed or has no wards.
    /// </summary>
    internal static class FOTFAbsorbOverlay
    {
        private const string Prefix = "ArcaneUI.FOTF.Absorb.";
        private const string DefaultHex = "#FFD130";
        private static bool _loaded;
        private static bool _enabled;
        private static string _hex;
        private static float _opacity;
        private static float _minimumVisibleWidth;
        private static Color _colour;
        // Deliberate short-lived visual-only check; never changes FOTF shield values.
        private static float _visualTestUntil;
        private static bool _testRenderLogged;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _enabled = PlayerPrefs.GetInt(Prefix + "Enabled", 1) != 0;
            _hex = PlayerPrefs.GetString(Prefix + "Colour", DefaultHex);
            _opacity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Opacity", 43f), 10f, 100f);
            // r52/Phase 6.3 visual enhancement: small wards can occupy a
            // minimum segment. This NEVER changes actual absorb values.
            _minimumVisibleWidth = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "MinimumVisibleWidth", 15f), 0f, 30f);
            RebuildColour();
            _loaded = true;
        }

        private static void RebuildColour()
        {
            Color rgb;
            if (!ColorUtility.TryParseHtmlString(_hex, out rgb))
            {
                _hex = DefaultHex;
                ColorUtility.TryParseHtmlString(_hex, out rgb);
            }
            rgb.a = _opacity / 100f;
            _colour = rgb;
        }

        internal static bool Enabled
        {
            get { EnsureLoaded(); return _enabled; }
        }

        internal static string ColourHex
        {
            get { EnsureLoaded(); return _hex; }
        }

        internal static float Opacity
        {
            get { EnsureLoaded(); return _opacity; }
        }

        internal static float MinimumVisibleWidth
        {
            get { EnsureLoaded(); return _minimumVisibleWidth; }
        }

        internal static void SetMinimumVisibleWidth(float percent)
        {
            EnsureLoaded();
            _minimumVisibleWidth = Mathf.Clamp(percent, 0f, 30f);
            PlayerPrefs.SetFloat(Prefix + "MinimumVisibleWidth", _minimumVisibleWidth);
            PlayerPrefs.Save();
        }

        internal static void SetEnabled(bool enabled)
        {
            EnsureLoaded();
            _enabled = enabled;
            PlayerPrefs.SetInt(Prefix + "Enabled", enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        internal static void SetColour(string hex)
        {
            EnsureLoaded();
            Color parsed;
            if (!ColorUtility.TryParseHtmlString(hex, out parsed)) return;
            _hex = "#" + ColorUtility.ToHtmlStringRGB(parsed);
            RebuildColour();
            PlayerPrefs.SetString(Prefix + "Colour", _hex);
            PlayerPrefs.Save();
        }

        internal static void SetOpacity(float opacity)
        {
            EnsureLoaded();
            _opacity = Mathf.Clamp(opacity, 10f, 100f);
            RebuildColour();
            PlayerPrefs.SetFloat(Prefix + "Opacity", _opacity);
            PlayerPrefs.Save();
        }

        internal static bool VisualTestActive
        {
            get { return Time.unscaledTime < _visualTestUntil; }
        }

        internal static void StartVisualTest()
        {
            EnsureLoaded();
            if (!_enabled)
            {
                Plugin.LogDiagnosticWarning("ArcaneUI Absorb TEST: enable Show Lightbringer Absorb first.");
                return;
            }
            _visualTestUntil = Time.unscaledTime + 20f;
            _testRenderLogged = false;
            Plugin.LogDiagnosticWarning("ArcaneUI Absorb TEST: showing a temporary 25% visual-only ward on living " +
                "units for 20 seconds. This is NOT actual FOTF shield data. Close /arcaneui to view it.");
        }

        internal static int Read(Stats stats)
        {
            if (!Enabled || stats == null || stats.CurrentHP <= 0) return 0;
            if (VisualTestActive)
                return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, stats.CurrentMaxHP) * .25f));
            return FOTFAbsorbInterop.GetRemaining(stats);
        }

        /// <summary>
        /// Overlay only the rightmost/progress-end portion of existing health;
        /// shield is not added to health or max-health. All four directions work.
        /// For parent-local bars pass zero offsets; for party/raid pass health insets.
        /// </summary>
        // The overlay colour and alpha are user-configured. Never substitute
        // a colour based on health or class colour; even white bars must
        // display the configured absorb colour unchanged.
        /// <summary>
        /// Health-region-aware overlay for the shared Party/Raid card renderer.
        /// Unlike the legacy anchor-based overlay, this uses the exact health
        /// band's local bounds, so vertical wards cannot overlap the mana strip.
        /// </summary>
        internal static void RenderWithinBounds(Image image, int current, int maximum,
            int absorb, int direction, Rect bounds, Color healthColour)
        {
            if (image == null) return;
            EnsureLoaded();
            float hp = Mathf.Clamp01((float)current / Mathf.Max(1, maximum));
            float shield = _enabled && absorb > 0
                ? Mathf.Min(hp, Mathf.Max(_minimumVisibleWidth / 100f,
                    (float)absorb / Mathf.Max(1, maximum))) : 0f;
            bool visible = shield > .0001f;
            if (image.gameObject.activeSelf != visible) image.gameObject.SetActive(visible);
            if (!visible) return;
            ArcaneUiBarFillGeometry.Apply(image.rectTransform, bounds, direction,
                hp - shield, hp);
            image.color = _colour;
            if (VisualTestActive && !_testRenderLogged)
            {
                _testRenderLogged = true;
                Plugin.LogDiagnosticWarning("ArcaneUI Absorb TEST DRAW: rendering a shield on " +
                    image.transform.parent.name + " current=" + current +
                    " max=" + maximum + " testAbsorb=" + absorb +
                    " rect=" + image.rectTransform.rect.size + ".");
            }
        }

        internal static void Render(Image image, int current, int maximum, int absorb,
            int direction, Vector2 offsetMin, Vector2 offsetMax, Color healthColour)
        {
            if (image == null) return;
            EnsureLoaded();
            float hp = Mathf.Clamp01((float)current / Mathf.Max(1, maximum));
            // A minimum visual width makes even small Lightbringer wards
            // legible. It is presentation only; FOTF remains authoritative
            // for actual remaining absorb and health is never inflated.
            float shield = _enabled && absorb > 0
                ? Mathf.Min(hp, Mathf.Max(_minimumVisibleWidth / 100f,
                    (float)absorb / Mathf.Max(1, maximum))) : 0f;
            bool visible = shield > .0001f;
            if (image.gameObject.activeSelf != visible) image.gameObject.SetActive(visible);
            if (!visible) return;

            float start = hp - shield;
            float end = hp;
            RectTransform rect = image.rectTransform;
            if (direction == 1) // Right to left
            {
                rect.anchorMin = new Vector2(1f - end, 0f);
                rect.anchorMax = new Vector2(1f - start, 1f);
            }
            else if (direction == 2) // Bottom to top
            {
                rect.anchorMin = new Vector2(0f, start);
                rect.anchorMax = new Vector2(1f, end);
            }
            else if (direction == 3) // Top to bottom
            {
                rect.anchorMin = new Vector2(0f, 1f - end);
                rect.anchorMax = new Vector2(1f, 1f - start);
            }
            else // Left to right
            {
                rect.anchorMin = new Vector2(start, 0f);
                rect.anchorMax = new Vector2(end, 1f);
            }
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            // Honour the selected colour *and opacity* on every frame,
            // including Lightbringer white health bars and the visual test.
            image.color = _colour;
            if (VisualTestActive && !_testRenderLogged)
            {
                _testRenderLogged = true;
                Plugin.LogDiagnosticWarning("ArcaneUI Absorb TEST DRAW: rendering a shield on " +
                    image.transform.parent.name + " current=" + current +
                    " max=" + maximum + " testAbsorb=" + absorb +
                    " rect=" + image.rectTransform.rect.size + ".");
            }
        }
    }
}
