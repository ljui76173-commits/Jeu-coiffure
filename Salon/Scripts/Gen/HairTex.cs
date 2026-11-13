using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>
    /// Texture de « carte de cheveux » : une vingtaine de cheveux fins par carte (alpha + nuances),
    /// répétable verticalement. R/G/B = luminosité du cheveu, A = présence.
    /// </summary>
    public static class HairTex
    {
        public static Img Card(int w = 128, int h = 512, int seed = 11)
        {
            var img = new Img(w, h, new Color(1, 1, 1, 0));
            var rng = new System.Random(seed);
            int hairs = 26;
            for (int k = 0; k < hairs; k++)
            {
                // davantage de cheveux au centre de la carte, moins sur les bords
                float u = (float)rng.NextDouble();
                u = 0.5f + (u - 0.5f) * (0.55f + 0.45f * (float)rng.NextDouble());
                float x0 = u * w;
                float width = 1.1f + (float)rng.NextDouble() * 1.4f;
                float bright = 0.72f + (float)rng.NextDouble() * 0.5f;
                float drift = ((float)rng.NextDouble() - 0.5f) * 6f;      // légère inclinaison
                float wav = (float)rng.NextDouble() * 1.5f;
                float ph = (float)rng.NextDouble() * 6.28f;
                // trous le long du cheveu (cheveux plus courts / qui se croisent)
                float gapStart = (float)rng.NextDouble(), gapLen = rng.NextDouble() < 0.35 ? 0.08f + (float)rng.NextDouble() * 0.2f : 0f;
                for (int y = 0; y < h; y++)
                {
                    float v = (float)y / h;
                    float dv = Mathf.Repeat(v - gapStart, 1f);
                    if (dv < gapLen) continue;
                    float cx = x0 + drift * Mathf.Sin(v * 6.2831f) + wav * Mathf.Sin(v * 6.2831f * 3f + ph);
                    cx = Mathf.Repeat(cx, w);
                    int xa = Mathf.FloorToInt(cx - width - 1), xb = Mathf.CeilToInt(cx + width + 1);
                    for (int x = xa; x <= xb; x++)
                    {
                        int xx = ((x % w) + w) % w;
                        float d = Mathf.Abs(x + 0.5f - cx);
                        float a = Mathf.Clamp01(width - d + 0.5f);
                        if (a <= 0) continue;
                        float edgeFade = 1f - Mathf.Pow(Mathf.Abs(xx / (float)w - 0.5f) * 2f, 4f);
                        a *= edgeFade;
                        // reflet fin au centre du cheveu
                        float shade = bright * (0.85f + 0.15f * (1f - d / (width + 0.5f)));
                        var dst = img.px[y * w + xx];
                        float na = Mathf.Max(dst.a, a);
                        float nb = dst.a > 0 ? Mathf.Lerp(dst.r, shade, a) : shade;
                        img.px[y * w + xx] = new Color(nb, nb, nb, na);
                    }
                }
            }
            return img;
        }
    }
}
