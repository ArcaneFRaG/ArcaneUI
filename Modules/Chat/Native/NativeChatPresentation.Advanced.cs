using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using ArcanesGroupFrames.Modules.Chat.Configuration;

namespace ArcanesGroupFrames.Modules.Chat.Native
{
    /// <summary>
    /// Phase 11.2C: optional native-root layout and reversible transcript effects.
    /// Never moves the transcript separately from its input or rewrites chat data.
    /// </summary>
    internal sealed partial class NativeChatPresentation
    {
        private bool _layoutCaptured;
        private bool _positionApplied;
        private bool _sizeApplied;
        private bool _moveAllowed;
        private bool _resizeAllowed;
        private Vector2 _nativePosition;
        private Vector2 _nativeSizeDelta;
        private Vector2 _nativeSize;
        private string _layoutStatus = "Chat has not been bound.";
        private float _skinAlpha = 1f;
        private float _lastSkinUpdate;
        private RectTransform _editOverlay;
        private bool _dragResize;
        private bool _isDragging;
        private Vector2 _dragStart;
        private Vector2 _dragInitialOffset;
        private Vector2 _dragInitialSize;
        private readonly List<NativeTextEffectSnapshot> _extraText = new List<NativeTextEffectSnapshot>();
        private readonly List<NativeTmpEffectSnapshot> _extraTmp = new List<NativeTmpEffectSnapshot>();

        internal string LayoutStatus { get { return _layoutStatus; } }
        internal bool CanMove { get { return _layoutCaptured && _moveAllowed; } }
        internal bool CanResize { get { return _layoutCaptured && _resizeAllowed; } }
        // Selecting the chat in Layout Editor does not require a movable root.
        // Its outline always follows the real transcript viewport, never the
        // small native input anchor or an approximate saved position.
        internal bool CanSelectInEditor
        {
            get
            {
                Bounds2D bounds;
                return _feed != null && _canvas != null && TryBounds(out bounds);
            }
        }
        internal void EnsureEditorBinding()
        {
            // Layout discovery is independent of the chat's visual theme.
            // The editor is already open when this is called.
            if (_root == null) { _nextScan = 0f; Update(); }
        }
        internal Vector2 NativeSize { get { return _nativeSize; } }
        // Layout Editor access is restricted to the verified shared chat root.
        // The editor bounds come from the real transcript + usable input, not
        // the native root's RectTransform (often an undersized anchor).
        internal RectTransform NativeEditorRect { get { return _moveAllowed ? _root : null; } }
        internal bool TryGetEditorBounds(out Vector3 worldTopLeft, out Vector3 worldBottomRight)
        {
            worldTopLeft = Vector3.zero;
            worldBottomRight = Vector3.zero;
            Bounds2D b;
            if (_canvas == null || !TryBounds(out b)) return false;
            worldTopLeft = _canvas.TransformPoint(new Vector3(b.MinX, b.MaxY, 0f));
            worldBottomRight = _canvas.TransformPoint(new Vector3(b.MaxX, b.MinY, 0f));
            return true;
        }
        internal Vector2 EditorOffsetForWorldTopLeft(Vector3 desiredTopLeft)
        {
            Vector3 previousTopLeft, previousBottomRight;
            if (!TryGetEditorBounds(out previousTopLeft, out previousBottomRight))
                return new Vector2(ChatSettings.OffsetX, ChatSettings.OffsetY);
            Vector3 delta = _root.parent.InverseTransformVector(desiredTopLeft - previousTopLeft);
            return new Vector2(ChatSettings.OffsetX + delta.x, ChatSettings.OffsetY + delta.y);
        }
        internal bool TryEditorMove(Vector2 offset)
        {
            if (!_moveAllowed || _root == null) return false;
            Vector2 before = new Vector2(ChatSettings.OffsetX, ChatSettings.OffsetY);
            if (!ChatSettings.MoveNative) ChatSettings.SetBool("MoveNative", true);
            ChatSettings.SetFloat("OffsetX", offset.x, false);
            ChatSettings.SetFloat("OffsetY", offset.y, false);
            ApplyNativeLayout();
            if (!_positionApplied)
            {
                ChatSettings.SetFloat("OffsetX", before.x, false);
                ChatSettings.SetFloat("OffsetY", before.y, false);
                ApplyNativeLayout();
                return false;
            }
            UpdateGeometry();
            return true;
        }
        internal bool TryEditorResize(Vector2 size)
        {
            if (!_resizeAllowed || _root == null) return false;
            if (!ChatSettings.ResizeNative) ChatSettings.SetBool("ResizeNative", true);
            Vector2 previous = new Vector2(ChatSettings.Width, ChatSettings.Height);
            ChatSettings.SetFloat("Width", size.x, false);
            ChatSettings.SetFloat("Height", size.y, false);
            ApplyNativeLayout();
            if (!_sizeApplied)
            {
                ChatSettings.SetFloat("Width", previous.x, false);
                ChatSettings.SetFloat("Height", previous.y, false);
                ApplyNativeLayout();
                return false;
            }
            UpdateGeometry();
            return true;
        }


        private sealed class NativeTextEffectSnapshot
        {
            internal Text Text;
            internal UnityEngine.FontStyle Style;
            internal Font OriginalFont;
            internal Shadow ExistingShadow;
            internal bool ShadowEnabled;
            internal Color ShadowColor;
            internal Vector2 ShadowDistance;
            internal Outline ExistingOutline;
            internal bool OutlineEnabled;
            internal Color OutlineColor;
            internal Vector2 OutlineDistance;
            internal Shadow AddedShadow;
            internal Outline AddedOutline;
        }
        private sealed class NativeTmpEffectSnapshot
        {
            internal TMP_Text Text;
            internal TMPro.FontStyles Style;
        }

        private static bool ChildOf(Transform child, Transform ancestor)
        {
            for (; child != null; child = child.parent) if (child == ancestor) return true;
            return false;
        }
        private static bool HasLayoutController(Transform rect)
        {
            Transform parent = rect != null ? rect.parent : null;
            return (rect != null && (rect.GetComponent<ContentSizeFitter>() != null || rect.GetComponent<AspectRatioFitter>() != null)) ||
                (parent != null && parent.GetComponent<LayoutGroup>() != null);
        }
        // When the nearest chat parent is only a small layout anchor, seek a
        // larger CHAT-NAMED ancestor that still owns both the transcript and
        // native input. Never move the canvas or a shared global UI parent.
        private void SelectNativeLayoutRoot()
        {
            if (_root == null || _canvas == null || _feed == null || _input == null) return;
            Bounds2D bounds;
            if (!TryBounds(out bounds)) return;
            RectTransform candidate = _root;
            for (int depth = 0; candidate != null && candidate != _canvas && depth < 5;
                depth++, candidate = candidate.parent as RectTransform)
            {
                if (candidate.GetComponent<Canvas>() != null ||
                    !ChatWord(candidate.name) || HasLayoutController(candidate) ||
                    !ChildOf(_feed, candidate) || !ChildOf(_input, candidate)) continue;
                Vector2 size = candidate.rect.size;
                if (size.x < bounds.Width * .68f || size.y < bounds.Height * .68f ||
                    size.x > bounds.Width * 1.30f || size.y > bounds.Height * 1.30f ||
                    size.x > _canvas.rect.width * .90f || size.y > _canvas.rect.height * .90f)
                    continue;
                // Moving a container that owns any unrelated scrolling UI
                // would also move inventory/quests; reject such candidates.
                bool unrelatedScroll = false;
                foreach (ScrollRect scroll in candidate.GetComponentsInChildren<ScrollRect>(true))
                    if (scroll != null && !ChildOf(_feed, scroll.transform) &&
                        !ChildOf(scroll.transform, _feed) && scroll.transform != _feed)
                    { unrelatedScroll = true; break; }
                if (unrelatedScroll) continue;
                _root = candidate;
                return;
            }
        }
        private void CaptureNativeLayout()
        {
            _layoutCaptured = false; _positionApplied = false; _sizeApplied = false;
            _moveAllowed = false; _resizeAllowed = false;
            if (_root == null || _feed == null || _input == null || _canvas == null)
            { _layoutStatus = "No verified native chat container."; return; }
            SelectNativeLayoutRoot();
            _nativePosition = _root.anchoredPosition;
            _nativeSizeDelta = _root.sizeDelta;
            _nativeSize = _root.rect.size;
            _layoutCaptured = true;
            Bounds2D bounds;
            if (!TryBounds(out bounds) || !ChildOf(_feed, _root) || !ChildOf(_input, _root) ||
                _root == _canvas || _root.parent == null || HasLayoutController(_root) ||
                _nativeSize.x > _canvas.rect.width * .90f ||
                _nativeSize.y > _canvas.rect.height * .90f)
            {
                _layoutStatus = "Native chat detected, but its shared container cannot be moved safely. " +
                    "Inspect its hierarchy in Chat > Native.";
                return;
            }
            // Some Erenshor chat roots are tiny layout anchors whose child
            // viewport is much larger. Their size is not a reason to reject
            // movement *if* they are explicitly named for chat and contain no
            // unrelated scrollable content. Resizing remains disabled.
            bool tinyRoot = _nativeSize.x < bounds.Width * .68f ||
                            _nativeSize.y < bounds.Height * .68f;
            if (tinyRoot)
            {
                bool unrelatedScroll = false;
                foreach (ScrollRect scroll in _root.GetComponentsInChildren<ScrollRect>(true))
                {
                    if (scroll != null && !ChildOf(_feed, scroll.transform) &&
                        !ChildOf(scroll.transform, _feed) && scroll.transform != _feed)
                    { unrelatedScroll = true; break; }
                }
                if (!ChatWord(_root.name) || unrelatedScroll)
                {
                    _layoutStatus = "Native chat detected; small shared anchor is not chat-exclusive. " +
                        "Selection available; movement withheld to protect other windows.";
                    return;
                }
            }
            _moveAllowed = true;
            if (tinyRoot)
            {
                _resizeAllowed = false;
                _layoutStatus = "Chat-only anchor verified: native chat movement available; resize disabled.";
                return;
            }
            // Only allow resizing if the verified viewport is anchored to expand
            // within its container. Fixed-size, layout-controlled chat hierarchies
            // must never be resized by guessing RectTransform dimensions.
            RectTransform candidate = _feed;
            bool stretched = true;
            while (candidate != null && candidate != _root)
            {
                if (candidate.anchorMax.x - candidate.anchorMin.x < .8f ||
                    candidate.anchorMax.y - candidate.anchorMin.y < .8f || HasLayoutController(candidate))
                { stretched = false; break; }
                candidate = candidate.parent as RectTransform;
            }
            _resizeAllowed = stretched && candidate == _root;
            _layoutStatus = _resizeAllowed ? "Verified container: move and resize available." :
                "Verified container: movement available; native resize not safe on this hierarchy.";
        }
        private void RestoreNativeLayout()
        {
            _isDragging = false;
            if (_layoutCaptured && _root != null)
            {
                // Preserve any native reflow unless ArcaneUI actually changed
                // the corresponding transform property.
                if (_positionApplied) _root.anchoredPosition = _nativePosition;
                if (_sizeApplied) _root.sizeDelta = _nativeSizeDelta;
            }
            _positionApplied = false; _sizeApplied = false;
            _layoutCaptured = false; _moveAllowed = false; _resizeAllowed = false;
            _layoutStatus = "Native layout restored / not bound.";
        }
        private void ApplyNativeLayout()
        {
            if (!_layoutCaptured || _root == null || _isDragging) return;
            bool move = _moveAllowed && ChatSettings.MoveNative;
            bool resize = _resizeAllowed && ChatSettings.ResizeNative;
            if (move)
            {
                if (!_positionApplied) _nativePosition = _root.anchoredPosition;
                _root.anchoredPosition = _nativePosition + new Vector2(ChatSettings.OffsetX, ChatSettings.OffsetY);
            }
            else if (_positionApplied) _root.anchoredPosition = _nativePosition;
            _positionApplied = move;

            if (resize)
            {
                if (!_sizeApplied)
                {
                    _nativeSizeDelta = _root.sizeDelta;
                    _nativeSize = _root.rect.size;
                }
                float width = ChatSettings.Width >= 180f ?
                    Mathf.Clamp(ChatSettings.Width, 180f, Mathf.Min(1000f, _canvas.rect.width * .85f)) : _nativeSize.x;
                float height = ChatSettings.Height >= 75f ?
                    Mathf.Clamp(ChatSettings.Height, 75f, Mathf.Min(750f, _canvas.rect.height * .85f)) : _nativeSize.y;
                _root.sizeDelta = _nativeSizeDelta + new Vector2(width - _nativeSize.x, height - _nativeSize.y);
            }
            else if (_sizeApplied) _root.sizeDelta = _nativeSizeDelta;
            _sizeApplied = resize;
            if (!move && !resize) return;
            Bounds2D b;
            // Keep chat visible and avoid leaving an unusable native input offscreen.
            if (!TryBounds(out b) || b.MinX < _canvas.rect.xMin - 12f || b.MaxX > _canvas.rect.xMax + 12f ||
                b.MinY < _canvas.rect.yMin - 12f || b.MaxY > _canvas.rect.yMax + 12f)
            {
                if (_positionApplied) _root.anchoredPosition = _nativePosition;
                if (_sizeApplied) _root.sizeDelta = _nativeSizeDelta;
                _positionApplied = false; _sizeApplied = false;
                _layoutStatus = "Requested geometry rejected (off-screen or invalid); native layout restored.";
            }
        }

        private void ApplyAdvancedText()
        {
            foreach (TextSnapshot snap in _texts)
            {
                if (snap.Text == null) continue;
                NativeTextEffectSnapshot item = _extraText.Find(x => x.Text == snap.Text);
                if (item == null)
                {
                    item = new NativeTextEffectSnapshot { Text = snap.Text, Style = snap.Text.fontStyle, OriginalFont = snap.Text.font };
                    foreach (Shadow sh in snap.Text.GetComponents<Shadow>())
                    {
                        if (sh.GetType() == typeof(Outline))
                        {
                            var outline = (Outline)sh;
                            item.ExistingOutline = outline; item.OutlineEnabled = outline.enabled;
                            item.OutlineColor = outline.effectColor; item.OutlineDistance = outline.effectDistance;
                        }
                        else if (sh.GetType() == typeof(Shadow))
                        {
                            item.ExistingShadow = sh; item.ShadowEnabled = sh.enabled;
                            item.ShadowColor = sh.effectColor; item.ShadowDistance = sh.effectDistance;
                        }
                    }
                    _extraText.Add(item);
                }
                snap.Text.font = ChatSettings.StyleText && ChatSettings.FontFamily != "Native" ?
                    ArcanesGroupFrames.UnitFrameAppearance.ResolveFont(ChatSettings.FontFamily) : item.OriginalFont;
                snap.Text.fontStyle = !ChatSettings.StyleText || ChatSettings.FontStyle == "Native" ? item.Style :
                    ChatSettings.FontStyle == "Bold" ? UnityEngine.FontStyle.Bold :
                    ChatSettings.FontStyle == "Italic" ? UnityEngine.FontStyle.Italic : UnityEngine.FontStyle.Normal;
                SetTextShadow(item);
                SetTextOutline(item);
            }
            foreach (TmpSnapshot snap in _tmp)
            {
                if (snap.Text == null) continue;
                NativeTmpEffectSnapshot item = _extraTmp.Find(x => x.Text == snap.Text);
                if (item == null)
                {
                    item = new NativeTmpEffectSnapshot { Text = snap.Text, Style = snap.Text.fontStyle };
                    _extraTmp.Add(item);
                }
                snap.Text.fontStyle = !ChatSettings.StyleText || ChatSettings.FontStyle == "Native" ? item.Style :
                    ChatSettings.FontStyle == "Bold" ? TMPro.FontStyles.Bold :
                    ChatSettings.FontStyle == "Italic" ? TMPro.FontStyles.Italic : TMPro.FontStyles.Normal;
            }
        }
        private void SetTextShadow(NativeTextEffectSnapshot item)
        {
            bool enabled = ChatSettings.StyleText && ChatSettings.ShadowText;
            Shadow target = item.ExistingShadow != null ? item.ExistingShadow : item.AddedShadow;
            if (enabled && target == null)
                target = item.AddedShadow = item.Text.gameObject.AddComponent<Shadow>();
            if (target == null) return;
            target.enabled = enabled || (target == item.ExistingShadow && item.ShadowEnabled);
            target.effectColor = enabled ? new Color(0f, 0f, 0f, .8f) : item.ShadowColor;
            target.effectDistance = enabled ? new Vector2(ChatSettings.TextEffectStrength, -ChatSettings.TextEffectStrength) : item.ShadowDistance;
        }
        private void SetTextOutline(NativeTextEffectSnapshot item)
        {
            bool enabled = ChatSettings.StyleText && ChatSettings.OutlineText;
            Outline target = item.ExistingOutline != null ? item.ExistingOutline : item.AddedOutline;
            if (enabled && target == null)
                target = item.AddedOutline = item.Text.gameObject.AddComponent<Outline>();
            if (target == null) return;
            target.enabled = enabled || (target == item.ExistingOutline && item.OutlineEnabled);
            target.effectColor = enabled ? new Color(0f, 0f, 0f, .8f) : item.OutlineColor;
            target.effectDistance = enabled ? new Vector2(ChatSettings.TextEffectStrength, ChatSettings.TextEffectStrength) : item.OutlineDistance;
        }
        private void RestoreAdvancedText()
        {
            foreach (var item in _extraText)
            {
                if (item.Text != null) { item.Text.fontStyle = item.Style; item.Text.font = item.OriginalFont; }
                if (item.ExistingShadow != null)
                { item.ExistingShadow.enabled = item.ShadowEnabled; item.ExistingShadow.effectColor = item.ShadowColor; item.ExistingShadow.effectDistance = item.ShadowDistance; }
                if (item.ExistingOutline != null)
                { item.ExistingOutline.enabled = item.OutlineEnabled; item.ExistingOutline.effectColor = item.OutlineColor; item.ExistingOutline.effectDistance = item.OutlineDistance; }
                if (item.AddedShadow != null) UnityEngine.Object.Destroy(item.AddedShadow);
                if (item.AddedOutline != null) UnityEngine.Object.Destroy(item.AddedOutline);
            }
            foreach (var item in _extraTmp) if (item.Text != null) item.Text.fontStyle = item.Style;
            _extraText.Clear(); _extraTmp.Clear();
        }
        private void AddInsetBorder(float inset, float width, Color color)
        {
            AddEdge("InnerTop", new Vector2(0,1), new Vector2(1,1),
                new Vector2(inset,-inset-width), new Vector2(-inset,-inset), color);
            AddEdge("InnerBottom", new Vector2(0,0), new Vector2(1,0),
                new Vector2(inset,inset), new Vector2(-inset,inset+width), color);
            AddEdge("InnerLeft", new Vector2(0,0), new Vector2(0,1),
                new Vector2(inset,inset), new Vector2(inset+width,-inset), color);
            AddEdge("InnerRight", new Vector2(1,0), new Vector2(1,1),
                new Vector2(-inset-width,inset), new Vector2(-inset,-inset), color);
        }
        private void RefreshSkinVisibility()
        {
            if (_root == null || _canvas == null) return;
            Bounds2D bounds;
            if (!TryBounds(out bounds)) return;
            Vector2 mouse;
            Canvas canvas = _canvas.GetComponent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            bool hovered = RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, Input.mousePosition, cam, out mouse) &&
                mouse.x >= bounds.MinX && mouse.x <= bounds.MaxX && mouse.y >= bounds.MinY && mouse.y <= bounds.MaxY;
            float target;
            if (ChatSettings.SkinVisibility == "Combat Skin Only")
            {
                // Match the existing player-frame combat reader. If Erenshor
                // cannot report combat state, do NOT hide the chat decoration.
                bool combat = false;
                Stats player = GameData.PlayerStats;
                bool known = player != null &&
                    ArcanesGroupFrames.Modules.UnitFrames.Target.TargetNativeReadAdapter.TryReadCombat(player, out combat);
                target = ChatSettings.EditLayout || !known || combat ? 1f : 0f;
            }
            else
                target = ChatSettings.SkinVisibility == "Always" || hovered || ChatSettings.EditLayout ? 1f :
                    ChatSettings.SkinVisibility == "Fade Outside Chat" ? ChatSettings.IdleOpacity / 100f : 0f;
            float deltaTime = _lastSkinUpdate <= 0f ? .15f : Mathf.Clamp(Time.unscaledTime - _lastSkinUpdate, .01f, .5f);
            _lastSkinUpdate = Time.unscaledTime;
            _skinAlpha = Mathf.MoveTowards(_skinAlpha, target, deltaTime * 5f);
            Color accent, surface, unused;
            Palette(out accent, out surface, out unused);
            foreach (GraphicSnapshot item in _images)
            {
                if (item.Image != null && ChatSettings.TintBackground)
                    item.Image.color = ChatSettings.BackgroundStyle == "Solid"
                        ? new Color(surface.r, surface.g, surface.b, ChatSettings.BackgroundOpacity / 100f * _skinAlpha)
                        : Color.clear;
            }
            if (_background != null)
            {
                var image = _background.GetComponent<Image>();
                if (image != null)
                    image.color = ChatSettings.BackgroundStyle == "Solid" && _images.Count == 0
                        ? new Color(surface.r, surface.g, surface.b, ChatSettings.BackgroundOpacity / 100f * _skinAlpha)
                        : Color.clear;
                if (ChatSettings.BackgroundStyle != "Solid")
                {
                    for (int i = 0; i < Mathf.Min(BackdropSegments, _background.childCount); ++i)
                    {
                        Image band = _background.GetChild(i).GetComponent<Image>();
                        if (band != null) band.color = BackdropSegmentColor(i, surface,
                            ChatSettings.BackgroundOpacity / 100f * _skinAlpha);
                    }
                }
            }
            if (_frame != null)
            {
                foreach (Image image in _frame.GetComponentsInChildren<Image>())
                {
                    Color color = image.color;
                    color.a = (ChatSettings.BorderOpacity / 100f) * _skinAlpha;
                    image.color = color;
                }
            }
        }
        private void RemoveEditOverlay()
        {
            if (_editOverlay != null) UnityEngine.Object.Destroy(_editOverlay.gameObject);
            _editOverlay = null;
            _isDragging = false;
        }
        private void UpdateEditOverlay()
        {
            if (!ChatSettings.EditLayout || !_moveAllowed || !ChatSettings.MoveNative || _canvas == null)
            { RemoveEditOverlay(); return; }
            if (_editOverlay == null)
            {
                _editOverlay = MakeOverlay("ArcaneUI_ChatEditHandles", _canvas, false);
                CreateHandle("MoveChat", "MOVE CHAT", new Vector2(0f,1f), new Vector2(0f,1f),
                    new Vector2(0f,0f), new Vector2(122f,24f), false);
                if (_resizeAllowed && ChatSettings.ResizeNative)
                    CreateHandle("ResizeChat", "+", new Vector2(1f,0f), new Vector2(1f,0f),
                        new Vector2(-26f,2f), new Vector2(26f,26f), true);
            }
            Bounds2D b;
            if (!TryBounds(out b)) { RemoveEditOverlay(); return; }
            _editOverlay.anchoredPosition = b.Center - _canvas.rect.center;
            _editOverlay.sizeDelta = new Vector2(b.Width, b.Height);
            var grip = _editOverlay.Find("ResizeChat");
            if (grip != null) grip.gameObject.SetActive(_resizeAllowed && ChatSettings.ResizeNative);
        }
        private void CreateHandle(string name, string caption, Vector2 min, Vector2 max,
            Vector2 anchored, Vector2 size, bool resize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(_editOverlay, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.pivot = resize ? new Vector2(1f,0f) : new Vector2(0f,1f);
            rect.anchoredPosition = anchored; rect.sizeDelta = size;
            var bg = go.GetComponent<Image>();
            bg.color = new Color(.13f,.27f,.38f,.92f);
            bg.raycastTarget = true;
            var handler = go.AddComponent<ArcaneUiChatDragHandle>();
            handler.Presentation = this; handler.Resize = resize;
            var textGo = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            var labelRect = textGo.GetComponent<RectTransform>();
            labelRect.SetParent(rect,false);
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.text = caption;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = resize ? 16 : 11;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
        }
        private bool DragLocal(PointerEventData evt, out Vector2 result)
        {
            result = Vector2.zero;
            RectTransform parent = _root != null ? _root.parent as RectTransform : null;
            return parent != null && evt != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, evt.position, evt.pressEventCamera, out result);
        }
        internal void BeginLayoutDrag(bool resize, PointerEventData evt)
        {
            if (!_moveAllowed || !ChatSettings.MoveNative || (resize && (!_resizeAllowed || !ChatSettings.ResizeNative))) return;
            if (!DragLocal(evt, out _dragStart)) return;
            _dragResize = resize;
            _dragInitialOffset = new Vector2(ChatSettings.OffsetX, ChatSettings.OffsetY);
            _dragInitialSize = _root.rect.size;
            _isDragging = true;
        }
        internal void DragLayout(PointerEventData evt)
        {
            Vector2 current;
            if (!_isDragging || !DragLocal(evt, out current)) return;
            Vector2 delta = current - _dragStart;
            if (_dragResize)
            {
                ChatSettings.SetFloat("Width", _dragInitialSize.x + delta.x, false);
                ChatSettings.SetFloat("Height", _dragInitialSize.y - delta.y, false);
            }
            else
            {
                ChatSettings.SetFloat("OffsetX", _dragInitialOffset.x + delta.x, false);
                ChatSettings.SetFloat("OffsetY", _dragInitialOffset.y + delta.y, false);
            }
            _isDragging = false; // apply guarded native layout to live geometry
            ApplyNativeLayout();
            _isDragging = true;
            UpdateGeometry();
            UpdateEditOverlay();
        }
        internal void EndLayoutDrag()
        {
            if (!_isDragging) return;
            _isDragging = false;
            PlayerPrefs.Save();
            ApplyNativeLayout();
            UpdateGeometry();
            UpdateEditOverlay();
        }
    }

    /// <summary>Pointer handling exists only on two explicit opt-in edit grips.</summary>
    internal sealed class ArcaneUiChatDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal NativeChatPresentation Presentation;
        internal bool Resize;
        public void OnBeginDrag(PointerEventData data)
        { if (Presentation != null) Presentation.BeginLayoutDrag(Resize, data); }
        public void OnDrag(PointerEventData data)
        { if (Presentation != null) Presentation.DragLayout(data); }
        public void OnEndDrag(PointerEventData data)
        { if (Presentation != null) Presentation.EndLayoutDrag(); }
    }
}
