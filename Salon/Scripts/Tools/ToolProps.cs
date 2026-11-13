using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>
    /// Outils en 3D qui suivent le doigt / la souris sur la chevelure : ciseaux qui s'ouvrent et se ferment,
    /// tondeuse qui vibre, douchette, flacons, pinceau teinté, sèche-cheveux.
    /// </summary>
    public class ToolProps : MonoBehaviour
    {
        readonly Dictionary<ToolType, Transform> props = new Dictionary<ToolType, Transform>();
        Transform bladeA, bladeB, brushTip;
        Material brushMat;
        ToolType shown = ToolType.Main;
        Vector3 smoothPos;
        Quaternion smoothRot = Quaternion.identity;
        float snip, buzz;
        bool visible;

        public void Init()
        {
            var gold = Mats.Lit(SalonMath.Hex("#D8B56A"), 0.75f, 1f);
            var chrome = Mats.Lit(SalonMath.Hex("#D7DADF"), 0.88f, 1f);
            var black = Mats.Lit(SalonMath.Hex("#1F1D22"), 0.6f);
            var rose = Mats.Lit(SalonMath.Hex("#E7B7B0"), 0.55f, 0.2f);
            var rosegold = Mats.Lit(SalonMath.Hex("#E3A995"), 0.7f, 0.9f);
            var white = Mats.Lit(SalonMath.Hex("#F6F2EC"), 0.3f);
            var mint = Mats.Lit(SalonMath.Hex("#A9D3C2"), 0.6f);
            brushMat = Mats.Lit(Color.white, 0.4f);

            // Ciseaux (2 lames qui pivotent)
            {
                var root = New(ToolType.Ciseaux);
                bladeA = Blade(root, gold, chrome, 1);
                bladeB = Blade(root, gold, chrome, -1);
                SalonUtil.Prim(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.008f, 0.004f, 0.008f), gold, Quaternion.Euler(90, 0, 0), false, "Vis");
            }
            // Tondeuse
            {
                var root = New(ToolType.Tondeuse);
                SalonUtil.Prim(PrimitiveType.Capsule, root, new Vector3(0, 0, -0.06f), new Vector3(0.034f, 0.06f, 0.026f), black, Quaternion.Euler(90, 0, 0), false, "Corps");
                SalonUtil.Prim(PrimitiveType.Cube, root, new Vector3(0, 0, 0.004f), new Vector3(0.036f, 0.022f, 0.008f), chrome, false, "Lame");
                SalonUtil.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.012f, -0.05f), new Vector3(0.012f, 0.004f, 0.02f), gold, false, "Bouton");
            }
            // Douchette
            {
                var root = New(ToolType.Douchette);
                SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0, -0.012f), new Vector3(0.05f, 0.008f, 0.05f), chrome, Quaternion.Euler(90, 0, 0), false, "Pomme");
                SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, -0.045f, -0.03f), new Vector3(0.016f, 0.05f, 0.016f), chrome, Quaternion.Euler(-25, 0, 0), false, "Manche");
                SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0, -0.007f), new Vector3(0.042f, 0.002f, 0.042f), Mats.Lit(SalonMath.Hex("#9FA6AE"), 0.6f, 0.5f), Quaternion.Euler(90, 0, 0), false, "Grille");
            }
            // Shampoing (flacon rose) et soin (pot menthe)
            {
                var root = New(ToolType.Shampoing);
                SalonUtil.Prim(PrimitiveType.Capsule, root, new Vector3(0, -0.02f, -0.03f), new Vector3(0.04f, 0.045f, 0.03f), rose, false, "Flacon");
                SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.03f, -0.03f), new Vector3(0.016f, 0.012f, 0.016f), gold, false, "Bouchon");
                SalonUtil.Prim(PrimitiveType.Cube, root, new Vector3(0, -0.02f, -0.014f), new Vector3(0.026f, 0.03f, 0.002f), white, false, "Etiquette");
            }
            {
                var root = New(ToolType.Soin);
                SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, -0.015f, -0.03f), new Vector3(0.05f, 0.018f, 0.05f), mint, false, "Pot");
                SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.006f, -0.03f), new Vector3(0.053f, 0.006f, 0.053f), gold, false, "Couvercle");
            }
            // Serviette
            {
                var root = New(ToolType.Serviette);
                SalonUtil.Prim(PrimitiveType.Cube, root, new Vector3(0, 0, -0.02f), new Vector3(0.11f, 0.08f, 0.025f), white, false, "Serviette");
                SalonUtil.Prim(PrimitiveType.Cube, root, new Vector3(0, -0.03f, -0.006f), new Vector3(0.112f, 0.008f, 0.004f), Mats.Lit(SalonMath.Hex("#D9A5A0"), 0.3f), false, "Liseré");
            }
            // Pinceau de coloration
            {
                var root = New(ToolType.Pinceau);
                SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0, -0.07f), new Vector3(0.008f, 0.055f, 0.008f), black, Quaternion.Euler(90, 0, 0), false, "Manche");
                SalonUtil.Prim(PrimitiveType.Cube, root, new Vector3(0, 0, -0.012f), new Vector3(0.024f, 0.004f, 0.01f), chrome, false, "Virole");
                brushTip = SalonUtil.Prim(PrimitiveType.Cube, root, new Vector3(0, 0, -0.002f), new Vector3(0.022f, 0.003f, 0.016f), brushMat, false, "Poils").transform;
            }
            // Sèche-cheveux
            {
                var root = New(ToolType.Seche);
                SalonUtil.Prim(PrimitiveType.Capsule, root, new Vector3(0, 0, -0.1f), new Vector3(0.06f, 0.06f, 0.06f), rosegold, Quaternion.Euler(90, 0, 0), false, "Corps");
                SalonUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0, -0.035f), new Vector3(0.036f, 0.018f, 0.036f), black, Quaternion.Euler(90, 0, 0), false, "Embout");
                SalonUtil.Prim(PrimitiveType.Capsule, root, new Vector3(0, -0.07f, -0.115f), new Vector3(0.028f, 0.05f, 0.028f), rosegold, Quaternion.Euler(-12, 0, 0), false, "Poignee");
            }
            foreach (var kv in props) kv.Value.gameObject.SetActive(false);
        }

        Transform New(ToolType t)
        {
            var go = new GameObject("Outil_" + t);
            go.transform.SetParent(transform, false);
            props[t] = go.transform;
            return go.transform;
        }

        Transform Blade(Transform root, Material gold, Material steel, int side)
        {
            var pivot = new GameObject(side > 0 ? "LameA" : "LameB").transform;
            pivot.SetParent(root, false);
            // lame vers l'avant (+Z), anneau vers l'arrière (-Z)
            SalonUtil.Prim(PrimitiveType.Cube, pivot, new Vector3(side * 0.002f, 0.001f * side, 0.032f), new Vector3(0.006f, 0.0018f, 0.064f), steel, Quaternion.Euler(0, side * 2f, 0), false, "Lame");
            SalonUtil.Prim(PrimitiveType.Cube, pivot, new Vector3(side * 0.004f, 0.001f * side, -0.02f), new Vector3(0.006f, 0.003f, 0.04f), gold, false, "Branche");
            var ring = ProcMesh.MeshObject("Anneau", pivot, UnityConvert.ToMesh(MeshData.Torus(0.012f, 0.0028f, 24, 8)), gold, false);
            ring.transform.localPosition = new Vector3(side * 0.012f, 0.001f * side, -0.046f);
            ring.transform.localRotation = Quaternion.Euler(0, 0, 0);
            return pivot;
        }

        public void Snip() { snip = 1f; }

        /// <summary>Place l'outil près du point visé, orienté vers la tête.</summary>
        public void Show(ToolType t, Vector3 hit, Vector3 camPos, Color dye, bool active)
        {
            if (t == ToolType.Main) { Hide(); return; }
            if (shown != t)
            {
                foreach (var kv in props) kv.Value.gameObject.SetActive(kv.Key == t);
                shown = t;
                visible = false;
            }
            Vector3 toCam = (camPos - hit).normalized;
            float back = t == ToolType.Seche ? 0.09f : (t == ToolType.Douchette ? 0.07f : (t == ToolType.Serviette ? 0.02f : 0.012f));
            Vector3 pos = hit + toCam * back;
            Quaternion rot = Quaternion.LookRotation(-toCam, Vector3.up);
            if (t == ToolType.Ciseaux) rot *= Quaternion.Euler(0, 0, 90f) * Quaternion.Euler(0, -25f, 0);
            if (!visible) { smoothPos = pos; smoothRot = rot; visible = true; }
            float k = 1f - Mathf.Exp(-Time.deltaTime * 22f);
            smoothPos = Vector3.Lerp(smoothPos, pos, k);
            smoothRot = Quaternion.Slerp(smoothRot, rot, k);
            Transform tr;
            if (!props.TryGetValue(t, out tr)) return;
            tr.gameObject.SetActive(true);
            buzz += Time.deltaTime * 60f;
            Vector3 jitter = (t == ToolType.Tondeuse || t == ToolType.Seche) && active ? Random.insideUnitSphere * 0.0007f : Vector3.zero;
            tr.position = smoothPos + jitter;
            tr.rotation = smoothRot;
            if (t == ToolType.Pinceau && brushMat != null)
            {
                brushMat.SetColor("_BaseColor", dye);
                brushMat.SetColor("_Color", dye);
            }
        }

        public void Hide()
        {
            if (shown != ToolType.Main && props.ContainsKey(shown)) props[shown].gameObject.SetActive(false);
            shown = ToolType.Main;
            visible = false;
        }

        void Update()
        {
            // ouverture / fermeture des ciseaux
            snip = Mathf.MoveTowards(snip, 0f, Time.deltaTime * 7f);
            float open = Mathf.Lerp(16f, 1f, Mathf.Sin(snip * Mathf.PI));
            if (bladeA != null) bladeA.localRotation = Quaternion.Euler(0, open, 0);
            if (bladeB != null) bladeB.localRotation = Quaternion.Euler(0, -open, 0);
        }
    }
}
