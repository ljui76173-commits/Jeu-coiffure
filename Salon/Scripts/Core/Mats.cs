using UnityEngine;
using UnityEngine.Rendering;

namespace SalonCoiffure
{
    /// <summary>Fabrique de matériaux compatible URP (et pipeline intégré en secours).</summary>
    public static class Mats
    {
        static Shader lit, unlit, hair;
        static Material litTemplate, emissiveTemplate, unlitTemplate, hairTemplate;
        static bool urp;
        public static bool IsURP { get { return urp; } }

        public static void Init()
        {
            urp = GraphicsSettings.currentRenderPipeline != null;
            lit = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            if (lit == null) lit = Shader.Find("Standard");
            unlit = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Texture");
            if (unlit == null) unlit = Shader.Find("Unlit/Texture");
            // Modèles créés par le menu « Salon de Coiffure » (garantissent l'inclusion des shaders dans les builds)
            litTemplate = Resources.Load<Material>("SalonMaterials/SalonLit");
            emissiveTemplate = Resources.Load<Material>("SalonMaterials/SalonLitEmissive");
            unlitTemplate = Resources.Load<Material>("SalonMaterials/SalonUnlit");
            hairTemplate = Resources.Load<Material>("SalonMaterials/SalonHair");
            if (litTemplate != null && litTemplate.shader != null && litTemplate.shader.isSupported) lit = litTemplate.shader; else litTemplate = null;
            if (unlitTemplate != null && unlitTemplate.shader != null && unlitTemplate.shader.isSupported) unlit = unlitTemplate.shader; else unlitTemplate = null;
            if (emissiveTemplate != null && emissiveTemplate.shader != lit) emissiveTemplate = null;
            hair = Shader.Find("Salon/Hair");
            if (hairTemplate != null && hairTemplate.shader != null) hair = hairTemplate.shader; else hairTemplate = null;
            if (hair == null || !hair.isSupported)
            {
                Debug.LogWarning("[Salon] Shader 'Salon/Hair' introuvable : rendu de secours pour les cheveux.");
                hair = null;
            }
        }

        public static Material Lit(Color c, float smoothness = 0.3f, float metallic = 0f, Texture tex = null, Vector2? tiling = null)
        {
            var m = litTemplate != null ? new Material(litTemplate) : new Material(lit);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTexture("_MainTex", tex);
                Vector2 t = tiling.HasValue ? tiling.Value : Vector2.one;
                m.SetTextureScale("_BaseMap", t);
                m.SetTextureScale("_MainTex", t);
            }
            m.enableInstancing = true;
            return m;
        }

        public static Material Emissive(Color baseColor, Color emission)
        {
            var m = Lit(baseColor, 0.5f);
            if (emissiveTemplate != null) m.CopyPropertiesFromMaterial(emissiveTemplate);
            m.SetColor("_BaseColor", baseColor);
            m.SetColor("_Color", baseColor);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        public static Material Unlit(Texture tex, Color tint)
        {
            var m = unlitTemplate != null ? new Material(unlitTemplate) : new Material(unlit);
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_Color", tint);
            return m;
        }

        /// <summary>Matériau des cheveux (shader anisotrope). Repli sur un Lit si le shader manque.</summary>
        public static Material Hair()
        {
            if (hairTemplate != null) return new Material(hairTemplate);
            if (hair != null) return new Material(hair);
            return Lit(Color.white, 0.45f);
        }
    }
}
