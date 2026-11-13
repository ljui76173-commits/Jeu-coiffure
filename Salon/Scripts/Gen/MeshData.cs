using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Maillage en mémoire (pur C#), converti en Mesh Unity par MeshDataUnity.</summary>
    public class MeshData
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Vector2> uv2 = new List<Vector2>();
        public readonly List<Vector2> uv3 = new List<Vector2>();
        public readonly List<Color> c = new List<Color>();
        public readonly List<Vector4> t = new List<Vector4>();
        public readonly List<int> tri = new List<int>();
        public string name = "Mesh";

        public int Add(Vector3 p, Vector2 u)
        {
            v.Add(p); uv.Add(u);
            return v.Count - 1;
        }

        public void Tri(int a, int b, int c2) { tri.Add(a); tri.Add(b); tri.Add(c2); }

        /// <summary>Grille (rows x cols) de sommets déjà ajoutés à partir de start ; wrap = colonne qui reboucle.</summary>
        public void Grid(int start, int rows, int cols, bool wrapCols, bool flip = false)
        {
            int cc = wrapCols ? cols : cols - 1;
            for (int i = 0; i < rows - 1; i++)
                for (int j = 0; j < cc; j++)
                {
                    int jn = (j + 1) % cols;
                    int a = start + i * cols + j, b = start + i * cols + jn;
                    int d = start + (i + 1) * cols + j, e = start + (i + 1) * cols + jn;
                    if (!flip) { Tri(a, b, d); Tri(b, e, d); }
                    else { Tri(a, d, b); Tri(b, d, e); }
                }
        }

        public void RecalculateNormals()
        {
            var acc = new Vector3[v.Count];
            for (int i = 0; i < tri.Count; i += 3)
            {
                int a = tri[i], b = tri[i + 1], d = tri[i + 2];
                Vector3 fn = Vector3.Cross(v[b] - v[a], v[d] - v[a]);
                acc[a] += fn; acc[b] += fn; acc[d] += fn;
            }
            n.Clear();
            for (int i = 0; i < acc.Length; i++) n.Add(SalonMath.SafeNormalize(acc[i], Vector3.up));
        }

        /// <summary>Lisse les normales des sommets superposés (coutures).</summary>
        public void WeldNormals(float eps = 1e-5f)
        {
            var map = new Dictionary<long, Vector3>();
            System.Func<Vector3, long> key = p => ((long)Mathf.RoundToInt(p.x / eps) * 73856093L) ^ ((long)Mathf.RoundToInt(p.y / eps) * 19349663L) ^ ((long)Mathf.RoundToInt(p.z / eps) * 83492791L);
            for (int i = 0; i < v.Count; i++)
            {
                long k = key(v[i]);
                Vector3 s;
                map[k] = map.TryGetValue(k, out s) ? s + n[i] : n[i];
            }
            for (int i = 0; i < v.Count; i++) n[i] = SalonMath.SafeNormalize(map[key(v[i])], n[i]);
        }

        /// <summary>Ajoute un autre maillage transformé (rotation + échelle + translation).</summary>
        public void Append(MeshData o, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            int start = v.Count;
            for (int i = 0; i < o.v.Count; i++)
            {
                v.Add(pos + rot * Vector3.Scale(o.v[i], scale));
                uv.Add(i < o.uv.Count ? o.uv[i] : Vector2.zero);
                if (o.n.Count == o.v.Count)
                {
                    Vector3 nn = new Vector3(o.n[i].x / scale.x, o.n[i].y / scale.y, o.n[i].z / scale.z);
                    n.Add((rot * nn).normalized);
                }
                if (o.c.Count == o.v.Count) c.Add(o.c[i]);
            }
            for (int i = 0; i < o.tri.Count; i++) tri.Add(o.tri[i] + start);
        }

        public static MeshData Sphere(float r, int lat, int lon)
        {
            var m = new MeshData { name = "Sphere" };
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    float ph = 2 * Mathf.PI * j / lon;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
                    m.Add(d * r, new Vector2((float)j / lon, 1f - (float)i / lat));
                    m.n.Add(d);
                }
            }
            m.Grid(0, lat + 1, lon + 1, false, true);
            return m;
        }

        /// <summary>Tore (axe Y).</summary>
        public static MeshData Torus(float R, float r, int seg, int side)
        {
            var m = new MeshData { name = "Torus" };
            for (int i = 0; i <= seg; i++)
            {
                float a = 2 * Mathf.PI * i / seg;
                var center = new Vector3(Mathf.Sin(a) * R, 0, Mathf.Cos(a) * R);
                var outDir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                for (int j = 0; j <= side; j++)
                {
                    float b = 2 * Mathf.PI * j / side;
                    var nn = outDir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    m.Add(center + nn * r, new Vector2((float)i / seg, (float)j / side));
                    m.n.Add(nn);
                }
            }
            m.Grid(0, seg + 1, side + 1, false, true);
            return m;
        }
    }
}
