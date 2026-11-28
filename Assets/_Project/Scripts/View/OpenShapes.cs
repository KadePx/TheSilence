using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Silence.View
{
    /// <summary>
    /// Flat coloured shapes drawn straight into the canvas: lines, rings and wedges for the gridless board.
    /// Points are in layout pixels from the top-left of the 1920x1080 root, y down, like <see cref="Ui.Box"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ShapeGraphic : MaskableGraphic
    {
        readonly List<UIVertex> verts = new List<UIVertex>();

        public void Clear()
        {
            verts.Clear();
            SetVerticesDirty();
        }

        public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            Add(a, col);
            Add(b, col);
            Add(c, col);
            SetVerticesDirty();
        }

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            Tri(a, b, c, col);
            Tri(a, c, d, col);
        }

        public void Rect(float x, float y, float w, float h, Color col)
            => Quad(new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h), col);

        public void Line(Vector2 a, Vector2 b, float width, Color col)
        {
            var d = b - a;
            if (d.sqrMagnitude < 0.0001f) return;
            var n = new Vector2(-d.y, d.x).normalized * (width / 2f);
            Quad(a + n, b + n, b - n, a - n, col);
        }

        public void Dashed(Vector2 a, Vector2 b, float width, Color col, float on = 10f, float off = 8f)
        {
            float len = (b - a).magnitude;
            if (len < 0.01f) return;
            var dir = (b - a) / len;
            for (float t = 0; t < len; t += on + off)
                Line(a + dir * t, a + dir * Mathf.Min(len, t + on), width, col);
        }

        void Add(Vector2 p, Color col)
        {
            var v = UIVertex.simpleVert;
            v.position = new Vector3(p.x, -p.y, 0f);
            v.color = col;
            verts.Add(v);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < verts.Count; i++) vh.AddVert(verts[i]);
            for (int i = 0; i + 2 < verts.Count; i += 3) vh.AddTriangle(i, i + 1, i + 2);
        }

        public static ShapeGraphic Create(Transform parent, string name)
        {
            var rt = Ui.Box(parent, name, 0, 0, Ui.RefW, Ui.RefH);
            var g = rt.gameObject.AddComponent<ShapeGraphic>();
            g.raycastTarget = false;
            return g;
        }
    }
}
