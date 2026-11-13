using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Image en mémoire (pur C#), convertie en Texture2D côté Unity.</summary>
    public class Img
    {
        public readonly int w, h;
        public readonly Color[] px;

        public Img(int w, int h, Color fill)
        {
            this.w = w; this.h = h;
            px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
        }

        public Color Get(int x, int y)
        {
            x = Mathf.Clamp(x, 0, w - 1); y = Mathf.Clamp(y, 0, h - 1);
            return px[y * w + x];
        }

        public void Set(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = c;
        }

        /// <summary>Peint (alpha « over ») : la couleur de dessous est conservée pondérée.</summary>
        public void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || a <= 0) return;
            int i = y * w + x;
            Color d = px[i];
            a = Mathf.Clamp01(a);
            float outA = a + d.a * (1 - a);
            if (outA <= 1e-5f) { px[i] = new Color(0, 0, 0, 0); return; }
            px[i] = new Color((c.r * a + d.r * d.a * (1 - a)) / outA, (c.g * a + d.g * d.a * (1 - a)) / outA, (c.b * a + d.b * d.a * (1 - a)) / outA, outA);
        }

        public Color Bilinear(float u, float v)
        {
            float x = u * w - 0.5f, y = v * h - 0.5f;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            return Color.Lerp(Color.Lerp(Get(x0, y0), Get(x0 + 1, y0), fx), Color.Lerp(Get(x0, y0 + 1), Get(x0 + 1, y0 + 1), fx), fy);
        }
    }
}
