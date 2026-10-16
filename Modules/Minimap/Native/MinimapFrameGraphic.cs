using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Modules.Minimap.Native
{
    /// <summary>A raycast-free outline. Never masks or replaces native map graphics.</summary>
    internal sealed class MinimapFrameGraphic : MaskableGraphic
    {
        internal string Shape = "Round";
        internal float Thickness = 4f;
        internal float Inset = 1f;
        // Screen-verified native minimap button hit areas in this graphic's local coordinates.
        internal readonly List<Rect> ExcludedControlRects = new List<Rect>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Thickness <= .01f || color.a <= .005f) return;
            Rect rect = rectTransform.rect;
            float margin = Mathf.Clamp(Inset, 0f, Mathf.Min(rect.width, rect.height) * .25f);
            Rect box = new Rect(rect.xMin + margin, rect.yMin + margin,
                Mathf.Max(4f, rect.width - margin * 2f), Mathf.Max(4f, rect.height - margin * 2f));
            float thick = Mathf.Clamp(Thickness, .5f, Mathf.Min(box.width, box.height) * .2f);
            bool round = Shape == "Round" || Shape == "Ornate";
            float radius = round ? Mathf.Min(box.width, box.height) * .5f :
                Shape == "Rounded" ? Mathf.Min(box.width, box.height) * .14f : 0f;
            DrawRing(vh, box, radius, thick, color, ExcludedControlRects);
            if (Shape == "Ornate")
            {
                float step = thick + 3f;
                Rect inner = new Rect(box.x + step, box.y + step,
                    Mathf.Max(4f, box.width - step * 2f), Mathf.Max(4f, box.height - step * 2f));
                DrawRing(vh, inner, Mathf.Min(inner.width, inner.height) * .5f, Mathf.Max(1f, thick * .35f),
                    new Color(color.r, color.g, color.b, color.a * .65f), ExcludedControlRects);
            }
        }

        private static List<Vector2> Path(Rect r, float radius)
        {
            var path = new List<Vector2>();
            if (radius < .1f)
            {
                path.Add(new Vector2(r.xMin, r.yMin)); path.Add(new Vector2(r.xMax, r.yMin));
                path.Add(new Vector2(r.xMax, r.yMax)); path.Add(new Vector2(r.xMin, r.yMax));
                return path;
            }
            // Counter-clockwise quarter arcs, starting at bottom-right.
            const int steps = 12;
            for (int corner = 0; corner < 4; ++corner)
            {
                Vector2 center = corner == 0 ? new Vector2(r.xMax - radius, r.yMin + radius) :
                    corner == 1 ? new Vector2(r.xMax - radius, r.yMax - radius) :
                    corner == 2 ? new Vector2(r.xMin + radius, r.yMax - radius) :
                        new Vector2(r.xMin + radius, r.yMin + radius);
                float start = -90f + corner * 90f;
                for (int i = 0; i <= steps; ++i)
                {
                    float rad = (start + i * 90f / steps) * Mathf.Deg2Rad;
                    path.Add(center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius);
                }
            }
            return path;
        }

        private static void DrawRing(VertexHelper vh, Rect outer, float radius, float thickness, Color tint, List<Rect> exclusions)
        {
            Rect inner = new Rect(outer.xMin + thickness, outer.yMin + thickness,
                Mathf.Max(1f, outer.width - thickness * 2f), Mathf.Max(1f, outer.height - thickness * 2f));
            var a = Path(outer, radius);
            var b = Path(inner, Mathf.Max(0f, radius - thickness));
            if (a.Count != b.Count) return;
            int start = vh.currentVertCount;
            for (int i = 0; i < a.Count; ++i)
            {
                vh.AddVert(a[i], tint, Vector2.zero);
                vh.AddVert(b[i], tint, Vector2.zero);
            }
            for (int i = 0; i < a.Count; ++i)
            {
                int next = (i + 1) % a.Count;
                int idx = start + i * 2;
                int nxt = start + next * 2;
                // Omit only segments crossing native zoom/map/button controls.
                // Unlike drawing on top and trusting sorting, this ensures the
                // border never paints across the clickable native buttons.
                float left = Mathf.Min(Mathf.Min(a[i].x, a[next].x), Mathf.Min(b[i].x, b[next].x));
                float right = Mathf.Max(Mathf.Max(a[i].x, a[next].x), Mathf.Max(b[i].x, b[next].x));
                float bottom = Mathf.Min(Mathf.Min(a[i].y, a[next].y), Mathf.Min(b[i].y, b[next].y));
                float top = Mathf.Max(Mathf.Max(a[i].y, a[next].y), Mathf.Max(b[i].y, b[next].y));
                Rect segment = Rect.MinMaxRect(left, bottom, right, top);
                bool blocked = false;
                if (exclusions != null)
                    foreach (Rect control in exclusions)
                        if (control.Overlaps(segment)) { blocked = true; break; }
                if (blocked) continue;
                vh.AddTriangle(idx, nxt, idx + 1);
                vh.AddTriangle(idx + 1, nxt, nxt + 1);
            }
        }
        internal void Redraw() { SetVerticesDirty(); }
    }
}

namespace ArcanesGroupFrames.Modules.Minimap.Native
{
    /// <summary>
    /// White, opaque interior and transparent exterior. Assigned to the *native*
    /// Image used by Unity UI Mask; not painted over the live map as a fake mask.
    /// The source map, terrain, fog, dots and zoom inputs remain native.
    /// </summary>
    internal static class MinimapShapeSprites
    {
        private static readonly Dictionary<string, UnityEngine.Sprite> PreviewCache =
            new Dictionary<string, UnityEngine.Sprite>();

        internal static UnityEngine.Sprite Preview(string shape)
        {
            UnityEngine.Sprite sprite;
            if (PreviewCache.TryGetValue(shape, out sprite) && sprite != null) return sprite;
            UnityEngine.Texture2D texture;
            sprite = Create(shape, out texture);
            PreviewCache[shape] = sprite;
            return sprite;
        }

        internal static UnityEngine.Sprite Create(string shape, out UnityEngine.Texture2D texture)
        {
            const int pixels = 256;
            texture = new UnityEngine.Texture2D(pixels, pixels, UnityEngine.TextureFormat.RGBA32, false);
            texture.name = "ArcaneUI_MinimapShape_" + shape;
            texture.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            texture.filterMode = UnityEngine.FilterMode.Bilinear;
            UnityEngine.Color32[] colors = new UnityEngine.Color32[pixels * pixels];
            float radius = shape == "Round" || shape == "Ornate" ? .5f :
                shape == "Rounded" ? .14f : 0f;
            for (int y = 0; y < pixels; ++y)
                for (int x = 0; x < pixels; ++x)
                {
                    float px = ((x + .5f) / pixels - .5f);
                    float py = ((y + .5f) / pixels - .5f);
                    float alpha = 1f;
                    if (radius > 0f)
                    {
                        float qx = UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(px) - (.5f - radius), 0f);
                        float qy = UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(py) - (.5f - radius), 0f);
                        float distance = UnityEngine.Mathf.Sqrt(qx * qx + qy * qy);
                        alpha = UnityEngine.Mathf.Clamp01((radius - distance) * pixels + .5f);
                    }
                    colors[y * pixels + x] = new UnityEngine.Color32(255, 255, 255,
                        (byte)UnityEngine.Mathf.RoundToInt(alpha * 255f));
                }
            texture.SetPixels32(colors);
            texture.Apply(false, false);
            UnityEngine.Sprite sprite = UnityEngine.Sprite.Create(texture,
                new UnityEngine.Rect(0f, 0f, pixels, pixels),
                new UnityEngine.Vector2(.5f, .5f), 100f);
            sprite.name = "ArcaneUI_MinimapShape_" + shape;
            return sprite;
        }
    }
}
