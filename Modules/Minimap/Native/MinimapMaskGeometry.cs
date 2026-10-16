using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Modules.Minimap.Native
{
    /// <summary>
    /// Changes the actual stencil *mesh* produced by an existing Unity UI Mask.
    /// Unity's Mask clips using graphic geometry, not the alpha silhouette of
    /// an assigned sprite. The original native sprite/material remain untouched.
    /// This component is removable; it never changes the map's render texture,
    /// camera, markers, UI hierarchy or native zoom buttons.
    /// </summary>
    internal sealed class MinimapMaskGeometry : BaseMeshEffect
    {
        internal string Shape = "Round";

        internal void SetShape(string shape)
        {
            if (Shape == shape) return;
            Shape = shape;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null || vh.currentVertCount == 0 || graphic == null)
                return;

            Rect rect = graphic.rectTransform.rect;
            if (rect.width < 8f || rect.height < 8f) return;

            // Retain the original Image's texture coordinates and tint; only the
            // silhouette is changed. Replacing the sprite can remove the native
            // minimap terrain/background, as in the earlier implementation.
            UIVertex first = new UIVertex();
            vh.PopulateUIVertex(ref first, 0);
            float uMin = first.uv0.x, uMax = first.uv0.x;
            float vMin = first.uv0.y, vMax = first.uv0.y;
            for (int i = 1; i < vh.currentVertCount; ++i)
            {
                UIVertex vertex = new UIVertex();
                vh.PopulateUIVertex(ref vertex, i);
                uMin = Mathf.Min(uMin, vertex.uv0.x);
                uMax = Mathf.Max(uMax, vertex.uv0.x);
                vMin = Mathf.Min(vMin, vertex.uv0.y);
                vMax = Mathf.Max(vMax, vertex.uv0.y);
            }
            // Native shaders may use nonstandard UV layouts. In this case do
            // not overwrite their mesh, because that could lose map imagery.
            if (uMax - uMin < .0001f || vMax - vMin < .0001f) return;

            List<Vector2> edge = BuildOutline(rect, Shape);
            if (edge.Count < 3) return;
            Color32 tint = first.color;
            Vector2 uvCenter = new Vector2((uMin + uMax) * .5f, (vMin + vMax) * .5f);
            vh.Clear();
            vh.AddVert(rect.center, tint, uvCenter);
            foreach (Vector2 point in edge)
            {
                float u = Mathf.InverseLerp(rect.xMin, rect.xMax, point.x);
                float v = Mathf.InverseLerp(rect.yMin, rect.yMax, point.y);
                vh.AddVert(point, tint, new Vector2(Mathf.Lerp(uMin, uMax, u),
                    Mathf.Lerp(vMin, vMax, v)));
            }
            for (int i = 0; i < edge.Count; ++i)
            {
                int next = i + 1 == edge.Count ? 1 : i + 2;
                vh.AddTriangle(0, i + 1, next);
            }
        }

        private static List<Vector2> BuildOutline(Rect rect, string shape)
        {
            var points = new List<Vector2>();
            bool round = shape == "Round" || shape == "Ornate";
            float radius = round ? Mathf.Min(rect.width, rect.height) * .5f :
                shape == "Rounded" ? Mathf.Min(rect.width, rect.height) * .14f : 0f;
            if (radius <= .01f)
            {
                points.Add(new Vector2(rect.xMin, rect.yMin));
                points.Add(new Vector2(rect.xMax, rect.yMin));
                points.Add(new Vector2(rect.xMax, rect.yMax));
                points.Add(new Vector2(rect.xMin, rect.yMax));
                return points;
            }
            const int steps = 16;
            for (int corner = 0; corner < 4; ++corner)
            {
                Vector2 center = corner == 0 ? new Vector2(rect.xMax - radius, rect.yMin + radius) :
                    corner == 1 ? new Vector2(rect.xMax - radius, rect.yMax - radius) :
                    corner == 2 ? new Vector2(rect.xMin + radius, rect.yMax - radius) :
                        new Vector2(rect.xMin + radius, rect.yMin + radius);
                float start = -90f + corner * 90f;
                for (int i = 0; i <= steps; ++i)
                {
                    float angle = (start + i * 90f / steps) * Mathf.Deg2Rad;
                    points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
            return points;
        }
    }
}
