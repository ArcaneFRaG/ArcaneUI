using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcanesGroupFrames.Framework.Layout.Editor
{
    // Handles pointer events only on the editing overlay. Gameplay frames never
    // receive editing clicks, and their original drag handlers remain intact.
    internal sealed class ArcaneUiLayoutMover : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        internal ArcaneUiLayoutEditor Editor;
        internal string ElementId;
        internal bool Resize;

        public void OnPointerClick(PointerEventData data)
        {
            if (Editor != null)
                Editor.Select(ElementId, Input.GetKey(KeyCode.LeftControl) ||
                    Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift));
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (Editor != null) Editor.BeginDrag(ElementId, Resize, data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (Editor != null) Editor.Drag(ElementId, data);
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (Editor != null) Editor.EndDrag(ElementId);
        }
    }
}
