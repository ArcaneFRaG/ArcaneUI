using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// HOTFIX: RAID MEMBER DRAGGING ALLOWED WHILE LOCKED - 2026-09-26
namespace ArcanesGroupFrames
{
    internal sealed class RaidMemberDragHandler :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private RaidMemberSlot _slot;
        private CanvasGroup _canvasGroup;
        private bool _dragging;

        private GameObject _dragGhost;
        private RectTransform _dragGhostRect;
        private RectTransform _dragCanvasRect;
        private Canvas _dragCanvas;

        private GameObject _dropHighlight;
        private RaidMemberDragHandler _highlightedMember;
        private int _highlightedGroup;


        internal RaidMemberSlot Slot
        {
            get
            {
                return _slot;
            }
        }


        internal void Initialize(
            RaidMemberSlot slot)
        {
            _slot = slot;

            _canvasGroup =
                GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
            {
                _canvasGroup =
                    gameObject.AddComponent<CanvasGroup>();
            }
        }


        public void OnBeginDrag(
            PointerEventData eventData)
        {
            if (_slot == null ||
                !GameData.RaidActive ||
                eventData == null)
            {
                return;
            }


            _dragging = true;


            if (_canvasGroup != null)
            {
                // Leave the real frame faintly visible in its slot so the
                // layout does not collapse while the cursor carries a visual
                // copy of the frame.
                _canvasGroup.alpha = 0.24f;
                _canvasGroup.blocksRaycasts = false;
            }


            CreateDragGhost(
                eventData);


            UpdateDragGhostPosition(
                eventData);


            UpdateDropHighlight(
                eventData);
        }


        public void OnDrag(
            PointerEventData eventData)
        {
            if (!_dragging ||
                eventData == null)
            {
                return;
            }


            UpdateDragGhostPosition(
                eventData);


            UpdateDropHighlight(
                eventData);
        }


        public void OnEndDrag(
            PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }


            // Resolve the member beneath the pointer while this dragged frame
            // is still ignoring raycasts. Dropping one occupied frame directly
            // onto another means "swap these two raid slots".
            RaidMemberSlot targetSlot =
                ResolveDropSlot(
                    eventData);


            ClearDropHighlight();


            DestroyDragGhost();


            _dragging = false;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }


            if (_slot == null ||
                eventData == null)
            {
                return;
            }


            if (targetSlot != null &&
                targetSlot != _slot)
            {
                RaidFramesUI.TrySwapRaidMembers(
                    _slot,
                    targetSlot);

                return;
            }


            int targetGroup =
                RaidFramesUI.GetDropGroup(
                    eventData.position);

            if (targetGroup < 1)
            {
                return;
            }


            RaidFramesUI.TryMoveRaidMemberToGroup(
                _slot,
                targetGroup);
        }


        private void UpdateDropHighlight(
            PointerEventData eventData)
        {
            if (!_dragging ||
                eventData == null)
            {
                ClearDropHighlight();
                return;
            }


            RaidMemberDragHandler targetMember =
                ResolveDropHandler(
                    eventData);


            if (targetMember != null &&
                targetMember != this &&
                targetMember.Slot != null)
            {
                if (_highlightedMember != targetMember ||
                    _highlightedGroup != 0)
                {
                    ClearDropHighlight();


                    _highlightedMember =
                        targetMember;


                    _dropHighlight =
                        CreateBorderHighlight(
                            targetMember.gameObject,
                            new Color(
                                0.20f,
                                0.88f,
                                1.00f,
                                0.98f),
                            3f);
                }


                return;
            }


            int group =
                RaidFramesUI.GetDropGroup(
                    eventData.position);


            if (group < 1)
            {
                ClearDropHighlight();
                return;
            }


            bool valid =
                RaidFramesUI.CanDropRaidMemberIntoGroup(
                    _slot,
                    group);


            if (_highlightedGroup != group ||
                _highlightedMember != null)
            {
                ClearDropHighlight();


                _highlightedGroup =
                    group;


                GameObject groupObject =
                    RaidFramesUI.GetDropGroupObject(
                        group);


                if (groupObject != null)
                {
                    _dropHighlight =
                        CreateBorderHighlight(
                            groupObject,
                            valid
                                ? new Color(
                                    0.24f,
                                    0.90f,
                                    0.52f,
                                    0.96f)
                                : new Color(
                                    0.95f,
                                    0.28f,
                                    0.24f,
                                    0.96f),
                            3f);
                }
            }
        }


        private RaidMemberDragHandler ResolveDropHandler(
            PointerEventData eventData)
        {
            if (eventData == null ||
                EventSystem.current == null)
            {
                return null;
            }


            List<RaycastResult> results =
                new List<RaycastResult>();


            EventSystem.current.RaycastAll(
                eventData,
                results);


            for (int i = 0;
                 i < results.Count;
                 i++)
            {
                GameObject hit =
                    results[i].gameObject;


                if (hit == null)
                {
                    continue;
                }


                RaidMemberDragHandler handler =
                    hit.GetComponentInParent<RaidMemberDragHandler>();


                if (handler == null ||
                    handler == this ||
                    handler.Slot == null)
                {
                    continue;
                }


                return handler;
            }


            return null;
        }


        private GameObject CreateBorderHighlight(
            GameObject target,
            Color color,
            float thickness)
        {
            if (target == null)
            {
                return null;
            }


            RectTransform targetRect =
                target.transform as RectTransform;


            if (targetRect == null)
            {
                return null;
            }


            GameObject root =
                new GameObject(
                    "RaidDropHighlight");


            root.transform.SetParent(
                target.transform,
                false);


            RectTransform rootRect =
                root.AddComponent<RectTransform>();


            rootRect.anchorMin =
                Vector2.zero;

            rootRect.anchorMax =
                Vector2.one;

            rootRect.offsetMin =
                Vector2.zero;

            rootRect.offsetMax =
                Vector2.zero;


            CreateHighlightEdge(
                root.transform,
                "Top",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -thickness),
                Vector2.zero,
                color);


            CreateHighlightEdge(
                root.transform,
                "Bottom",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                Vector2.zero,
                new Vector2(0f, thickness),
                color);


            CreateHighlightEdge(
                root.transform,
                "Left",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(thickness, 0f),
                color);


            CreateHighlightEdge(
                root.transform,
                "Right",
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(-thickness, 0f),
                Vector2.zero,
                color);


            root.transform.SetAsLastSibling();


            return root;
        }


        private void CreateHighlightEdge(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            GameObject edge =
                new GameObject(
                    name);


            edge.transform.SetParent(
                parent,
                false);


            RectTransform rect =
                edge.AddComponent<RectTransform>();


            rect.anchorMin =
                anchorMin;

            rect.anchorMax =
                anchorMax;

            rect.offsetMin =
                offsetMin;

            rect.offsetMax =
                offsetMax;


            Image image =
                edge.AddComponent<Image>();


            image.color =
                color;

            image.raycastTarget =
                false;
        }


        private void ClearDropHighlight()
        {
            if (_dropHighlight != null)
            {
                UnityEngine.Object.Destroy(
                    _dropHighlight);
            }


            _dropHighlight =
                null;

            _highlightedMember =
                null;

            _highlightedGroup =
                0;
        }


        private void CreateDragGhost(
            PointerEventData eventData)
        {
            DestroyDragGhost();


            _dragCanvas =
                GetComponentInParent<Canvas>();


            if (_dragCanvas == null)
            {
                return;
            }


            _dragCanvasRect =
                _dragCanvas.transform as RectTransform;


            if (_dragCanvasRect == null)
            {
                return;
            }


            _dragGhost =
                Instantiate(
                    gameObject,
                    _dragCanvas.transform,
                    false);


            _dragGhost.name =
                gameObject.name +
                "_DragGhost";


            _dragGhostRect =
                _dragGhost.transform as RectTransform;


            if (_dragGhostRect == null)
            {
                DestroyDragGhost();
                return;
            }


            RectTransform sourceRect =
                transform as RectTransform;


            if (sourceRect != null)
            {
                _dragGhostRect.anchorMin =
                    new Vector2(
                        0.5f,
                        0.5f);

                _dragGhostRect.anchorMax =
                    new Vector2(
                        0.5f,
                        0.5f);

                _dragGhostRect.pivot =
                    sourceRect.pivot;

                _dragGhostRect.sizeDelta =
                    sourceRect.rect.size;
            }


            _dragGhostRect.localScale =
                Vector3.one *
                1.03f;


            // The clone is visual-only. It must never consume pointer events
            // or run unit-frame interactions while it follows the mouse.
            RaidMemberDragHandler ghostHandler =
                _dragGhost.GetComponent<RaidMemberDragHandler>();


            if (ghostHandler != null)
            {
                ghostHandler.enabled =
                    false;
            }


            UnitFrameInteraction interaction =
                _dragGhost.GetComponent<UnitFrameInteraction>();


            if (interaction != null)
            {
                interaction.enabled =
                    false;
            }


            Graphic[] graphics =
                _dragGhost.GetComponentsInChildren<Graphic>(
                    true);


            for (int i = 0;
                 i < graphics.Length;
                 i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].raycastTarget =
                        false;
                }
            }


            CanvasGroup ghostCanvasGroup =
                _dragGhost.GetComponent<CanvasGroup>();


            if (ghostCanvasGroup == null)
            {
                ghostCanvasGroup =
                    _dragGhost.AddComponent<CanvasGroup>();
            }


            ghostCanvasGroup.alpha =
                0.88f;

            ghostCanvasGroup.blocksRaycasts =
                false;

            ghostCanvasGroup.interactable =
                false;


            _dragGhost.transform.SetAsLastSibling();
        }


        private void UpdateDragGhostPosition(
            PointerEventData eventData)
        {
            if (_dragGhostRect == null ||
                _dragCanvasRect == null ||
                eventData == null)
            {
                return;
            }


            Vector2 localPoint;


            Camera eventCamera =
                null;


            if (_dragCanvas != null &&
                _dragCanvas.renderMode !=
                    RenderMode.ScreenSpaceOverlay)
            {
                eventCamera =
                    eventData.pressEventCamera != null
                        ? eventData.pressEventCamera
                        : _dragCanvas.worldCamera;
            }


            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragCanvasRect,
                    eventData.position,
                    eventCamera,
                    out localPoint))
            {
                _dragGhostRect.anchoredPosition =
                    localPoint;
            }
        }


        private void DestroyDragGhost()
        {
            if (_dragGhost != null)
            {
                UnityEngine.Object.Destroy(
                    _dragGhost);
            }


            _dragGhost =
                null;

            _dragGhostRect =
                null;

            _dragCanvasRect =
                null;

            _dragCanvas =
                null;
        }


        private RaidMemberSlot ResolveDropSlot(
            PointerEventData eventData)
        {
            RaidMemberDragHandler handler =
                ResolveDropHandler(
                    eventData);


            return handler != null
                ? handler.Slot
                : null;
        }


        private void OnDisable()
        {
            _dragging = false;


            ClearDropHighlight();


            DestroyDragGhost();


            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
        }
    }
}
