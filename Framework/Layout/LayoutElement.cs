using UnityEngine;

namespace ArcanesGroupFrames.Framework.Layout
{
    // Normalized IDs are persisted by the future profile/layout service.
    // Positions are anchored canvas-space, not Unity world coordinates.
    public sealed class LayoutElement
    {
        public string Id;
        public Vector2 AnchoredPosition;
        public Vector2 Size;
        public Vector2 AnchorMin = new Vector2(0.5f, 0.5f);
        public Vector2 AnchorMax = new Vector2(0.5f, 0.5f);
        public bool Locked;
        public bool Visible = true;
    }
}
