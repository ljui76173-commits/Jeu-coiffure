using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>
    /// Cliente en buste : visage sculpté (HeadGen), yeux qui suivent la caméra et clignent,
    /// expressions (sourire / moue), cou, cape de coiffure et chevelure.
    /// Le pivot est au niveau de la poitrine ; la tête est à HeadOffset.
    /// </summary>
    public class CustomerAvatar : MonoBehaviour
    {
        public static readonly Vector3 HeadOffset = new Vector3(0f, 0.40f, 0.01f);

        public Transform Head { get; private set; }
        public HairSystem Hair { get; private set; }
        public CustomerProfile Profile { get; private set; }

        Transform eyeL, eyeR, lidL, lidR;
        Quaternion eyeBaseL, eyeBaseR;
        MeshFilter faceFilter;
        HeadResult head;
        readonly List<Object> owned = new List<Object>();
        float blinkTimer = 2f, blinkT = -1f, idleT;
        float smileNow, smileTarget, exprT = 1f;
        Vector3[] exprFrom, exprTo, exprCur;
        float lidOpen = -38f, lidClosed = 46f;

        public static CustomerAvatar Create(CustomerProfile profile, Transform parent, int strands, int segments)
        {
            var go = new GameObject("Cliente_" + profile.firstName);
            go.transform.SetParent(parent, false);
            var av = go.AddComponent<CustomerAvatar>();
            av.Build(profile, strands, segments);
            return av;
        }

        T Own<T>(T o) where T : Object { owned.Add(o); return o; }

        GameObject Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, bool shadows = true)
        {
            var go = ProcMesh.MeshObject(name, parent, mesh, mat, shadows);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            return go;
        }

        void Build(CustomerProfile p, int strands, int segments)
        {
            Profile = p;
            head = HeadGen.Build(p.face, 0f);

            var skinTex = Own(UnityConvert.ToTexture(head.skinTex, true, true));
            var eyeTex = Own(UnityConvert.ToTexture(head.eyeTex, true, true));
            var skinMat = Own(Mats.Lit(Color.white, 0.42f, 0f, skinTex, Vector2.one));
            var skinPlain = Own(Mats.Lit(p.skin, 0.38f));
            var eyeMat = Own(Mats.Lit(Color.white, 0.9f, 0f, eyeTex, Vector2.one));
            var lashMat = Own(Mats.Lit(SalonMath.Hex("#1A1210"), 0.35f));
            lashMat.SetFloat("_Cull", 0f);
            var capeMat = Own(Mats.Lit(SalonMath.Hex("#24342E"), 0.58f));
            var towelMat = Own(Mats.Lit(SalonMath.Hex("#F4EEE6"), 0.12f));

            // Corps (espace tête, placé à HeadOffset sous le pivot du buste)
            var body = new GameObject("Corps").transform;
            body.SetParent(transform, false);
            body.localPosition = HeadOffset;
            Part("Cou", body, Own(UnityConvert.ToMesh(BodyGen.Neck())), skinPlain, Vector3.zero, Quaternion.identity);
            Part("Cape", body, Own(UnityConvert.ToMesh(BodyGen.Cape(p.seed))), capeMat, Vector3.zero, Quaternion.identity);
            Part("Col", body, Own(UnityConvert.ToMesh(BodyGen.Collar())), towelMat, Vector3.zero, Quaternion.identity);

            // Tête
            Head = new GameObject("Tete").transform;
            Head.SetParent(transform, false);
            Head.localPosition = HeadOffset;
            faceFilter = Part("Visage", Head, Own(UnityConvert.ToMesh(head.head)), skinMat, Vector3.zero, Quaternion.identity).GetComponent<MeshFilter>();
            Part("Oreilles", Head, Own(UnityConvert.ToMesh(head.ears)), skinPlain, Vector3.zero, Quaternion.identity);
            Part("Cils", Head, Own(UnityConvert.ToMesh(head.lashes)), lashMat, Vector3.zero, Quaternion.identity, false);

            var eyeMesh = Own(UnityConvert.ToMesh(head.eyeball));
            var lidMesh = Own(UnityConvert.ToMesh(head.lidUpper));
            eyeL = MakeEye("OeilG", head.eyeL, head.eyeRotL, eyeMesh, eyeMat, lidMesh, skinPlain, out lidL);
            eyeR = MakeEye("OeilD", head.eyeR, head.eyeRotR, eyeMesh, eyeMat, lidMesh, skinPlain, out lidR);
            eyeBaseL = head.eyeRotL; eyeBaseR = head.eyeRotR;

            // Chevelure
            var hairGo = new GameObject("Chevelure");
            hairGo.transform.SetParent(Head, false);
            Hair = hairGo.AddComponent<HairSystem>();
            Hair.Setup(p.HairConfig(), strands, segments, Mats.Hair());
        }

        Transform MakeEye(string name, Vector3 pos, Quaternion rot, Mesh eye, Material eyeMat, Mesh lid, Material lidMat, out Transform lidT)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(Head, false);
            pivot.localPosition = pos;
            pivot.localRotation = rot;
            Part("Globe", pivot, eye, eyeMat, Vector3.zero, Quaternion.identity, false);
            // la paupière ne tourne pas avec le regard : elle est sœur du globe, sous la tête
            var lidPivot = new GameObject(name + "_Paupiere").transform;
            lidPivot.SetParent(Head, false);
            lidPivot.localPosition = pos;
            lidPivot.localRotation = Quaternion.Euler(lidOpen, 0, 0);
            Part("Paupiere", lidPivot, lid, lidMat, Vector3.zero, Quaternion.identity, false);
            lidT = lidPivot;
            return pivot;
        }

        /// <summary>Expression : 1 = grand sourire, 0 = neutre, -1 = moue.</summary>
        public void SetExpression(float smile)
        {
            smile = Mathf.Clamp(smile, -1f, 1f);
            if (Mathf.Abs(smile - smileTarget) < 0.01f) return;
            smileTarget = smile;
            var md = HeadGen.BuildHeadMesh(Profile.face, smile);
            exprFrom = faceFilter.sharedMesh.vertices;
            exprTo = md.v.ToArray();
            exprCur = new Vector3[exprTo.Length];
            exprT = 0f;
        }

        public void SetScalpColor(Color c) { }

        void Update()
        {
            float dt = Time.deltaTime;
            idleT += dt;

            // Clignement
            blinkTimer -= dt;
            if (blinkTimer <= 0 && blinkT < 0) { blinkT = 0; blinkTimer = Random.Range(2.5f, 5.5f); }
            float close = 0;
            if (blinkT >= 0)
            {
                blinkT += dt;
                close = blinkT < 0.07f ? blinkT / 0.07f : (blinkT < 0.16f ? 1f - (blinkT - 0.07f) / 0.09f : 0f);
                if (blinkT >= 0.16f) blinkT = -1;
            }
            float lidAngle = Mathf.Lerp(lidOpen, lidClosed, close);
            if (lidL != null) lidL.localRotation = Quaternion.Euler(lidAngle, 0, 0);
            if (lidR != null) lidR.localRotation = Quaternion.Euler(lidAngle, 0, 0);

            // Regard : suit la caméra (avec limites)
            var cam = Camera.main;
            if (cam != null && eyeL != null)
            {
                Vector3 target = Head.InverseTransformPoint(cam.transform.position);
                Look(eyeL, eyeBaseL, target);
                Look(eyeR, eyeBaseR, target);
            }

            // Respiration / petits mouvements de tête
            if (Head != null)
                Head.localRotation = Quaternion.Euler(Mathf.Sin(idleT * 0.7f) * 1.2f, Mathf.Sin(idleT * 0.43f) * 2f, Mathf.Sin(idleT * 0.31f) * 0.8f);

            // Expression : transition douce entre deux sculptures du visage
            if (exprT < 1f && exprTo != null)
            {
                exprT = Mathf.Min(1f, exprT + dt * 2.5f);
                float k = exprT * exprT * (3 - 2 * exprT);
                for (int i = 0; i < exprTo.Length; i++) exprCur[i] = Vector3.LerpUnclamped(exprFrom[i], exprTo[i], k);
                var m = faceFilter.sharedMesh;
                m.vertices = exprCur;
                m.RecalculateNormals();
                smileNow = Mathf.Lerp(smileNow, smileTarget, k);
            }
        }

        void Look(Transform eye, Quaternion baseRot, Vector3 targetLocal)
        {
            Vector3 dir = (targetLocal - eye.localPosition).normalized;
            Vector3 fwd = baseRot * Vector3.forward;
            float ang = Vector3.Angle(fwd, dir);
            Vector3 lim = ang > 18f ? Vector3.Slerp(fwd, dir, 18f / ang) : dir;
            Quaternion want = Quaternion.LookRotation(lim, Vector3.up);
            eye.localRotation = Quaternion.Slerp(eye.localRotation, want, Time.deltaTime * 8f);
        }

        void OnDestroy()
        {
            foreach (var o in owned) if (o != null) Destroy(o);
        }
    }
}
