using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Modules.CooldownAuras.UI
{
    /// <summary>Allocation-free-at-runtime vector geometry. Optional vertical gradient
    /// and ring/arc shapes; Unity regenerates triangles only when styling changes.</summary>
    internal sealed class ArcaneUiAuraShapeGraphic : MaskableGraphic
    {
        private string _shape = "Rectangle";
        private float _ringThickness = .24f;
        private bool _gradientEnabled;
        private float _progress = 1f;
        private bool _clockwise;
        private Color _gradient = new Color(1f,.85f,.53f,1f);
        internal void SetShape(string value)
        {
            value = value ?? "Rectangle";
            if (_shape == value) return;
            _shape = value;
            SetVerticesDirty();
        }
        internal void SetRingThickness(float value)
        {
            value = Mathf.Clamp(value,.02f,.48f);
            if (Mathf.Abs(value - _ringThickness) < .001f) return;
            _ringThickness = value; SetVerticesDirty();
        }
        internal void SetGradient(bool enabled, Color second)
        {
            if (_gradientEnabled == enabled && _gradient == second) return;
            _gradientEnabled = enabled; _gradient = second; SetVerticesDirty();
        }
        internal void SetProgress(float value, bool clockwise)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(_progress - value) < .001f && _clockwise == clockwise) return;
            _progress = value; _clockwise = clockwise; SetVerticesDirty();
        }
        private Color Shade(float y, Rect rect)
        {
            if (!_gradientEnabled) return color;
            Color other = _gradient;
            other.a = color.a;
            return Color.Lerp(color, other, Mathf.Clamp01((y-rect.yMin) / Mathf.Max(1f,rect.height)));
        }
        private void Vertex(VertexHelper helper, Rect rect, Vector2 position)
        {
            UIVertex v = UIVertex.simpleVert;
            v.color = Shade(position.y, rect);
            v.position = position;
            helper.AddVert(v);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            if (_shape == "Ring" || _shape == "Arc")
            {
                const int sides = 64;
                if (_progress <= .0001f) return;
                int steps = Mathf.Max(1, Mathf.CeilToInt(sides * _progress));
                float start = _shape == "Arc" ? -225f : -90f;
                float sweep = (_shape == "Arc" ? 270f : 360f) * _progress;
                if (_clockwise) sweep = -sweep;
                for (int i = 0; i <= steps; ++i)
                {
                    float angle = (start + sweep * i / steps) * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    Vector2 outer = center + Vector2.Scale(direction, new Vector2(rect.width,rect.height)*.5f);
                    Vector2 inner = center + Vector2.Scale(direction, new Vector2(rect.width,rect.height)*(.5f-_ringThickness));
                    Vertex(vh,rect,outer); Vertex(vh,rect,inner);
                    if (i > 0)
                    {
                        int startIndex = (i-1)*2;
                        vh.AddTriangle(startIndex,startIndex+1,startIndex+2);
                        vh.AddTriangle(startIndex+1,startIndex+3,startIndex+2);
                    }
                }
                return;
            }
            int sidesPolygon = _shape == "Circle" ? 48 : _shape == "Hexagon" ? 6 :
                _shape == "Triangle" ? 3 : _shape == "Diamond" ? 4 : 0;
            if (sidesPolygon == 0)
            {
                Vertex(vh,rect,new Vector2(rect.xMin,rect.yMin));
                Vertex(vh,rect,new Vector2(rect.xMin,rect.yMax));
                Vertex(vh,rect,new Vector2(rect.xMax,rect.yMax));
                Vertex(vh,rect,new Vector2(rect.xMax,rect.yMin));
                vh.AddTriangle(0,1,2); vh.AddTriangle(0,2,3);
                return;
            }
            Vertex(vh,rect,center);
            for (int i = 0; i <= sidesPolygon; ++i)
            {
                float angle = ((float)i/sidesPolygon)*Mathf.PI*2f + Mathf.PI*.5f;
                if (_shape == "Diamond") angle += Mathf.PI*.25f;
                Vertex(vh,rect,new Vector2(center.x + Mathf.Cos(angle)*rect.width*.5f,
                    center.y + Mathf.Sin(angle)*rect.height*.5f));
                if (i > 0) vh.AddTriangle(0,i,i+1);
            }
        }
    }
}
