using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    // Phase 11.6O: presentation-only two-column layout for shared setting types.
    // Reuses the real buttons/sliders/input widgets and their original callbacks.
    // Section headings, descriptions, action commands, previews and custom
    // editor workspaces remain full width. No settings values are changed.
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private const float CompactPairHeight = 59f;
        private const float CompactCellWidth = 391f;
        private const float CompactSecondCellX = 403f;
        private int _compactPairCount;

        private static bool CanCompact(RectTransform row)
        {
            if (row == null) return false;
            string id = row.name;
            return id.StartsWith("Toggle_", System.StringComparison.Ordinal) ||
                id.StartsWith("Slider_", System.StringComparison.Ordinal) ||
                id.StartsWith("Choice_", System.StringComparison.Ordinal) ||
                id.StartsWith("DirectChoice_", System.StringComparison.Ordinal) ||
                id.StartsWith("Manual_", System.StringComparison.Ordinal) ||
                id.StartsWith("Hex_", System.StringComparison.Ordinal);
        }

        private void ArrangeTwoColumnSettings()
        {
            if (_content == null) return;
            // Snapshot first: the pairing process reparents existing live
            // controls, never recreates them or changes their event listeners.
            var originalRows = new List<RectTransform>();
            for (int i = 0; i < _content.childCount; ++i)
            {
                RectTransform child = _content.GetChild(i) as RectTransform;
                if (child != null) originalRows.Add(child);
            }
            _compactPairCount = 0;
            RectTransform pending = null;
            foreach (RectTransform row in originalRows)
            {
                if (row == null || !CanCompact(row))
                {
                    // A section heading or full-width action breaks the pair.
                    // A single remaining control still gets a narrow cell.
                    if (pending != null) CreateCompactPair(pending, null);
                    pending = null;
                    continue;
                }
                if (pending == null)
                {
                    pending = row;
                    continue;
                }
                CreateCompactPair(pending, row);
                pending = null;
            }
            if (pending != null) CreateCompactPair(pending, null);
        }

        private void CreateCompactPair(RectTransform first, RectTransform second)
        {
            int position = first.GetSiblingIndex();
            RectTransform pair = ArcaneUiControls.Row(_content,
                "CompactPair_" + (++_compactPairCount), CompactPairHeight, Color.clear);
            pair.SetSiblingIndex(position);
            SetCompactCell(first, pair, 0f);
            if (second != null) SetCompactCell(second, pair, CompactSecondCellX);
        }

        private static void SetCompactCell(RectTransform row, RectTransform pair, float x)
        {
            row.SetParent(pair, false);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(x, 0f);
            row.sizeDelta = new Vector2(CompactCellWidth, CompactPairHeight);
            LayoutElement element = row.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.minWidth = CompactCellWidth;
                element.preferredWidth = CompactCellWidth;
                element.preferredHeight = CompactPairHeight;
                element.minHeight = CompactPairHeight;
            }
            ArrangeCompactChildren(row);
        }

        private static void ArrangeCompactChildren(RectTransform row)
        {
            string kind = row.name;
            if (kind.StartsWith("Toggle_", System.StringComparison.Ordinal))
            {
                MoveChild(row, "Label", 11f, 6f, 292f, 19f, false);
                MoveChild(row, "Description", 11f, 29f, 297f, 21f, true);
                MoveChild(row, "Toggle", 308f, 15f, 72f, 28f, false);
            }
            else if (kind.StartsWith("Slider_", System.StringComparison.Ordinal))
            {
                MoveChild(row, "Label", 11f, 5f, 280f, 20f, false);
                MoveChild(row, "Value", 291f, 5f, 89f, 20f, false);
                // Track/fill/handle are stretch-anchored to this slider root.
                // Their source logic is unchanged, and the shorter width is
                // enough for the existing 10-pixel handle at each extreme.
                MoveChild(row, "Slider", 12f, 29f, 367f, 22f, false);
            }
            else if (kind.StartsWith("Choice_", System.StringComparison.Ordinal) ||
                     kind.StartsWith("DirectChoice_", System.StringComparison.Ordinal))
            {
                MoveChild(row, "Title", 11f, 4f, 367f, 19f, false);
                MoveChild(row, "Description", 11f, 26f, 182f, 22f, true);
                MoveChild(row, "Choose", 200f, 23f, 180f, 28f, false);
            }
            else if (kind.StartsWith("Manual_", System.StringComparison.Ordinal))
            {
                MoveChild(row, "Label", 11f, 4f, 367f, 19f, false);
                MoveChild(row, "Value", 11f, 26f, 182f, 22f, true);
                MoveChild(row, "Select", 200f, 23f, 180f, 28f, false);
            }
            else if (kind.StartsWith("Hex_", System.StringComparison.Ordinal))
            {
                MoveChild(row, "Title", 11f, 4f, 367f, 19f, false);
                MoveChild(row, "Description", 11f, 26f, 182f, 22f, true);
                MoveChild(row, "EditColour", 200f, 23f, 180f, 28f, false);
            }
        }

        private static void MoveChild(RectTransform parent, string name,
            float x, float y, float width, float height, bool description)
        {
            RectTransform rect = parent.Find(name) as RectTransform;
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            Text label = rect.GetComponent<Text>();
            if (label != null)
            {
                // Keep labels readable. Very long names/notes can be truncated
                // within their own cell rather than overlap the next setting.
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                if (!description)
                {
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 10;
                    label.resizeTextMaxSize = label.fontSize;
                }
            }
        }
    }
}
