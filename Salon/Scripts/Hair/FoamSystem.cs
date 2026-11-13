using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Bulles de mousse et gouttes d'eau de la douchette.</summary>
    public class FoamSystem : MonoBehaviour
    {
        class Drop { public Transform tr; public Vector3 vel; public float life; }

        Material bubbleMat, waterMat;
        readonly List<Transform> bubbles = new List<Transform>();
        readonly List<Drop> drops = new List<Drop>();
        readonly Stack<Transform> dropPool = new Stack<Transform>();
        public int maxBubbles = 320;

        public void Init()
        {
            bubbleMat = Mats.Lit(new Color(0.97f, 0.98f, 1f), 0.85f);
            waterMat = Mats.Lit(new Color(0.75f, 0.85f, 0.95f), 0.95f);
        }

        public void SpawnBubble(Vector3 world, Transform parent)
        {
            if (bubbles.Count >= maxBubbles) return;
            float s = Random.Range(0.007f, 0.019f);
            var go = SalonUtil.Prim(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * s, bubbleMat, false, "Bulle");
            go.transform.SetParent(parent, true);
            go.transform.position = world + Random.insideUnitSphere * 0.012f;
            bubbles.Add(go.transform);
        }

        /// <summary>Rince les bulles proches du rayon.</summary>
        public void RinseNear(Ray ray, float radius)
        {
            float r2 = radius * radius * 2.2f;
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                if (b == null) { bubbles.RemoveAt(i); continue; }
                Vector3 v = b.position - ray.origin;
                float t = Vector3.Dot(v, ray.direction);
                if (t < 0 || v.sqrMagnitude - t * t > r2) continue;
                b.localScale *= 0.85f;
                if (b.localScale.x < 0.0035f) { Destroy(b.gameObject); bubbles.RemoveAt(i); }
            }
        }

        public void ClearBubbles()
        {
            foreach (var b in bubbles) if (b != null) Destroy(b.gameObject);
            bubbles.Clear();
        }

        public int BubbleCount { get { return bubbles.Count; } }

        /// <summary>Jet d'eau de la douchette vers un point.</summary>
        public void Spray(Vector3 from, Vector3 to)
        {
            for (int k = 0; k < 2; k++)
            {
                Transform tr = dropPool.Count > 0 ? dropPool.Pop() : SalonUtil.Prim(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.004f, waterMat, false, "Goutte").transform;
                tr.gameObject.SetActive(true);
                tr.position = from + Random.insideUnitSphere * 0.01f;
                Vector3 dir = (to - from).normalized;
                var d = new Drop { tr = tr, vel = (dir + Random.insideUnitSphere * 0.12f) * 1.6f, life = 0.45f };
                drops.Add(d);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                d.life -= dt;
                d.vel.y -= 4f * dt;
                d.tr.position += d.vel * dt;
                if (d.life <= 0)
                {
                    d.tr.gameObject.SetActive(false);
                    dropPool.Push(d.tr);
                    drops.RemoveAt(i);
                }
            }
        }
    }
}
