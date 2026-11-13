using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Cache des icônes générées (IconGen) en textures Unity.</summary>
    public static class Icons
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string name)
        {
            Texture2D t;
            if (!cache.TryGetValue(name, out t) || t == null)
            {
                t = UnityConvert.ToTexture(IconGen.Render(name, 128), true, true);
                cache[name] = t;
            }
            return t;
        }

        public static void Draw(Rect r, string name, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Get(name), ScaleMode.ScaleToFit);
            GUI.color = old;
        }
    }

    /// <summary>Repères dorés autour de la tête : hauteur de coupe demandée par zone.</summary>
    public class CutGuides : MonoBehaviour
    {
        readonly List<LineRenderer> lines = new List<LineRenderer>();
        Material mat;

        public void Build(Transform head, HairRequest req)
        {
            Clear();
            if (mat == null) mat = Mats.Unlit(Texture2D.whiteTexture, new Color(1f, 0.82f, 0.4f));
            transform.SetParent(head, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            // secteurs (angle autour de Y, 0 = face) pour chaque zone
            float[][] sectors =
            {
                new[] { -55f, 55f },                       // devant
                new[] { 50f, 125f, -125f, -50f },          // côtés (deux arcs)
                new[] { -180f, 180f },                     // dessus (anneau complet, discret)
                new[] { 120f, 240f },                      // nuque
            };
            for (int z = 0; z < 4; z++)
            {
                var t = req.zones[z];
                if (t.kind != TargetKind.Hauteur) continue;
                var sec = sectors[z];
                for (int k = 0; k < sec.Length; k += 2)
                    lines.Add(Arc(t.value, sec[k], sec[k + 1], z == 2 ? 0.0012f : 0.0024f));
            }
        }

        LineRenderer Arc(float y, float a0, float a1, float width)
        {
            var go = new GameObject("Repere");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = mat;
            lr.widthMultiplier = width;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            int n = 40;
            lr.positionCount = n + 1;
            float rx, rz;
            if (BodyGen.BodyAt(y, out rx, out rz)) { rx += 0.035f; rz += 0.035f; }
            else
            {
                float k = Mathf.Clamp01(1f - (y / HeadGen.HairR.y) * (y / HeadGen.HairR.y));
                rx = Mathf.Max(0.085f, HeadGen.HairR.x * Mathf.Sqrt(k) + 0.03f);
                rz = Mathf.Max(0.09f, HeadGen.HairR.z * Mathf.Sqrt(k) + 0.04f);
            }
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, (float)i / n) * Mathf.Deg2Rad;
                lr.SetPosition(i, new Vector3(Mathf.Sin(a) * rx, y, Mathf.Cos(a) * rz));
            }
            return lr;
        }

        public void SetVisible(bool v)
        {
            foreach (var l in lines) if (l != null) l.enabled = v;
        }

        public void Clear()
        {
            foreach (var l in lines) if (l != null) Destroy(l.gameObject);
            lines.Clear();
        }
    }
}
