using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SalonCoiffure
{
    /// <summary>Les mèches coupées tombent et restent au sol.</summary>
    public class HairClippings : MonoBehaviour
    {
        class Piece
        {
            public GameObject go;
            public Mesh mesh;
            public Vector3 vel;
            public float minY;
            public bool landed;
        }

        public float floorY = 0.002f;
        public int maxPieces = 160;
        public readonly List<Vector3> TempPoints = new List<Vector3>(32);

        readonly List<Piece> pieces = new List<Piece>();
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector4> t = new List<Vector4>();
        readonly List<Color> c = new List<Color>();
        readonly List<Vector2> u0 = new List<Vector2>();
        readonly List<Vector2> u1 = new List<Vector2>();
        readonly List<int> idx = new List<int>();
        bool building;
        bool linear;

        void Awake() { linear = QualitySettings.activeColorSpace == ColorSpace.Linear; }

        public void BeginPiece()
        {
            building = true;
            v.Clear(); n.Clear(); t.Clear(); c.Clear(); u0.Clear(); u1.Clear(); idx.Clear();
        }

        public void AddStrip(List<Vector3> pts, Color color, float halfWidth, float wet)
        {
            if (!building || pts.Count < 2) return;
            Color lc = linear ? color.linear : color;
            lc.a = wet;
            int start = v.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 T = SalonUtil.SafeNormalize(pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)], Vector3.down);
                Vector3 side = SalonUtil.SafeNormalize(Vector3.Cross(T, Vector3.up), Vector3.right);
                Vector3 nn = SalonUtil.SafeNormalize(Vector3.Cross(side, T), Vector3.up);
                v.Add(pts[i] - side * halfWidth); v.Add(pts[i] + side * halfWidth);
                n.Add(nn); n.Add(nn);
                var t4 = new Vector4(T.x, T.y, T.z, 0f); // w = 0 : pas de balancement
                t.Add(t4); t.Add(t4);
                c.Add(lc); c.Add(lc);
                u0.Add(new Vector2(0, 0.1f)); u0.Add(new Vector2(1, 0.1f));
                u1.Add(new Vector2(0.85f, 0)); u1.Add(new Vector2(0.85f, 0));
                if (i < pts.Count - 1)
                {
                    int a = start + i * 2;
                    idx.Add(a); idx.Add(a + 1); idx.Add(a + 2);
                    idx.Add(a + 2); idx.Add(a + 1); idx.Add(a + 3);
                }
            }
        }

        public void EndPiece(Material mat)
        {
            building = false;
            if (v.Count == 0) return;
            // centrer sur le point le plus bas
            Vector3 center = Vector3.zero;
            float minY = float.MaxValue;
            for (int i = 0; i < v.Count; i++) { center += v[i]; if (v[i].y < minY) minY = v[i].y; }
            center /= v.Count;
            for (int i = 0; i < v.Count; i++) v[i] -= center;

            var mesh = new Mesh { name = "MecheCoupee" };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTangents(t);
            mesh.SetColors(c);
            mesh.SetUVs(0, u0);
            mesh.SetUVs(1, u1);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();

            var go = new GameObject("Coupe");
            go.transform.SetParent(transform, false);
            go.transform.position = center;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            var p = new Piece
            {
                go = go, mesh = mesh, minY = minY - center.y,
                vel = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.1f, 0.05f), Random.Range(-0.15f, 0.15f))
            };
            pieces.Add(p);
            while (pieces.Count > maxPieces)
            {
                Destroy(pieces[0].go);
                Destroy(pieces[0].mesh);
                pieces.RemoveAt(0);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p.landed) continue;
                p.vel.y = Mathf.Max(p.vel.y - 3.5f * dt, -1.1f);
                p.vel.x *= 1f - dt * 0.8f;
                p.vel.z *= 1f - dt * 0.8f;
                var pos = p.go.transform.position + p.vel * dt;
                if (pos.y + p.minY <= floorY)
                {
                    pos.y = floorY - p.minY;
                    p.landed = true;
                }
                p.go.transform.position = pos;
            }
        }

        /// <summary>Coup de balai.</summary>
        public void Sweep()
        {
            foreach (var p in pieces) { Destroy(p.go); Destroy(p.mesh); }
            pieces.Clear();
        }
    }
}
