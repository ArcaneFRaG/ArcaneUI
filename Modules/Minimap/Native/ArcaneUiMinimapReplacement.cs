using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Modules.Minimap.Configuration;

namespace ArcanesGroupFrames.Modules.Minimap.Native
{
    /// <summary>
    /// ArcaneUI-owned HUD, using Erenshor's live minimap texture and native action handlers.
    /// This deliberately does NOT disable, destroy, reparent or reimplement the native map
    /// camera, fog database, marker tracking or world-map state. Native runtime stays alive
    /// behind this HUD, even when the original HUD is invisible.
    /// </summary>
    internal sealed class ArcaneUiMinimapReplacement
    {
        private static readonly string[] Actions = { "+", "-", "V", "M", "^" };
        private readonly ThemeManager _themes;
        private RectTransform _root;
        private RectTransform _clipRoot;
        private RectTransform _imageRect;
        private RawImage _image;
        private Image _shapeImage;
        private Mask _shapeMask;
        private RectMask2D _rectangleMask;
        private MinimapMaskGeometry _shapeGeometry;
        private MinimapFrameGraphic _border;
        private RectTransform _borderRect;
        private Text _zone;
        private Text _coords;
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private readonly Dictionary<string, GameObject> _nativeActions = new Dictionary<string, GameObject>();
        private string _lastShape;
        private string _lastTypography;
        private float _nextControlsScan;
        private float _nextLabels;
        private string _status = "Replacement not constructed";
        private bool _ready;
        private bool _expanded;
        internal bool IsExpanded { get { return _expanded; } }
        internal void ToggleExpanded() { if (Ready) _expanded = !_expanded; }
        internal void SetExpanded(bool expanded) { _expanded = expanded; }
        internal bool Ready { get { return _ready && _root != null && _image != null && _image.texture != null; } }
        internal RectTransform EditorRect { get { return Ready ? _root : null; } }
        internal string Status { get { return _status; } }
        internal ArcaneUiMinimapReplacement(ThemeManager themes) { _themes = themes; }

        private static GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
        private static void Center(RectTransform r, float width, float height, Vector2 position)
        {
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(width, height);
            r.anchoredPosition = position;
        }
        private static Font FindFont(Transform root)
        {
            if (root != null)
            {
                Text t = root.GetComponentInChildren<Text>(true);
                if (t != null && t.font != null) return t.font;
            }
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        private static Text Label(Transform root, string name, string content, float size, Vector2 pos, Font font)
        {
            GameObject go = Node(name, root);
            RectTransform r = go.GetComponent<RectTransform>();
            Center(r, 250f, size + 9f, pos);
            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = Mathf.RoundToInt(size);
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            ArcaneUiGlobalTypography.ApplyControl(text, Mathf.RoundToInt(size));
            return text;
        }
        private static string FindGlyph(GameObject control)
        {
            if (control == null) return null;
            foreach (TMP_Text t in control.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == null) continue;
                string glyph = t.text != null ? t.text.Trim() : "";
                foreach (string wanted in Actions)
                    if (string.Equals(glyph, wanted, StringComparison.OrdinalIgnoreCase)) return wanted;
            }
            foreach (Text t in control.GetComponentsInChildren<Text>(true))
            {
                if (t == null) continue;
                string glyph = t.text != null ? t.text.Trim() : "";
                foreach (string wanted in Actions)
                    if (string.Equals(glyph, wanted, StringComparison.OrdinalIgnoreCase)) return wanted;
            }
            return null;
        }
        private void FindActions(RectTransform nativeRoot)
        {
            _nativeActions.Clear();
            if (nativeRoot == null) return;
            Button[] buttons = nativeRoot.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button == null || !button.gameObject.activeInHierarchy) continue;
                string glyph = FindGlyph(button.gameObject);
                if (!string.IsNullOrEmpty(glyph) && !_nativeActions.ContainsKey(glyph))
                    _nativeActions[glyph] = button.gameObject;
            }
            // For game versions with EventTrigger instead of Button, accept only
            // a unique pointer-click handler whose label matches an exact glyph.
            foreach (TMP_Text label in nativeRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label == null || !label.gameObject.activeInHierarchy) continue;
                string glyph = label.text == null ? "" : label.text.Trim().ToUpperInvariant();
                if (_nativeActions.ContainsKey(glyph) || Array.IndexOf(Actions, glyph) < 0) continue;
                GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(label.gameObject);
                if (target != null && target.transform.IsChildOf(nativeRoot)) _nativeActions[glyph] = target;
            }
            foreach (Text label in nativeRoot.GetComponentsInChildren<Text>(true))
            {
                if (label == null || !label.gameObject.activeInHierarchy) continue;
                string glyph = label.text == null ? "" : label.text.Trim().ToUpperInvariant();
                if (_nativeActions.ContainsKey(glyph) || Array.IndexOf(Actions, glyph) < 0) continue;
                GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(label.gameObject);
                if (target != null && target.transform.IsChildOf(nativeRoot)) _nativeActions[glyph] = target;
            }
        }
        private void Forward(string glyph)
        {
            GameObject target;
            if (!_nativeActions.TryGetValue(glyph, out target) || target == null) return;
            try
            {
                Button button = target.GetComponent<Button>();
                if (button != null)
                {
                    if (button.IsActive() && button.IsInteractable()) button.onClick.Invoke();
                    return;
                }
                if (EventSystem.current != null)
                {
                    var pointer = new PointerEventData(EventSystem.current);
                    pointer.button = PointerEventData.InputButton.Left;
                    ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
                }
            }
            catch (Exception ex)
            {
                Plugin.LogInfo("[ArcaneUI Minimap Replacement] native " + glyph + " action failed: " + ex.Message);
            }
        }
        private void Create(RectTransform nativeHud, RectTransform viewport, RawImage original)
        {
            RectTransform parent = nativeHud != null ? nativeHud.parent as RectTransform : null;
            if (parent == null || viewport == null || original == null) return;
            GameObject go = Node("ArcaneUI_ReplacementMinimap", parent);
            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = nativeHud.anchorMin;
            _root.anchorMax = nativeHud.anchorMax;
            _root.pivot = nativeHud.pivot;
            _root.sizeDelta = new Vector2(270f, 324f);
            _root.anchoredPosition = nativeHud.anchoredPosition;
            _root.localScale = Vector3.one;
            _root.SetAsLastSibling();

            GameObject clipGo = Node("MapViewport", _root);
            _clipRoot = clipGo.GetComponent<RectTransform>();
            Center(_clipRoot, 250f, 250f, new Vector2(0f, 22f));
            _shapeImage = clipGo.AddComponent<Image>();
            _shapeImage.color = Color.white;
            _shapeImage.raycastTarget = false;
            _shapeMask = clipGo.AddComponent<Mask>();
            _shapeMask.showMaskGraphic = false;
            _shapeGeometry = clipGo.AddComponent<MinimapMaskGeometry>();

            GameObject feedGo = Node("NativeLiveMapFeed", clipGo.transform);
            _imageRect = feedGo.GetComponent<RectTransform>();
            // Matching the 800x800 native child INSIDE a 250x250 viewport is crucial:
            // copying the texture into a 250x250 RawImage would show the wrong zoom.
            _imageRect.anchorMin = original.rectTransform.anchorMin;
            _imageRect.anchorMax = original.rectTransform.anchorMax;
            _imageRect.pivot = original.rectTransform.pivot;
            _imageRect.sizeDelta = original.rectTransform.sizeDelta;
            _imageRect.anchoredPosition = original.rectTransform.anchoredPosition;
            _imageRect.localRotation = original.rectTransform.localRotation;
            _imageRect.localScale = original.rectTransform.localScale;
            _image = feedGo.AddComponent<RawImage>();
            _image.raycastTarget = false;
            // Wheel zoom is a convenience action: it forwards the exact native
            // +/- controls, so game zoom limits and camera behavior stay native.
            GameObject wheelGo = Node("MapWheelZoom", clipGo.transform);
            RectTransform wheelRect = wheelGo.GetComponent<RectTransform>();
            Center(wheelRect, 250f, 250f, Vector2.zero);
            Image wheelRaycast = wheelGo.AddComponent<Image>();
            wheelRaycast.color = Color.clear;
            wheelRaycast.raycastTarget = true;
            var wheel = wheelGo.AddComponent<ArcaneUiMinimapWheelHandler>();
            wheel.Zoom = delta => Forward(delta > 0 ? "+" : "-");

            GameObject borderGo = Node("MapBorder", _root);
            _borderRect = borderGo.GetComponent<RectTransform>();
            Center(_borderRect, 250f, 250f, new Vector2(0f, 22f));
            _border = borderGo.AddComponent<MinimapFrameGraphic>();
            _border.raycastTarget = false;
            Font font = FindFont(nativeHud);
            _zone = Label(_root, "ZoneLabel", "", 15f, new Vector2(0f, -72f), font);
            _coords = Label(_root, "Coordinates", "", 12f, new Vector2(0f, 166f), font);

            for (int i = 0; i < Actions.Length; ++i)
            {
                string action = Actions[i];
                GameObject bgo = Node("NativeAction_" + action, _root);
                RectTransform br = bgo.GetComponent<RectTransform>();
                Center(br, 41f, 32f, new Vector2((i - 2) * 47f, -145f));
                Image bg = bgo.AddComponent<Image>();
                bg.color = new Color(.035f, .105f, .14f, .94f);
                Button b = bgo.AddComponent<Button>();
                b.targetGraphic = bg;
                b.onClick.AddListener(() => Forward(action));
                _buttons[action] = b;
                Label(bgo.transform, "Glyph", action, 21f, Vector2.zero, font).rectTransform.sizeDelta = new Vector2(40f, 32f);
            }
        }
        private static void SyncTransform(RectTransform src, RectTransform dest)
        {
            if (src == null || dest == null) return;
            dest.anchorMin = src.anchorMin;
            dest.anchorMax = src.anchorMax;
            dest.pivot = src.pivot;
            dest.sizeDelta = src.sizeDelta;
            dest.anchoredPosition = src.anchoredPosition;
            dest.localRotation = src.localRotation;
            dest.localScale = src.localScale;
        }
        internal void Refresh(RectTransform nativeHud, RectTransform viewport, RawImage original)
        {
            if (nativeHud == null || viewport == null || original == null || original.texture == null)
            {
                Destroy();
                _status = "No live native map texture; original HUD retained for safety.";
                return;
            }
            if (_root == null) Create(nativeHud, viewport, original);
            if (_root == null) { _ready = false; return; }
            if (_root.parent != nativeHud.parent) { Destroy(); return; }
            _image.texture = original.texture;
            _image.uvRect = original.uvRect;
            _image.color = original.color;
            _image.material = original.material;
            SyncTransform(original.rectTransform, _imageRect);

            string style = MinimapSettings.Style;
            if (_lastShape != style)
            {
                if (_rectangleMask != null) _rectangleMask.enabled = false;
                _shapeMask.enabled = style != "Square";
                _shapeImage.enabled = style != "Square";
                if (style == "Square")
                {
                    if (_rectangleMask == null) _rectangleMask = _clipRoot.gameObject.AddComponent<RectMask2D>();
                    _rectangleMask.enabled = true;
                }
                else
                {
                    _shapeImage.sprite = MinimapShapeSprites.Preview(style);
                    _shapeGeometry.SetShape(style);
                    _shapeImage.SetVerticesDirty();
                }
                _lastShape = style;
            }
            _border.Shape = style;
            _border.Inset = MinimapSettings.Inset;
            _border.Thickness = MinimapSettings.ShowBorder && MinimapSettings.Enabled ? MinimapSettings.BorderThickness : 0f;
            Color tint = _themes != null ? _themes.Current.Accent : Color.cyan;
            if (!MinimapSettings.InheritTheme) ColorUtility.TryParseHtmlString(MinimapSettings.BorderHex, out tint);
            tint.a = MinimapSettings.BorderOpacity / 100f;
            _border.color = tint;
            _border.Redraw();
            string typography = ArcaneUiGlobalTypography.FontScale + ":" +
                ArcaneUiGlobalTypography.FontStyleIndex + ":" +
                ArcaneUiGlobalTypography.OutlineIndex + ":" + ArcaneUiStyleService.GlobalFont;
            if (_lastTypography != typography)
            {
                _lastTypography = typography;
                if (_zone != null) ArcaneUiGlobalTypography.ApplyControl(_zone, 15);
                if (_coords != null) ArcaneUiGlobalTypography.ApplyControl(_coords, 12);
                foreach (Button actionButton in _buttons.Values)
                    if (actionButton != null)
                    {
                        Text actionText = actionButton.GetComponentInChildren<Text>();
                        if (actionText != null) ArcaneUiGlobalTypography.ApplyControl(actionText, 21);
                    }
            }

            // Enlarge toward the centre of the screen, not outside the HUD
            // edge. Keep native map camera, texture and button actions running.
            float scale = MinimapSettings.Scale / 100f;
            float expand = _expanded ? MinimapSettings.EnlargedScale / 100f : 1f;
            float expansionPixels = 270f * scale * (expand - 1f);
            _root.anchoredPosition = nativeHud.anchoredPosition +
                (_expanded ? new Vector2(-expansionPixels * .55f, -expansionPixels * .52f) : Vector2.zero);
            _root.localScale = new Vector3(scale * expand, scale * expand, 1f);
            if (Time.unscaledTime >= _nextControlsScan)
            {
                _nextControlsScan = Time.unscaledTime + 3f;
                FindActions(nativeHud);
            }
            foreach (KeyValuePair<string, Button> entry in _buttons)
                if (entry.Value != null) entry.Value.interactable = _nativeActions.ContainsKey(entry.Key);
            if (Time.unscaledTime >= _nextLabels)
            {
                _nextLabels = Time.unscaledTime + .25f;
                try
                {
                    if (_zone != null) _zone.text = GameData.SceneName ?? "";
                    if (_coords != null && GameData.PlayerControl != null)
                    {
                        Vector3 p = GameData.PlayerControl.transform.position;
                        _coords.text = Mathf.RoundToInt(p.x) + ", " + Mathf.RoundToInt(p.y) + ", " + Mathf.RoundToInt(p.z);
                    }
                }
                catch { /* Nonessential labels must never disable the minimap. */ }
            }
            // Do not replace a functional native HUD with a pretty but unusable
            // reproduction. All five original actions must be bound first.
            _ready = _nativeActions.Count == Actions.Length;
            _status = _ready ? "ArcaneUI map ready: live texture, " + style +
                " clipping and 5 native action proxies. Fog/markers remain game-owned." :
                "Replacement waiting for native controls (" + _nativeActions.Count +
                "/5). Original minimap remains visible. INSPECT logs candidates.";
            _root.gameObject.SetActive(MinimapSettings.ShowCustom && _ready);
        }
        internal void Inspect(RectTransform nativeRoot)
        {
            if (nativeRoot == null) return;
            FindActions(nativeRoot);
            foreach (string action in Actions)
            {
                GameObject target;
                _nativeActions.TryGetValue(action, out target);
                Plugin.LogInfo("[ArcaneUI Minimap Replacement] action=" + action +
                    " target=" + (target != null ? target.name : "UNBOUND"));
            }
            foreach (Button b in nativeRoot.GetComponentsInChildren<Button>(true))
                if (b != null) Plugin.LogInfo("[ArcaneUI Minimap Replacement] BUTTON " + b.name +
                    " glyph=" + (FindGlyph(b.gameObject) ?? "none") +
                    " active=" + b.gameObject.activeInHierarchy);
            foreach (TMP_Text t in nativeRoot.GetComponentsInChildren<TMP_Text>(true))
                if (t != null && !string.IsNullOrEmpty(t.text) && t.text.Length < 16)
                    Plugin.LogInfo("[ArcaneUI Minimap Replacement] TMP " + t.name + "=" + t.text);
        }
        internal void Destroy()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _root = null; _clipRoot = null; _imageRect = null; _image = null;
            _shapeImage = null; _shapeMask = null; _rectangleMask = null;
            _shapeGeometry = null; _border = null; _borderRect = null;
            _zone = null; _coords = null; _buttons.Clear(); _nativeActions.Clear();
            _ready = false; _expanded = false; _lastShape = null;
        }
    }
}

namespace ArcanesGroupFrames.Modules.Minimap.Native
{
    /// <summary>ArcaneUI viewport wheel only; delegates to existing native zoom buttons.</summary>
    internal sealed class ArcaneUiMinimapWheelHandler : MonoBehaviour, IScrollHandler
    {
        internal Action<float> Zoom;
        private float _next;
        public void OnScroll(PointerEventData data)
        {
            if (data == null || Mathf.Abs(data.scrollDelta.y) < .01f || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + .07f;
            if (Zoom != null) Zoom(data.scrollDelta.y);
        }
    }
}
