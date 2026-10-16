using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private RectTransform _choicePopup;
        private int _choicePopupOpenedFrame;

        // A detached list on the settings window: never parent it under a clipped
        // setting row, column or ScrollRect. This also keeps native mouse input
        // and the existing colour-picker behaviour independent.
        private void OpenChoicePopup(RectTransform anchor, string[] choices,
            string current, Action<string> chosen)
        {
            CloseChoicePopup();
            if (_window == null || anchor == null || choices == null || choices.Length == 0) return;
            const float popupWidth = 238f;
            const float rowHeight = 33f;
            float popupHeight = Mathf.Min(choices.Length * rowHeight + 10f, 275f);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_window, anchor);
            float x = Mathf.Clamp(bounds.max.x + Width * .5f - popupWidth,
                BodyX + 8f, Width - popupWidth - 12f);
            float y = Height * .5f - bounds.min.y + 3f;
            // If the popup would cross the footer, open upward instead.
            if (y + popupHeight > Height - 55f)
                y = Height * .5f - bounds.max.y - popupHeight - 3f;
            y = Mathf.Clamp(y, 83f, Height - 55f - popupHeight);
            _choicePopup = ArcaneUiControls.Panel(_window, "ArcaneUI_ChoicePopup", x, y,
                popupWidth, popupHeight, new Color(.075f, .09f, .12f, .99f));
            _choicePopup.SetAsLastSibling();
            _choicePopupOpenedFrame = Time.frameCount;
            ArcaneUiControls.Panel(_choicePopup, "Accent", 0f, 0f, popupWidth, 2f,
                Theme.Accent, false);
            RectTransform list = ArcaneUiControls.ScrollContent(_choicePopup, "Choices",
                4f, 5f, popupWidth - 8f, popupHeight - 10f, Color.clear);
            foreach (string choice in choices)
            {
                string selected = choice;
                bool active = string.Equals(choice, current, StringComparison.OrdinalIgnoreCase);
                RectTransform row = ArcaneUiControls.Row(list, "Option_" + choice,
                    rowHeight - 2f, active ? Card : Theme.Panel);
                var button = ArcaneUiControls.Button(row, "Select", choice, 2f, 1f,
                    popupWidth - 24f, rowHeight - 4f,
                    active ? Theme.Accent : Theme.Panel, Theme.Text, () =>
                    {
                        CloseChoicePopup();
                        if (!active && chosen != null) chosen(selected);
                    }, 11);
                Text caption = button.GetComponentInChildren<Text>();
                if (caption != null) caption.alignment = TextAnchor.MiddleLeft;
            }
            ArcaneUiControls.RefreshScrollLayout(list, true);
        }

        private void CloseChoicePopup()
        {
            if (_choicePopup == null) return;
            _choicePopup.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(_choicePopup.gameObject);
            _choicePopup = null;
        }

        private void UpdateChoicePopupDismissal()
        {
            if (_choicePopup == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { CloseChoicePopup(); return; }
            if (Time.frameCount <= _choicePopupOpenedFrame || !Input.GetMouseButtonDown(0)) return;
            // An outside click dismisses the menu and doesn't change the value.
            if (!RectTransformUtility.RectangleContainsScreenPoint(_choicePopup, Input.mousePosition, null))
                CloseChoicePopup();
        }

        private void DirectChoiceSetting(string label, string description, string current,
            string[] options, Action<string> chosen)
        {
            var row = ArcaneUiControls.Row(_content, "DirectChoice_" + label, 57f, Card);
            RegisterSetting(label, row);
            ArcaneUiControls.Label(row, "Title", label, 14f, 4f, 485f, 23f, 13,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Description", description, 14f, 27f,
                565f, 22f, 11, Muted);
            Button button = ArcaneUiControls.Button(row, "Choose", current + "  ▼",
                595f, 10f, 176f, 34f, Theme.Panel, Theme.Text, null, 11);
            button.onClick.AddListener(() => OpenChoicePopup(button.GetComponent<RectTransform>(),
                options, current, selected =>
                {
                    if (chosen != null) chosen(selected);
                    RenderPage();
                }));
        }

        private static string[] WithFirst(string first, string[] names)
        {
            var list = new List<string> { first };
            if (names != null) list.AddRange(names);
            return list.ToArray();
        }

        private static string[] OptionsForChoice(string label, string description)
        {
            if (label.EndsWith(" Alignment", StringComparison.Ordinal))
                return new[] { "Left", "Centre", "Right" };
            if (label.EndsWith(" Border", StringComparison.Ordinal) && label != "Selected Bar Border")
                return new[] { "None", "Thin Border", "Thick Border", "Pixel Border", "Soft Shadow" };
            if (label == "Health Fill Direction" || label == "Mana Fill Direction")
                return new[] { "Left to Right", "Right to Left", "Bottom to Top", "Top to Bottom" };
            if (label == "Member Growth Direction")
                return new[] { "Vertical (Down)", "Horizontal (Right)" };
            if (label == "Tracker Growth Direction")
                return new[] { "Horizontal", "Vertical" };
            if (label == "Player Cast Bar Placement" || label == "Cast Bar Placement")
                return new[] { "Overlay Mana", "Above Health", "Below Frame", "Detached" };
            if (label == "Aura Placement")
                return new[] { "Below Frame", "Above Frame", "Left", "Right" };
            if (label == "Action Bar Preset" || label == "Selected Bar Theme")
                return new[] { "Arcane Dark", "Gilded Reliquary", "Runebound", "Emberforged", "Voidglass", "Minimal", "Custom" };
            if (label == "Action Bar Border Style" || label == "Selected Bar Border")
                return new[] { "Ornate", "Runic", "Bevelled", "Glowing", "Thin", "None" };
            if (label == "Selected Bar Visibility")
                return new[] { "Always Visible", "Mouseover Only", "Combat Only", "Combat + Mouseover", "Hidden" };
            if (label == "Health Bar Texture")
                return WithFirst("Theme Default", UnitFrameAppearance.TextureNames);
            if (label == "Bar Texture")
                return UnitFrameAppearance.TextureNames;
            if (label == "Font")
                return description != null && description.StartsWith("Global", StringComparison.Ordinal)
                    ? WithFirst("Theme Default", UnitFrameAppearance.FontNames)
                    : UnitFrameAppearance.FontNames;
            return null;
        }

        private static void ApplyCycledChoice(string original, string desired,
            string[] choices, Action next)
        {
            if (next == null || choices == null || choices.Length == 0 || original == desired) return;
            int from = Array.IndexOf(choices, original);
            int to = Array.IndexOf(choices, desired);
            if (to < 0) return;
            // A manual texture/font override begins at "Global", but its
            // existing native Next setter starts at the first concrete entry.
            // "Global" remains available via the existing Reset Override button.
            int steps = from < 0 ? to + 1 : (to - from + choices.Length) % choices.Length;
            for (int i = 0; i < steps; i++) next();
        }
    }
}
