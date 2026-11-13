using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    public enum HairZone { Devant = 0, Cotes = 1, Dessus = 2, Nuque = 3 }

    public struct HairConfig
    {
        public Color natural;   // couleur naturelle (sRGB)
        public float length;    // longueur des mèches en mètres
        public float waviness;  // 0 = raides, 1 = ondulés
        public bool bangs;      // frange de départ
        public int seed;
    }

    /// <summary>Une mèche coupée (espace de la tête) à faire tomber.</summary>
    public struct CutPiece
    {
        public Vector3[] pts;
        public Color color;
        public float halfWidth, wet;
    }

    /// <summary>
    /// Cœur (pur C#) de la chevelure : mèches regroupées en « boucles » (clumps), physique simplifiée,
    /// longueur / humidité / mousse / soin / coloration par mèche, et génération des sommets.
    /// Espace : celui de la tête (visage vers +Z), mètres.
    /// </summary>
    public class HairCore
    {
        public static readonly string[] ZoneNames = { "Devant", "Côtés", "Dessus", "Nuque" };
        static Vector3 R { get { return HeadGen.HairR; } }
        public static readonly Vector3 TorsoC = new Vector3(0f, -0.31f, -0.01f);
        public static readonly Vector3 TorsoR = new Vector3(0.225f, 0.17f, 0.135f);

        public int N, P;
        Vector3[] rootDir;
        byte[] zone;
        float[] maxLen, len, origLen, segLen, wet, foam, care, layer, phase, widthK, clumpK;
        int[] guide;
        bool[] dirty, bangStrand;
        bool anyDirty;
        Vector3[] dryPath, wetPath, waveOff, upright, cur;
        Color[] baseCol, pasteCol;
        float[] paste;
        float rootW, tipW, waviness;
        bool linear;

        // Tampons de sommets (2 par point)
        public Vector3[] mv, mn;
        public Vector4[] mt;
        public Color[] mc;
        public Vector2[] uv0, uv1, uv2;
        public int[] tris;

        public float RootWidth { get { return rootW; } }
        public int StrandCount { get { return N; } }

        // ------------------------------------------------------------------ Création
        public void Setup(HairConfig cfg, int strands, int segments, bool linearColor)
        {
            linear = linearColor;
            P = Mathf.Max(4, segments + 1 + Mathf.RoundToInt(cfg.waviness * segments * 0.8f));
            waviness = cfg.waviness;
            var rng = new System.Random(cfg.seed);
            var dirs = GenerateRoots(strands, rng);
            N = dirs.Count;

            rootDir = dirs.ToArray();
            zone = new byte[N];
            maxLen = new float[N]; len = new float[N]; origLen = new float[N]; segLen = new float[N];
            wet = new float[N]; foam = new float[N]; care = new float[N];
            layer = new float[N]; phase = new float[N]; widthK = new float[N]; clumpK = new float[N];
            guide = new int[N];
            dirty = new bool[N]; bangStrand = new bool[N];
            int np = N * P;
            dryPath = new Vector3[np]; wetPath = new Vector3[np]; waveOff = new Vector3[np];
            upright = new Vector3[np]; cur = new Vector3[np];
            baseCol = new Color[np]; pasteCol = new Color[np]; paste = new float[np];

            rootW = Mathf.Clamp(0.0135f * Mathf.Sqrt(2600f / N), 0.008f, 0.024f);
            tipW = rootW * 0.6f;

            // Mèches guides : une sur 10 environ ; chaque mèche suit la guide la plus proche
            var guides = new List<int>();
            for (int s = 0; s < N; s += 10) guides.Add(s);
            Color[] clumpTint = new Color[N];
            for (int s = 0; s < N; s++)
            {
                Vector3 d = rootDir[s];
                int best = guides[0]; float bd = 9;
                foreach (int gi in guides) { float dd = (rootDir[gi] - d).sqrMagnitude; if (dd < bd) { bd = dd; best = gi; } }
                guide[s] = best;
            }

            for (int s = 0; s < N; s++)
            {
                Vector3 d = rootDir[s];
                zone[s] = (byte)ZoneOf(d);
                layer[s] = (float)rng.NextDouble();
                phase[s] = (float)rng.NextDouble() * 6.2831f;
                widthK[s] = 0.75f + 0.5f * (float)rng.NextDouble();
                clumpK[s] = 0.55f + 0.4f * (float)rng.NextDouble();
                bangStrand[s] = cfg.bangs && d.z > 0.55f && d.y > 0.3f;
                float L = cfg.length * (zone[s] == (byte)HairZone.Dessus ? 1.15f : 1f) * (0.94f + 0.12f * (float)rng.NextDouble());
                maxLen[s] = L; segLen[s] = L / (P - 1);
                len[s] = bangStrand[s] ? Mathf.Min(L, 0.075f + 0.01f * (float)rng.NextDouble()) : L;
                origLen[s] = len[s];
            }
            // couleur : teinte par boucle + variation par mèche + pointes plus claires
            for (int s = 0; s < N; s++)
            {
                var crng = new System.Random(cfg.seed * 7919 + guide[s]);
                Color clump = Jitter(cfg.natural, crng, 0.10f);
                Color c = Jitter(clump, rng, 0.05f);
                int b = s * P;
                for (int j = 0; j < P; j++)
                {
                    float t = (float)j / (P - 1);
                    Color tipC = new Color(c.r * 1.12f + 0.02f, c.g * 1.12f + 0.018f, c.b * 1.1f + 0.012f, 1);
                    baseCol[b + j] = Color.Lerp(c, tipC, Mathf.Pow(t, 1.4f) * 0.5f);
                }
            }

            ComputeAllPaths(Vector3.down);
            System.Array.Copy(dryPath, upright, np);
            BuildBuffers();
            MarkAllDirty();
            UpdateDirty();
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
                d += new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.025f;
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
            float h = f > 0 ? Mathf.Lerp(-0.12f, 0.52f, f) : Mathf.Lerp(-0.12f, -0.6f, -f);
            // tempes / pattes : la ligne descend un peu devant les oreilles
            if (Mathf.Abs(d.x) > 0.75f && f > -0.1f && f < 0.35f) h -= 0.05f;
            return d.y > h;
        }

        static HairZone ZoneOf(Vector3 d)
        {
            if (d.z > 0.4f && d.y > 0.3f) return HairZone.Devant;
            if (d.y > 0.62f) return HairZone.Dessus;
            if (d.z < -0.2f) return HairZone.Nuque;
            return HairZone.Cotes;
        }

        static Color Jitter(Color c, System.Random rng, float amount)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            h = Mathf.Repeat(h + ((float)rng.NextDouble() - 0.5f) * amount * 0.2f, 1f);
            s = Mathf.Clamp01(s * (1f + ((float)rng.NextDouble() - 0.5f) * amount * 2f));
            v = Mathf.Clamp01(v * (1f + ((float)rng.NextDouble() - 0.5f) * amount * 2.6f));
            return Color.HSVToRGB(h, s, v);
        }

        // ------------------------------------------------------------------ Trajectoires
        public static Vector3 EllNormal(Vector3 p)
        {
            return SalonMath.SafeNormalize(new Vector3(p.x / (R.x * R.x), p.y / (R.y * R.y), p.z / (R.z * R.z)), Vector3.up);
        }

        static Vector3 Collide(Vector3 q, float off, bool bang, float side)
        {
            Vector3 ri = new Vector3(R.x + off, R.y + off, R.z + off);
            Vector3 e = new Vector3(q.x / ri.x, q.y / ri.y, q.z / ri.z);
            float m = e.magnitude;
            if (m < 1f && m > 1e-5f) { e /= m; q = Vector3.Scale(e, ri); }
            if (q.y < -0.04f && q.y > -0.17f)
            {
                Vector2 r = new Vector2(q.x, q.z + 0.01f);
                float rr = 0.068f + off;
                if (r.magnitude < rr)
                {
                    r = r.sqrMagnitude > 1e-8f ? r.normalized * rr : new Vector2(0, -rr);
                    q.x = r.x; q.z = r.y - 0.01f;
                }
            }
            // bouclier du visage : les mèches glissent autour des joues
            {
                Vector3 fc = new Vector3(0f, -0.035f, 0.03f);
                Vector3 fr = new Vector3(0.071f + off, 0.088f + off, 0.098f + off);
                Vector3 fq = q - fc;
                e = new Vector3(fq.x / fr.x, fq.y / fr.y, fq.z / fr.z);
                m = e.magnitude;
                if (m < 1f && m > 1e-5f) { e /= m; q = fc + Vector3.Scale(e, fr); }
            }
            // le visage reste dégagé : les mèches passent de chaque côté (sauf la frange au-dessus des sourcils)
            if (q.z > -0.01f && q.y < 0.05f && q.y > -0.24f && !(bang && q.y > 0.022f))
            {
                float w = q.y > 0f ? 0.074f : (q.y > -0.1f ? Mathf.Lerp(0.074f, 0.058f, -q.y / 0.1f) : 0.06f);
                w += off * 0.6f;
                if (Mathf.Abs(q.x) < w)
                {
                    float sg = side;
                    q.x = sg * w;
                }
            }
            // buste / épaules (même profil que la cape)
            float ba, bb;
            if (BodyGen.BodyAt(q.y, out ba, out bb))
            {
                ba += off; bb += off;
                float qx = q.x, qz = q.z + 0.01f;
                float rr2 = (qx / ba) * (qx / ba) + (qz / bb) * (qz / bb);
                if (rr2 < 1f)
                {
                    if (false) { }
                    else
                    {
                        float k = 1f / Mathf.Sqrt(Mathf.Max(rr2, 1e-6f));
                        q.x = qx * k; q.z = qz * k - 0.01f;
                    }
                }
            }
            return q;
        }

        Vector3 InitialDir(int s, Vector3 n)
        {
            Vector3 d = rootDir[s];
            if (bangStrand[s])
            {
                // frange : vers l'avant et le bas, légèrement écartée
                Vector3 fa = new Vector3(d.x * 0.6f, -0.9f, 0.5f);
                fa -= n * Vector3.Dot(fa, n);
                return (n * 0.5f + SalonMath.SafeNormalize(fa, Vector3.down)).normalized;
            }
            float sx = d.x >= 0.02f ? 1f : (d.x <= -0.02f ? -1f : (phase[s] > 3.14f ? 1f : -1f));
            float front = Mathf.Clamp01(d.z);
            float top = Mathf.Clamp01(d.y);
            // raie au milieu : on peigne vers le côté et vers l'arrière, jamais vers le visage
            Vector3 a = new Vector3(sx * (0.25f + 1.0f * front * Mathf.Clamp01(d.y + 0.3f)), -0.7f - 0.6f * (1f - top), -0.3f - 0.5f * front * top);
            a -= n * Vector3.Dot(a, n);
            if (a.z > -0.05f && d.z > -0.2f) a.z = -0.05f;
            a = SalonMath.SafeNormalize(a, Vector3.down);
            float lift = 0.12f + 0.14f * top; // volume au sommet
            Vector3 dir = (n * lift + a).normalized;
            if (dir.z > 0.1f) dir.z = 0.1f;
            return dir.normalized;
        }

        float OffDry(int s) { return 0.0022f + layer[s] * 0.0115f; }
        float OffWet(int s) { return 0.0018f + layer[s] * 0.0042f; }

        void ComputePath(int s, Vector3 g, float gStep, float off, Vector3[] outArr)
        {
            int b = s * P;
            Vector3 root = Vector3.Scale(rootDir[s], R);
            Vector3 n = EllNormal(root);
            Vector3 dir = InitialDir(s, n);
            bool bang = bangStrand[s];
            float side = rootDir[s].x >= 0.02f ? 1f : (rootDir[s].x <= -0.02f ? -1f : (phase[s] > 3.14f ? 1f : -1f));
            Vector3 p = root + n * (off * 0.3f);
            outArr[b] = p;
            float sl = segLen[s];
            float gs = gStep * sl / 0.025f;
            for (int j = 1; j < P; j++)
            {
                dir = (dir + g * gs).normalized;
                Vector3 q = Collide(p + dir * sl, off, bang, side);
                Vector3 dq = q - p;
                float mm = dq.magnitude;
                if (mm > 1e-6f) dir = dq / mm;
                q = Collide(p + dir * sl, off, bang, side);
                outArr[b + j] = q;
                p = q;
            }
        }

        /// <summary>Rapproche chaque mèche de sa guide (boucles naturelles), plus fort vers les pointes.</summary>
        void ApplyClumps(Vector3[] path, float strength)
        {
            var tmp = new Vector3[P];
            for (int s = 0; s < N; s++)
            {
                int gsi = guide[s];
                if (gsi == s) continue;
                int b = s * P, gb = gsi * P;
                for (int j = 0; j < P; j++)
                {
                    float t = (float)j / (P - 1);
                    float k = strength * clumpK[s] * SalonMath.Smooth01((t - 0.08f) / 0.7f);
                    tmp[j] = Vector3.Lerp(path[b + j], path[gb + j] + (path[b] - path[gb]) * (1f - t) * 0.6f, k);
                }
                for (int j = 0; j < P; j++) path[b + j] = tmp[j];
            }
        }

        void Recollide(int s, Vector3[] path, float off)
        {
            int b = s * P;
            float side = rootDir[s].x >= 0.02f ? 1f : (rootDir[s].x <= -0.02f ? -1f : (phase[s] > 3.14f ? 1f : -1f));
            for (int j = 1; j < P; j++) path[b + j] = Collide(path[b + j], off, bangStrand[s], side);
        }

        void ComputeWave(int s)
        {
            int b = s * P;
            float sl = segLen[s];
            float ph = phase[guide[s]] + phase[s] * 0.15f;
            for (int j = 0; j < P; j++)
            {
                if (waviness <= 0.001f) { waveOff[b + j] = Vector3.zero; continue; }
                Vector3 a = dryPath[b + Mathf.Max(0, j - 1)], c = dryPath[b + Mathf.Min(P - 1, j + 1)];
                Vector3 T = SalonMath.SafeNormalize(c - a, Vector3.down);
                Vector3 n = EllNormal(dryPath[b + j]);
                Vector3 side = SalonMath.SafeNormalize(Vector3.Cross(T, n), Vector3.right);
                float along = j * sl;
                float ramp = Mathf.SmoothStep(0f, 1f, along / 0.08f);
                float ang = along * (6.2831f / 0.11f) + ph;
                waveOff[b + j] = (side * Mathf.Sin(ang) * 0.011f + n * Mathf.Cos(ang) * 0.004f) * waviness * ramp;
            }
        }

        void ComputeAllPaths(Vector3 gLocal)
        {
            for (int s = 0; s < N; s++)
            {
                ComputePath(s, gLocal, 0.22f, OffDry(s), dryPath);
                ComputePath(s, gLocal, 0.34f, OffWet(s), wetPath);
            }
            ApplyClumps(dryPath, 0.55f);
            ApplyClumps(wetPath, 0.85f);
            for (int s = 0; s < N; s++) { Recollide(s, dryPath, OffDry(s)); Recollide(s, wetPath, OffWet(s)); }
            for (int s = 0; s < N; s++) ComputeWave(s);
        }

        /// <summary>Recalcule la retombée des cheveux pour une gravité donnée (espace tête).</summary>
        public void SetGravity(Vector3 gLocal)
        {
            ComputeAllPaths(gLocal.normalized);
            MarkAllDirty();
        }

        // ------------------------------------------------------------------ Sommets
        void BuildBuffers()
        {
            int nv = N * P * 2;
            mv = new Vector3[nv]; mn = new Vector3[nv]; mt = new Vector4[nv]; mc = new Color[nv];
            uv0 = new Vector2[nv]; uv1 = new Vector2[nv]; uv2 = new Vector2[nv];
            tris = new int[N * (P - 1) * 6];
            int ti = 0;
            for (int s = 0; s < N; s++)
            {
                float ao = Mathf.Lerp(0.45f, 1f, layer[s]);
                for (int j = 0; j < P; j++)
                {
                    int vi = (s * P + j) * 2;
                    uv1[vi] = new Vector2(ao, phase[guide[s]] + phase[s] * 0.2f);
                    uv1[vi + 1] = uv1[vi];
                    if (j < P - 1)
                    {
                        tris[ti++] = vi; tris[ti++] = vi + 1; tris[ti++] = vi + 2;
                        tris[ti++] = vi + 2; tris[ti++] = vi + 1; tris[ti++] = vi + 3;
                    }
                }
            }
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
                Vector3 T = SalonMath.SafeNormalize(cur[b + Mathf.Min(P - 1, j + 1)] - cur[b + Mathf.Max(0, j - 1)], prevT);
                prevT = T;
                Vector3 n = EllNormal(p);
                Vector3 side = SalonMath.SafeNormalize(Vector3.Cross(T, n), prevSide);
                prevSide = side;

                float hw;
                if (j <= last) hw = Mathf.Lerp(rootW, tipW, j * sl / maxLen[s]);
                else if (j == last + 1) hw = Mathf.Lerp(rootW, tipW, L / maxLen[s]);
                else hw = 0f;
                hw *= 0.5f * widthK[s];
                if (j == 0) hw *= 0.8f;

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
                float tipK = SalonMath.Smooth01(1f - (L - along) / 0.025f);
                uv2[vi] = new Vector2(tipK, 0); uv2[vi + 1] = uv2[vi];
            }
        }

        /// <summary>Met à jour les sommets des mèches modifiées. Renvoie vrai si quelque chose a changé.</summary>
        public bool UpdateDirty()
        {
            if (!anyDirty) return false;
            for (int s = 0; s < N; s++)
            {
                if (!dirty[s]) continue;
                UpdateStrand(s);
                dirty[s] = false;
            }
            anyDirty = false;
            return true;
        }

        // ------------------------------------------------------------------ Rayon (espace tête)
        public struct Brush { public Vector3 o, d; public float r2, limit; }

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

        public static Brush MakeBrush(Vector3 origin, Vector3 dir, float radius)
        {
            Brush br;
            br.o = origin;
            br.d = dir.normalized;
            br.r2 = radius * radius;
            float t1 = RayEllipsoid(br.o, br.d, Vector3.zero, R + Vector3.one * 0.02f);
            float t2 = RayEllipsoid(br.o, br.d, TorsoC, TorsoR);
            br.limit = Mathf.Min(t1, t2) + 0.035f;
            return br;
        }

        /// <summary>Le rayon touche-t-il la tête (ellipsoïde du crâne + marge) ?</summary>
        public static bool RayHitsHead(Vector3 origin, Vector3 dir, float margin)
        {
            return RayEllipsoid(origin, dir.normalized, Vector3.zero, R + Vector3.one * margin) < float.PositiveInfinity;
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

        /// <summary>Point de cheveu touché le plus proche (espace tête).</summary>
        public bool Probe(Brush br, out Vector3 local)
        {
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
            local = bp;
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

        void EmitPiece(int s, float from, float to, List<CutPiece> pieces)
        {
            if (pieces == null) return;
            var pts = new List<Vector3>();
            pts.Add(PointAt(s, from));
            int b = s * P;
            for (int j = 1; j < P; j++)
            {
                float d = j * segLen[s];
                if (d > from && d < to) pts.Add(cur[b + j]);
            }
            pts.Add(PointAt(s, to));
            int mid = b + Mathf.Clamp((int)((from + to) * 0.5f / segLen[s]), 0, P - 1);
            pieces.Add(new CutPiece { pts = pts.ToArray(), color = DisplayColor(mid, foam[s]), halfWidth = rootW * 0.4f * widthK[s], wet = wet[s] });
        }

        // ------------------------------------------------------------------ Outils
        public int Cut(Brush br, int zoneFilter, List<CutPiece> pieces, float minLen = 0.01f)
        {
            int count = 0;
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
                        EmitPiece(s, nl, len[s], pieces);
                        len[s] = nl;
                        MarkDirty(s);
                        count++;
                    }
                    break;
                }
            }
            return count;
        }

        /// <summary>Coupe toutes les mèches de la zone à une hauteur donnée (tête droite) : « égaliser ».</summary>
        public int CutToHeight(int zoneFilter, float height, List<CutPiece> pieces)
        {
            int count = 0;
            for (int s = 0; s < N; s++)
            {
                if (zoneFilter >= 0 && zone[s] != zoneFilter) continue;
                int b = s * P;
                float sl = segLen[s];
                // première distance où la mèche (droite, sèche) passe sous la hauteur
                for (int j = 1; j < P; j++)
                {
                    if (upright[b + j].y > height) continue;
                    float y0 = upright[b + j - 1].y, y1 = upright[b + j].y;
                    float f = y0 - y1 > 1e-6f ? (y0 - height) / (y0 - y1) : 0f;
                    float nl = Mathf.Max(0.01f, (j - 1 + f) * sl);
                    if (len[s] - nl > 0.004f)
                    {
                        EmitPiece(s, nl, len[s], pieces);
                        len[s] = nl;
                        MarkDirty(s);
                        count++;
                    }
                    break;
                }
            }
            return count;
        }

        public int Clip(Brush br, int zoneFilter, float sabot, List<CutPiece> pieces)
        {
            int count = 0;
            for (int s = 0; s < N; s++)
            {
                if (zoneFilter >= 0 && zone[s] != zoneFilter) continue;
                if (len[s] <= sabot + 0.002f) continue;
                int b = s * P;
                if (!Hit(ref br, cur[b]) && !Hit(ref br, cur[b + 1])) continue;
                EmitPiece(s, sabot, len[s], pieces);
                len[s] = sabot;
                MarkDirty(s);
                count++;
            }
            return count;
        }

        public int Water(Brush br, float amount, float develop)
        {
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

        public int Lather(Brush br, float amount, List<Vector3> hitLocal)
        {
            int count = 0, tooDry = 0;
            for (int s = 0; s < N; s++)
            {
                if (!StrandHit(ref br, s)) continue;
                if (wet[s] < 0.35f) { tooDry++; continue; }
                foam[s] = Mathf.Min(1f, foam[s] + amount);
                MarkDirty(s);
                count++;
                if (hitLocal != null && hitLocal.Count < 6 && (s % 7) == 0) hitLocal.Add(cur[s * P + Mathf.Min(2, P - 1)]);
            }
            return count == 0 && tooDry > 0 ? -1 : count;
        }

        public int Condition(Brush br, float amount)
        {
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

        public int Dry(Brush br, float amount, float minWet)
        {
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

        /// <summary>Mouille toute la chevelure (pour l'aperçu et les tests).</summary>
        public void SetAllWet(float w) { for (int s = 0; s < N; s++) wet[s] = w; MarkAllDirty(); }

        public int Paint(Brush br, int zoneFilter, Color dye, float amount)
        {
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
                    if (j > 0) AddPaste(b + j - 1, dye, amount * 0.6f);
                    if (j < P - 1) AddPaste(b + j + 1, dye, amount * 0.6f);
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

        public float TipHeight(int s)
        {
            int b = s * P;
            float f = len[s] / segLen[s];
            int j = Mathf.Clamp((int)f, 0, P - 1);
            if (j >= P - 1) return upright[b + P - 1].y;
            return Mathf.Lerp(upright[b + j].y, upright[b + j + 1].y, f - j);
        }

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

        /// <summary>Part des mèches de la zone déjà à la bonne hauteur (± tolérance) pour un objectif.</summary>
        public float ZoneProgress(int z, TargetKind kind, float value, float tol)
        {
            int cnt = 0, ok = 0;
            for (int s = 0; s < N; s++)
            {
                if (zone[s] != z) continue;
                cnt++;
                float err;
                switch (kind)
                {
                    case TargetKind.Hauteur: err = TipHeight(s) - value; break;
                    case TargetKind.Longueur: err = value - len[s]; break;
                    case TargetKind.Pointes:
                        float cut = origLen[s] - len[s];
                        err = cut < 0.015f ? cut - 0.03f : (cut > 0.06f ? cut - 0.06f : 0f);
                        break;
                    default: err = 0; break;
                }
                if (Mathf.Abs(err) <= tol) ok++;
            }
            return cnt > 0 ? (float)ok / cnt : 1f;
        }
    }
}
