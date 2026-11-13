using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Cou, épaules sous la cape de coiffure (avec plis) et col. Espace de la tête.</summary>
    public static class BodyGen
    {
        public static MeshData Neck(float scale = 1f)
        {
            var m = new MeshData { name = "Cou" };
            int rows = 16, cols = 36;
            for (int i = 0; i <= rows; i++)
            {
                float t = (float)i / rows;
                float y = Mathf.Lerp(-0.035f, -0.17f, t);
                float rx = Mathf.Lerp(0.054f, 0.066f, t * t) * scale, rz = Mathf.Lerp(0.053f, 0.06f, t) * scale;
                for (int j = 0; j <= cols; j++)
                {
                    float a = 2 * Mathf.PI * j / cols;
                    // nuque légèrement en arrière, gorge en avant
                    float zOff = -0.012f + 0.004f * t;
                    m.Add(new Vector3(Mathf.Sin(a) * rx, y, Mathf.Cos(a) * rz + zOff), new Vector2((float)j / cols, t));
                }
            }
            m.Grid(0, rows + 1, cols + 1, false, true);
            Outward(m, new Vector3(0, -0.15f, -0.01f), true);
            m.RecalculateNormals();
            m.WeldNormals();
            return m;
        }

        // Profil du buste sous la cape : (demi-largeur x, demi-profondeur z, hauteur y)
        static readonly Vector3[] Profile =
        {
            new Vector3(0.078f, 0.072f, -0.138f),
            new Vector3(0.125f, 0.095f, -0.166f),
            new Vector3(0.182f, 0.118f, -0.194f),
            new Vector3(0.212f, 0.128f, -0.238f),
            new Vector3(0.222f, 0.133f, -0.306f),
            new Vector3(0.232f, 0.142f, -0.476f),
            new Vector3(0.258f, 0.16f, -0.896f),
        };

        /// <summary>Demi-largeur et demi-profondeur du buste à la hauteur y (0 si au-dessus des épaules).</summary>
        public static bool BodyAt(float y, out float a, out float b)
        {
            a = 0; b = 0;
            if (y > Profile[0].z) return false;
            for (int i = 0; i < Profile.Length - 1; i++)
            {
                if (y <= Profile[i].z && y >= Profile[i + 1].z)
                {
                    float t = (Profile[i].z - y) / (Profile[i].z - Profile[i + 1].z);
                    t = t * t * (3 - 2 * t) * 0.35f + t * 0.65f;
                    a = Mathf.Lerp(Profile[i].x, Profile[i + 1].x, t);
                    b = Mathf.Lerp(Profile[i].y, Profile[i + 1].y, t);
                    return true;
                }
            }
            a = Profile[Profile.Length - 1].x; b = Profile[Profile.Length - 1].y;
            return true;
        }

        /// <summary>Cape de coiffure : épaules en pente douce puis tombé avec des plis.</summary>
        public static MeshData Cape(int seed)
        {
            var m = new MeshData { name = "Cape" };
            int rows = 56, cols = 96;
            var rng = new System.Random(seed);
            float ph1 = (float)rng.NextDouble() * 6f, ph2 = (float)rng.NextDouble() * 6f;
            for (int i = 0; i <= rows; i++)
            {
                float t = (float)i / rows;
                float tt = t * t * 0.6f + t * 0.4f;
                float y = Mathf.Lerp(-0.138f, -0.92f, tt);
                float a, b;
                BodyAt(y, out a, out b);
                float fold = 0.012f * SalonMath.Smooth01((-0.3f - y) / 0.3f);
                for (int j = 0; j <= cols; j++)
                {
                    float ang = 2 * Mathf.PI * j / cols;
                    float r = 1f + (fold / Mathf.Max(a, b)) * (Mathf.Sin(ang * 9f + ph1 + y * 3f) * 0.7f + Mathf.Sin(ang * 5f + ph2) * 0.3f);
                    m.Add(new Vector3(Mathf.Sin(ang) * a * r, y, Mathf.Cos(ang) * b * r - 0.01f), new Vector2((float)j / cols, 1f - t));
                }
            }
            m.Grid(0, rows + 1, cols + 1, false, true);
            Outward(m, new Vector3(0, -0.5f, -0.01f), true);
            m.RecalculateNormals();
            m.WeldNormals();
            return m;
        }

        public static MeshData Collar()
        {
            var t = MeshData.Torus(0.076f, 0.013f, 48, 14);
            var m = new MeshData { name = "Col" };
            m.Append(t, new Vector3(0, -0.139f, -0.008f), Quaternion.identity, new Vector3(1f, 0.8f, 0.95f));
            return m;
        }

        /// <summary>Oriente les triangles vers l'extérieur d'un axe vertical passant par c.</summary>
        public static void Outward(MeshData m, Vector3 c, bool axisY)
        {
            for (int i = 0; i < m.tri.Count; i += 3)
            {
                int a = m.tri[i], b = m.tri[i + 1], d = m.tri[i + 2];
                Vector3 fn = Vector3.Cross(m.v[b] - m.v[a], m.v[d] - m.v[a]);
                Vector3 cen = (m.v[a] + m.v[b] + m.v[d]) / 3f - c;
                if (axisY) cen.y = 0;
                if (Vector3.Dot(fn, cen) < 0) { m.tri[i + 1] = d; m.tri[i + 2] = b; }
            }
        }
    }
}
