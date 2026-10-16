using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Theming.Rendering;

namespace ArcanesGroupFrames.Framework.Theming
{
    /// <summary>
    /// Global presentation defaults. Never changes game/native Text components.
    /// Explicit module/tag settings remain authoritative.
    /// </summary>
    // Retain each Text's original weight for a clean switch back to
    // Module Default after selecting global Bold/Italic/etc.
    internal sealed class ArcaneUiTypographyOriginalStyle : MonoBehaviour
    {
        internal UnityEngine.FontStyle Original;
        internal bool Initialized;
    }

    internal static class ArcaneUiGlobalTypography
    {
        private const string Prefix = "ArcaneUI.Style.Global.";
        // 100% keeps the sizes authored by each module.
        internal static int FontScale { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "FontScale", 100), 75, 150); } }
        // 0 keeps original text style; 1=Normal, 2=Bold, 3=Italic, 4=Bold Italic.
        internal static int FontStyleIndex { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "FontStyle", 0), 0, 4); } }
        // 0 inherits module/default, 1=None, 2=Thin, 3=Thick, 4=Pixel, 5=Soft Shadow.
        internal static int OutlineIndex { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "FontOutline", 0), 0, 5); } }
        internal static string FontStyleName { get {
            switch (FontStyleIndex) {
                case 1: return "Normal"; case 2: return "Bold";
                case 3: return "Italic"; case 4: return "Bold Italic";
                default: return "Module Default";
            }
        } }
        internal static string OutlineName { get {
            switch (OutlineIndex) {
                case 1: return "None"; case 2: return "Thin Border";
                case 3: return "Thick Border"; case 4: return "Pixel Border";
                case 5: return "Soft Shadow";
                default: return "Module Default";
            }
        } }
        internal static void SetFontScale(float scale)
        {
            PlayerPrefs.SetInt(Prefix + "FontScale", Mathf.Clamp(Mathf.RoundToInt(scale), 75, 150));
            PlayerPrefs.Save(); ArcaneUiStyleService.NotifyVisualChanged();
        }
        internal static void CycleFontStyle()
        {
            PlayerPrefs.SetInt(Prefix + "FontStyle", (FontStyleIndex + 1) % 5);
            PlayerPrefs.Save(); ArcaneUiStyleService.NotifyVisualChanged();
        }
        internal static void CycleOutline()
        {
            PlayerPrefs.SetInt(Prefix + "FontOutline", (OutlineIndex + 1) % 6);
            PlayerPrefs.Save(); ArcaneUiStyleService.NotifyVisualChanged();
        }
        internal static int Scale(int authoredSize)
        {
            return Mathf.Clamp(Mathf.RoundToInt(authoredSize * FontScale / 100f), 7, 44);
        }
        internal static string OutlineMode(string moduleDefault)
        {
            if (OutlineIndex == 0) return moduleDefault;
            return OutlineIndex == 1 ? ArcaneUiTextTagStyle.None :
                OutlineIndex == 2 ? ArcaneUiTextTagStyle.Thin :
                OutlineIndex == 3 ? ArcaneUiTextTagStyle.Thick :
                OutlineIndex == 4 ? ArcaneUiTextTagStyle.Pixel : ArcaneUiTextTagStyle.SoftShadow;
        }
        internal static void ApplyTextStyle(Text text)
        {
            if (text == null) return;
            ArcaneUiTypographyOriginalStyle original = text.GetComponent<ArcaneUiTypographyOriginalStyle>();
            if (original == null) original = text.gameObject.AddComponent<ArcaneUiTypographyOriginalStyle>();
            if (!original.Initialized)
            {
                original.Original = text.fontStyle;
                original.Initialized = true;
            }
            switch (FontStyleIndex)
            {
                case 0: text.fontStyle = original.Original; break;
                case 1: text.fontStyle = UnityEngine.FontStyle.Normal; break;
                case 2: text.fontStyle = UnityEngine.FontStyle.Bold; break;
                case 3: text.fontStyle = UnityEngine.FontStyle.Italic; break;
                case 4: text.fontStyle = UnityEngine.FontStyle.BoldAndItalic; break;
            }
        }
        internal static void ApplyFont(Text text, bool locallyOverridden)
        {
            if (text == null || locallyOverridden) return;
            string name = ArcaneUiStyleService.GlobalFont;
            if (string.IsNullOrEmpty(name) || name == "Theme Default") return;
            Font font = UnitFrameAppearance.ResolveFont(name);
            if (font != null) text.font = font;
        }
        // For ArcaneUI-created settings controls only; never native game UI.
        internal static void ApplyControl(Text text, int authoredSize)
        {
            if (text == null) return;
            text.fontSize = Scale(authoredSize);
            ApplyTextStyle(text);
            ApplyFont(text, false);
            string mode = OutlineMode(ArcaneUiTextTagStyle.None);
            ArcaneUiTextEffects effects = text.GetComponent<ArcaneUiTextEffects>();
            if (effects == null && mode != ArcaneUiTextTagStyle.None)
                effects = text.gameObject.AddComponent<ArcaneUiTextEffects>();
            if (effects != null) effects.SetMode(mode);
        }
    }
}
