using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Maths pures (sans dépendance au moteur) : couleurs, bruit, interpolations.</summary>
    public static class SalonMath
    {
        public static Color Hex(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length < 6) return Color.magenta;
            float r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            float g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            float b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            float a = hex.Length >= 8 ? System.Convert.ToInt32(hex.Substring(6, 2), 16) / 255f : 1f;
            return new Color(r, g, b, a);
        }

        public static Color Mul(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }

        public static Color WithA(Color c, float a) { c.a = a; return c; }

        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static int Wrap(int v, int p) { if (p <= 0) return v; int r = v % p; return r < 0 ? r + p : r; }

        public static float Noise(float x, float y, int seed, int px = 0, int py = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            float u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
            float a = Hash(Wrap(xi, px), Wrap(yi, py), seed);
            float b = Hash(Wrap(xi + 1, px), Wrap(yi, py), seed);
            float c = Hash(Wrap(xi, px), Wrap(yi + 1, py), seed);
            float d = Hash(Wrap(xi + 1, px), Wrap(yi + 1, py), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public static float Fbm(float x, float y, int seed, int octaves = 4, int px = 0, int py = 0)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y, seed + i * 31, px, py) * amp;
                norm += amp;
                x *= 2; y *= 2; px *= 2; py *= 2;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        public static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            float m = v.magnitude;
            return m > 1e-6f ? v / m : fallback;
        }

        public static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        /// <summary>1 au centre, 0 au-delà de r (bord doux).</summary>
        public static float Bump(float d, float r) { return d >= r ? 0f : Smooth01(1f - d / r); }

        public static float Gauss(float dx, float dy, float sx, float sy)
        {
            return Mathf.Exp(-(dx * dx) / (2 * sx * sx) - (dy * dy) / (2 * sy * sy));
        }

        /// <summary>Fusion « screen » d'une couleur sur une autre avec opacité.</summary>
        public static Color Over(Color dst, Color src, float a)
        {
            a = Mathf.Clamp01(a);
            return new Color(dst.r + (src.r - dst.r) * a, dst.g + (src.g - dst.g) * a, dst.b + (src.b - dst.b) * a, dst.a);
        }
    }
}
