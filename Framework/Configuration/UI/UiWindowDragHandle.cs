using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    // Moves only the ArcaneUI settings window (never native game panels).
    internal sealed class UiWindowDragHandle : MonoBehaviour, IDragHandler
    {
        internal RectTransform Window;
        internal UnityEngine.Canvas Canvas;

        public void OnDrag(PointerEventData eventData)
        {
            if (Window == null || Canvas == null || eventData.button != PointerEventData.InputButton.Left)
                return;

            float scale = Mathf.Max(0.01f, Canvas.scaleFactor);
            Window.anchoredPosition += eventData.delta / scale;
        }
    }
}
