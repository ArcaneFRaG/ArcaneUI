using UnityEngine;

namespace ArcanesGroupFrames.Framework.Layout
{
    public static class GridSnap
    {
        public static Vector2 Snap(Vector2 point, float gridSize)
        {
            if (gridSize <= 0f) return point;
            return new Vector2(
                Mathf.Round(point.x / gridSize) * gridSize,
                Mathf.Round(point.y / gridSize) * gridSize);
        }
    }
}
