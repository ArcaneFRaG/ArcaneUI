using System;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Theming;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    // Lightweight runtime uGUI components. No assetbundles, textures or
    // additional native EventSystem are required.
    internal static class ArcaneUiControls
    {
        private static Font _font;
        internal static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        internal static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        internal static RectTransform Panel(Transform parent, string name, float x, float y,
            float w, float h, Color color, bool blocksRaycasts = true)
        {
            var rt = Rect(parent, name, x, y, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocksRaycasts;
            return rt;
        }

        internal static Text Label(Transform parent, string name, string value,
            float x, float y, float w, float h, int size, Color color,
            TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(parent, name, x, y, w, h);
            Text txt = rt.gameObject.AddComponent<Text>();
            txt.font = Font;
            txt.text = value;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            ArcaneUiGlobalTypography.ApplyControl(txt, size);
            return txt;
        }

        internal static Button Button(Transform parent, string name, string label,
            float x, float y, float w, float h, Color fill, Color text, Action pressed, int fontSize = 14)
        {
            var rt = Panel(parent, name, x, y, w, h, fill);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = rt.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.13f, 1.13f, 1.13f, 1f);
            colors.pressedColor = new Color(.8f, .8f, .8f, 1f);
            button.colors = colors;
            if (pressed != null) button.onClick.AddListener(() => pressed());
            Label(rt, "Caption", label, 8, 0, w - 16, h, fontSize, text, TextAnchor.MiddleCenter, FontStyle.Bold);
            return button;
        }

        internal static Button IconTabButton(Transform parent, string name, string icon, string label,
            float x, float y, float w, float h, Color fill, Color iconColor, Color text,
            Action pressed, int labelSize = 10, int iconSize = 11)
        {
            var rt = Panel(parent, name, x, y, w, h, fill);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = rt.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            colors.pressedColor = new Color(.84f, .84f, .84f, 1f);
            button.colors = colors;
            if (pressed != null) button.onClick.AddListener(() => pressed());
            var iconLabel = Label(rt, "Icon", icon, 6f, 3f, w - 12f, 12f, iconSize, iconColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            iconLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            var caption = Label(rt, "Caption", label, 5f, 13f, w - 10f, h - 15f, labelSize, text,
                TextAnchor.UpperCenter, FontStyle.Bold);
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Overflow;
            return button;
        }

        internal static InputField Search(Transform parent, string name, float x, float y, float w,
            float h, string placeholder, Color bg, Color textColor, Action<string> changed)
        {
            var rt = Panel(parent, name, x, y, w, h, bg);
            var input = rt.gameObject.AddComponent<InputField>();
            var text = Label(rt, "InputText", "", 12, 0, w - 24, h, 13, textColor);
            var hint = Label(rt, "Placeholder", placeholder, 12, 0, w - 24, h, 13,
                new Color(textColor.r, textColor.g, textColor.b, .55f));
            input.textComponent = text;
            input.placeholder = hint;
            input.targetGraphic = rt.GetComponent<Image>();
            input.lineType = InputField.LineType.SingleLine;
            if (changed != null) input.onValueChanged.AddListener(value => changed(value));
            return input;
        }

        // A ScrollRect with a top-anchored, automatically height-fitted content column.
        // Returns the CONTENT child (for adding rows), not the root ScrollRect.
        // Layout geometry must be changed on content.GetComponentInParent<ScrollRect>()
        // and NEVER by changing content.anchoredPosition/sizeDelta.
        internal static RectTransform ScrollContent(Transform parent, string name,
            float x, float y, float w, float h, Color viewportColor)
        {
            // ScrollRect needs a raycastable surface under the pointer or the
            // mouse wheel / drag events will never reach it. Use invisible
            // Images for the root and viewport so the scroll area behaves like
            // a normal interactive panel while still looking clean.
            var root = Panel(parent, name, x, y, w, h, new Color(0f, 0f, 0f, 0f));
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 22f;
            scroll.inertia = true;

            var viewport = Panel(root, "Viewport", 0, 0, w, h,
                new Color(viewportColor.r, viewportColor.g, viewportColor.b, 0.001f));
            // Stretch with the outer ScrollRect: the settings page changes its
            // viewport height when the fixed preview is shown/hidden.
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = Rect(viewport, "Content", 0, 0, w - 10, 0);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.sizeDelta = new Vector2(-10f, 0f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 6, 18);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        internal static void RefreshScrollLayout(RectTransform content, bool resetToTop = false)
        {
            if (content == null) return;
            // Force height calculation immediately when a page is rebuilt,
            // rather than waiting for another Unity canvas layout pass.
            UnityEngine.Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var scroll = content.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                UnityEngine.Canvas.ForceUpdateCanvases();
                if (resetToTop) scroll.verticalNormalizedPosition = 1f;
            }
        }

        internal static RectTransform Row(Transform list, string name, float height, Color background)
        {
            var rt = Panel(list, name, 0, 0, 100, height, background, false);
            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
            // Keep every row's children inside its own visual boundaries.
            // The parent list has its own RectMask2D for scrolling, whereas
            // this mask prevents wide labels, buttons, and sliders bleeding
            // into adjacent settings when a layout dimension changes.
            rt.gameObject.AddComponent<RectMask2D>();
            return rt;
        }

        internal static void ClearChildren(Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        internal static Color Multiply(Color color, float amount)
        {
            return new Color(Mathf.Clamp01(color.r * amount), Mathf.Clamp01(color.g * amount),
                Mathf.Clamp01(color.b * amount), color.a);
        }
    }
}
