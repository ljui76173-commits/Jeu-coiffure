using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SalonCoiffure
{
    /// <summary>Maillages générés par le code.</summary>
    public static class ProcMesh
    {
        /// <summary>
        /// Ellipsoïde sans couture. <paramref name="modify"/> déforme les sommets,
        /// <paramref name="keep"/> filtre les triangles d'après la direction (sphère unité) de leur centre.
        /// </summary>
        public static Mesh Ellipsoid(Vector3 radii, int lat, int lon, Func<Vector3, Vector3> modify = null, Func<Vector3, bool> keep = null)
        {
            var dirs = new List<Vector3>();
            dirs.Add(Vector3.up);
            for (int i = 1; i < lat; i++)
            {
                float th = Mathf.PI * i / lat;
                float y = Mathf.Cos(th), r = Mathf.Sin(th);
                for (int j = 0; j < lon; j++)
                {
                    float ph = 2f * Mathf.PI * j / lon;
                    dirs.Add(new Vector3(r * Mathf.Sin(ph), y, r * Mathf.Cos(ph)));
                }
            }
            dirs.Add(Vector3.down);
            int bottom = dirs.Count - 1;

            var verts = new Vector3[dirs.Count];
            for (int i = 0; i < dirs.Count; i++)
            {
                Vector3 p = Vector3.Scale(dirs[i], radii);
                verts[i] = modify != null ? modify(p) : p;
            }

            var tris = new List<int>();
            Action<int, int, int> add = (a, b, c) =>
            {
                Vector3 dc = (dirs[a] + dirs[b] + dirs[c]).normalized;
                if (keep != null && !keep(dc)) return;
                Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                Vector3 centroid = (verts[a] + verts[b] + verts[c]) / 3f;
                if (Vector3.Dot(n, centroid) < 0) { tris.Add(a); tris.Add(c); tris.Add(b); }
                else { tris.Add(a); tris.Add(b); tris.Add(c); }
            };

            for (int j = 0; j < lon; j++) add(0, 1 + j, 1 + (j + 1) % lon);
            for (int k = 0; k < lat - 2; k++)
            {
                int r0 = 1 + k * lon, r1 = 1 + (k + 1) * lon;
                for (int j = 0; j < lon; j++)
                {
                    int jn = (j + 1) % lon;
                    add(r0 + j, r1 + j, r0 + jn);
                    add(r0 + jn, r1 + j, r1 + jn);
                }
            }
            int last = 1 + (lat - 2) * lon;
            for (int j = 0; j < lon; j++) add(bottom, last + j, last + (j + 1) % lon);

            var m = new Mesh();
            if (verts.Length > 65000) m.indexFormat = IndexFormat.UInt32;
            m.vertices = verts;
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }

    /// <summary>Forme de la tête (espace local de la tête, visage vers +Z).</summary>
    public static class HeadShape
    {
        public static readonly Vector3 R = new Vector3(0.095f, 0.115f, 0.105f);

        /// <summary>Déformation : mâchoire et menton plus doux (look « mignon »).</summary>
        public static Vector3 Sculpt(Vector3 p)
        {
            float ny = p.y / R.y;
            if (ny < 0)
            {
                float k = Mathf.Pow(-ny, 1.6f);
                p.x *= Mathf.Lerp(1f, 0.72f, k);
                p.z *= Mathf.Lerp(1f, 0.86f, k);
            }
            return p;
        }

        static void Factors(float y, out float kx, out float kz)
        {
            float ny = y / R.y;
            kx = 1; kz = 1;
            if (ny < 0)
            {
                float k = Mathf.Pow(-ny, 1.6f);
                kx = Mathf.Lerp(1f, 0.72f, k);
                kz = Mathf.Lerp(1f, 0.86f, k);
            }
        }

        /// <summary>Point de la surface avant du visage pour (x, y), et sa normale approximative.</summary>
        public static Vector3 FrontSurface(float x, float y, out Vector3 normal)
        {
            float kx, kz;
            Factors(y, out kx, out kz);
            float rx = R.x * kx, rz = R.z * kz;
            float s = 1f - (x * x) / (rx * rx) - (y * y) / (R.y * R.y);
            float z = rz * Mathf.Sqrt(Mathf.Max(0.0001f, s));
            normal = new Vector3(x / (rx * rx), y / (R.y * R.y), z / (rz * rz)).normalized;
            return new Vector3(x, y, z);
        }
    }
}
