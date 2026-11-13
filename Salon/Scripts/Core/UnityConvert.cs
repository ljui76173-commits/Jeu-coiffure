using UnityEngine;
using UnityEngine.Rendering;

namespace SalonCoiffure
{
    /// <summary>Conversion des données pures (MeshData, Img) en objets Unity.</summary>
    public static class UnityConvert
    {
        public static Mesh ToMesh(MeshData d)
        {
            var m = new Mesh { name = d.name };
            if (d.v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(d.v);
            if (d.n.Count == d.v.Count) m.SetNormals(d.n);
            if (d.uv.Count == d.v.Count) m.SetUVs(0, d.uv);
            if (d.uv2.Count == d.v.Count) m.SetUVs(1, d.uv2);
            if (d.uv3.Count == d.v.Count) m.SetUVs(2, d.uv3);
            if (d.c.Count == d.v.Count) m.SetColors(d.c);
            if (d.t.Count == d.v.Count) m.SetTangents(d.t);
            m.SetTriangles(d.tri, 0);
            if (d.n.Count != d.v.Count) m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static Texture2D ToTexture(Img img, bool srgb, bool mips, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var t = new Texture2D(img.w, img.h, TextureFormat.RGBA32, mips, !srgb);
            t.wrapMode = wrap;
            t.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
            t.anisoLevel = 4;
            t.SetPixels(img.px);
            t.Apply(mips, false);
            return t;
        }

        /// <summary>
        /// Texture de carte de cheveux avec mips qui conservent la couverture alpha
        /// (sinon les cheveux « fondent » au loin).
        /// </summary>
        public static Texture2D ToHairCardTexture(Img img)
        {
            var t = new Texture2D(img.w, img.h, TextureFormat.RGBA32, true, true);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            Color[] level = img.px;
            int w = img.w, h = img.h;
            float target = Coverage(level, 1f);
            t.SetPixels(level, 0);
            for (int mip = 1; mip < t.mipmapCount; mip++)
            {
                int nw = Mathf.Max(1, w / 2), nh = Mathf.Max(1, h / 2);
                var next = new Color[nw * nh];
                for (int y = 0; y < nh; y++)
                    for (int x = 0; x < nw; x++)
                    {
                        Color s = Color.clear;
                        for (int dy = 0; dy < 2; dy++)
                            for (int dx = 0; dx < 2; dx++)
                                s += level[Mathf.Min(h - 1, y * 2 + dy) * w + Mathf.Min(w - 1, x * 2 + dx)];
                        next[y * nw + x] = s / 4f;
                    }
                // ajuste l'alpha pour garder la même proportion de pixels > 0.5
                float lo = 0.5f, hi = 4f;
                for (int it = 0; it < 14; it++)
                {
                    float k = (lo + hi) / 2;
                    if (Coverage(next, k) < target) lo = k; else hi = k;
                }
                float kk = (lo + hi) / 2;
                for (int i = 0; i < next.Length; i++) next[i].a = Mathf.Clamp01(next[i].a * kk);
                t.SetPixels(next, mip);
                level = next; w = nw; h = nh;
            }
            t.Apply(false, false);
            return t;
        }

        static float Coverage(Color[] px, float k)
        {
            int n = 0;
            for (int i = 0; i < px.Length; i++) if (px[i].a * k > 0.5f) n++;
            return (float)n / px.Length;
        }
    }
}
