using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SalonCoiffure
{
    public enum HairZone { Devant = 0, Cotes = 1, Dessus = 2, Nuque = 3 }

    public struct HairConfig
    {
        public Color natural;   // couleur naturelle (sRGB)
        public float length;    // longueur des mèches en mètres
        public float waviness;  // 0 = raides, 1 = ondulés
        public int seed;
    }

    /// <summary>
    /// Chevelure par mèches (rubans fins), avec longueur, humidité, mousse, soin et coloration
    /// gérés mèche par mèche. Toutes les positions sont dans l'espace local de la tête (visage vers +Z).
    /// </summary>
    public class HairSystem : MonoBehaviour
    {
        public static readonly string[] ZoneNames = { "Devant", "Côtés", "Dessus", "Nuque" };

        static Vector3 R { get { return HeadShape.R; } }
        static readonly Vector3 TorsoC = new Vector3(0f, -0.40f, -0.01f);
        static readonly Vector3 TorsoR = new Vector3(0.21f, 0.16f, 0.125f);

        int N, P;
        Vector3[] rootDir;
        byte[] zone;
        float[] maxLen, len, origLen, segLen, wet, foam, care, layer, phase, widthK;
        bool[] dirty;
        bool anyDirty;
        Vector3[] dryPath, wetPath, waveOff, upright, cur;
        Color[] baseCol, pasteCol;
        float[] paste;

        Mesh mesh;
        Vector3[] mv, mn;
        Vector4[] mt;
        Color[] mc;
        Vector2[] uv0, uv1;
        float rootW, tipW, waviness;
        bool linear;

        public Material Material { get; private set; }
        public int StrandCount { get { return N; } }
        public float RootWidth { get { return rootW; } }

        // ------------------------------------------------------------------ Création
        public void Setup(HairConfig cfg, int strands, int segments, Material mat)
        {
            linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Material = mat;
            P = Mathf.Max(4, segments + 1);
            waviness = cfg.waviness;
            var rng = new System.Random(cfg.seed);
            var dirs = GenerateRoots(strands, rng);
            N = dirs.Count;

            rootDir = dirs.ToArray();
            zone = new byte[N];
            maxLen = new float[N]; len = new float[N]; origLen = new float[N]; segLen = new float[N];
            wet = new float[N]; foam = new float[N]; care = new float[N];
            layer = new float[N]; phase = new float[N]; widthK = new float[N];
            dirty = new bool[N];
            int np = N * P;
            dryPath = new Vector3[np]; wetPath = new Vector3[np]; waveOff = new Vector3[np];
            upright = new Vector3[np]; cur = new Vector3[np];
            baseCol = new Color[np]; pasteCol = new Color[np]; paste = new float[np];

            rootW = Mathf.Clamp(0.0085f * Mathf.Sqrt(2600f / N), 0.005f, 0.016f);
            tipW = rootW * 0.35f;

            for (int s = 0; s < N; s++)
            {
                Vector3 d = rootDir[s];
                zone[s] = (byte)ZoneOf(d);
                layer[s] = (float)rng.NextDouble();
                phase[s] = (float)rng.NextDouble() * 6.2831f;
                widthK[s] = 0.75f + 0.5f * (float)rng.NextDouble();
                float L = cfg.length * (zone[s] == (byte)HairZone.Dessus ? 1.15f : 1f) * (0.93f + 0.14f * (float)rng.NextDouble());
                maxLen[s] = L; len[s] = L; origLen[s] = L; segLen[s] = L / (P - 1);

                Color c = Jitter(cfg.natural, rng);
                int b = s * P;
                for (int j = 0; j < P; j++)
                {
                    float t = (float)j / (P - 1);
                    Color tipC = new Color(c.r * 1.18f + 0.025f, c.g * 1.16f + 0.02f, c.b * 1.12f + 0.012f, 1);
                    baseCol[b + j] = Color.Lerp(c, tipC, Mathf.Pow(t, 1.5f) * 0.55f);
                }
            }

            ComputeAllPaths(Vector3.down);
            System.Array.Copy(dryPath, upright, np);
            BuildMesh();
            MarkAllDirty();
            Refresh();
        }

        static List<Vector3> GenerateRoots(int strands, System.Random rng)
        {
            var res = new List<Vector3>();
            int M = strands * 3;
            for (int k = 0; k < M; k++)
            {
                float y = 1f - 2f * (k + 0.5f) / M;
                float r = Mathf.Sqrt(Mathf.Max(0, 1 - y * y));
                float th = k * 2.3999632f;
                var d = new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r);
                d += new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.03f;
                d.Normalize();
                if (AcceptScalp(d)) res.Add(d);
            }
            if (res.Count > strands)
            {
                var thin = new List<Vector3>(strands);
                for (int i = 0; i < strands; i++) thin.Add(res[(int)((long)i * res.Count / strands)]);
                return thin;
            }
            return res;
        }

        /// <summary>Vrai si la direction (sphère unité) est dans la zone du cuir chevelu.</summary>
        public static bool AcceptScalp(Vector3 d)
        {
            float f = d.z;
            float h = f > 0 ? Mathf.Lerp(-0.15f, 0.5f, f) : Mathf.Lerp(-0.15f, -0.62f, -f);
            return d.y > h;
        }

        static HairZone ZoneOf(Vector3 d)
        {
            if (d.z > 0.45f && d.y > 0.25f) return HairZone.Devant;
            if (d.y > 0.6f) return HairZone.Dessus;
            if (d.z < -0.2f) return HairZone.Nuque;
            return HairZone.Cotes;
        }

        static Color Jitter(Color c, System.Random rng)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            h = Mathf.Repeat(h + ((float)rng.NextDouble() - 0.5f) * 0.02f, 1f);
            s = Mathf.Clamp01(s * (0.9f + 0.2f * (float)rng.NextDouble()));
            v = Mathf.Clamp01(v * (0.86f + 0.26f * (float)rng.NextDouble()));
            return Color.HSVToRGB(h, s, v);
        }

        // ------------------------------------------------------------------ Trajectoires
        static Vector3 EllNormal(Vector3 p)
        {
            return SalonUtil.SafeNormalize(new Vector3(p.x / (R.x * R.x), p.y / (R.y * R.y), p.z / (R.z * R.z)), Vector3.up);
        }

        static Vector3 Collide(Vector3 q, float off)
        {
            // tête
            Vector3 ri = new Vector3(R.x + off, R.y + off, R.z + off);
            Vector3 e = new Vector3(q.x / ri.x, q.y / ri.y, q.z / ri.z);
            float m = e.magnitude;
            if (m < 1f && m > 1e-5f) { e /= m; q = Vector3.Scale(e, ri); }
            // cou
            if (q.y < -0.04f && q.y > -0.34f)
            {
                Vector2 r = new Vector2(q.x, q.z + 0.01f);
                float rr = 0.052f + off;
                if (r.magnitude < rr)
                {
                    r = r.sqrMagnitude > 1e-8f ? r.normalized * rr : new Vector2(0, -rr);
                    q.x = r.x; q.z = r.y - 0.01f;
                }
            }
            // épaules / buste
            Vector3 tr = new Vector3(TorsoR.x + off, TorsoR.y + off, TorsoR.z + off);
            Vector3 tq = q - TorsoC;
            e = new Vector3(tq.x / tr.x, tq.y / tr.y, tq.z / tr.z);
            m = e.magnitude;
            if (m < 1f && m > 1e-5f) { e /= m; q = TorsoC + Vector3.Scale(e, tr); }
            return q;
        }

        Vector3 InitialDir(int s, Vector3 n)
        {
            Vector3 d = rootDir[s];
            float sx = d.x >= 0 ? 1f : -1f;
            float front = Mathf.Clamp01(d.z);
            Vector3 a = new Vector3(sx * (0.35f + 0.9f * front * Mathf.Clamp01(d.y + 0.2f)), -1f, -0.25f * front);
            a -= n * Vector3.Dot(a, n);
            a = SalonUtil.SafeNormalize(a, Vector3.down);
            return (n * 0.45f + a).normalized;
        }

        float OffDry(int s) { return 0.0035f + layer[s] * 0.017f; }
        float OffWet(int s) { return 0.002f + layer[s] * 0.0045f; }

        void ComputePath(int s, Vector3 g, float gStep, float off, Vector3[] outArr)
        {
            int b = s * P;
            Vector3 root = Vector3.Scale(rootDir[s], R);
            Vector3 n = EllNormal(root);
            Vector3 dir = InitialDir(s, n);
            Vector3 p = root + n * (off * 0.35f);
            outArr[b] = p;
            float sl = segLen[s];
            float gs = gStep * sl / 0.025f;
            for (int j = 1; j < P; j++)
            {
                dir = (dir + g * gs).normalized;
                Vector3 q = Collide(p + dir * sl, off);
                Vector3 dq = q - p;
                float mm = dq.magnitude;
                if (mm > 1e-6f) dir = dq / mm;
                q = Collide(p + dir * sl, off);
                outArr[b + j] = q;
                p = q;
            }
        }

        void ComputeWave(int s)
        {
            int b = s * P;
            float sl = segLen[s];
            for (int j = 0; j < P; j++)
            {
                if (waviness <= 0.001f) { waveOff[b + j] = Vector3.zero; continue; }
                Vector3 a = dryPath[b + Mathf.Max(0, j - 1)], c = dryPath[b + Mathf.Min(P - 1, j + 1)];
                Vector3 T = SalonUtil.SafeNormalize(c - a, Vector3.down);
                Vector3 n = EllNormal(dryPath[b + j]);
                Vector3 side = SalonUtil.SafeNormalize(Vector3.Cross(T, n), Vector3.right);
                float along = j * sl;
                float ramp = Mathf.SmoothStep(0f, 1f, along / 0.08f);
                float ang = along * (6.2831f / 0.075f) + phase[s];
                waveOff[b + j] = (side * Mathf.Sin(ang) * 0.011f + n * Mathf.Cos(ang) * 0.004f) * waviness * ramp;
            }
        }

        void ComputeAllPaths(Vector3 gLocal)
        {
            for (int s = 0; s < N; s++)
            {
                ComputePath(s, gLocal, 0.16f, OffDry(s), dryPath);
                ComputePath(s, gLocal, 0.34f, OffWet(s), wetPath);
                ComputeWave(s);
            }
        }

        /// <summary>À appeler quand la tête change d'orientation (fauteuil ↔ bac).</summary>
        public void UpdateGravity()
        {
            Vector3 g = transform.InverseTransformDirection(Vector3.down).normalized;
            ComputeAllPaths(g);
            MarkAllDirty();
        }

        // ------------------------------------------------------------------ Maillage
        void BuildMesh()
        {
            int nv = N * P * 2;
            mv = new Vector3[nv]; mn = new Vector3[nv]; mt = new Vector4[nv]; mc = new Color[nv];
            uv0 = new Vector2[nv]; uv1 = new Vector2[nv];
            var tris = new int[N * (P - 1) * 6];
            int ti = 0;
            for (int s = 0; s < N; s++)
            {
                float ao = Mathf.Lerp(0.5f, 1f, layer[s]);
                for (int j = 0; j < P; j++)
                {
                    int vi = (s * P + j) * 2;
                    uv1[vi] = new Vector2(ao, phase[s]);
                    uv1[vi + 1] = uv1[vi];
                    if (j < P - 1)
                    {
                        tris[ti++] = vi; tris[ti++] = vi + 1; tris[ti++] = vi + 2;
                        tris[ti++] = vi + 2; tris[ti++] = vi + 1; tris[ti++] = vi + 3;
                    }
                }
            }
            mesh = new Mesh { name = "Chevelure" };
            if (nv > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.MarkDynamic();
            mesh.vertices = mv;
            mesh.triangles = tris;
            mesh.SetUVs(1, new List<Vector2>(uv1));
            mesh.bounds = new Bounds(new Vector3(0, -0.15f, 0), new Vector3(0.8f, 0.9f, 0.8f));

            var mf = GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = GetComponent<MeshRenderer>();
            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void MarkAllDirty()
        {
            for (int s = 0; s < N; s++) dirty[s] = true;
            anyDirty = true;
        }

        void MarkDirty(int s) { dirty[s] = true; anyDirty = true; }

        Color DisplayColor(int p, float strandFoam)
        {
            Color c = baseCol[p];
            if (paste[p] > 0.001f)
            {
                Color pc = Color.Lerp(pasteCol[p], new Color(0.97f, 0.95f, 0.9f), 0.25f);
                c = Color.Lerp(c, pc, Mathf.Min(1f, paste[p] * 1.2f));
            }
            if (strandFoam > 0.001f) c = Color.Lerp(c, new Color(0.98f, 0.98f, 1f), strandFoam * 0.65f);
            return c;
        }

        void UpdateStrand(int s)
        {
            int b = s * P;
            float w = wet[s], sl = segLen[s], L = len[s];
            float wv = 1f - 0.6f * w;
            for (int j = 0; j < P; j++)
                cur[b + j] = Vector3.Lerp(dryPath[b + j], wetPath[b + j], w) + waveOff[b + j] * wv;

            int last = Mathf.Min(P - 1, (int)(L / sl));
            float frac = L / sl - last;
            Vector3 tip = last < P - 1 ? Vector3.Lerp(cur[b + last], cur[b + last + 1], frac) : cur[b + P - 1];
            for (int j = last + 1; j < P; j++) cur[b + j] = tip;

            Vector3 prevT = Vector3.down, prevSide = Vector3.right;
            float fm = foam[s];
            for (int j = 0; j < P; j++)
            {
                Vector3 p = cur[b + j];
                Vector3 T = SalonUtil.SafeNormalize(cur[b + Mathf.Min(P - 1, j + 1)] - cur[b + Mathf.Max(0, j - 1)], prevT);
                prevT = T;
                Vector3 n = EllNormal(p);
                Vector3 side = SalonUtil.SafeNormalize(Vector3.Cross(T, n), prevSide);
                prevSide = side;

                float hw;
                if (j <= last) hw = Mathf.Lerp(rootW, tipW, j * sl / maxLen[s]);
                else if (j == last + 1) hw = Mathf.Lerp(rootW, tipW, L / maxLen[s]);
                else hw = 0f;
                hw *= 0.5f * widthK[s];
                if (j == 0) hw *= 0.7f;

                Color c = DisplayColor(b + j, fm);
                if (linear) c = c.linear;
                c.a = w;

                float along = Mathf.Min(j * sl, L);
                int vi = (b + j) * 2;
                mv[vi] = p - side * hw; mv[vi + 1] = p + side * hw;
                mn[vi] = n; mn[vi + 1] = n;
                var t4 = new Vector4(T.x, T.y, T.z, 1f);
                mt[vi] = t4; mt[vi + 1] = t4;
                mc[vi] = c; mc[vi + 1] = c;
                uv0[vi] = new Vector2(0, along); uv0[vi + 1] = new Vector2(1, along);
            }
        }

        public void Refresh()
        {
            if (!anyDirty || mesh == null) return;
            for (int s = 0; s < N; s++)
            {
                if (!dirty[s]) continue;
                UpdateStrand(s);
                dirty[s] = false;
            }
            mesh.SetVertices(mv);
            mesh.SetNormals(mn);
            mesh.SetTangents(mt);
            mesh.SetColors(mc);
            mesh.SetUVs(0, uv0);
            anyDirty = false;
        }

        void LateUpdate() { Refresh(); }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        // ------------------------------------------------------------------ Pinceau / rayon
        struct Brush { public Vector3 o, d; public float r2, limit; }

        static float RayEllipsoid(Vector3 o, Vector3 d, Vector3 c, Vector3 r)
        {
            Vector3 oo = new Vector3((o.x - c.x) / r.x, (o.y - c.y) / r.y, (o.z - c.z) / r.z);
            Vector3 dd = new Vector3(d.x / r.x, d.y / r.y, d.z / r.z);
            float a = Vector3.Dot(dd, dd), bb = 2f * Vector3.Dot(oo, dd), cc = Vector3.Dot(oo, oo) - 1f;
            float disc = bb * bb - 4f * a * cc;
            if (disc < 0) return float.PositiveInfinity;
            float sq = Mathf.Sqrt(disc);
            float t = (-bb - sq) / (2f * a);
            if (t < 0) t = (-bb + sq) / (2f * a);
            return t < 0 ? float.PositiveInfinity : t;
        }

        Brush MakeBrush(Ray ray, float radius)
        {
            Brush br;
            br.o = transform.InverseTransformPoint(ray.origin);
            br.d = transform.InverseTransformDirection(ray.direction).normalized;
            br.r2 = radius * radius;
            float t1 = RayEllipsoid(br.o, br.d, Vector3.zero, R + Vector3.one * 0.02f);
            float t2 = RayEllipsoid(br.o, br.d, TorsoC, TorsoR);
            br.limit = Mathf.Min(t1, t2) + 0.035f;
            return br;
        }

        static bool Hit(ref Brush br, Vector3 p)
        {
            Vector3 v = p - br.o;
            float t = Vector3.Dot(v, br.d);
            if (t < 0 || t > br.limit) return false;
            return v.sqrMagnitude - t * t < br.r2;
        }

        int LastIndex(int s) { return Mathf.Min(P - 1, (int)(len[s] / segLen[s]) + 1); }

        bool StrandHit(ref Brush br, int s)
        {
            int b = s * P, last = LastIndex(s);
            for (int j = 0; j <= last; j++) if (Hit(ref br, cur[b + j])) return true;
            return false;
        }

        /// <summary>Point de cheveu touché le plus proche de la caméra (monde).</summary>
        public bool Probe(Ray ray, float radius, out Vector3 world)
        {
            var br = MakeBrush(ray, radius);
            float best = float.PositiveInfinity;
            Vector3 bp = Vector3.zero;
            for (int s = 0; s < N; s++)
            {
                int b = s * P, last = LastIndex(s);
                for (int j = 0; j <= last; j++)
                {
                    Vector3 p = cur[b + j];
                    if (!Hit(ref br, p)) continue;
                    float t = Vector3.Dot(p - br.o, br.d);
                    if (t < best) { best = t; bp = p; }
                }
            }
            world = transform.TransformPoint(bp);
            return best < float.PositiveInfinity;
        }

        Vector3 PointAt(int s, float dist)
        {
            int b = s * P;
            float f = dist / segLen[s];
            int j = Mathf.Clamp((int)f, 0, P - 1);
            if (j >= P - 1) return cur[b + P - 1];
            return Vector3.Lerp(cur[b + j], cur[b + j + 1], f - j);
        }

        void EmitPiece(int s, float from, float to, HairClippings clips)
        {
            if (clips == null) return;
            var pts = clips.TempPoints;
            pts.Clear();
            pts.Add(transform.TransformPoint(PointAt(s, from)));
            int b = s * P;
            for (int j = 1; j < P; j++)
            {
                float d = j * segLen[s];
                if (d > from && d < to) pts.Add(transform.TransformPoint(cur[b + j]));
            }
            pts.Add(transform.TransformPoint(PointAt(s, to)));
            int mid = b + Mathf.Clamp((int)((from + to) * 0.5f / segLen[s]), 0, P - 1);
            Color c = DisplayColor(mid, foam[s]);
            clips.AddStrip(pts, c, rootW * 0.45f * widthK[s], wet[s]);
        }

        // ------------------------------------------------------------------ Outils
        /// <summary>Ciseaux : coupe chaque mèche à l'endroit touché.</summary>
        public int Cut(Ray ray, float radius, int zoneFilter, HairClippings clips, float minLen = 0.01f)
        {
            var br = MakeBrush(ray, radius);
            int count = 0;
            if (clips != null) clips.BeginPiece();
            for (int s = 0; s < N; s++)
            {
                if (zoneFilter >= 0 && zone[s] != zoneFilter) continue;
                int b = s * P, last = LastIndex(s);
                float sl = segLen[s];
                for (int j = 1; j <= last; j++)
                {
                    if (!Hit(ref br, cur[b + j])) continue;
                    float nl = Mathf.Min(len[s], Mathf.Max(minLen, j * sl - sl * 0.5f));
                    if (len[s] - nl > 0.004f)
                    {
                        EmitPiece(s, nl, len[s], clips);
                        len[s] = nl;
                        MarkDirty(s);
                        count++;
                    }
                    break;
                }
            }
            if (clips != null) clips.EndPiece(Material);
            return count;
        }

        /// <summary>Tondeuse : ramène les mèches touchées à la hauteur du sabot.</summary>
        public int Clip(Ray ray, float radius, int zoneFilter, float sabot, HairClippings clips)
        {
            var br = MakeBrush(ray, radius);
            int count = 0;
            if (clips != null) clips.BeginPiece();
            for (int s = 0; s < N; s++)
            {
                if (zoneFilter >= 0 && zone[s] != zoneFilter) continue;
                if (len[s] <= sabot + 0.002f) continue;
                int b = s * P;
                if (!Hit(ref br, cur[b]) && !Hit(ref br, cur[b + 1])) continue;
                EmitPiece(s, sabot, len[s], clips);
                len[s] = sabot;
                MarkDirty(s);
                count++;
            }
            if (clips != null) clips.EndPiece(Material);
            return count;
        }

        /// <summary>Douchette : mouille, rince la mousse et révèle la couleur de la coloration.</summary>
        public int Water(Ray ray, float radius, float amount, float develop)
        {
            var br = MakeBrush(ray, radius);
            int count = 0;
            for (int s = 0; s < N; s++)
            {
                if (!StrandHit(ref br, s)) continue;
                wet[s] = Mathf.Min(1f, wet[s] + amount);
                foam[s] = Mathf.Max(0f, foam[s] - amount * 0.8f);
                int b = s * P;
                for (int j = 0; j < P; j++)
                {
                    int p = b + j;
                    if (paste[p] <= 0f) continue;
                    Color res = Color.Lerp(baseCol[p], pasteCol[p], 0.88f);
                    baseCol[p] = Color.Lerp(baseCol[p], res, Mathf.Clamp01(paste[p]) * develop);
                    paste[p] = 0f;
                }
                MarkDirty(s);
                count++;
            }
            return count;
        }

        /// <summary>Shampoing : fait mousser (cheveux mouillés seulement). Renvoie -1 si trop secs.</summary>
        public int Lather(Ray ray, float radius, float amount, List<Vector3> hitWorld)
        {
            var br = MakeBrush(ray, radius);
            int count = 0, tooDry = 0;
            for (int s = 0; s < N; s++)
            {
                if (!StrandHit(ref br, s)) continue;
                if (wet[s] < 0.35f) { tooDry++; continue; }
                foam[s] = Mathf.Min(1f, foam[s] + amount);
                MarkDirty(s);
                count++;
                if (hitWorld != null && hitWorld.Count < 6 && (s % 7) == 0)
                    hitWorld.Add(transform.TransformPoint(cur[s * P + Mathf.Min(2, P - 1)]));
            }
            return count == 0 && tooDry > 0 ? -1 : count;
        }

        public int Condition(Ray ray, float radius, float amount)
        {
            var br = MakeBrush(ray, radius);
            int count = 0, tooDry = 0;
            for (int s = 0; s < N; s++)
            {
                if (!StrandHit(ref br, s)) continue;
                if (wet[s] < 0.35f) { tooDry++; continue; }
                care[s] = Mathf.Min(1f, care[s] + amount);
                count++;
            }
            return count == 0 && tooDry > 0 ? -1 : count;
        }

        /// <summary>Serviette / sèche-cheveux : retire l'humidité jusqu'à minWet.</summary>
        public int Dry(Ray ray, float radius, float amount, float minWet)
        {
            var br = MakeBrush(ray, radius);
            int count = 0;
            for (int s = 0; s < N; s++)
            {
                if (wet[s] <= minWet || !StrandHit(ref br, s)) continue;
                wet[s] = Mathf.Max(minWet, wet[s] - amount);
                MarkDirty(s);
                count++;
            }
            return count;
        }

        /// <summary>Pinceau de coloration : dépose la pâte sur les points touchés.</summary>
        public int Paint(Ray ray, float radius, int zoneFilter, Color dye, float amount)
        {
            var br = MakeBrush(ray, radius);
            int count = 0;
            for (int s = 0; s < N; s++)
            {
                if (zoneFilter >= 0 && zone[s] != zoneFilter) continue;
                int b = s * P, last = LastIndex(s);
                bool any = false;
                for (int j = 0; j <= last; j++)
                {
                    if (!Hit(ref br, cur[b + j])) continue;
                    AddPaste(b + j, dye, amount);
                    if (j > 0) AddPaste(b + j - 1, dye, amount * 0.5f);
                    if (j < P - 1) AddPaste(b + j + 1, dye, amount * 0.5f);
                    any = true;
                }
                if (any) { MarkDirty(s); count++; }
            }
            return count;
        }

        void AddPaste(int p, Color dye, float amount)
        {
            float before = paste[p];
            float after = Mathf.Min(1f, before + amount);
            if (after <= before) return;
            pasteCol[p] = before <= 0.001f ? dye : Color.Lerp(pasteCol[p], dye, (after - before) / after);
            paste[p] = after;
        }

        // ------------------------------------------------------------------ Mesures
        public HairZone Zone(int s) { return (HairZone)zone[s]; }
        public float Length(int s) { return len[s]; }
        public float OriginalLength(int s) { return origLen[s]; }

        /// <summary>Hauteur de la pointe (tête droite, cheveux secs), en mètres par rapport au centre de la tête.</summary>
        public float TipHeight(int s)
        {
            int b = s * P;
            float f = len[s] / segLen[s];
            int j = Mathf.Clamp((int)f, 0, P - 1);
            if (j >= P - 1) return upright[b + P - 1].y;
            return Mathf.Lerp(upright[b + j].y, upright[b + j + 1].y, f - j);
        }

        /// <summary>Couleur moyenne (sRGB) de la partie visible de la mèche.</summary>
        public Color StrandColor(int s)
        {
            int b = s * P, last = Mathf.Max(0, LastIndex(s) - 1);
            Color sum = Color.black;
            for (int j = 0; j <= last; j++) sum += baseCol[b + j];
            return sum / (last + 1);
        }

        public Color AverageRootColor()
        {
            Color sum = Color.black;
            for (int s = 0; s < N; s++) sum += baseCol[s * P];
            return N > 0 ? sum / N : Color.black;
        }

        public float AverageWet() { return Avg(wet); }
        public float AverageFoam() { return Avg(foam); }
        public float AverageCare() { return Avg(care); }

        float Avg(float[] a)
        {
            if (N == 0) return 0;
            float t = 0;
            for (int i = 0; i < N; i++) t += a[i];
            return t / N;
        }

        /// <summary>Part des points visibles recouverts de pâte de coloration.</summary>
        public float PasteCoverage()
        {
            int tot = 0, cov = 0;
            for (int s = 0; s < N; s++)
            {
                int b = s * P, last = LastIndex(s);
                for (int j = 0; j <= last; j++)
                {
                    tot++;
                    if (paste[b + j] > 0.3f) cov++;
                }
            }
            return tot > 0 ? (float)cov / tot : 0;
        }

        public bool HasPaste()
        {
            for (int i = 0; i < paste.Length; i++) if (paste[i] > 0.01f) return true;
            return false;
        }

        /// <summary>Vent / souffle : direction monde et amplitude (balancement des mèches).</summary>
        public void SetWind(Vector3 dir, float amplitude, float speed)
        {
            if (Material == null) return;
            Material.SetVector("_HairWind", new Vector4(dir.x, dir.y, dir.z, amplitude));
            Material.SetVector("_HairWindParams", new Vector4(speed, 0, 0, 0));
        }
    }
}
