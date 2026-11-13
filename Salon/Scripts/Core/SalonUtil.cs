using UnityEngine;
using UnityEngine.Rendering;

namespace SalonCoiffure
{
    /// <summary>Petits outils partagés : couleurs, bruit procédural, création de primitives.</summary>
    public static class SalonUtil
    {
        public static Color Hex(string hex)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out c)) c = Color.magenta;
            return c;
        }

        public static Color Mul(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale,
            Material mat, Quaternion localRot, bool castShadows = true, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale,
            Material mat, bool castShadows = true, string name = null)
        {
            return Prim(type, parent, localPos, localScale, mat, Quaternion.identity, castShadows, name);
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Material mat, bool castShadows = true, string name = null)
        {
            return Prim(PrimitiveType.Cube, parent, pos, size, mat, castShadows, name);
        }

        // ---------- Bruit ----------
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

        /// <summary>Bruit de valeur, répétable (période px, py ; 0 = pas de répétition).</summary>
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
    }
}
