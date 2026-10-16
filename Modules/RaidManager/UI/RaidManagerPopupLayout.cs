using UnityEngine;

namespace ArcanesGroupFrames
{
    internal static class RaidManagerPopupLayout
    {
        private const float Gap =
            8f;


        internal static void Apply(
            RectTransform popup)
        {
            RectTransform raidRect =
                RaidControlPanel.PanelRect;


            if (popup == null ||
                raidRect == null)
            {
                return;
            }


            if (popup.parent !=
                raidRect)
            {
                popup.SetParent(
                    raidRect,
                    false);
            }


            popup.anchorMin =
                new Vector2(
                    1f,
                    0f);

            popup.anchorMax =
                new Vector2(
                    1f,
                    0f);

            popup.pivot =
                new Vector2(
                    0f,
                    0f);

            popup.anchoredPosition =
                new Vector2(
                    Gap,
                    0f);

            popup.localScale =
                Vector3.one;


            popup.SetAsLastSibling();


            ClampToScreen(
                popup);
        }


        private static void ClampToScreen(
            RectTransform popup)
        {
            Canvas canvas =
                popup.GetComponentInParent<Canvas>();


            if (canvas == null)
            {
                return;
            }


            Canvas.ForceUpdateCanvases();


            Camera camera =
                canvas.renderMode ==
                    RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;


            Vector3[] corners =
                new Vector3[4];


            popup.GetWorldCorners(
                corners);


            float minX =
                float.MaxValue;

            float minY =
                float.MaxValue;

            float maxX =
                float.MinValue;

            float maxY =
                float.MinValue;


            for (int i = 0;
                 i < corners.Length;
                 i++)
            {
                Vector2 screen =
                    RectTransformUtility.WorldToScreenPoint(
                        camera,
                        corners[i]);


                minX =
                    Mathf.Min(
                        minX,
                        screen.x);

                minY =
                    Mathf.Min(
                        minY,
                        screen.y);

                maxX =
                    Mathf.Max(
                        maxX,
                        screen.x);

                maxY =
                    Mathf.Max(
                        maxY,
                        screen.y);
            }


            Vector2 correction =
                Vector2.zero;


            if (maxX >
                Screen.width)
            {
                correction.x +=
                    Screen.width -
                    maxX;
            }


            if (minX +
                correction.x <
                0f)
            {
                correction.x +=
                    -(minX +
                      correction.x);
            }


            if (maxY >
                Screen.height)
            {
                correction.y +=
                    Screen.height -
                    maxY;
            }


            if (minY +
                correction.y <
                0f)
            {
                correction.y +=
                    -(minY +
                      correction.y);
            }


            if (correction ==
                Vector2.zero)
            {
                return;
            }


            RectTransform parent =
                popup.parent as RectTransform;


            if (parent == null)
            {
                return;
            }


            Vector2 currentScreen =
                RectTransformUtility.WorldToScreenPoint(
                    camera,
                    popup.position);


            Vector2 correctedScreen =
                currentScreen +
                correction;


            Vector3 correctedWorld;


            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    parent,
                    correctedScreen,
                    camera,
                    out correctedWorld))
            {
                popup.position =
                    correctedWorld;
            }
        }
    }
}
