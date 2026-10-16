using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcanesGroupFrames
{
    internal sealed class RaidFramesDragHandler :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private RectTransform _target;
        private Vector2 _offset;
        private bool _dragging;

        internal void Initialize(RectTransform target)
        {
            _target = target;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = false;

            if (RaidFramesSettings.Locked || _target == null)
            {
                return;
            }

            RectTransform parent = _target.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out localPoint))
            {
                return;
            }

            Vector2 topLeftPoint = ConvertToTopLeftSpace(parent, localPoint);
            _offset = _target.anchoredPosition - topLeftPoint;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || RaidFramesSettings.Locked || _target == null)
            {
                return;
            }

            RectTransform parent = _target.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out localPoint))
            {
                return;
            }

            _target.anchoredPosition =
                ConvertToTopLeftSpace(parent, localPoint) + _offset;

            ClampToParent(parent);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || _target == null)
            {
                return;
            }

            _dragging = false;

            RectTransform parent = _target.parent as RectTransform;
            if (parent != null)
            {
                ClampToParent(parent);
            }

            RaidFramesUI.StoreCurrentPosition();
        }

        private static Vector2 ConvertToTopLeftSpace(
            RectTransform parent,
            Vector2 localPoint)
        {
            Rect rect = parent.rect;
            return new Vector2(
                localPoint.x - rect.xMin,
                localPoint.y - rect.yMax);
        }

        private void ClampToParent(RectTransform parent)
        {
            if (_target == null || parent == null)
            {
                return;
            }

            float maxX = Mathf.Max(0f, parent.rect.width - _target.rect.width);
            float minY = -Mathf.Max(0f, parent.rect.height - _target.rect.height);

            Vector2 position = _target.anchoredPosition;
            position.x = Mathf.Clamp(position.x, 0f, maxX);
            position.y = Mathf.Clamp(position.y, minY, 0f);
            _target.anchoredPosition = position;
        }
    }
}
