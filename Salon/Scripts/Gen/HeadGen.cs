using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Paramètres du visage d'un personnage.</summary>
    public class FaceParams
    {
        public Color skin = SalonMath.Hex("#F0CCB0");
        public Color lips = SalonMath.Hex("#C77079");
        public Color brows = SalonMath.Hex("#4A3020");
        public Color iris = SalonMath.Hex("#5B3A22");
        public Color blush = SalonMath.Hex("#F08C8C");
        public Color shadow = SalonMath.Hex("#B98A7A");
        public Color scalp = SalonMath.Hex("#3A2416");
        public float blushAmount = 0.35f;
        public float liner = 1f;        // trait d'eye-liner (0 = aucun)
        public float freckles = 0f;
        public float jaw = 0.22f;       // affinement de la mâchoire
        public float nose = 1f;         // taille du nez
        public float lipFull = 1f;      // volume des lèvres
        public float eyeSize = 1.1f;
        public int seed = 1;
    }

    /// <summary>Résultat de la génération d'une tête.</summary>
    public class HeadResult
    {
        public MeshData head, ears, eyeball, lashes, lidUpper;
        public Img skinTex, eyeTex;
        public Vector3 eyeL, eyeR;     // centres des yeux (espace tête)
        public Quaternion eyeRotL, eyeRotR;
        public float eyeRadius;
    }

    /// <summary>
    /// Génère une tête sculptée « réaliste mais mignonne » : une seule surface lisse avec nez, lèvres,
    /// pommettes et orbites, peau peinte (lèvres, sourcils, blush, eye-liner), yeux avec iris et cils.
    /// Espace : centre de la tête = origine, visage vers +Z, mètres.
    /// </summary>
    public static class HeadGen
    {
        public static readonly Vector3 R = new Vector3(0.077f, 0.111f, 0.098f);
        /// <summary>Ellipsoïde du cuir chevelu (collisions des cheveux).</summary>
        public static readonly Vector3 HairR = new Vector3(0.083f, 0.116f, 0.103f);
        const int LAT = 150, LON = 200;

        // Yeux
        const float EX = 0.0315f, EY = 0.006f, AX = 0.0158f, AY = 0.0079f, TILT = 7f * Mathf.Deg2Rad;
        const float EYE_DEPTH = 0.0125f, EYE_R = 0.0148f, EYE_OUT = 0.0042f;

        // ------------------------------------------------------------ Paramétrage de la surface
        /// <summary>u (0..1, face au milieu) → angle φ (0 = face).</summary>
        public static float PhiOf(float u)
        {
            float s = u * 2f - 1f;
            return Mathf.PI * (0.5f * s + 0.5f * s * s * s);
        }

        static float UOfPhi(float phi)
        {
            // inverse numérique de PhiOf
            float lo = 0, hi = 1;
            for (int i = 0; i < 30; i++) { float m = (lo + hi) / 2; if (PhiOf(m) < phi) lo = m; else hi = m; }
            return (lo + hi) / 2;
        }

        static Vector3 Dir(float theta, float phi)
        {
            return new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Cos(phi));
        }

        /// <summary>Forme de base (crâne + mâchoire), sans les détails du visage.</summary>
        static Vector3 Base(Vector3 d, FaceParams fp)
        {
            Vector3 p = Vector3.Scale(d, R);
            float k = Mathf.Clamp01(-d.y);
            p.x *= 1f - fp.jaw * Mathf.Pow(k, 1.4f);
            if (p.z > 0) p.z *= 1f - 0.08f * k * k;
            else p.z *= 1f - 0.32f * Mathf.Pow(k, 1.2f);
            // menton un peu plus court (visage doux)
            if (d.y < -0.55f) p.y = -R.y * 0.55f + (p.y + R.y * 0.55f) * 0.78f;
            return p;
        }

        static Vector3 BaseNormal(Vector3 p)
        {
            return SalonMath.SafeNormalize(new Vector3(p.x / (R.x * R.x), p.y / (R.y * R.y), p.z / (R.z * R.z)), Vector3.forward);
        }

        static float G(float dx, float dy, float sx, float sy) { return SalonMath.Gauss(dx, dy, sx, sy); }

        static float MouthY(float x, float smile) { return -0.0515f + smile * 0.0045f * Mathf.Clamp01((x / 0.024f) * (x / 0.024f)); }

        /// <summary>Coordonnées elliptiques de l'œil (e &lt; 1 : dans l'ouverture).</summary>
        static float EyeE(float x, float y, float side, float size)
        {
            float dx = (x - side * EX) * side, dy = y - EY;
            float c = Mathf.Cos(TILT), s = Mathf.Sin(TILT);
            float rx = dx * c + dy * s, ry = -dx * s + dy * c;
            return Mathf.Sqrt((rx / (AX * size)) * (rx / (AX * size)) + (ry / (AY * size)) * (ry / (AY * size)));
        }

        /// <summary>Déplacement (le long de la normale) qui sculpte le visage.</summary>
        static float Disp(float x, float y, FaceParams fp, float smile)
        {
            float d = 0;
            // museau (bouche un peu en avant) et pommettes rondes
            d += 0.0075f * G(x, y + 0.052f, 0.021f, 0.026f);
            // plan du visage en avant du crâne
            d += 0.0095f * G(x, y + 0.03f, 0.03f, 0.055f);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                d += (0.0062f + smile * 0.002f) * G(x - sgn * 0.041f, y + 0.026f - smile * 0.004f, 0.019f, 0.018f);
                // orbite à fond plat
                float e = EyeE(x, y, sgn, fp.eyeSize);
                d -= EYE_DEPTH * Mathf.Lerp(0.45f, 1f, SalonMath.Smooth01((y - EY + 0.004f) / 0.008f)) * (1f - SalonMath.Smooth01((e - 0.8f) / 0.55f));
                // arcade sourcilière
                d += 0.0032f * G(x - sgn * 0.031f, y - 0.03f, 0.02f, 0.006f);
                // tempes légèrement creusées
                d -= 0.0025f * G(x - sgn * 0.066f, y - 0.03f, 0.01f, 0.018f);
            }
            // nez
            float ns = fp.nose;
            float t = Mathf.Clamp01((0.014f - y) / 0.044f);
            float bridge = Mathf.Lerp(0.0025f, 0.0118f, Mathf.Pow(t, 1.3f)) * SalonMath.Smooth01((y + 0.034f) / 0.007f) * SalonMath.Smooth01((0.02f - y) / 0.008f);
            d += bridge * ns * G(x, 0, Mathf.Lerp(0.0042f, 0.0062f, t) * ns, 1f);
            d += 0.0058f * ns * G(x, y + 0.027f, 0.0062f * ns, 0.0062f);
            for (int sgn = -1; sgn <= 1; sgn += 2) d += 0.0032f * ns * G(x - sgn * 0.0088f * ns, y + 0.0315f, 0.0034f, 0.0034f);
            // lèvres
            float cy = MouthY(x, smile);
            float lf = fp.lipFull;
            d += 0.0040f * lf * G(x, y - (cy + 0.0045f), 0.0128f, 0.0040f);
            d += 0.0050f * lf * G(x, y - (cy - 0.006f), 0.0112f, 0.0048f);
            d -= 0.0024f * G(x, y - cy, 0.0165f, 0.001f);
            d -= 0.0011f * G(x, y + 0.041f, 0.0028f, 0.0042f); // philtrum
            for (int sgn = -1; sgn <= 1; sgn += 2) d -= 0.0014f * G(x - sgn * 0.0215f, y - cy, 0.0028f, 0.0028f);
            // menton
            d += 0.0125f * G(x, y + 0.089f, 0.017f, 0.014f);
            d += 0.004f * G(x, y + 0.072f, 0.02f, 0.01f);
            return d;
        }

        static float FrontW(Vector3 p) { return SalonMath.Smooth01((p.z - 0.02f) / 0.05f); }

        /// <summary>Point final de la peau pour une direction.</summary>
        public static Vector3 SkinPoint(Vector3 d, FaceParams fp, float smile)
        {
            Vector3 p = Base(d, fp);
            float w = FrontW(p);
            if (w <= 0) return p;
            return p + BaseNormal(p) * (Disp(p.x, p.y, fp, smile) * w);
        }

        /// <summary>Point de peau devant (x, y) (recherche de la direction correspondante).</summary>
        static Vector3 FrontPoint(float x, float y, FaceParams fp, float smile)
        {
            // direction approximative puis raffinement
            Vector3 d = new Vector3(x / R.x, y / R.y, 0);
            d.z = Mathf.Sqrt(Mathf.Max(0.01f, 1f - d.x * d.x - d.y * d.y));
            d.Normalize();
            for (int i = 0; i < 6; i++)
            {
                Vector3 b = Base(d, fp);
                d.x += (x - b.x) / R.x * 0.9f;
                d.y += (y - b.y) / R.y * 0.9f;
                d.z = Mathf.Sqrt(Mathf.Max(0.01f, 1f - d.x * d.x - d.y * d.y));
                d.Normalize();
            }
            return SkinPoint(d, fp, smile);
        }

        // ------------------------------------------------------------ Génération
        public static HeadResult Build(FaceParams fp, float smile = 0f)
        {
            var res = new HeadResult();
            res.head = BuildHeadMesh(fp, smile);
            res.head.name = "Visage";

            // Yeux
            res.eyeRadius = EYE_R * fp.eyeSize;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 s = FrontPoint(sgn * EX, EY, fp, 0f);
                Vector3 nrm = BaseNormal(Base(new Vector3(sgn * EX / R.x, EY / R.y, 0.93f).normalized, fp));
                Vector3 c = s - Vector3.forward * (res.eyeRadius - EYE_OUT) - new Vector3(sgn * 0.0011f, 0.0004f, 0f);
                var rot = Quaternion.LookRotation((Vector3.forward + new Vector3(sgn * 0.005f, -0.02f, 0)).normalized, Vector3.up);
                if (sgn < 0) { res.eyeL = c; res.eyeRotL = rot; } else { res.eyeR = c; res.eyeRotR = rot; }
            }
            res.eyeball = BuildEyeball(res.eyeRadius);
            res.lidUpper = BuildLid(res.eyeRadius * 1.04f);
            res.ears = BuildEars(fp);
            res.lashes = BuildLashes(fp);
            res.skinTex = PaintSkin(fp, res, smile);
            res.eyeTex = PaintEye(fp);
            return res;
        }

        public static MeshData BuildHeadMesh(FaceParams fp, float smile)
        {
            var m = new MeshData();
            for (int i = 0; i <= LAT; i++)
            {
                float v = (float)i / LAT;
                float th = Mathf.PI * v;
                for (int j = 0; j <= LON; j++)
                {
                    float u = (float)j / LON;
                    Vector3 d = Dir(th, PhiOf(u));
                    m.Add(SkinPoint(d, fp, smile), new Vector2(u, 1f - v));
                }
            }
            m.Grid(0, LAT + 1, LON + 1, false, true);
            BodyGen.Outward(m, Vector3.zero, false);
            m.RecalculateNormals();
            m.WeldNormals();
            return m;
        }

        static MeshData BuildEyeball(float r)
        {
            var m = MeshData.Sphere(r, 28, 36);
            m.name = "Oeil";
            BodyGen.Outward(m, Vector3.zero, false);
            // UV planaires de face (l'iris est peint au centre de la texture)
            m.uv.Clear();
            for (int i = 0; i < m.v.Count; i++)
            {
                Vector3 p = m.v[i] / r;
                float k = p.z < 0 ? 1.0f : 1f;
                m.uv.Add(new Vector2(0.5f + p.x * 0.5f * k, 0.5f + p.y * 0.5f * k));
            }
            return m;
        }

        /// <summary>Paupière supérieure (calotte au-dessus de l'œil) : on la fait pivoter autour de X pour cligner.</summary>
        static MeshData BuildLid(float r)
        {
            var m = new MeshData { name = "Paupiere" };
            int rows = 14, cols = 28;
            for (int i = 0; i <= rows; i++)
            {
                float el = Mathf.Lerp(5f, 90f, (float)i / rows) * Mathf.Deg2Rad;
                for (int j = 0; j <= cols; j++)
                {
                    float ph = Mathf.Lerp(-80f, 80f, (float)j / cols) * Mathf.Deg2Rad;
                    Vector3 d = new Vector3(Mathf.Sin(ph) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(ph) * Mathf.Cos(el));
                    m.Add(d * r, new Vector2((float)j / cols, (float)i / rows));
                    m.n.Add(d);
                }
            }
            m.Grid(0, rows + 1, cols + 1, false, false);
            FixWinding(m);
            return m;
        }

        /// <summary>Oriente les triangles vers l'extérieur (d'après les normales fournies).</summary>
        static void FixWinding(MeshData m)
        {
            for (int i = 0; i < m.tri.Count; i += 3)
            {
                int a = m.tri[i], b = m.tri[i + 1], c = m.tri[i + 2];
                Vector3 fn = Vector3.Cross(m.v[b] - m.v[a], m.v[c] - m.v[a]);
                if (Vector3.Dot(fn, m.n[a] + m.n[b] + m.n[c]) < 0) { m.tri[i + 1] = c; m.tri[i + 2] = b; }
            }
        }

        static MeshData BuildEars(FaceParams fp)
        {
            var ear = MeshData.Sphere(1f, 18, 24);
            for (int i = 0; i < ear.v.Count; i++)
            {
                Vector3 p = ear.v[i];
                // creux de la conque côté extérieur (+x)
                if (p.x > 0.1f) p.x -= 0.6f * p.x * SalonMath.Smooth01(1f - Mathf.Sqrt(p.y * p.y * 1.5f + p.z * p.z * 1.3f));
                // lobe plus fin en bas
                if (p.y < -0.4f) p.z *= 0.8f;
                ear.v[i] = p;
            }
            FixWinding(ear);
            ear.n.Clear();
            var m = new MeshData { name = "Oreilles" };
            var rot = Quaternion.Euler(0, 16f, -5f);
            m.Append(ear, new Vector3(R.x * 0.93f, -0.006f, -0.008f), rot, new Vector3(0.009f, 0.029f, 0.019f));
            int half = m.v.Count, halfTri = m.tri.Count;
            for (int i = 0; i < half; i++)
            {
                Vector3 p = m.v[i];
                m.v.Add(new Vector3(-p.x, p.y, p.z));
                m.uv.Add(m.uv[i]);
            }
            for (int i = 0; i < halfTri; i += 3)
            {
                m.tri.Add(m.tri[i] + half); m.tri.Add(m.tri[i + 2] + half); m.tri.Add(m.tri[i + 1] + half);
            }
            m.RecalculateNormals();
            return m;
        }

        static MeshData BuildLashes(FaceParams fp)
        {
            var m = new MeshData { name = "Cils" };
            Color lash = new Color(0.06f, 0.045f, 0.04f, 0f);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                int count = 26;
                for (int k = 0; k < count; k++)
                {
                    float a = Mathf.Lerp(0.12f, 0.92f, (k + 0.5f) / count) * Mathf.PI; // demi-ellipse supérieure
                    float lx = Mathf.Cos(a) * AX * fp.eyeSize * 1.02f, ly = Mathf.Sin(a) * AY * fp.eyeSize * 1.08f;
                    float c = Mathf.Cos(TILT), s = Mathf.Sin(TILT);
                    float x = sgn * EX + sgn * (lx * c - ly * s), y = EY + (lx * s + ly * c);
                    Vector3 root = FrontPoint(x, y, fp, 0f);
                    float outer = Mathf.Clamp01(sgn * (x - sgn * EX) / (AX * fp.eyeSize) * 0.5f + 0.5f); // 1 vers l'extérieur
                    float len = Mathf.Lerp(0.0032f, 0.0062f, outer);
                    Vector3 dir = new Vector3(sgn * Mathf.Lerp(-0.1f, 0.55f, outer), 0.55f, 0.85f).normalized;
                    AddLash(m, root, dir, len, lash, sgn);
                }
            }
            return m;
        }

        static void AddLash(MeshData m, Vector3 root, Vector3 dir, float len, Color col, int sgn)
        {
            int seg = 4;
            Vector3 p = root;
            Vector3 d = dir;
            Vector3 side = Vector3.Cross(d, Vector3.forward).normalized;
            if (side.sqrMagnitude < 0.1f) side = Vector3.right;
            int start = m.v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float t = (float)i / seg;
                float w = Mathf.Lerp(0.00045f, 0.00008f, t);
                m.v.Add(p - side * w); m.v.Add(p + side * w);
                Vector3 nn = Vector3.forward;
                m.n.Add(nn); m.n.Add(nn);
                m.t.Add(new Vector4(d.x, d.y, d.z, 0)); m.t.Add(new Vector4(d.x, d.y, d.z, 0));
                m.c.Add(col); m.c.Add(col);
                m.uv.Add(new Vector2(0, 0.1f)); m.uv.Add(new Vector2(1, 0.1f));
                m.uv2.Add(new Vector2(0.9f, 0)); m.uv2.Add(new Vector2(0.9f, 0));
                // courbure vers le haut
                d = (d + Vector3.up * 0.35f).normalized;
                p += d * (len / seg);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = start + i * 2;
                m.Tri(a, a + 1, a + 2); m.Tri(a + 2, a + 1, a + 3);
            }
        }

        // ------------------------------------------------------------ Peinture de la peau
        static float Seg(float px, float py, float ax, float ay, float bx, float by, out float t)
        {
            float vx = bx - ax, vy = by - ay;
            t = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
            float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static Vector2 Bez(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1 - t;
            return a * (u * u) + b * (2 * u * t) + c * (t * t);
        }

        public static Img PaintSkin(FaceParams fp, HeadResult hr, float smile)
        {
            int W = 1024, H = 1024;
            var img = new Img(W, H, fp.skin);
            Color skin = fp.skin;
            Color warm = Color.Lerp(skin, SalonMath.Hex("#E59A8C"), 0.35f);
            Color lipC = fp.lips;
            Color browC = fp.brows;
            var rng = new System.Random(fp.seed);

            // sourcil : 3 points de contrôle (x, y) pour le côté droit (+x)
            Vector2 b0 = new Vector2(0.0115f, 0.0215f), b1 = new Vector2(0.033f, 0.0345f), b2 = new Vector2(0.0525f, 0.0245f);
            var browPts = new Vector2[24];
            for (int i = 0; i < browPts.Length; i++) browPts[i] = Bez(b0, b1, b2, i / (browPts.Length - 1f));

            var sinP = new float[W]; var cosP = new float[W];
            for (int px = 0; px < W; px++) { float ph = PhiOf((px + 0.5f) / W); sinP[px] = Mathf.Sin(ph); cosP[px] = Mathf.Cos(ph); }
            for (int py = 0; py < H; py++)
            {
                float v = 1f - (py + 0.5f) / H;
                float th = Mathf.PI * v;
                float sT = Mathf.Sin(th), cT = Mathf.Cos(th);
                for (int px = 0; px < W; px++)
                {
                    float u = (px + 0.5f) / W;
                    Vector3 d = new Vector3(sT * sinP[px], cT, sT * cosP[px]);
                    Vector3 bp = Base(d, fp);
                    float x = bp.x, y = bp.y;
                    float front = FrontW(bp);
                    Color c = skin;
                    float n = 0.6f * SalonMath.Noise(u * 70f, v * 70f, fp.seed) + 0.4f * SalonMath.Noise(u * 190f, v * 190f, fp.seed + 1);
                    c = SalonMath.Mul(c, 0.965f + 0.06f * n);
                    // cuir chevelu teinté (sous les cheveux) avec une lisière douce
                    {
                        float f = d.z;
                        float hl = f > 0 ? Mathf.Lerp(-0.12f, 0.52f, f) : Mathf.Lerp(-0.12f, -0.6f, -f);
                        if (Mathf.Abs(d.x) > 0.75f && f > -0.1f && f < 0.35f) hl -= 0.05f;
                        float k = SalonMath.Smooth01((d.y - hl + 0.015f) / 0.06f);
                        if (k > 0) c = SalonMath.Over(c, Color.Lerp(fp.scalp, c, 0.25f), k * 0.92f);
                    }

                    if (front > 0.01f)
                    {
                        float ax = Mathf.Abs(x);
                        // rougeurs naturelles : nez, joues, menton
                        float red = 0.5f * G(x, y + 0.026f, 0.008f, 0.01f) + 0.25f * G(x, y + 0.085f, 0.015f, 0.01f);
                        c = SalonMath.Over(c, warm, red * front);
                        // blush
                        float bl = G(ax - 0.047f, y + 0.024f - smile * 0.004f, 0.017f, 0.013f) * fp.blushAmount;
                        c = SalonMath.Over(c, fp.blush, bl * front);

                        // yeux : fard, cernes, eye-liner calé sur l'ouverture réelle de l'œil
                        int sgn = x >= 0 ? 1 : -1;
                        float e = EyeE(x, y, sgn, fp.eyeSize);
                        float dyE = y - EY;
                        float up = SalonMath.Smooth01((dyE + 0.004f) / 0.006f);
                        c = SalonMath.Over(c, fp.shadow, 0.24f * up * SalonMath.Smooth01(1f - Mathf.Abs(e - 1.25f) / 0.45f) * front);
                        c = SalonMath.Over(c, SalonMath.Hex("#B88C86"), 0.10f * (1f - up) * SalonMath.Smooth01(1f - Mathf.Abs(e - 1.35f) / 0.4f) * front);
                        if (fp.liner > 0 && e < 1.6f)
                        {
                            Vector3 sp = SkinPoint(d, fp, smile);
                            Vector3 ec = sgn < 0 ? hr.eyeL : hr.eyeR;
                            float dist = (sp - ec).magnitude - hr.eyeRadius;
                            float outer = Mathf.Clamp01((sgn * (x - ec.x)) / 0.012f);
                            float upper = SalonMath.Smooth01((y - ec.y + 0.0015f + outer * 0.003f) / 0.002f);
                            float wid = Mathf.Lerp(0.0007f, 0.0016f, outer);
                            float band = dist > -0.0004f ? SalonMath.Smooth01(1f - (dist + 0.0002f) / wid) * upper : 0f;
                            float lower = (1f - upper) * 0.22f * (dist > -0.0004f ? SalonMath.Smooth01(1f - (dist + 0.0002f) / 0.0006f) : 0f);
                            float tw;
                            Vector2 oc = new Vector2(sgn * (EX + AX * fp.eyeSize * 0.92f), EY + 0.0012f);
                            Vector2 wt = new Vector2(sgn * (EX + AX * fp.eyeSize * 1.3f), EY + 0.0055f);
                            float dw = Seg(x, y, oc.x, oc.y, wt.x, wt.y, out tw);
                            float wing = 0f;
                            c = SalonMath.Over(c, new Color(0.08f, 0.055f, 0.05f), Mathf.Max(Mathf.Max(band, wing), lower) * fp.liner * front);
                        }

                        // sourcils (traits de poils)
                        float best = 1f; float bt = 0;
                        if (ax > 0.005f && ax < 0.065f && y > 0.012f && y < 0.05f)
                        for (int i = 0; i < browPts.Length - 1; i++)
                        {
                            float tt;
                            float dd = Seg(ax, y, browPts[i].x, browPts[i].y, browPts[i + 1].x, browPts[i + 1].y, out tt);
                            if (dd < best) { best = dd; bt = (i + tt) / (browPts.Length - 1); }
                        }
                        float thick = Mathf.Lerp(0.0046f, 0.0016f, bt);
                        if (best < thick * 1.6f)
                        {
                            float strokes = 0.55f + 0.45f * SalonMath.Noise(ax * 2600f + y * 900f, y * 4200f, 7);
                            float a = SalonMath.Smooth01(1f - best / thick) * strokes * Mathf.Lerp(0.75f, 1f, bt);
                            c = SalonMath.Over(c, browC, a * 0.92f * front);
                        }

                        // narines
                        for (int s2 = -1; s2 <= 1; s2 += 2)
                        {
                            float nd = Mathf.Sqrt(Mathf.Pow((x - s2 * 0.0052f * fp.nose) / 0.0022f, 2) + Mathf.Pow((y + 0.0352f) / 0.0012f, 2));
                            c = SalonMath.Over(c, SalonMath.Hex("#8A5448"), 0.45f * SalonMath.Smooth01(1f - nd) * front);
                        }

                        // lèvres
                        float cy = MouthY(x, smile);
                        float lipA = 0f, shade = 0f;
                        float upW = 0.0198f * fp.lipFull, loW = 0.0175f * fp.lipFull;
                        if (ax < upW && y >= cy)
                        {
                            float k = 1f - (ax / upW) * (ax / upW);
                            float top = cy + 0.0072f * fp.lipFull * Mathf.Pow(k, 0.55f) - 0.0013f * G(x, 0, 0.0035f, 1) + 0.0006f * G(ax - 0.006f, 0, 0.004f, 1);
                            lipA = SalonMath.Smooth01((top - y) / 0.0009f);
                            shade = 1f - (y - cy) / 0.008f;
                        }
                        else if (ax < loW && y < cy)
                        {
                            float k = 1f - (ax / loW) * (ax / loW);
                            float bot = cy - 0.0092f * fp.lipFull * Mathf.Pow(k, 0.5f);
                            lipA = SalonMath.Smooth01((y - bot) / 0.0011f);
                            shade = 0.6f * (1f - (cy - y) / 0.006f);
                        }
                        if (lipA > 0)
                        {
                            Color lc = SalonMath.Mul(lipC, 1f - 0.18f * Mathf.Clamp01(shade));
                            float hl = y < cy ? 0.35f * G(x, y - (cy - 0.0055f), 0.006f, 0.0018f) : 0f;
                            lc = Color.Lerp(lc, Color.white, hl);
                            c = SalonMath.Over(c, lc, lipA * front);
                        }
                        float line = G(x, y - cy, 0.016f, 0.0005f) * SalonMath.Smooth01((0.023f - ax) / 0.003f);
                        c = SalonMath.Over(c, SalonMath.Mul(lipC, 0.45f), line * 0.8f * front);

                        // taches de rousseur
                        if (fp.freckles > 0)
                        {
                            float fz = G(ax - 0.03f, y + 0.015f, 0.025f, 0.014f);
                            float f = SalonMath.Noise(u * 900f, v * 900f, fp.seed + 3);
                            if (f > 0.82f) c = SalonMath.Over(c, SalonMath.Mul(skin, 0.72f), (f - 0.82f) * 5f * fz * fp.freckles * front);
                        }
                    }
                    img.px[py * W + px] = c;
                }
            }
            return img;
        }

        // ------------------------------------------------------------ Œil
        public static Img PaintEye(FaceParams fp)
        {
            int S = 256;
            var img = new Img(S, S, Color.white);
            Color sclera = SalonMath.Hex("#F7F2EE");
            Color iris = fp.iris;
            Color irisLight = Color.Lerp(iris, Color.white, 0.35f);
            float ir = 0.6f * 0.5f; // rayon de l'iris (en UV, 0.5 = bord de l'œil)
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S - 0.5f, v = (y + 0.5f) / S - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ang = Mathf.Atan2(v, u);
                    Color c = sclera;
                    c = SalonMath.Over(c, SalonMath.Hex("#E9C9C2"), SalonMath.Smooth01((r - 0.3f) / 0.2f) * 0.6f);
                    if (r < ir + 0.01f)
                    {
                        float t = r / ir;
                        float fib = SalonMath.Noise(ang * 18f, t * 3f, fp.seed) * 0.5f + SalonMath.Noise(ang * 40f, t * 6f, fp.seed + 1) * 0.5f;
                        Color ic = Color.Lerp(irisLight, iris, Mathf.Clamp01(t * 1.1f));
                        ic = SalonMath.Mul(ic, 0.8f + 0.4f * fib);
                        ic = Color.Lerp(ic, SalonMath.Mul(iris, 0.35f), SalonMath.Smooth01((t - 0.82f) / 0.18f)); // anneau limbal
                        float pupil = SalonMath.Smooth01((0.38f - t) / 0.04f);
                        ic = Color.Lerp(ic, new Color(0.03f, 0.025f, 0.03f), pupil);
                        float edge = SalonMath.Smooth01((ir + 0.01f - r) / 0.012f);
                        c = Color.Lerp(c, ic, edge);
                    }
                    // reflets (catchlights)
                    float h1 = Mathf.Sqrt((u + 0.06f) * (u + 0.06f) + (v - 0.07f) * (v - 0.07f));
                    float h2 = Mathf.Sqrt((u - 0.06f) * (u - 0.06f) + (v + 0.06f) * (v + 0.06f));
                    c = Color.Lerp(c, Color.white, SalonMath.Smooth01((0.05f - h1) / 0.012f));
                    c = Color.Lerp(c, Color.white, 0.7f * SalonMath.Smooth01((0.022f - h2) / 0.01f));
                    img.px[y * S + x] = c;
                }
            return img;
        }
    }
}
