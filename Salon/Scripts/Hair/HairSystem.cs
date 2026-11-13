using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SalonCoiffure
{
    /// <summary>
    /// Chevelure dans Unity : enveloppe HairCore (calculs purs) et gère le maillage, le matériau
    /// et la conversion monde ↔ tête des rayons d'outils.
    /// </summary>
    public class HairSystem : MonoBehaviour
    {
        public static string[] ZoneNames { get { return HairCore.ZoneNames; } }
        static Texture2D cardTex;

        public HairCore Core { get; private set; }
        public Material Material { get; private set; }
        Mesh mesh;
        readonly List<CutPiece> pieces = new List<CutPiece>();

        public static Texture2D CardTexture
        {
            get
            {
                if (cardTex == null) cardTex = UnityConvert.ToHairCardTexture(HairTex.Card());
                return cardTex;
            }
        }

        public void Setup(HairConfig cfg, int strands, int segments, Material mat)
        {
            Material = mat;
            Material.SetTexture("_HairTex", CardTexture);
            Core = new HairCore();
            Core.Setup(cfg, strands, segments, QualitySettings.activeColorSpace == ColorSpace.Linear);

            mesh = new Mesh { name = "Chevelure" };
            if (Core.mv.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.MarkDynamic();
            Upload();
            mesh.triangles = Core.tris;
            mesh.SetUVs(1, Core.uv1);
            mesh.bounds = new Bounds(new Vector3(0, -0.3f, 0), new Vector3(1.2f, 1.4f, 1.2f));

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void Upload()
        {
            mesh.SetVertices(Core.mv);
            mesh.SetNormals(Core.mn);
            mesh.SetTangents(Core.mt);
            mesh.SetColors(Core.mc);
            mesh.SetUVs(0, Core.uv0);
            mesh.SetUVs(2, Core.uv2);
        }

        public void Refresh()
        {
            if (Core != null && Core.UpdateDirty()) Upload();
        }

        void LateUpdate() { Refresh(); }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (Material != null) Destroy(Material);
        }

        public void UpdateGravity()
        {
            Core.SetGravity(transform.InverseTransformDirection(Vector3.down));
        }

        HairCore.Brush Brush(Ray ray, float radius)
        {
            return HairCore.MakeBrush(transform.InverseTransformPoint(ray.origin), transform.InverseTransformDirection(ray.direction), radius);
        }

        public bool RayHitsHead(Ray ray, float margin)
        {
            return HairCore.RayHitsHead(transform.InverseTransformPoint(ray.origin), transform.InverseTransformDirection(ray.direction), margin);
        }

        public bool Probe(Ray ray, float radius, out Vector3 world)
        {
            Vector3 local;
            bool ok = Core.Probe(Brush(ray, radius), out local);
            world = transform.TransformPoint(local);
            return ok;
        }

        void Emit(HairClippings clips)
        {
            if (clips != null && pieces.Count > 0) clips.AddPieces(pieces, transform, Material);
            pieces.Clear();
        }

        public int Cut(Ray ray, float radius, int zoneFilter, HairClippings clips)
        {
            int n = Core.Cut(Brush(ray, radius), zoneFilter, pieces);
            Emit(clips);
            return n;
        }

        public int Clip(Ray ray, float radius, int zoneFilter, float sabot, HairClippings clips)
        {
            int n = Core.Clip(Brush(ray, radius), zoneFilter, sabot, pieces);
            Emit(clips);
            return n;
        }

        public int CutToHeight(int zoneFilter, float height, HairClippings clips)
        {
            int n = Core.CutToHeight(zoneFilter, height, pieces);
            Emit(clips);
            return n;
        }

        public int Water(Ray ray, float radius, float amount, float develop) { return Core.Water(Brush(ray, radius), amount, develop); }

        readonly List<Vector3> localHits = new List<Vector3>();
        public int Lather(Ray ray, float radius, float amount, List<Vector3> hitWorld)
        {
            localHits.Clear();
            int n = Core.Lather(Brush(ray, radius), amount, localHits);
            if (hitWorld != null) foreach (var p in localHits) hitWorld.Add(transform.TransformPoint(p));
            return n;
        }

        public int Condition(Ray ray, float radius, float amount) { return Core.Condition(Brush(ray, radius), amount); }
        public int Dry(Ray ray, float radius, float amount, float minWet) { return Core.Dry(Brush(ray, radius), amount, minWet); }
        public int Paint(Ray ray, float radius, int zoneFilter, Color dye, float amount) { return Core.Paint(Brush(ray, radius), zoneFilter, dye, amount); }

        public float AverageWet() { return Core.AverageWet(); }
        public float AverageFoam() { return Core.AverageFoam(); }
        public float AverageCare() { return Core.AverageCare(); }
        public float PasteCoverage() { return Core.PasteCoverage(); }
        public bool HasPaste() { return Core.HasPaste(); }
        public Color AverageRootColor() { return Core.AverageRootColor(); }
        public float RootWidth { get { return Core.RootWidth; } }

        public void SetWind(Vector3 dir, float amplitude, float speed)
        {
            if (Material == null) return;
            Material.SetVector("_HairWind", new Vector4(dir.x, dir.y, dir.z, amplitude));
            Material.SetVector("_HairWindParams", new Vector4(speed, 0, 0, 0));
        }
    }
}
