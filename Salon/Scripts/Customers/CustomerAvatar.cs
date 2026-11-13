using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>
    /// Buste de la cliente (style « réaliste mais mignon ») : tête, visage, cou, cape de coiffure,
    /// cuir chevelu et chevelure. Le pivot est au niveau de la poitrine.
    /// </summary>
    public class CustomerAvatar : MonoBehaviour
    {
        public static readonly Vector3 HeadOffset = new Vector3(0f, 0.40f, 0.01f);

        public Transform Head { get; private set; }
        public HairSystem Hair { get; private set; }
        public CustomerProfile Profile { get; private set; }

        Transform eyeL, eyeR;
        Material scalpMat, browMat;
        float blinkTimer = 2f, blinkT = -1f;
        float idleT;

        public static CustomerAvatar Create(CustomerProfile profile, Transform parent, int strands, int segments)
        {
            var go = new GameObject("Cliente_" + profile.firstName);
            go.transform.SetParent(parent, false);
            var av = go.AddComponent<CustomerAvatar>();
            av.Build(profile, strands, segments);
            return av;
        }

        void Build(CustomerProfile p, int strands, int segments)
        {
            Profile = p;
            Color skin = p.skin;
            var skinMat = Mats.Lit(skin, 0.38f);
            var capeMat = Mats.Lit(SalonUtil.Hex("#25222B"), 0.62f);
            var collarMat = Mats.Lit(SalonUtil.Hex("#F4EFE8"), 0.2f);

            // Corps (cape de coiffure)
            SalonUtil.Prim(PrimitiveType.Sphere, transform, Vector3.zero, new Vector3(0.42f, 0.30f, 0.24f), capeMat, true, "Epaules");
            SalonUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, -0.33f, 0f), new Vector3(0.44f, 0.35f, 0.30f), capeMat, true, "Cape");
            SalonUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.155f, 0.005f), new Vector3(0.125f, 0.018f, 0.115f), collarMat, true, "Col");
            SalonUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.25f, 0.0f), new Vector3(0.095f, 0.11f, 0.09f), skinMat, true, "Cou");

            // Tête
            var head = new GameObject("Tete").transform;
            head.SetParent(transform, false);
            head.localPosition = HeadOffset;
            Head = head;
            var headMesh = ProcMesh.Ellipsoid(HeadShape.R, 40, 64, HeadShape.Sculpt);
            ProcMesh.MeshObject("Visage", head, headMesh, skinMat);

            // Cuir chevelu (couleur des cheveux sous les mèches)
            scalpMat = Mats.Lit(SalonUtil.Mul(p.naturalHair.color, 0.85f), 0.2f);
            var scalpMesh = ProcMesh.Ellipsoid(HeadShape.R * 1.006f, 40, 64, HeadShape.Sculpt, HairSystem.AcceptScalp);
            ProcMesh.MeshObject("CuirChevelu", head, scalpMesh, scalpMat);

            BuildFace(head, p, skin);

            // Chevelure
            var hairGo = new GameObject("Chevelure");
            hairGo.transform.SetParent(head, false);
            Hair = hairGo.AddComponent<HairSystem>();
            Hair.Setup(p.HairConfig(), strands, segments, Mats.Hair());
        }

        void BuildFace(Transform head, CustomerProfile p, Color skin)
        {
            var white = Mats.Lit(SalonUtil.Hex("#FBF8F4"), 0.7f);
            var iris = Mats.Lit(p.eyes, 0.8f);
            var pupil = Mats.Lit(SalonUtil.Hex("#0D0A0A"), 0.9f);
            var shine = Mats.Emissive(Color.white, new Color(0.9f, 0.9f, 0.9f));
            var lash = Mats.Lit(SalonUtil.Hex("#1C1513"), 0.3f);
            browMat = Mats.Lit(SalonUtil.Mul(p.naturalHair.color, 0.8f), 0.2f);
            var lip = Mats.Lit(Color.Lerp(skin, SalonUtil.Hex("#C9646E"), 0.55f), 0.55f);
            var blush = Mats.Lit(Color.Lerp(skin, SalonUtil.Hex("#F08C8C"), 0.28f), 0.3f);
            var nose = Mats.Lit(Color.Lerp(skin, SalonUtil.Hex("#E7A08F"), 0.12f), 0.35f);
            var skinMat = Mats.Lit(skin, 0.38f);

            eyeL = Eye(head, -1, white, iris, pupil, shine, lash);
            eyeR = Eye(head, 1, white, iris, pupil, shine, lash);

            // Sourcils
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 n;
                Vector3 pos = HeadShape.FrontSurface(sgn * 0.035f, 0.033f, out n);
                var rot = Quaternion.LookRotation(n) * Quaternion.Euler(0, 0, 90 + sgn * 8);
                SalonUtil.Prim(PrimitiveType.Capsule, head, pos + n * 0.001f, new Vector3(0.0055f, 0.017f, 0.004f), browMat, rot, false, "Sourcil");
            }

            // Nez
            {
                Vector3 n;
                Vector3 pos = HeadShape.FrontSurface(0f, -0.022f, out n);
                SalonUtil.Prim(PrimitiveType.Sphere, head, pos - n * 0.003f, new Vector3(0.017f, 0.021f, 0.02f), nose, Quaternion.LookRotation(n), true, "Nez");
            }

            // Bouche
            {
                Vector3 n;
                Vector3 pos = HeadShape.FrontSurface(0f, -0.051f, out n);
                var rot = Quaternion.LookRotation(n);
                SalonUtil.Prim(PrimitiveType.Sphere, head, pos - n * 0.002f, new Vector3(0.028f, 0.0075f, 0.012f), lip, rot, false, "LevreHaut");
                pos = HeadShape.FrontSurface(0f, -0.059f, out n);
                SalonUtil.Prim(PrimitiveType.Sphere, head, pos - n * 0.002f, new Vector3(0.025f, 0.0095f, 0.012f), lip, Quaternion.LookRotation(n), false, "LevreBas");
            }

            // Joues roses
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 n;
                Vector3 pos = HeadShape.FrontSurface(sgn * 0.05f, -0.028f, out n);
                SalonUtil.Prim(PrimitiveType.Sphere, head, pos - n * 0.0028f, new Vector3(0.03f, 0.019f, 0.006f), blush, Quaternion.LookRotation(n), false, "Joue");
            }

            // Oreilles
            for (int sgn = -1; sgn <= 1; sgn += 2)
                SalonUtil.Prim(PrimitiveType.Sphere, head, new Vector3(sgn * HeadShape.R.x * 0.96f, -0.01f, -0.005f),
                    new Vector3(0.02f, 0.036f, 0.027f), skinMat, true, "Oreille");
        }

        Transform Eye(Transform head, int sgn, Material white, Material iris, Material pupil, Material shine, Material lash)
        {
            Vector3 n;
            Vector3 pos = HeadShape.FrontSurface(sgn * 0.034f, 0.006f, out n);
            var eye = new GameObject(sgn < 0 ? "OeilG" : "OeilD").transform;
            eye.SetParent(head, false);
            eye.localPosition = pos - n * 0.004f;
            eye.localRotation = Quaternion.LookRotation(n);
            SalonUtil.Prim(PrimitiveType.Sphere, eye, Vector3.zero, new Vector3(0.027f, 0.021f, 0.013f), white, false, "Blanc");
            SalonUtil.Prim(PrimitiveType.Sphere, eye, new Vector3(0, -0.0005f, 0.0042f), new Vector3(0.0155f, 0.0165f, 0.007f), iris, false, "Iris");
            SalonUtil.Prim(PrimitiveType.Sphere, eye, new Vector3(0, -0.0005f, 0.0062f), new Vector3(0.0075f, 0.0085f, 0.004f), pupil, false, "Pupille");
            SalonUtil.Prim(PrimitiveType.Sphere, eye, new Vector3(-0.003f, 0.003f, 0.0078f), new Vector3(0.0038f, 0.0038f, 0.002f), shine, false, "Reflet");
            // Cils (paupière supérieure)
            SalonUtil.Prim(PrimitiveType.Capsule, eye, new Vector3(0, 0.0085f, 0.0035f), new Vector3(0.0032f, 0.0155f, 0.003f), lash,
                Quaternion.Euler(0, 0, 90 - sgn * 6), false, "Cils");
            return eye;
        }

        public void SetScalpColor(Color hairColor)
        {
            if (scalpMat == null) return;
            Color c = SalonUtil.Mul(hairColor, 0.85f);
            scalpMat.SetColor("_BaseColor", c);
            scalpMat.SetColor("_Color", c);
        }

        void Update()
        {
            // Clignement des yeux
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0 && blinkT < 0) { blinkT = 0; blinkTimer = Random.Range(2.5f, 5.5f); }
            if (blinkT >= 0)
            {
                blinkT += Time.deltaTime;
                float k = blinkT < 0.07f ? 1f - blinkT / 0.07f : (blinkT < 0.14f ? (blinkT - 0.07f) / 0.07f : 1f);
                float sy = Mathf.Lerp(0.1f, 1f, k);
                if (eyeL != null) eyeL.localScale = new Vector3(1, sy, 1);
                if (eyeR != null) eyeR.localScale = new Vector3(1, sy, 1);
                if (blinkT >= 0.14f) blinkT = -1;
            }
            idleT += Time.deltaTime;
        }

        /// <summary>Petite respiration (décalage à ajouter à la pose).</summary>
        public float Breath { get { return Mathf.Sin(idleT * 1.6f) * 0.0025f; } }

        void OnDestroy()
        {
            // Libère les maillages générés
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && (mf.sharedMesh.name == "" || mf.gameObject.name == "Visage" || mf.gameObject.name == "CuirChevelu"))
                    Destroy(mf.sharedMesh);
        }
    }
}
