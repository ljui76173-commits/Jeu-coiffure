using System;
using System.Reflection;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>
    /// Post-traitement URP créé par le code (tonemapping ACES, bloom doux, vignette, couleurs chaudes,
    /// flou d'arrière-plan). Passe par la réflexion pour ne jamais casser la compilation.
    /// </summary>
    public static class PostFX
    {
        const string Core = "Unity.RenderPipelines.Core.Runtime";
        const string Urp = "Unity.RenderPipelines.Universal.Runtime";

        static object depthOfField;

        public static void Setup(Camera cam)
        {
            try
            {
                var volType = Type.GetType("UnityEngine.Rendering.Volume, " + Core);
                var profType = Type.GetType("UnityEngine.Rendering.VolumeProfile, " + Core);
                if (volType == null || profType == null) return;

                var go = new GameObject("PostTraitement");
                var vol = go.AddComponent(volType);
                SetMember(vol, "isGlobal", true);
                SetMember(vol, "priority", 10f);
                var prof = ScriptableObject.CreateInstance(profType);
                SetMember(vol, "sharedProfile", prof);

                var add = profType.GetMethod("Add", new[] { typeof(Type), typeof(bool) });
                Func<string, object> addComp = n =>
                {
                    var t = Type.GetType("UnityEngine.Rendering.Universal." + n + ", " + Urp);
                    return t != null ? add.Invoke(prof, new object[] { t, true }) : null;
                };

                var tone = addComp("Tonemapping");
                Override(tone, "mode", "ACES");

                var bloom = addComp("Bloom");
                Override(bloom, "intensity", 0.35f);
                Override(bloom, "threshold", 1.05f);
                Override(bloom, "scatter", 0.65f);

                var color = addComp("ColorAdjustments");
                Override(color, "postExposure", 0.35f);
                Override(color, "contrast", 8f);
                Override(color, "saturation", 6f);
                Override(color, "colorFilter", new Color(1f, 0.97f, 0.93f));

                var vig = addComp("Vignette");
                Override(vig, "intensity", 0.24f);
                Override(vig, "smoothness", 0.45f);

                depthOfField = addComp("DepthOfField");
                Override(depthOfField, "mode", "Gaussian");
                Override(depthOfField, "gaussianStart", 1.25f);
                Override(depthOfField, "gaussianEnd", 3.6f);
                Override(depthOfField, "gaussianMaxRadius", 1.2f);
                Override(depthOfField, "highQualitySampling", true);

                // Caméra : post-traitement + antialiasing
                var camDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, " + Urp);
                if (camDataType != null)
                {
                    var data = cam.GetComponent(camDataType);
                    if (data == null) data = cam.gameObject.AddComponent(camDataType);
                    SetMember(data, "renderPostProcessing", true);
                    var aaType = Type.GetType("UnityEngine.Rendering.Universal.AntialiasingMode, " + Urp);
                    if (aaType != null) SetMember(data, "antialiasing", Enum.Parse(aaType, "SubpixelMorphologicalAntiAliasing"));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Salon] Post-traitement non disponible : " + e.Message);
            }
        }

        /// <summary>Distance de mise au point (le flou commence au-delà de la cliente).</summary>
        public static void SetFocus(float distance)
        {
            if (depthOfField == null) return;
            try
            {
                Override(depthOfField, "gaussianStart", distance + 0.45f);
                Override(depthOfField, "gaussianEnd", distance + 2.8f);
            }
            catch (Exception) { }
        }

        static void SetMember(object o, string name, object value)
        {
            if (o == null) return;
            var t = o.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { f.SetValue(o, value); return; }
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) p.SetValue(o, value, null);
        }

        static void Override(object comp, string field, object value)
        {
            if (comp == null) return;
            var f = comp.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f == null) return;
            var param = f.GetValue(comp);
            if (param == null) return;
            var m = param.GetType().GetMethod("Override", BindingFlags.Public | BindingFlags.Instance);
            if (m == null) return;
            var pt = m.GetParameters()[0].ParameterType;
            object v = value;
            if (pt.IsEnum && value is string) v = Enum.Parse(pt, (string)value);
            else if (pt != value.GetType()) v = Convert.ChangeType(value, pt);
            m.Invoke(param, new[] { v });
        }
    }
}
