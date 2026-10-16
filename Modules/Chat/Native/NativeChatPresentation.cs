using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Modules.Chat.Configuration;

namespace ArcanesGroupFrames.Modules.Chat.Native
{
    /// <summary>
    /// Reversible, appearance-only decoration of the native chat. The frame is
    /// measured from the actual transcript viewport rather than an arbitrary
    /// parent RectTransform (which can be a tiny layout anchor).
    /// </summary>
    internal sealed partial class NativeChatPresentation
    {
        private const float MinimumFeedWidth = 180f;
        private const float MinimumFeedHeight = 65f;
        private readonly ThemeManager _themes;
        private RectTransform _root;
        private RectTransform _feed;
        private RectTransform _input;
        private RectTransform _canvas;
        private readonly List<GraphicSnapshot> _images = new List<GraphicSnapshot>();
        private readonly List<TextSnapshot> _texts = new List<TextSnapshot>();
        private readonly List<TmpSnapshot> _tmp = new List<TmpSnapshot>();
        private RectTransform _frame;
        private RectTransform _background;
        private const int BackdropSegments = 20;
        private float _nextScan;
        private float _nextAppearance;
        private string _status = "Waiting for a verified native chat transcript.";
        private string _geometry = "No verified chat geometry.";
        private bool _logged;
        private readonly Vector3[] _corners = new Vector3[4];

        internal string Status { get { return _status; } }
        internal string NativePath { get { return FullPath(_root); } }
        internal string Geometry { get { return _geometry; } }
        internal int TextComponents { get { return _texts.Count + _tmp.Count; } }
        internal NativeChatPresentation(ThemeManager themes) { _themes = themes; }

        private sealed class GraphicSnapshot { internal Image Image; internal Color Original; }
        private sealed class TextSnapshot { internal Text Text; internal int Size; internal float Spacing; internal Color Color; }
        private sealed class TmpSnapshot { internal TMP_Text Text; internal float Size; internal Color Color; }
        private sealed class Candidate
        {
            internal RectTransform Root;
            internal RectTransform Feed;
            internal RectTransform Input;
            internal RectTransform Canvas;
            internal int Score;
        }
        private struct Bounds2D
        {
            internal float MinX, MinY, MaxX, MaxY;
            internal float Width { get { return MaxX - MinX; } }
            internal float Height { get { return MaxY - MinY; } }
            internal Vector2 Center { get { return new Vector2((MinX + MaxX) * .5f, (MinY + MaxY) * .5f); } }
        }

        private static bool IsOwn(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.StartsWith("ArcaneUI", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        private static string FullPath(Transform t)
        {
            if (t == null) return "Not bound";
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
        private static bool ChatWord(string name)
        {
            string s = (name ?? "").ToLowerInvariant();
            return s.Contains("chat") || s.Contains("talk") || s.Contains("message") ||
                   s.Contains("say") || s.Contains("textwindow") || s.Contains("chatbox");
        }
        private static bool LogWord(string name)
        {
            string s = (name ?? "").ToLowerInvariant();
            return s.Contains("chat") || s.Contains("log") || s.Contains("history") ||
                   s.Contains("output") || s.Contains("feed") || s.Contains("message") || s.Contains("scroll");
        }
        private static Transform GetNativeTypingField()
        {
            foreach (TypeText typeText in UnityEngine.Object.FindObjectsOfType<TypeText>())
            {
                if (typeText == null || !typeText.gameObject.activeInHierarchy || IsOwn(typeText.transform)) continue;
                var field = typeText.typed as Component;
                if (field != null && !IsOwn(field.transform)) return field.transform;
            }
            // The game's TypeText component may be inactive until the player
            // starts typing. We still need its hierarchy to move the message
            // history and typing control as one native UI unit. Restrict this
            // fallback to instantiated scene objects, never asset prefabs.
            foreach (TypeText typeText in Resources.FindObjectsOfTypeAll<TypeText>())
            {
                if (typeText == null || !typeText.gameObject.scene.IsValid() || IsOwn(typeText.transform)) continue;
                var field = typeText.typed as Component;
                if (field != null && field.gameObject.scene.IsValid() && !IsOwn(field.transform))
                    return field.transform;
            }
            return null;
        }
        private static bool IsInputRelated(Transform t)
        {
            for (int i = 0; t != null && i < 5; i++, t = t.parent)
                if (t.GetComponent<InputField>() != null || t.GetComponent<TMP_InputField>() != null)
                    return true;
            return false;
        }
        private static bool HasTranscriptText(ScrollRect scroll)
        {
            foreach (Text t in scroll.GetComponentsInChildren<Text>(true))
                if (!IsOwn(t.transform) && !IsInputRelated(t.transform)) return true;
            foreach (TMP_Text t in scroll.GetComponentsInChildren<TMP_Text>(true))
                if (!IsOwn(t.transform) && !IsInputRelated(t.transform)) return true;
            return false;
        }
        private static RectTransform FeedRect(ScrollRect scroll)
        {
            return scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
        }
        private static bool ValidFeed(RectTransform rect)
        {
            return rect != null && rect.gameObject.activeInHierarchy &&
                   rect.rect.width >= MinimumFeedWidth && rect.rect.height >= MinimumFeedHeight;
        }
        private static RectTransform GetCanvasRect(Transform t)
        {
            Canvas canvas = t != null ? t.GetComponentInParent<Canvas>() : null;
            return canvas != null && canvas.rootCanvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        }
        private static Candidate DetectBinding()
        {
            Transform typing = GetNativeTypingField();
            if (typing == null) return null;
            RectTransform typingRect = typing as RectTransform;
            Candidate winner = null;
            bool tied = false;
            // Search only a small branch around the actual TypeText input; do not
            // scan every ScrollRect on the UI canvas and accidentally bind inventory.
            Transform ancestor = typing;
            for (int depth = 0; ancestor != null && depth < 9; depth++, ancestor = ancestor.parent)
            {
                RectTransform parent = ancestor as RectTransform;
                if (parent == null || IsOwn(parent)) continue;
                if (parent.GetComponent<Canvas>() != null || parent.name == "UI" || parent.name == "UIElements") break;
                foreach (ScrollRect scroll in parent.GetComponentsInChildren<ScrollRect>(true))
                {
                    if (scroll == null || IsOwn(scroll.transform) || IsInputRelated(scroll.transform) ||
                        !scroll.gameObject.activeInHierarchy) continue;
                    if (!ChatWord(parent.name) && !LogWord(scroll.name)) continue;
                    RectTransform feed = FeedRect(scroll);
                    if (!ValidFeed(feed) || !HasTranscriptText(scroll)) continue;
                    RectTransform canvas = GetCanvasRect(feed);
                    if (canvas == null || canvas.rect.width < 400f || canvas.rect.height < 250f) continue;
                    int score = (ChatWord(parent.name) ? 30 : 0) + (LogWord(scroll.name) ? 25 : 0) - depth * 2;
                    if (winner == null || score > winner.Score)
                    {
                        winner = new Candidate { Root = parent, Feed = feed, Input = typingRect, Canvas = canvas, Score = score };
                        tied = false;
                    }
                    else if (score == winner.Score && feed != winner.Feed) tied = true;
                }
            }
            return tied ? null : winner;
        }
        private void Include(RectTransform rect, ref Bounds2D bounds)
        {
            rect.GetWorldCorners(_corners);
            for (int i = 0; i < 4; i++)
            {
                Vector3 point = _canvas.InverseTransformPoint(_corners[i]);
                bounds.MinX = Mathf.Min(bounds.MinX, point.x);
                bounds.MinY = Mathf.Min(bounds.MinY, point.y);
                bounds.MaxX = Mathf.Max(bounds.MaxX, point.x);
                bounds.MaxY = Mathf.Max(bounds.MaxY, point.y);
            }
        }
        private bool TryBounds(out Bounds2D bounds)
        {
            bounds = new Bounds2D { MinX = float.MaxValue, MinY = float.MaxValue,
                MaxX = float.MinValue, MaxY = float.MinValue };
            if (_canvas == null || !ValidFeed(_feed)) return false;
            Include(_feed, ref bounds);
            float feedWidth = bounds.Width, feedHeight = bounds.Height;
            if (feedWidth < MinimumFeedWidth * .5f || feedHeight < MinimumFeedHeight * .5f) return false;
            // An input is optional for sizing: some native input fields are a
            // zero-size text anchor. Ignore those, and any distant unrelated input.
            if (_input != null && _input.gameObject.activeInHierarchy &&
                _input.rect.width >= 100f && _input.rect.height >= 12f)
            {
                Bounds2D input = new Bounds2D { MinX = float.MaxValue, MinY = float.MaxValue,
                    MaxX = float.MinValue, MaxY = float.MinValue };
                Include(_input, ref input);
                bool nearX = input.MinX <= bounds.MaxX + feedWidth * .35f &&
                             input.MaxX >= bounds.MinX - feedWidth * .35f;
                bool nearY = input.MinY <= bounds.MaxY + feedHeight * .65f &&
                             input.MaxY >= bounds.MinY - feedHeight * .65f;
                if (nearX && nearY)
                {
                    bounds.MinX = Mathf.Min(bounds.MinX, input.MinX);
                    bounds.MinY = Mathf.Min(bounds.MinY, input.MinY);
                    bounds.MaxX = Mathf.Max(bounds.MaxX, input.MaxX);
                    bounds.MaxY = Mathf.Max(bounds.MaxY, input.MaxY);
                }
            }
            // A small margin keeps the frame off the transcript glyphs.
            bounds.MinX -= ChatSettings.PaddingX; bounds.MaxX += ChatSettings.PaddingX;
            bounds.MinY -= ChatSettings.PaddingY; bounds.MaxY += ChatSettings.PaddingY;
            // Reject collapsed layouts and accidental near-fullscreen panels.
            return bounds.Width >= 185f && bounds.Height >= 75f &&
                   bounds.Width <= _canvas.rect.width * .90f &&
                   bounds.Height <= _canvas.rect.height * .90f;
        }
        internal void Update()
        {
            bool layoutEditorOpen = Plugin.Instance != null && Plugin.Instance.UiRuntime != null &&
                Plugin.Instance.UiRuntime.LayoutEditor != null &&
                Plugin.Instance.UiRuntime.LayoutEditor.IsOpen;
            bool needBinding = ChatSettings.Enabled || ChatSettings.MoveNative || layoutEditorOpen;
            if (!needBinding || !NativeUiLayering.CanShowGameplayUi)
            {
                if (_root != null) Restore();
                _status = !needBinding ? "Native chat styling and positioning disabled." : "Waiting for gameplay UI.";
                return;
            }
            if (_root != null)
            {
                if (!_root.gameObject.activeInHierarchy || _feed == null || _canvas == null || IsOwn(_root))
                { Restore(); return; }
                if (Time.unscaledTime >= _nextAppearance)
                {
                    _nextAppearance = Time.unscaledTime + .3f;
                    if (_texts.Exists(x => x.Text == null) || _tmp.Exists(x => x.Text == null))
                    { Restore(); return; }
                    if (!UpdateGeometry())
                    { Restore(); _status = "Chat bounds collapsed or moved: native chat left unchanged."; return; }
                    if (ChatSettings.Enabled) RefreshSkinVisibility();
                    else ApplyNativeLayout();
                    UpdateEditOverlay();
                }
                return;
            }
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 2f;
            Candidate candidate = DetectBinding();
            if (candidate == null)
            {
                _status = "No verified live chat transcript. Open chat and use Chat > Native > INSPECT.";
                return;
            }
            Bind(candidate);
        }
        private void Bind(Candidate binding)
        {
            Restore();
            _root = binding.Root; _feed = binding.Feed; _input = binding.Input; _canvas = binding.Canvas;
            Bounds2D bounds;
            if (!TryBounds(out bounds))
            {
                Restore();
                _status = "Rejected tiny/invalid chat geometry. Native appearance preserved; use INSPECT.";
                return;
            }
            var seen = new HashSet<Component>();
            foreach (ScrollRect scroll in _root.GetComponentsInChildren<ScrollRect>(true))
            {
                if (IsOwn(scroll.transform) || IsInputRelated(scroll.transform) ||
                    (!LogWord(scroll.name) && !ChatWord(_root.name))) continue;
                // Style the verified transcript only. This avoids treating other
                // nearby scrollable UI (e.g. item lists) as chat messages.
                if (FeedRect(scroll) != _feed) continue;
                foreach (Text txt in scroll.GetComponentsInChildren<Text>(true))
                {
                    if (IsOwn(txt.transform) || IsInputRelated(txt.transform) || !seen.Add(txt)) continue;
                    _texts.Add(new TextSnapshot { Text = txt, Size = txt.fontSize, Spacing = txt.lineSpacing, Color = txt.color });
                }
                foreach (TMP_Text txt in scroll.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (IsOwn(txt.transform) || IsInputRelated(txt.transform) || !seen.Add(txt)) continue;
                    _tmp.Add(new TmpSnapshot { Text = txt, Size = txt.fontSize, Color = txt.color });
                }
            }
            if (_texts.Count + _tmp.Count == 0) { Restore(); return; }
            // Only tint a real backdrop whose rectangle covers the transcript;
            // an input cursor, one-pixel layout anchor, or icon is not a skin.
            for (Transform t = _root; t != null && t != _canvas; t = t.parent)
            {
                Image img = t.GetComponent<Image>();
                RectTransform rect = t as RectTransform;
                if (img == null || rect == null || IsInputRelated(t)) continue;
                if (rect.rect.width < MinimumFeedWidth || rect.rect.height < MinimumFeedHeight) continue;
                Bounds2D candidateBounds = new Bounds2D { MinX = float.MaxValue, MinY = float.MaxValue,
                    MaxX = float.MinValue, MaxY = float.MinValue };
                Include(rect, ref candidateBounds);
                if (candidateBounds.Width >= bounds.Width * .65f && candidateBounds.Height >= bounds.Height * .65f &&
                    candidateBounds.Width <= bounds.Width * 1.35f && candidateBounds.Height <= bounds.Height * 1.35f)
                {
                    _images.Add(new GraphicSnapshot { Image = img, Original = img.color });
                    break;
                }
            }
            CaptureNativeLayout();
            if (ChatSettings.Enabled) ApplyStyle();
            else { ApplyNativeLayout(); UpdateGeometry(); }
            _status = "Bound native transcript: " + FullPath(_feed) + " (" + TextComponents + " labels).";
            if (!_logged) { Plugin.LogInfo("ArcaneUI chat: " + _status + " " + _geometry); _logged = true; }
        }
        private static Color Parse(string value, Color fallback)
        {
            Color color;
            return ColorUtility.TryParseHtmlString(value, out color) ? color : fallback;
        }
        private void Palette(out Color accent, out Color background, out Color ink)
        {
            accent = _themes.Current.Accent;
            background = _themes.Current.Panel;
            ink = _themes.Current.Text;
            if (!ChatSettings.InheritTheme)
            {
                accent = Parse(ChatSettings.AccentHex, accent);
                background = Parse(ChatSettings.BackgroundHex, background);
                ink = Parse(ChatSettings.TextHex, ink);
            }
            if (ChatSettings.Preset == "Refined Fantasy")
            {
                accent = Color.Lerp(accent, new Color(.83f,.63f,.33f), .45f);
                background = Color.Lerp(background, new Color(.13f,.10f,.12f), .50f);
            }
            else if (ChatSettings.Preset == "Minimal Clean")
            {
                accent = Color.Lerp(accent, new Color(.47f,.73f,.77f), .4f);
                background = Color.Lerp(background, new Color(.08f,.11f,.14f), .55f);
            }
        }
        private static RectTransform MakeOverlay(string name, RectTransform canvas, bool atBack)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(canvas, false);
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            if (atBack) rect.SetAsFirstSibling();
            else rect.SetAsLastSibling();
            return rect;
        }
        private bool UpdateGeometry()
        {
            Bounds2D bounds;
            if (!TryBounds(out bounds)) return false;
            Vector2 position = bounds.Center - _canvas.rect.center;
            Vector2 size = new Vector2(bounds.Width, bounds.Height);
            if (_background != null) { _background.anchoredPosition = position; _background.sizeDelta = size; }
            if (_frame != null) { _frame.anchoredPosition = position; _frame.sizeDelta = size; }
            _geometry = "Transcript=" + FullPath(_feed) + " size=" + _feed.rect.size +
                        ", skin=" + size + ", root=" + (_root != null ? _root.rect.size.ToString() : "none");
            return true;
        }
        internal void ApplyStyle()
        {
            if (_root == null) return;
            // A registered native-chat layout can remain active while the
            // cosmetic skin is disabled. Do not accidentally theme Erenshor's
            // text when a global theme refresh occurs in that state.
            if (!ChatSettings.Enabled)
            {
                ApplyNativeLayout();
                UpdateGeometry();
                return;
            }
            ApplyNativeLayout();
            Bounds2D bounds;
            if (!TryBounds(out bounds))
            {
                // Never create a 1x1 tinted square at the input anchor.
                RemoveDecoration();
                _status = "Chat surface too small; native appearance preserved.";
                return;
            }
            Color accent, surface, ink;
            Palette(out accent, out surface, out ink);
            Color border = ChatSettings.CustomBorderColor ? Parse(ChatSettings.BorderHex, accent) : accent;
            float alpha = ChatSettings.BackgroundOpacity / 100f;
            bool patterned = ChatSettings.BackgroundStyle != "Solid";
            ApplyAdvancedText();
            foreach (GraphicSnapshot item in _images)
            {
                if (item.Image == null) continue;
                item.Image.color = ChatSettings.TintBackground ?
                    (patterned ? Color.clear : new Color(surface.r, surface.g, surface.b, alpha)) : item.Original;
            }
            foreach (TextSnapshot item in _texts)
            {
                if (item.Text == null) continue;
                item.Text.fontSize = ChatSettings.StyleText ? Mathf.RoundToInt(ChatSettings.FontSize) : item.Size;
                item.Text.lineSpacing = ChatSettings.StyleText ? ChatSettings.LineSpacing : item.Spacing;
                item.Text.color = item.Color; // Preserve native channel colours.
            }
            foreach (TmpSnapshot item in _tmp)
            {
                if (item.Text == null) continue;
                item.Text.fontSize = ChatSettings.StyleText ? ChatSettings.FontSize : item.Size;
                item.Text.color = item.Color;
            }
            // A backdrop and border are separate non-raycasting canvas siblings:
            // the backdrop is BEHIND native controls, borders are ABOVE them.
            // Neither inherits potentially tiny input-field anchors/layout groups.
            if (ChatSettings.TintBackground && alpha > 0f)
            {
                if (_background == null)
                {
                    _background = MakeOverlay("ArcaneUI_ChatBackdrop", _canvas, true);
                    Image image = _background.gameObject.AddComponent<Image>();
                    image.raycastTarget = false;
                }
                Image imageSurface = _background.GetComponent<Image>();
                // The native verified background receives the tint directly;
                // own backdrop is the fallback when none exists.
                imageSurface.color = !patterned && _images.Count == 0 ?
                    new Color(surface.r, surface.g, surface.b, alpha) : Color.clear;
                BuildBackdropSegments(surface, alpha);
                _background.SetAsFirstSibling();
            }
            else if (_background != null)
            {
                UnityEngine.Object.Destroy(_background.gameObject);
                _background = null;
            }
            if (ChatSettings.Frame && ChatSettings.BorderThickness >= .5f && ChatSettings.BorderStyle != "None")
            {
                if (_frame != null) UnityEngine.Object.Destroy(_frame.gameObject);
                _frame = MakeOverlay("ArcaneUI_ChatBorder", _canvas, false);
                float px = ChatSettings.BorderThickness;
                AddEdge("Top", new Vector2(0f,1f), new Vector2(1f,1f), new Vector2(0f,-px), Vector2.zero, border);
                AddEdge("Bottom", new Vector2(0f,0f), new Vector2(1f,0f), Vector2.zero, new Vector2(0f,px), border);
                AddEdge("Left", new Vector2(0f,0f), new Vector2(0f,1f), Vector2.zero, new Vector2(px,0f), border);
                AddEdge("Right", new Vector2(1f,0f), new Vector2(1f,1f), new Vector2(-px,0f), Vector2.zero, border);
                if (ChatSettings.BorderStyle == "Double" || ChatSettings.BorderStyle == "Inset")
                {
                    Color inner = ChatSettings.BorderStyle == "Double" ? border : Color.Lerp(border, surface, .65f);
                    float inset = px + (ChatSettings.BorderStyle == "Double" ? 4f : 2f);
                    AddInsetBorder(inset, Mathf.Max(1f, px * .6f), inner);
                }
            }
            else if (_frame != null)
            {
                UnityEngine.Object.Destroy(_frame.gameObject);
                _frame = null;
            }
            UpdateGeometry();
            RefreshSkinVisibility();
            UpdateEditOverlay();
        }
        // Procedural, non-interactive colour bands. No sprites or external
        // textures are required, and original native chat graphics are restored
        // when the optional skin is disabled. This is purely presentation.
        private Color BackdropSegmentColor(int i, Color baseColor, float alpha)
        {
            string style = ChatSettings.BackgroundStyle;
            Color second = Parse(ChatSettings.GradientHex, baseColor);
            float step = (i + .5f) / BackdropSegments;
            Color shade = style == "Soft Bands"
                ? (i % 2 == 0 ? baseColor : Color.Lerp(baseColor, second, .35f))
                : Color.Lerp(baseColor, second, style == "Horizontal Gradient" ? step : 1f - step);
            shade.a = alpha;
            return shade;
        }
        private void BuildBackdropSegments(Color baseColor, float alpha)
        {
            if (_background == null) return;
            // Rebuild only when the skin is refreshed, never on each frame.
            for (int i = _background.childCount - 1; i >= 0; --i)
                UnityEngine.Object.Destroy(_background.GetChild(i).gameObject);
            if (ChatSettings.BackgroundStyle == "Solid") return;
            bool horizontal = ChatSettings.BackgroundStyle == "Horizontal Gradient";
            for (int i = 0; i < BackdropSegments; ++i)
            {
                var go = new GameObject("ArcaneUI_BackdropBand_" + i,
                    typeof(RectTransform), typeof(Image));
                RectTransform r = go.GetComponent<RectTransform>();
                r.SetParent(_background, false);
                float a = (float)i / BackdropSegments;
                float b = (float)(i + 1) / BackdropSegments;
                r.anchorMin = horizontal ? new Vector2(a, 0f) : new Vector2(0f, a);
                r.anchorMax = horizontal ? new Vector2(b, 1f) : new Vector2(1f, b);
                r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
                Image image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = BackdropSegmentColor(i, baseColor, alpha);
            }
        }
        private void AddEdge(string name, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(_frame, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
        private void RemoveDecoration()
        {
            if (_frame != null) UnityEngine.Object.Destroy(_frame.gameObject);
            if (_background != null) UnityEngine.Object.Destroy(_background.gameObject);
            _frame = null; _background = null;
        }
        internal void Restore()
        {
            RemoveEditOverlay();
            RestoreAdvancedText();
            RestoreNativeLayout();
            RemoveDecoration();
            foreach (GraphicSnapshot item in _images)
                if (item.Image != null) item.Image.color = item.Original;
            foreach (TextSnapshot item in _texts)
                if (item.Text != null) { item.Text.fontSize = item.Size; item.Text.lineSpacing = item.Spacing; item.Text.color = item.Color; }
            foreach (TmpSnapshot item in _tmp)
                if (item.Text != null) { item.Text.fontSize = item.Size; item.Text.color = item.Color; }
            _images.Clear(); _texts.Clear(); _tmp.Clear();
            _root = null; _feed = null; _input = null; _canvas = null;
            _geometry = "No verified chat geometry.";
            _skinAlpha = 1f;
            _lastSkinUpdate = 0f;
        }
        internal void Refresh()
        {
            if (!ChatSettings.Enabled && !ChatSettings.MoveNative &&
                (Plugin.Instance == null || Plugin.Instance.UiRuntime == null ||
                 Plugin.Instance.UiRuntime.LayoutEditor == null ||
                 !Plugin.Instance.UiRuntime.LayoutEditor.IsOpen)) { Restore(); return; }
            if (_root != null)
            {
                if (ChatSettings.Enabled) ApplyStyle();
                else { ApplyNativeLayout(); UpdateGeometry(); }
            }
            else { _nextScan = 0f; Update(); }
        }
        internal void InspectNativeHierarchy()
        {
            Transform typing = GetNativeTypingField();
            if (typing == null)
            {
                Plugin.LogWarning("ArcaneUI chat scan: no active TypeText.typed input component found. Open chat and retry.");
                return;
            }
            var builder = new StringBuilder();
            builder.AppendLine("ArcaneUI native chat hierarchy (TypeText anchor): " + FullPath(typing));
            Transform scope = typing;
            for (int i = 0; i < 5 && scope.parent != null; i++) scope = scope.parent;
            int printed = 0;
            var stack = new Stack<KeyValuePair<Transform, int>>();
            stack.Push(new KeyValuePair<Transform, int>(scope, 0));
            while (stack.Count > 0 && printed < 160)
            {
                var node = stack.Pop();
                if (IsOwn(node.Key)) continue;
                var r = node.Key as RectTransform;
                builder.Append(' ', node.Value * 2).Append(node.Key.name);
                if (r != null) builder.Append(" rect=").Append(r.rect.size)
                    .Append(" anchor=").Append(r.anchorMin).Append("/").Append(r.anchorMax);
                if (node.Key.GetComponent<ScrollRect>() != null) builder.Append(" ScrollRect");
                if (node.Key.GetComponent<InputField>() != null || node.Key.GetComponent<TMP_InputField>() != null) builder.Append(" INPUT");
                if (node.Key.GetComponent<Text>() != null) builder.Append(" UI.Text");
                if (node.Key.GetComponent<TMP_Text>() != null) builder.Append(" TMP_Text");
                builder.AppendLine(); printed++;
                for (int i = node.Key.childCount - 1; i >= 0; i--)
                    stack.Push(new KeyValuePair<Transform, int>(node.Key.GetChild(i), node.Value + 1));
            }
            Candidate detected = DetectBinding();
            builder.AppendLine("Detected safe chat window: " + FullPath(detected != null ? detected.Root : null));
            builder.AppendLine("Detected transcript viewport: " + FullPath(detected != null ? detected.Feed : null));
            if (detected != null)
                builder.AppendLine("Transcript rect=" + detected.Feed.rect.size + " root rect=" + detected.Root.rect.size +
                    " canvas rect=" + detected.Canvas.rect.size);
            builder.AppendLine("Active skin geometry: " + _geometry);
            Plugin.LogInfo(builder.ToString());
        }
        internal void Shutdown() { Restore(); }
    }
}
