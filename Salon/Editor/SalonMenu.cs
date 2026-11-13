using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SalonCoiffure.EditorTools
{
    /// <summary>Menu « Salon de Coiffure » dans la barre de menus de Unity.</summary>
    [InitializeOnLoad]
    public static class SalonMenu
    {
        const string ScenePath = "Assets/SalonScene.unity";
        const string MatFolder = "Assets/SalonGenerated/Resources/SalonMaterials";
        const string WelcomeKey = "SalonCoiffure_Welcome_v1";

        static SalonMenu()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(WelcomeKey, false)) return;
                EditorPrefs.SetBool(WelcomeKey, true);
                if (EditorUtility.DisplayDialog("Salon de Coiffure",
                        "Le jeu est installé !\n\nPour jouer : menu « Salon de Coiffure » > « Jouer ».\n\nVeux-tu préparer la scène du salon maintenant ?",
                        "Oui, préparer", "Plus tard"))
                    CreateScene();
            };
        }

        [MenuItem("Salon de Coiffure/Jouer", false, 1)]
        public static void Play()
        {
            if (EditorApplication.isPlaying) return;
            if (!File.Exists(ScenePath)) { if (!CreateScene()) return; }
            else
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            }
            EnsureMaterials();
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Salon de Coiffure/Créer (ou recréer) la scène du salon", false, 2)]
        public static bool CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EnsureMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("SalonGame").AddComponent<SalonGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == ScenePath);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Salon] Scène créée : " + ScenePath + ". Appuie sur Play (ou menu Salon de Coiffure > Jouer).");
            return true;
        }

        [MenuItem("Salon de Coiffure/Ouvrir le dossier des photos", false, 20)]
        public static void OpenPhotos()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Photos");
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        [MenuItem("Salon de Coiffure/Réinitialiser la sauvegarde", false, 21)]
        public static void ResetSave()
        {
            if (EditorUtility.DisplayDialog("Salon de Coiffure", "Effacer la progression (nom du salon, recettes, statistiques) ?", "Effacer", "Annuler"))
            {
                PlayerPrefs.DeleteKey("salon_coiffure_save_v1");
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Crée des matériaux modèles dans un dossier Resources pour que les shaders
        /// (URP Lit / Unlit et Salon/Hair) soient inclus dans les builds PC et mobile.
        /// </summary>
        static void EnsureMaterials()
        {
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            Directory.CreateDirectory(MatFolder);
            MakeMat("SalonLit", urp ? "Universal Render Pipeline/Lit" : "Standard", false);
            MakeMat("SalonLitEmissive", urp ? "Universal Render Pipeline/Lit" : "Standard", true);
            MakeMat("SalonUnlit", urp ? "Universal Render Pipeline/Unlit" : "Unlit/Texture", false);
            MakeMat("SalonHair", "Salon/Hair", false);
            AssetDatabase.SaveAssets();
        }

        static void MakeMat(string name, string shaderName, bool emissive)
        {
            string path = MatFolder + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogWarning("[Salon] Shader introuvable : " + shaderName); return; }
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (existing.shader != shader) existing.shader = shader;
                return;
            }
            var m = new Material(shader);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(m, path);
        }
    }
}
