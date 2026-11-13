using System;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Sauvegarde locale (PlayerPrefs, format JSON).</summary>
    [Serializable]
    public class SaveData
    {
        const string Key = "salon_coiffure_save_v1";

        public string salonName = "Salon Belle Époque";
        public float musicVolume = 0.55f;
        public float sfxVolume = 0.8f;
        public int quality = -1;          // -1 = auto, 0 = bas, 1 = moyen, 2 = haut
        public int clientsServed;
        public int totalEarned;
        public int fiveStars;
        public int photosSaved;

        public static SaveData Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var d = JsonUtility.FromJson<SaveData>(json);
                    if (d != null) return d;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Salon] Sauvegarde illisible : " + e.Message); }
            return new SaveData();
        }

        public void Store()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public int EffectiveQuality
        {
            get
            {
                if (quality >= 0) return quality;
                return Application.isMobilePlatform ? 1 : 2;
            }
        }

        public static readonly string[] QualityNames = { "Bas", "Moyen", "Haut" };
        public static readonly int[] StrandsForQuality = { 1300, 2300, 3600 };
        public static readonly int[] SegmentsForQuality = { 13, 15, 18 };
    }
}
