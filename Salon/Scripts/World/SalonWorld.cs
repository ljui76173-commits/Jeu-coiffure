using UnityEngine;
using UnityEngine.Rendering;

namespace SalonCoiffure
{
    /// <summary>
    /// Construit le salon chic parisien : parquet en point de Hongrie, moulures, miroir doré,
    /// velours, plantes, lustre et vue sur les toits de Paris.
    /// </summary>
    public class SalonWorld : MonoBehaviour
    {
        // Postes de travail
        public static readonly Vector3 ChairPos = new Vector3(0.8f, 0f, 1.4f);
        public static readonly Vector3 BacPos = new Vector3(-1.7f, 0f, 0.7f);
        public const float ChairChestY = 0.88f;
        public const float BacChestY = 0.80f;
        public const float BacRecline = -55f;

        public Light KeyLight { get; private set; }
        public Light FillLight { get; private set; }
        TextMesh sign;

        const float W = 4f, D = 3f, H = 3.2f;

        Material wall, moulding, gold, velvetRose, velvetSage, wood, marble, white, black, chrome, terracotta, leaf, leafDark;

        public static Vector3 ChestAt(bool bac)
        {
            return bac ? new Vector3(BacPos.x, BacChestY, BacPos.z) : new Vector3(ChairPos.x, ChairChestY, ChairPos.z);
        }

        public static Quaternion ChestRot(bool bac)
        {
            return bac ? Quaternion.Euler(BacRecline, 0, 0) : Quaternion.identity;
        }

        public static Vector3 HeadWorld(bool bac)
        {
            return ChestAt(bac) + ChestRot(bac) * CustomerAvatar.HeadOffset;
        }

        public void Build(string salonName)
        {
            wall = Mats.Lit(SalonUtil.Hex("#EFE5D8"), 0.15f);
            moulding = Mats.Lit(SalonUtil.Hex("#F8F2E9"), 0.25f);
            gold = Mats.Lit(SalonUtil.Hex("#D2A85A"), 0.62f, 0.85f);
            var velvetTex = ProcTex.Velvet();
            velvetRose = Mats.Lit(SalonUtil.Hex("#D59C98"), 0.22f, 0f, velvetTex, new Vector2(2, 2));
            velvetSage = Mats.Lit(SalonUtil.Hex("#8DA48C"), 0.22f, 0f, velvetTex, new Vector2(2, 2));
            wood = Mats.Lit(SalonUtil.Hex("#7B5236"), 0.45f);
            marble = Mats.Lit(Color.white, 0.75f, 0f, ProcTex.Marble(), Vector2.one);
            white = Mats.Lit(SalonUtil.Hex("#F7F5F2"), 0.8f);
            black = Mats.Lit(SalonUtil.Hex("#1E1C21"), 0.55f);
            chrome = Mats.Lit(SalonUtil.Hex("#C9CCD2"), 0.85f, 1f);
            terracotta = Mats.Lit(SalonUtil.Hex("#F2ECE4"), 0.4f);
            leaf = Mats.Lit(SalonUtil.Hex("#4E7A4A"), 0.35f);
            leafDark = Mats.Lit(SalonUtil.Hex("#36593A"), 0.35f);

            BuildRoom();
            BuildWindow();
            BuildMirrorStation(salonName);
            BuildChair(ChairPos, velvetRose, false);
            BuildBac();
            BuildDecor();
            BuildLights();
        }

        // ------------------------------------------------------------ Pièce
        void BuildRoom()
        {
            var t = transform;
            var parquet = Mats.Lit(Color.white, 0.55f, 0f, ProcTex.Parquet(), new Vector2(2 * W / 1.6f, 2 * D / 1.6f));
            SalonUtil.Box(t, new Vector3(0, -0.01f, 0), new Vector3(2 * W, 0.02f, 2 * D), parquet, false, "Parquet");
            SalonUtil.Box(t, new Vector3(0, H + 0.01f, 0), new Vector3(2 * W, 0.02f, 2 * D), moulding, false, "Plafond");
            // Rosace du lustre
            SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, H - 0.005f, 0.5f), new Vector3(0.5f, 0.01f, 0.5f), moulding, false, "Rosace");

            Wall(new Vector3(0, H / 2, D), new Vector3(2 * W, H, 0.1f), true, 0);       // fond (miroir)
            Wall(new Vector3(0, H / 2, -D), new Vector3(2 * W, H, 0.1f), true, 1);      // fenêtre
            Wall(new Vector3(-W, H / 2, 0), new Vector3(0.1f, H, 2 * D), false, 2);
            Wall(new Vector3(W, H / 2, 0), new Vector3(0.1f, H, 2 * D), false, 3);

            // Tapis
            var rug = Mats.Lit(SalonUtil.Hex("#3F5E52"), 0.1f);
            SalonUtil.Box(t, new Vector3(ChairPos.x, 0.004f, ChairPos.z - 0.1f), new Vector3(1.9f, 0.006f, 1.5f), rug, false, "Tapis");
            SalonUtil.Box(t, new Vector3(ChairPos.x, 0.006f, ChairPos.z - 0.1f), new Vector3(1.75f, 0.006f, 1.35f), Mats.Lit(SalonUtil.Hex("#E8D8C0"), 0.1f), false, "TapisBord");
            SalonUtil.Box(t, new Vector3(ChairPos.x, 0.008f, ChairPos.z - 0.1f), new Vector3(1.65f, 0.006f, 1.25f), rug, false, "TapisCentre");
        }

        void Wall(Vector3 pos, Vector3 size, bool alongX, int id)
        {
            var t = transform;
            SalonUtil.Box(t, pos, size, wall, false, "Mur" + id);
            float len = alongX ? size.x : size.z;
            Vector3 inward = alongX ? new Vector3(0, 0, -Mathf.Sign(pos.z)) : new Vector3(-Mathf.Sign(pos.x), 0, 0);
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            Vector3 face = pos + inward * 0.05f;
            face.y = 0;

            // plinthe, cimaise, corniche
            Strip(face + Vector3.up * 0.07f, along, len, 0.14f, 0.025f, inward);
            Strip(face + Vector3.up * 1.0f, along, len, 0.045f, 0.03f, inward);
            Strip(face + Vector3.up * (H - 0.08f), along, len, 0.16f, 0.06f, inward);

            // panneaux moulurés (boiseries)
            int panels = Mathf.RoundToInt(len / 1.1f);
            float pw = len / panels;
            for (int i = 0; i < panels; i++)
            {
                float c = -len / 2 + pw * (i + 0.5f);
                // éviter fenêtre et miroir
                if (id == 1 && Mathf.Abs(c) < 1.5f) continue;
                if (id == 0 && Mathf.Abs(c - ChairPos.x) < 0.8f) continue;
                Vector3 center = face + along * c;
                Frame(center + Vector3.up * 0.55f, along, inward, pw * 0.72f, 0.62f, moulding, 0.018f);
                Frame(center + Vector3.up * 1.95f, along, inward, pw * 0.72f, 1.35f, moulding, 0.018f);
            }
        }

        void Strip(Vector3 center, Vector3 along, float len, float height, float depth, Vector3 inward)
        {
            Vector3 size = along == Vector3.right ? new Vector3(len, height, depth) : new Vector3(depth, height, len);
            SalonUtil.Box(transform, center + inward * (depth / 2), size, moulding, false, "Moulure");
        }

        void Frame(Vector3 center, Vector3 along, Vector3 inward, float w, float h, Material m, float th)
        {
            bool x = along == Vector3.right;
            Vector3 hs = x ? new Vector3(w, th, th) : new Vector3(th, th, w);
            Vector3 vs = x ? new Vector3(th, h, th) : new Vector3(th, h, th);
            Vector3 off = inward * (th / 2);
            SalonUtil.Box(transform, center + off + Vector3.up * (h / 2), hs, m, false, "Cadre");
            SalonUtil.Box(transform, center + off - Vector3.up * (h / 2), hs, m, false, "Cadre");
            SalonUtil.Box(transform, center + off + along * (w / 2), vs, m, false, "Cadre");
            SalonUtil.Box(transform, center + off - along * (w / 2), vs, m, false, "Cadre");
        }

        // ------------------------------------------------------------ Fenêtre sur Paris
        void BuildWindow()
        {
            var t = transform;
            float z = -D + 0.06f;
            float ww = 2.4f, wh = 2.2f, y0 = 0.62f;
            var view = Mats.Unlit(ProcTex.ParisView(), Color.white);
            var q = SalonUtil.Prim(PrimitiveType.Quad, t, new Vector3(0, y0 + wh / 2, z + 0.002f), new Vector3(ww, wh, 1), view,
                Quaternion.Euler(0, 180, 0), false, "VueParis");
            q.GetComponent<MeshRenderer>().receiveShadows = false;

            // Croisillons de la porte-fenêtre
            var frame = Mats.Lit(SalonUtil.Hex("#FAF7F2"), 0.5f);
            float th = 0.06f;
            SalonUtil.Box(t, new Vector3(0, y0, z + 0.03f), new Vector3(ww + 0.12f, 0.1f, 0.08f), frame, false, "Appui");
            SalonUtil.Box(t, new Vector3(0, y0 + wh, z + 0.03f), new Vector3(ww + 0.12f, 0.1f, 0.08f), frame, false, "Linteau");
            for (int i = -2; i <= 2; i++)
                SalonUtil.Box(t, new Vector3(i * ww / 4, y0 + wh / 2, z + 0.03f), new Vector3(i == 0 || Mathf.Abs(i) == 2 ? th * 1.4f : th * 0.6f, wh, 0.05f), frame, false, "Montant");
            for (int k = 1; k <= 3; k++)
                SalonUtil.Box(t, new Vector3(0, y0 + wh * k / 4f, z + 0.03f), new Vector3(ww, th * 0.5f, 0.04f), frame, false, "Traverse");
            // Garde-corps en fer forgé
            var iron = Mats.Lit(SalonUtil.Hex("#2A2A2E"), 0.4f, 0.6f);
            SalonUtil.Box(t, new Vector3(0, y0 + 0.55f, z + 0.08f), new Vector3(ww, 0.025f, 0.025f), iron, true, "Rambarde");
            for (int i = 0; i <= 24; i++)
                SalonUtil.Box(t, new Vector3(-ww / 2 + ww * i / 24f, y0 + 0.28f, z + 0.08f), new Vector3(0.012f, 0.55f, 0.012f), iron, true, "Barreau");

            // Rideaux en velours (plis)
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                for (int k = 0; k < 6; k++)
                {
                    float x = sgn * (ww / 2 + 0.08f + k * 0.075f);
                    SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(x, (H - 0.15f) / 2 + 0.02f, z + 0.16f + (k % 2) * 0.03f),
                        new Vector3(0.09f, (H - 0.15f) / 2, 0.06f), velvetSage, true, "Rideau");
                }
                // embrasse dorée
                SalonUtil.Prim(PrimitiveType.Sphere, t, new Vector3(sgn * (ww / 2 + 0.28f), 1.05f, z + 0.24f), new Vector3(0.07f, 0.07f, 0.07f), gold, true, "Embrasse");
            }
            SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, H - 0.18f, z + 0.18f), new Vector3(0.03f, ww / 2 + 0.7f, 0.03f), gold,
                Quaternion.Euler(0, 0, 90), true, "Tringle");
        }

        // ------------------------------------------------------------ Poste de coiffage
        void BuildMirrorStation(string salonName)
        {
            var t = transform;
            float x = ChairPos.x, z = D - 0.05f;
            // Console en marbre sur pieds dorés
            SalonUtil.Box(t, new Vector3(x, 0.86f, z - 0.24f), new Vector3(1.3f, 0.05f, 0.46f), marble, true, "Console");
            for (int sx = -1; sx <= 1; sx += 2)
                SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(x + sx * 0.58f, 0.42f, z - 0.1f), new Vector3(0.04f, 0.42f, 0.04f), gold, true, "Pied");
            // Miroir et cadre doré
            var mirror = Mats.Lit(SalonUtil.Hex("#B9C3C9"), 0.97f, 1f);
            SalonUtil.Box(t, new Vector3(x, 1.72f, z - 0.01f), new Vector3(1.0f, 1.45f, 0.02f), mirror, false, "Miroir");
            float fw = 0.07f;
            SalonUtil.Box(t, new Vector3(x, 1.72f + 0.725f + fw / 2, z - 0.03f), new Vector3(1.0f + 2 * fw, fw, 0.05f), gold, false, "CadreH");
            SalonUtil.Box(t, new Vector3(x, 1.72f - 0.725f - fw / 2, z - 0.03f), new Vector3(1.0f + 2 * fw, fw, 0.05f), gold, false, "CadreB");
            SalonUtil.Box(t, new Vector3(x - 0.5f - fw / 2, 1.72f, z - 0.03f), new Vector3(fw, 1.45f, 0.05f), gold, false, "CadreG");
            SalonUtil.Box(t, new Vector3(x + 0.5f + fw / 2, 1.72f, z - 0.03f), new Vector3(fw, 1.45f, 0.05f), gold, false, "CadreD");
            SalonUtil.Prim(PrimitiveType.Sphere, t, new Vector3(x, 2.53f, z - 0.03f), new Vector3(0.16f, 0.12f, 0.05f), gold, false, "Fronton");

            // Petits flacons et brosse sur la console
            Color[] bottle = { SalonUtil.Hex("#E9B7C0"), SalonUtil.Hex("#F3E3C3"), SalonUtil.Hex("#A9C3B3") };
            for (int i = 0; i < 3; i++)
            {
                var m = Mats.Lit(bottle[i], 0.75f);
                SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(x + 0.35f + i * 0.08f, 0.94f, z - 0.2f), new Vector3(0.05f, 0.06f + i * 0.01f, 0.05f), m, true, "Flacon");
                SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(x + 0.35f + i * 0.08f, 1.01f + i * 0.01f, z - 0.2f), new Vector3(0.02f, 0.015f, 0.02f), gold, true, "Bouchon");
            }
            SalonUtil.Box(t, new Vector3(x - 0.4f, 0.9f, z - 0.25f), new Vector3(0.22f, 0.025f, 0.06f), black, true, "Brosse");

            // Enseigne
            var plaque = Mats.Lit(SalonUtil.Hex("#2F4A40"), 0.5f);
            SalonUtil.Box(t, new Vector3(x, 2.8f, z - 0.02f), new Vector3(1.5f, 0.3f, 0.03f), plaque, false, "Enseigne");
            Frame(new Vector3(x, 2.8f - 0.15f, z - 0.035f), Vector3.right, Vector3.back, 1.5f, 0.3f, gold, 0.02f);
            sign = MakeText(t, new Vector3(x, 2.8f, z - 0.045f), SalonUtil.Hex("#E8C77E"));
            SetSalonName(salonName);
        }

        TextMesh MakeText(Transform parent, Vector3 pos, Color c)
        {
            var go = new GameObject("TexteEnseigne");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            var tm = go.AddComponent<TextMesh>();
            Font f = UIStyles.SerifFont();
            tm.font = f;
            go.GetComponent<MeshRenderer>().sharedMaterial = f.material;
            tm.fontSize = 64;
            tm.characterSize = 0.0065f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Italic;
            tm.color = c;
            return tm;
        }

        public void SetSalonName(string n)
        {
            if (sign != null) sign.text = string.IsNullOrEmpty(n) ? "Salon" : n;
        }

        // ------------------------------------------------------------ Fauteuils
        void BuildChair(Vector3 p, Material velvet, bool reclined)
        {
            var root = new GameObject(reclined ? "FauteuilBac" : "FauteuilCoiffure").transform;
            root.SetParent(transform, false);
            root.localPosition = p;
            // pied chromé
            SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.015f, 0), new Vector3(0.55f, 0.015f, 0.55f), chrome, true, "Socle");
            SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.22f, 0), new Vector3(0.08f, 0.2f, 0.08f), chrome, true, "Verin");
            // assise capitonnée
            SalonUtil.Box(root, new Vector3(0, 0.47f, 0.02f), new Vector3(0.58f, 0.14f, 0.52f), velvet, true, "Assise");
            var back = SalonUtil.Box(root, new Vector3(0, 0.82f, -0.25f), new Vector3(0.56f, 0.62f, 0.1f), velvet, true, "Dossier");
            if (reclined) back.transform.localRotation = Quaternion.Euler(BacRecline + 20f, 0, 0);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                SalonUtil.Box(root, new Vector3(sx * 0.31f, 0.65f, 0.02f), new Vector3(0.08f, 0.06f, 0.48f), velvet, true, "Accoudoir");
                SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(sx * 0.31f, 0.56f, 0.2f), new Vector3(0.03f, 0.06f, 0.03f), gold, true, "PiedAccoudoir");
            }
            // boutons de capiton
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                    SalonUtil.Prim(PrimitiveType.Sphere, back.transform, new Vector3(-0.25f + i * 0.25f, -0.15f + j * 0.3f, 0.5f),
                        new Vector3(0.04f, 0.04f, 0.3f), gold, false, "Capiton");
        }

        void BuildBac()
        {
            var t = transform;
            BuildChair(BacPos, velvetSage, true);
            Vector3 head = HeadWorld(true);
            Vector3 bowl = head + new Vector3(0, -0.2f, -0.06f);
            // vasque blanche
            SalonUtil.Prim(PrimitiveType.Sphere, t, bowl, new Vector3(0.48f, 0.26f, 0.42f), white, true, "Vasque");
            SalonUtil.Prim(PrimitiveType.Cylinder, t, bowl + new Vector3(0, 0.1f, 0), new Vector3(0.5f, 0.01f, 0.44f), white, true, "Rebord");
            // meuble du bac
            SalonUtil.Box(t, new Vector3(bowl.x, bowl.y / 2 - 0.02f, bowl.z - 0.05f), new Vector3(0.6f, bowl.y - 0.04f, 0.5f), black, true, "MeubleBac");
            SalonUtil.Box(t, new Vector3(bowl.x, bowl.y / 2, bowl.z + 0.21f), new Vector3(0.62f, 0.03f, 0.02f), gold, true, "Liseré");
            // robinet col de cygne
            SalonUtil.Prim(PrimitiveType.Cylinder, t, bowl + new Vector3(0, 0.2f, -0.22f), new Vector3(0.025f, 0.1f, 0.025f), chrome, true, "Robinet");
            SalonUtil.Prim(PrimitiveType.Capsule, t, bowl + new Vector3(0, 0.3f, -0.17f), new Vector3(0.022f, 0.06f, 0.022f), chrome,
                Quaternion.Euler(70, 0, 0), true, "ColDeCygne");
            // serviettes roulées
            var towel = Mats.Lit(SalonUtil.Hex("#F3E6E1"), 0.1f);
            for (int i = 0; i < 3; i++)
                SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(bowl.x + 0.5f, 0.75f + i * 0.09f, bowl.z - 0.1f), new Vector3(0.09f, 0.16f, 0.09f), towel,
                    Quaternion.Euler(0, 0, 90), true, "Serviette");
            SalonUtil.Box(t, new Vector3(bowl.x + 0.5f, 0.35f, bowl.z - 0.1f), new Vector3(0.4f, 0.7f, 0.35f), wood, true, "Desserte");
        }

        // ------------------------------------------------------------ Décor
        void BuildDecor()
        {
            var t = transform;
            // Plantes de part et d'autre de la fenêtre
            Plant(new Vector3(-1.85f, 0, -D + 0.45f), 1.1f, 1);
            Plant(new Vector3(1.85f, 0, -D + 0.45f), 0.95f, 2);
            Plant(new Vector3(W - 0.45f, 0, D - 0.45f), 1.25f, 3);

            // Étagères de produits (future boutique)
            float sx = W - 0.2f;
            for (int k = 0; k < 3; k++)
            {
                float y = 1.15f + k * 0.42f;
                SalonUtil.Box(t, new Vector3(sx, y, -0.6f), new Vector3(0.3f, 0.03f, 1.6f), wood, true, "Etagere");
                for (int i = 0; i < 7; i++)
                {
                    var c = Color.HSVToRGB(Mathf.Repeat(0.02f + i * 0.13f + k * 0.31f, 1f), 0.25f, 0.92f);
                    float h = 0.13f + 0.05f * SalonUtil.Hash(i, k, 5);
                    SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(sx - 0.02f, y + h / 2 + 0.015f, -1.25f + i * 0.21f),
                        new Vector3(0.07f, h / 2, 0.07f), Mats.Lit(c, 0.7f), true, "Produit");
                    SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(sx - 0.02f, y + h + 0.03f, -1.25f + i * 0.21f),
                        new Vector3(0.03f, 0.015f, 0.03f), gold, true, "Bouchon");
                }
            }

            // Tableaux dorés
            Painting(new Vector3(-W + 0.06f, 1.9f, 0.9f), Quaternion.Euler(0, 90, 0), 11);
            Painting(new Vector3(-W + 0.06f, 1.9f, -1.2f), Quaternion.Euler(0, 90, 0), 12);
            Painting(new Vector3(W - 0.06f, 2.05f, 1.6f), Quaternion.Euler(0, -90, 0), 13);

            // Lustre
            var lustre = new GameObject("Lustre").transform;
            lustre.SetParent(t, false);
            lustre.localPosition = new Vector3(0, H - 0.7f, 0.5f);
            SalonUtil.Prim(PrimitiveType.Cylinder, lustre, new Vector3(0, 0.35f, 0), new Vector3(0.012f, 0.35f, 0.012f), gold, false, "Tige");
            SalonUtil.Prim(PrimitiveType.Cylinder, lustre, Vector3.zero, new Vector3(0.6f, 0.012f, 0.6f), gold, false, "Couronne");
            var bulb = Mats.Emissive(SalonUtil.Hex("#FFF2D6"), SalonUtil.Hex("#FFD9A0") * 2.2f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.3f, 0.06f, Mathf.Sin(a) * 0.3f);
                SalonUtil.Prim(PrimitiveType.Cylinder, lustre, p - Vector3.up * 0.03f, new Vector3(0.03f, 0.03f, 0.03f), gold, false, "Bougeoir");
                SalonUtil.Prim(PrimitiveType.Sphere, lustre, p + Vector3.up * 0.03f, new Vector3(0.05f, 0.07f, 0.05f), bulb, false, "Ampoule");
                // pampilles
                SalonUtil.Prim(PrimitiveType.Sphere, lustre, p * 0.8f - Vector3.up * 0.1f, new Vector3(0.02f, 0.04f, 0.02f), white, false, "Pampille");
            }

            // Banquette d'attente
            var bench = new GameObject("Banquette").transform;
            bench.SetParent(t, false);
            bench.localPosition = new Vector3(-W + 0.4f, 0, -0.3f);
            SalonUtil.Box(bench, new Vector3(0, 0.25f, 0), new Vector3(0.55f, 0.2f, 1.6f), velvetRose, true, "Assise");
            SalonUtil.Box(bench, new Vector3(-0.22f, 0.55f, 0), new Vector3(0.12f, 0.5f, 1.6f), velvetRose, true, "Dossier");
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    SalonUtil.Prim(PrimitiveType.Cylinder, bench, new Vector3(i * 0.2f, 0.08f, j * 0.7f), new Vector3(0.04f, 0.08f, 0.04f), gold, true, "Pied");

            // Petite table ronde + vase de pivoines
            SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(-W + 0.45f, 0.55f, 0.85f), new Vector3(0.45f, 0.02f, 0.45f), marble, true, "Table");
            SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(-W + 0.45f, 0.28f, 0.85f), new Vector3(0.04f, 0.27f, 0.04f), gold, true, "PiedTable");
            SalonUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(-W + 0.45f, 0.65f, 0.85f), new Vector3(0.1f, 0.09f, 0.1f), white, true, "Vase");
            var peony = Mats.Lit(SalonUtil.Hex("#F2B5C4"), 0.3f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.256f;
                SalonUtil.Prim(PrimitiveType.Sphere, t, new Vector3(-W + 0.45f + Mathf.Cos(a) * 0.06f, 0.8f + (i % 2) * 0.03f, 0.85f + Mathf.Sin(a) * 0.06f),
                    Vector3.one * 0.075f, peony, true, "Pivoine");
            }
        }

        void Plant(Vector3 p, float h, int seed)
        {
            var root = new GameObject("Plante").transform;
            root.SetParent(transform, false);
            root.localPosition = p;
            SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.2f, 0), new Vector3(0.36f, 0.2f, 0.36f), terracotta, true, "Pot");
            SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.41f, 0), new Vector3(0.38f, 0.015f, 0.38f), gold, true, "Liseré");
            var rng = new System.Random(seed);
            for (int i = 0; i < 14; i++)
            {
                float a = (float)rng.NextDouble() * 6.283f;
                float r = 0.05f + (float)rng.NextDouble() * 0.2f;
                float y = 0.5f + (float)rng.NextDouble() * (h - 0.4f);
                var pos = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                var rot = Quaternion.Euler((float)rng.NextDouble() * 60 - 30, a * Mathf.Rad2Deg, (float)rng.NextDouble() * 60 - 30);
                SalonUtil.Prim(PrimitiveType.Sphere, root, pos, new Vector3(0.22f, 0.03f, 0.12f), (i % 2) == 0 ? leaf : leafDark, rot, true, "Feuille");
                SalonUtil.Prim(PrimitiveType.Cylinder, root, (pos + new Vector3(0, 0.42f, 0)) / 2, new Vector3(0.008f, (y - 0.42f) / 2, 0.008f), leafDark, true, "Tige");
            }
        }

        void Painting(Vector3 pos, Quaternion rot, int seed)
        {
            var root = new GameObject("Tableau").transform;
            root.SetParent(transform, false);
            root.localPosition = pos;
            root.localRotation = rot;
            var art = Mats.Lit(Color.white, 0.2f, 0f, ProcTex.Painting(seed), Vector2.one);
            SalonUtil.Prim(PrimitiveType.Quad, root, new Vector3(0, 0, 0.01f), new Vector3(0.6f, 0.75f, 1), art, Quaternion.Euler(0, 180, 0), false, "Toile");
            SalonUtil.Box(root, new Vector3(0, 0.41f, 0), new Vector3(0.72f, 0.07f, 0.04f), gold, false, "Cadre");
            SalonUtil.Box(root, new Vector3(0, -0.41f, 0), new Vector3(0.72f, 0.07f, 0.04f), gold, false, "Cadre");
            SalonUtil.Box(root, new Vector3(0.33f, 0, 0), new Vector3(0.07f, 0.75f, 0.04f), gold, false, "Cadre");
            SalonUtil.Box(root, new Vector3(-0.33f, 0, 0), new Vector3(0.07f, 0.75f, 0.04f), gold, false, "Cadre");
        }

        // ------------------------------------------------------------ Lumières
        void BuildLights()
        {
            var key = new GameObject("LumierePrincipale").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Directional;
            key.color = SalonUtil.Hex("#FFEBD2");
            key.intensity = 1.15f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.6f;
            key.transform.rotation = Quaternion.Euler(38f, 200f, 0f);
            KeyLight = key;

            var fill = new GameObject("LumiereFenetre").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.type = LightType.Point;
            fill.color = SalonUtil.Hex("#C9D8FF");
            fill.intensity = 1.6f;
            fill.range = 5.5f;
            fill.transform.position = new Vector3(0, 1.9f, -D + 0.6f);
            FillLight = fill;

            var lustre = new GameObject("LumiereLustre").AddComponent<Light>();
            lustre.transform.SetParent(transform, false);
            lustre.type = LightType.Point;
            lustre.color = SalonUtil.Hex("#FFD9A8");
            lustre.intensity = 1.4f;
            lustre.range = 6f;
            lustre.transform.position = new Vector3(0, H - 0.8f, 0.5f);

            var vanity = new GameObject("LumiereMiroir").AddComponent<Light>();
            vanity.transform.SetParent(transform, false);
            vanity.type = LightType.Point;
            vanity.color = SalonUtil.Hex("#FFF0DC");
            vanity.intensity = 1.1f;
            vanity.range = 3f;
            vanity.transform.position = new Vector3(ChairPos.x, 1.9f, D - 0.5f);

            Color amb = SalonUtil.Hex("#8C7F7A");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = amb;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(amb.linear);
            RenderSettings.ambientProbe = sh;
            RenderSettings.fog = false;

            // Sonde de réflexion (dorures et miroir)
            var probeGo = new GameObject("SondeReflexion");
            probeGo.transform.SetParent(transform, false);
            probeGo.transform.position = new Vector3(0, 1.5f, 0.5f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(2 * W, H, 2 * D);
            probe.center = new Vector3(0, 0.1f, -0.5f);
            probe.boxProjection = true;
            probe.resolution = 256;
            probe.RenderProbe();
        }
    }
}
