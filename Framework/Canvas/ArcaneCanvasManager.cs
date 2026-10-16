using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.Canvas
{
    // Owns only ArcaneUI's canvas. Does not disturb game canvases or EventSystem.
    public sealed class ArcaneCanvasManager
    {
        private GameObject _root;
        private UnityEngine.Canvas _canvas;
        public UnityEngine.Canvas Canvas { get { return _canvas; } }
        public RectTransform Root { get; private set; }
        public bool IsReady { get { return _root != null && _canvas != null; } }

        public void Initialize()
        {
            if (IsReady) return;
            _root = new GameObject("ArcaneUI_Canvas", typeof(RectTransform), typeof(UnityEngine.Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(_root);
            Root = _root.GetComponent<RectTransform>();
            _canvas = _root.GetComponent<UnityEngine.Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // The canvas root holds gameplay HUD elements. Keep it BEHIND the
            // native Erenshor loading/menu canvases: even when the game shows
            // a loading overlay without changing scenes, that overlay must win.
            // Configuration and layout editor use independently sorted nested
            // canvases (32760/32761) so they still appear above game windows.
            _canvas.sortingOrder = -100;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            // No blocking raycast image: empty canvas is mouse-transparent.
            _root.GetComponent<GraphicRaycaster>().enabled = true;
        }

        public RectTransform CreateLayer(string layerName, int siblingIndex = -1)
        {
            if (!IsReady) return null;
            var layer = new GameObject(layerName, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(Root, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;
            if (siblingIndex >= 0) layer.SetSiblingIndex(siblingIndex);
            return layer;
        }

        // Unlike Canvas.enabled, disabling the owned root also suppresses
        // independently sorted nested canvases (settings/editor) during loads.
        public void SetGameplayVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible)
                _root.SetActive(visible);
        }

        public void Shutdown()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _canvas = null;
            Root = null;
        }
    }
}
