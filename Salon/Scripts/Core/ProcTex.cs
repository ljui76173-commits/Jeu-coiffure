using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Textures générées par le code (aucun fichier image nécessaire).</summary>
    public static class ProcTex
    {
        static Texture2D New(int w, int h, bool mips = true, bool repeat = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips);
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
            t.anisoLevel = 4;
            return t;
        }

        static Texture2D Finish(Texture2D t, Color[] px)
        {
            t.SetPixels(px);
            t.Apply(true, false);
            return t;
        }

        /// <summary>Parquet en point de Hongrie (chevrons), répétable.</summary>
        public static Texture2D Parquet(int size = 512)
        {
            var t = New(size, size);
            var px = new Color[size * size];
            Color[] woods =
            {
                SalonUtil.Hex("#B98250"), SalonUtil.Hex("#A56F3E"), SalonUtil.Hex("#C8955F"),
                SalonUtil.Hex("#AE7845"), SalonUtil.Hex("#BF8A55")
            };
            const int cols = 2;
            const int planks = 8; // planches par répétition verticale
            float colW = 1f / cols, pw = 1f / planks;
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    int ci = Mathf.Min(cols - 1, (int)(u / colW));
                    float lx = u - ci * colW;
                    float lxm = (ci & 1) == 1 ? colW - lx : lx;
                    float diag = v + lxm;
                    float pf = diag / pw;
                    int pi = Mathf.FloorToInt(pf);
                    float across = pf - pi;
                    int pim = ((pi % planks) + planks) % planks;
                    float h = SalonUtil.Hash(pim, ci, 11);
                    Color c = woods[Mathf.Min(woods.Length - 1, (int)(h * woods.Length))];
                    c = SalonUtil.Mul(c, 0.92f + 0.16f * SalonUtil.Hash(pim, ci, 23));
                    // veinage le long de la planche (répétable)
                    float along = (v - lxm) * 6f;
                    float n = SalonUtil.Fbm(along, across * 3f + pim * 7.3f, 5, 3, 6, 0);
                    float grain = 0.5f + 0.5f * Mathf.Sin(across * 38f + n * 9f);
                    c = SalonUtil.Mul(c, 0.9f + 0.14f * grain);
                    // joints
                    float edge = Mathf.Min(across, 1f - across) * pw * size;
                    float side = Mathf.Min(lx, colW - lx) * size;
                    float j = Mathf.Min(edge, side);
                    if (j < 1.2f) c = SalonUtil.Mul(c, Mathf.Lerp(0.45f, 1f, j / 1.2f));
                    c.a = 1;
                    px[y * size + x] = c;
                }
            }
            return Finish(t, px);
        }

        public static Texture2D Marble(int size = 256)
        {
            var t = New(size, size);
            var px = new Color[size * size];
            Color baseC = SalonUtil.Hex("#F3F0EB"), vein = SalonUtil.Hex("#B8B2AA");
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float n = SalonUtil.Fbm(u * 4, v * 4, 3, 5, 4, 4);
                    float s = Mathf.Abs(Mathf.Sin((u + v) * Mathf.PI * 2f + n * 7f));
                    float k = Mathf.Pow(1f - s, 14f) * 0.8f + (n - 0.5f) * 0.08f;
                    Color c = Color.Lerp(baseC, vein, Mathf.Clamp01(k));
                    c.a = 1;
                    px[y * size + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Texture de velours (légère variation).</summary>
        public static Texture2D Velvet(int size = 128)
        {
            var t = New(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = SalonUtil.Fbm(x / 16f, y / 16f, 9, 4, size / 16, size / 16);
                    float k = 0.86f + 0.2f * n;
                    px[y * size + x] = new Color(k, k, k, 1);
                }
            return Finish(t, px);
        }

        /// <summary>Vue sur les toits de Paris au coucher du soleil.</summary>
        public static Texture2D ParisView(int w = 1024, int h = 640)
        {
            var t = New(w, h, true, false);
            var px = new Color[w * h];
            Color skyTop = SalonUtil.Hex("#7E9CC9"), skyMid = SalonUtil.Hex("#E9B9B0"), skyLow = SalonUtil.Hex("#FBD9A8");
            Vector2 sun = new Vector2(w * 0.3f, h * 0.36f);
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                Color sky = v > 0.55f ? Color.Lerp(skyMid, skyTop, (v - 0.55f) / 0.45f) : Color.Lerp(skyLow, skyMid, v / 0.55f);
                for (int x = 0; x < w; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), sun) / h;
                    Color c = sky + new Color(1f, 0.85f, 0.6f) * Mathf.Exp(-d * 9f) * 0.45f;
                    // nuages doux
                    float cl = SalonUtil.Fbm(x / 140f, y / 40f, 77, 4);
                    if (v > 0.45f) c = Color.Lerp(c, new Color(1f, 0.93f, 0.9f), Mathf.Clamp01((cl - 0.55f) * 2.2f) * 0.5f);
                    c.a = 1;
                    px[y * w + x] = c;
                }
            }

            // Tour Eiffel au loin
            Color haze = SalonUtil.Hex("#9C98AE");
            int ex = (int)(w * 0.72f), eBase = (int)(h * 0.2f), eTop = (int)(h * 0.78f);
            for (int y = eBase; y < eTop; y++)
            {
                float tt = (float)(y - eBase) / (eTop - eBase);
                float half = 4 + 70f * Mathf.Pow(1f - tt, 2.6f);
                bool arch = tt < 0.16f && Mathf.Abs(0) < 1;
                for (int x = (int)(ex - half); x <= (int)(ex + half); x++)
                {
                    if (x < 0 || x >= w) continue;
                    float dx = Mathf.Abs(x - ex);
                    if (arch && dx < half * 0.62f * Mathf.Sqrt(1f - tt / 0.16f)) continue; // arche
                    if (Mathf.Abs(tt - 0.16f) < 0.008f || Mathf.Abs(tt - 0.42f) < 0.006f) { Blend(px, w, x, y, haze, 1); continue; }
                    // treillis : transparence légère
                    bool lattice = ((x + y) % 7 == 0) || ((x - y + 1000) % 7 == 0) || dx > half - 2;
                    Blend(px, w, x, y, haze, lattice ? 1f : 0.55f);
                }
            }
            for (int y = eTop; y < eTop + 14 && y < h; y++) Blend(px, w, ex, y, haze, 1);

            // Deux rangées d'immeubles haussmanniens
            DrawRow(px, w, h, 0.32f, 0.12f, 60, 120, SalonUtil.Hex("#C9BBB0"), SalonUtil.Hex("#8C94A6"), 0.6f, 101);
            DrawRow(px, w, h, 0.0f, 0.3f, 110, 220, SalonUtil.Hex("#EADBC6"), SalonUtil.Hex("#6F7A8C"), 1f, 202);
            return Finish(t, px);
        }

        static void Blend(Color[] px, int w, int x, int y, Color c, float a)
        {
            int i = y * w + x;
            if (i < 0 || i >= px.Length) return;
            px[i] = Color.Lerp(px[i], c, a);
            px[i].a = 1;
        }

        static void DrawRow(Color[] px, int w, int h, float baseV, float heightV, int minW, int maxW,
            Color facade, Color zinc, float detail, int seed)
        {
            var rng = new System.Random(seed);
            int x = -rng.Next(0, 60);
            while (x < w)
            {
                int bw = rng.Next(minW, maxW);
                int top = (int)(h * (baseV + heightV * (0.75f + 0.35f * (float)rng.NextDouble())));
                int roofH = (int)(bw * 0.28f);
                int bottom = (int)(h * baseV);
                Color fac = SalonUtil.Mul(facade, 0.9f + 0.15f * (float)rng.NextDouble());
                for (int y = 0; y < top + roofH && y < h; y++)
                {
                    for (int xx = x; xx < x + bw; xx++)
                    {
                        if (xx < 0 || xx >= w) continue;
                        Color c;
                        if (y <= top)
                        {
                            if (y < bottom && baseV > 0.01f) continue;
                            c = fac;
                            // fenêtres
                            int lx = xx - x, ly = top - y;
                            int cell = Mathf.Max(10, bw / 6);
                            bool win = (lx % cell) > cell * 0.3f && (lx % cell) < cell * 0.7f && (ly % 26) > 6 && (ly % 26) < 20 && lx > 4 && lx < bw - 4 && ly > 4;
                            if (win) c = Color.Lerp(SalonUtil.Hex("#3E4A63"), SalonUtil.Hex("#F6C27A"), SalonUtil.Hash(lx / cell, ly / 26, seed) > 0.8f ? 0.8f : 0f);
                            if (ly % 26 == 20 && detail > 0.9f) c = SalonUtil.Mul(c, 0.75f); // balcons
                        }
                        else
                        {
                            // toit mansardé en zinc
                            float r = (float)(y - top) / roofH;
                            int inset = (int)(r * bw * 0.22f);
                            if (xx < x + inset || xx > x + bw - inset) continue;
                            c = SalonUtil.Mul(zinc, 0.9f + 0.2f * r);
                            if (((xx - x) % 14) == 0) c = SalonUtil.Mul(c, 0.85f);
                        }
                        c = Color.Lerp(c, SalonUtil.Hex("#E8C6B8"), (1 - detail) * 0.35f);
                        Blend(px, w, xx, y, c, 1);
                    }
                }
                // cheminées en terre cuite
                int nch = rng.Next(1, 3);
                for (int k = 0; k < nch; k++)
                {
                    int cx = x + rng.Next(bw / 5, bw * 4 / 5);
                    int cw = 8 + rng.Next(0, 8), ch = 16 + rng.Next(0, 14);
                    int cy = top + roofH - 4;
                    for (int y = cy; y < cy + ch && y < h; y++)
                        for (int xx = cx; xx < cx + cw; xx++)
                            if (xx >= 0 && xx < w)
                                Blend(px, w, xx, y, Color.Lerp(SalonUtil.Hex("#B5674D"), SalonUtil.Hex("#E8C6B8"), (1 - detail) * 0.35f), 1);
                }
                x += bw;
            }
        }

        /// <summary>Petit tableau abstrait pour les murs.</summary>
        public static Texture2D Painting(int seed, int w = 192, int h = 240)
        {
            var t = New(w, h, true, false);
            var px = new Color[w * h];
            var rng = new System.Random(seed);
            Color bg = Color.Lerp(SalonUtil.Hex("#EFE3D3"), SalonUtil.Hex("#E7CFC4"), (float)rng.NextDouble());
            Color[] pal = { SalonUtil.Hex("#C98B7E"), SalonUtil.Hex("#8FA38A"), SalonUtil.Hex("#D9B26F"), SalonUtil.Hex("#7D8FB3"), SalonUtil.Hex("#E3A7B1") };
            int nb = 4;
            Vector3[] blobs = new Vector3[nb];
            Color[] bc = new Color[nb];
            for (int i = 0; i < nb; i++)
            {
                blobs[i] = new Vector3((float)rng.NextDouble() * w, (float)rng.NextDouble() * h, 30 + (float)rng.NextDouble() * 70);
                bc[i] = pal[rng.Next(pal.Length)];
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = bg;
                    for (int i = 0; i < nb; i++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), blobs[i]) / blobs[i].z;
                        float n = SalonUtil.Fbm(x / 30f, y / 30f, seed + i, 3);
                        c = Color.Lerp(c, bc[i], Mathf.Clamp01(1.2f - d - n * 0.4f) * 0.85f);
                    }
                    c.a = 1;
                    px[y * w + x] = c;
                }
            return Finish(t, px);
        }

        // ---------- Textures d'interface ----------
        public static Texture2D RoundRect(int size, float radius, Color fill, Color border, float borderW)
        {
            var t = New(size, size, false, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundRectDist(x + 0.5f, y + 0.5f, size, size, radius);
                    float aOut = Mathf.Clamp01(0.5f - d);
                    float aBorder = borderW > 0 ? Mathf.Clamp01(d + borderW + 0.5f) : 0f;
                    Color c = Color.Lerp(fill, border, aBorder);
                    c.a *= aOut;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }

        static float RoundRectDist(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w / 2) - (w / 2 - r);
            float qy = Mathf.Abs(y - h / 2) - (h / 2 - r);
            float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        public static Texture2D Ring(int size, float thickness, Color c)
        {
            var t = New(size, size, false, false);
            var px = new Color[size * size];
            float r = size / 2f - 1;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                    float a = Mathf.Clamp01(thickness / 2 - Mathf.Abs(d - (r - thickness / 2)) + 0.5f);
                    Color col = c;
                    col.a *= a;
                    px[y * size + x] = col;
                }
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }

        /// <summary>Ombre portée douce (rectangle arrondi flou).</summary>
        public static Texture2D SoftShadow(int size, float radius, float blur, float alpha)
        {
            var t = New(size, size, false, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundRectDist(x + 0.5f, y + 0.5f, size, size, radius + blur) + blur;
                    float a = Mathf.Clamp01(1f - (d + blur) / (2f * blur));
                    a = a * a * (3 - 2 * a) * alpha;
                    px[y * size + x] = new Color(0.12f, 0.08f, 0.06f, a);
                }
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }
    }
}
