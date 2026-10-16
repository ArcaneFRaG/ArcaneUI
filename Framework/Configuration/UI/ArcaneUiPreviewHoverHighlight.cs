using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    /// <summary>
    /// Preview-only hover affordance. It never changes the in-game frame,
    /// its text mesh/effects, or the existing click-to-settings Button.
    /// All decoration is raycast-transparent, so clicks still reach the tag.
    /// </summary>
    internal sealed class ArcaneUiPreviewHoverHighlight : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform _target;
        private RectTransform _decoration;
        private Image _tint;
        private Image[] _edges;
        private Action<ArcaneUiPreviewHoverHighlight, bool> _changed;
        private bool _hovered;
        private const float Edge = 2f;
        private const float MinimumHeight = 14f;

        internal bool IsHovered { get { return _hovered; } }

        internal void Initialize(Color accent, bool isText,
            Action<ArcaneUiPreviewHoverHighlight, bool> onHoverChanged)
        {
            _target = GetComponent<RectTransform>();
            _changed = onHoverChanged;
            if (_target == null) return;

            // A minimum visible selection height is important for mana bars:
            // a 5px-high bar must still display an obvious hover indicator.
            var go = new GameObject("ArcaneUI_PreviewHoverOnly", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            _decoration = go.GetComponent<RectTransform>();
            _decoration.SetParent(_target, false);
            Stretch(_decoration);
            _tint = go.GetComponent<Image>();
            _tint.raycastTarget = false;
            _tint.color = new Color(accent.r, accent.g, accent.b, isText ? 0.04f : 0.10f);

            _edges = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var border = new GameObject("HoverBorder_" + i, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                var r = border.GetComponent<RectTransform>();
                r.SetParent(_decoration, false);
                var img = border.GetComponent<Image>();
                img.color = new Color(accent.r, accent.g, accent.b, 1f);
                img.raycastTarget = false;
                _edges[i] = img;
            }
            // Highlight is drawn above a health fill but never swallows input.
            _decoration.SetAsLastSibling();
            LayoutDecoration();
            _decoration.gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void PlaceEdge(Image image, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = image.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void LayoutDecoration()
        {
            if (_target == null || _decoration == null || _edges == null) return;
            float verticalExtension = Mathf.Max(0f, (MinimumHeight - _target.rect.height) * 0.5f);
            _decoration.offsetMin = new Vector2(0f, -verticalExtension);
            _decoration.offsetMax = new Vector2(0f, verticalExtension);
            PlaceEdge(_edges[0], new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -Edge), Vector2.zero);
            PlaceEdge(_edges[1], Vector2.zero, new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, Edge));
            PlaceEdge(_edges[2], Vector2.zero, new Vector2(0f, 1f),
                Vector2.zero, new Vector2(Edge, 0f));
            PlaceEdge(_edges[3], new Vector2(1f, 0f), Vector2.one,
                new Vector2(-Edge, 0f), Vector2.zero);
        }

        private void OnRectTransformDimensionsChange()
        {
            // Preview geometry follows the settings sliders in real time.
            LayoutDecoration();
        }

        internal void SetHighlighted(bool highlighted)
        {
            _hovered = highlighted;
            if (_decoration != null)
            {
                if (highlighted) LayoutDecoration();
                _decoration.gameObject.SetActive(highlighted);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_hovered) return;
            SetHighlighted(true);
            if (_changed != null) _changed(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_hovered) return;
            SetHighlighted(false);
            if (_changed != null) _changed(this, false);
        }

        private void OnDisable()
        {
            if (!_hovered) return;
            _hovered = false;
            if (_decoration != null) _decoration.gameObject.SetActive(false);
            if (_changed != null) _changed(this, false);
        }
    }
}
