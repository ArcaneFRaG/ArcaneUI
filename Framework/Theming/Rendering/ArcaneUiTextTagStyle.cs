using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace ArcanesGroupFrames.Framework.Theming.Rendering
{
    // Each module/tag stores its own font size and outline mode.
    // Do not use this for targeting highlights: it styles Text graphics only.
    internal static class ArcaneUiTextTagStyle
    {
        private const string Prefix = "ArcaneUI.Text.";

        internal const string None = "None";
        internal const string Thin = "Thin Border";
        internal const string Thick = "Thick Border";
        internal const string Pixel = "Pixel Border";
        internal const string SoftShadow = "Soft Shadow";

        internal static int Size(string module, string tag, int defaultSize)
        {
            string key = Prefix + module + "." + tag + ".Size";
            return PlayerPrefs.HasKey(key) ? Mathf.Clamp(PlayerPrefs.GetInt(key, defaultSize), 7, 36) :
                ArcaneUiGlobalTypography.Scale(defaultSize);
        }

        internal static void SetSize(string module, string tag, float value)
        {
            PlayerPrefs.SetInt(Prefix + module + "." + tag + ".Size", Mathf.Clamp(Mathf.RoundToInt(value), 7, 36));
            PlayerPrefs.Save();
        }

        internal static string OutlineMode(string module, string tag)
        {
            string key = Prefix + module + "." + tag + ".Outline";
            if (!PlayerPrefs.HasKey(key)) return ArcaneUiGlobalTypography.OutlineMode(None);
            int index = Mathf.Clamp(PlayerPrefs.GetInt(key, 0), 0, 4);
            return index == 1 ? Thin : index == 2 ? Thick : index == 3 ? Pixel :
                index == 4 ? SoftShadow : None;
        }

        internal static void CycleOutline(string module, string tag)
        {
            string key = Prefix + module + "." + tag + ".Outline";
            PlayerPrefs.SetInt(key, (Mathf.Clamp(PlayerPrefs.GetInt(key, 0), 0, 4) + 1) % 5);
            PlayerPrefs.Save();
        }

        internal static Vector2 Position(string module, string tag)
        {
            string key = Prefix + module + "." + tag;
            return new Vector2(Mathf.Clamp(PlayerPrefs.GetFloat(key + ".X", 0f), -150f, 150f),
                Mathf.Clamp(PlayerPrefs.GetFloat(key + ".Y", 0f), -150f, 150f));
        }

        internal static void SetPosition(string module, string tag, float x, float y)
        {
            string key = Prefix + module + "." + tag;
            PlayerPrefs.SetFloat(key + ".X", Mathf.Clamp(x, -150f, 150f));
            PlayerPrefs.SetFloat(key + ".Y", Mathf.Clamp(y, -150f, 150f));
            PlayerPrefs.Save();
        }

        internal static string Alignment(string module, string tag)
        {
            int v = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + module + "." + tag + ".Align", 0), 0, 2);
            return v == 1 ? "Centre" : v == 2 ? "Right" : "Left";
        }
        internal static void CycleAlignment(string module, string tag)
        {
            string key = Prefix + module + "." + tag + ".Align";
            PlayerPrefs.SetInt(key, (Mathf.Clamp(PlayerPrefs.GetInt(key, 0), 0, 2) + 1) % 3);
            PlayerPrefs.Save();
        }

        internal static void Apply(Text text, string module, string tag, int defaultSize)
        {
            if (text == null) return;
            text.fontSize = Size(module, tag, defaultSize);
            ArcaneUiGlobalTypography.ApplyTextStyle(text);
            ArcaneUiGlobalTypography.ApplyFont(text, PlayerPrefs.HasKey(Prefix + module + "." + tag + ".Font") ||
                !string.IsNullOrEmpty(ArcaneUiStyleService.ModuleFontOverride(module)) ||
                (module == ArcanesGroupFrames.Framework.Modules.ArcaneUiModuleSettings.Player &&
                 !string.IsNullOrEmpty(ArcanesGroupFrames.Modules.UnitFrames.Player.PlayerFrameSettings.Font)) ||
                (module == ArcanesGroupFrames.Framework.Modules.ArcaneUiModuleSettings.Target &&
                 !string.IsNullOrEmpty(ArcanesGroupFrames.Modules.UnitFrames.Target.TargetFrameSettings.Font)));
            int align = PlayerPrefs.GetInt(Prefix + module + "." + tag + ".Align", -1);
            if (align >= 0) text.alignment = align == 1 ? TextAnchor.MiddleCenter :
                align == 2 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            // Legacy uGUI Text silently truncates glyphs when the font is
            // taller than the RectTransform. Users must be able to enlarge tags.
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var mode = OutlineMode(module, tag);
            ArcaneUiTextEffects effects = text.GetComponent<ArcaneUiTextEffects>();
            if (effects != null || mode != None)
            {
                if (effects == null) effects = text.gameObject.AddComponent<ArcaneUiTextEffects>();
                effects.SetMode(mode);
            }
            // An explicitly chosen text effect owns the entire glyph outline.
            // Permanent Raid/Party Unity Outlines and the conditional white-bar
            // contrast Outline must NOT stack beneath Soft Shadow (or any
            // custom border). This is why HP values looked heavier than the
            // mana 100% reference despite identical setting names.
            Outline legacyOutline = text.GetComponent<Outline>();
            Shadow legacyShadow = text.GetComponent<Shadow>();
            ArcaneUiLegacyTextEffectsState legacy = text.GetComponent<ArcaneUiLegacyTextEffectsState>();
            if (mode != None)
            {
                if (legacy == null) legacy = text.gameObject.AddComponent<ArcaneUiLegacyTextEffectsState>();
                if (legacyOutline != null)
                {
                    if (!legacy.OutlineRecorded) { legacy.OutlineWasEnabled = legacyOutline.enabled; legacy.OutlineRecorded = true; }
                    legacyOutline.enabled = false;
                }
                if (legacyShadow != null && !(legacyShadow is Outline))
                {
                    if (!legacy.ShadowRecorded) { legacy.ShadowWasEnabled = legacyShadow.enabled; legacy.ShadowRecorded = true; }
                    legacyShadow.enabled = false;
                }
            }
            else if (legacy != null)
            {
                // Switching Global Font Outline back to Module Default must
                // restore any pre-existing native Unity text effects.
                if (legacyOutline != null && legacy.OutlineRecorded)
                    legacyOutline.enabled = legacy.OutlineWasEnabled;
                if (legacyShadow != null && !(legacyShadow is Outline) && legacy.ShadowRecorded)
                    legacyShadow.enabled = legacy.ShadowWasEnabled;
                legacy.OutlineRecorded = false;
                legacy.ShadowRecorded = false;
            }
            // Render-time translation survives the Group/Raid resource layout
            // which recalculates offsets on each refresh. Don't mutate anchors.
            ArcaneUiTextOffset offset = text.GetComponent<ArcaneUiTextOffset>();
            Vector2 position = Position(module, tag);
            if (offset == null) offset = text.gameObject.AddComponent<ArcaneUiTextOffset>();
            offset.SetOffset(position);
        }
    }

    internal sealed class ArcaneUiLegacyTextEffectsState : MonoBehaviour
    {
        internal bool OutlineRecorded, OutlineWasEnabled;
        internal bool ShadowRecorded, ShadowWasEnabled;
    }

    /// <summary>
    /// One outline pass built from the ORIGINAL text geometry. Stacking
    /// multiple Unity Shadow components multiplies already-expanded glyphs
    /// and makes tiny HUD text nearly illegible. This effect never re-outlines
    /// previous shadow copies, and always draws the original glyph LAST.
    /// </summary>
    internal sealed class ArcaneUiTextEffects : BaseMeshEffect
    {
        private string _mode = ArcaneUiTextTagStyle.None;
        private readonly List<UIVertex> _baseTriangles = new List<UIVertex>(96);
        private readonly List<UIVertex> _resultTriangles = new List<UIVertex>(384);

        internal string Mode { get { return _mode; } }

        internal void SetMode(string mode)
        {
            mode = string.IsNullOrEmpty(mode) ? ArcaneUiTextTagStyle.None : mode;
            if (_mode == mode) return;
            _mode = mode;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null || vh.currentVertCount == 0 ||
                _mode == ArcaneUiTextTagStyle.None) return;

            _baseTriangles.Clear();
            _resultTriangles.Clear();
            vh.GetUIVertexStream(_baseTriangles);

            if (_mode == ArcaneUiTextTagStyle.Thin)
            {
                // Less than one UI pixel: legible edge without filling letters.
                AddEdge(.55f, 0f, .70f);
                AddEdge(-.55f, 0f, .70f);
                AddEdge(0f, .55f, .70f);
                AddEdge(0f, -.55f, .70f);
            }
            else if (_mode == ArcaneUiTextTagStyle.Thick)
            {
                // Denser, but only ~one pixel instead of the old (2,2).
                AddEdge(1.1f, 0f, .77f);
                AddEdge(-1.1f, 0f, .77f);
                AddEdge(0f, 1.1f, .77f);
                AddEdge(0f, -1.1f, .77f);
                AddEdge(.78f, .78f, .77f);
                AddEdge(-.78f, .78f, .77f);
                AddEdge(.78f, -.78f, .77f);
                AddEdge(-.78f, -.78f, .77f);
            }
            else if (_mode == ArcaneUiTextTagStyle.Pixel)
            {
                // Four crisp cardinal edges, no diagonals or multiplied meshes.
                AddEdge(1f, 0f, .87f);
                AddEdge(-1f, 0f, .87f);
                AddEdge(0f, 1f, .87f);
                AddEdge(0f, -1f, .87f);
            }
            else if (_mode == ArcaneUiTextTagStyle.SoftShadow)
            {
                // Gentle bottom-right shadow is often clearer on 8-12px text.
                AddEdge(.8f, -.8f, .45f);
                AddEdge(1.45f, -1.45f, .22f);
            }

            // Original text is rendered last so its bright interior survives.
            _resultTriangles.AddRange(_baseTriangles);
            vh.Clear();
            vh.AddUIVertexTriangleStream(_resultTriangles);
        }

        private void AddEdge(float x, float y, float opacity)
        {
            for (int i = 0; i < _baseTriangles.Count; i++)
            {
                UIVertex v = _baseTriangles[i];
                Vector3 pos = v.position;
                pos.x += x;
                pos.y += y;
                v.position = pos;
                Color32 c = v.color;
                c.r = c.g = c.b = 0;
                c.a = (byte)Mathf.RoundToInt(c.a * opacity);
                v.color = c;
                _resultTriangles.Add(v);
            }
        }
    }
    /// <summary>Applies a per-tag visual offset without fighting frame layout anchors.</summary>
    internal sealed class ArcaneUiTextOffset : BaseMeshEffect
    {
        private Vector2 _offset;

        internal void SetOffset(Vector2 value)
        {
            if (_offset == value) return;
            _offset = value;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null || vh.currentVertCount == 0 ||
                _offset.sqrMagnitude < .0001f) return;
            UIVertex v = default(UIVertex);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                Vector3 p = v.position;
                p.x += _offset.x;
                p.y += _offset.y;
                v.position = p;
                vh.SetUIVertex(v, i);
            }
        }
    }

}
