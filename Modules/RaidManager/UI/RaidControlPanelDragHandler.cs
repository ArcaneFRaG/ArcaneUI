using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcanesGroupFrames
{
    internal sealed class RaidControlPanelDragHandler :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private RectTransform _target;

        private Vector2 _offset;


        // ============================================================
        // INITIALIZE
        // ============================================================

        internal void Initialize(
            RectTransform target)
        {
            _target =
                target;
        }


        // ============================================================
        // BEGIN DRAG
        // ============================================================

        public void OnBeginDrag(
            PointerEventData eventData)
        {
            if (_target == null)
            {
                return;
            }


            RectTransform parent =
                _target.parent
                    as RectTransform;


            if (parent == null)
            {
                return;
            }


            if (!RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    parent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }


            _offset =
                _target.anchoredPosition -
                localPoint;
        }


        // ============================================================
        // DRAG
        // ============================================================

        public void OnDrag(
            PointerEventData eventData)
        {
            if (_target == null)
            {
                return;
            }


            RectTransform parent =
                _target.parent
                    as RectTransform;


            if (parent == null)
            {
                return;
            }


            if (!RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    parent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }


            _target.anchoredPosition =
                localPoint +
                _offset;


            ClampToParent(
                parent);
        }


        // ============================================================
        // END DRAG
        // ============================================================

        public void OnEndDrag(
            PointerEventData eventData)
        {
            if (_target == null)
            {
                return;
            }


            RectTransform parent =
                _target.parent
                    as RectTransform;


            if (parent != null)
            {
                ClampToParent(
                    parent);
            }


            RaidControlPanel
                .StoreCurrentPosition();
        }


        // ============================================================
        // CLAMP
        // ============================================================

        private void ClampToParent(
            RectTransform parent)
        {
            if (_target == null ||
                parent == null)
            {
                return;
            }


            Vector2 position =
                _target.anchoredPosition;


            Rect parentRect =
                parent.rect;


            float width =
                _target.rect.width;

            float height =
                _target.rect.height;


            // Panel uses a top-left pivot.

            position.x =
                Mathf.Clamp(
                    position.x,
                    parentRect.xMin,
                    parentRect.xMax -
                    width);


            position.y =
                Mathf.Clamp(
                    position.y,
                    parentRect.yMin +
                    height,
                    parentRect.yMax);


            _target.anchoredPosition =
                position;
        }
    }
}