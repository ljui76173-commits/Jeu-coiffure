using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Photos avant / après, montage côte à côte et galerie.</summary>
    public class PhotoBooth : MonoBehaviour
    {
        public bool Capturing { get; private set; }

        public static string Folder
        {
            get { return Path.Combine(Application.persistentDataPath, "Photos"); }
        }

        /// <summary>Photo portrait de la cliente (l'interface est masquée pendant la prise).</summary>
        public IEnumerator Capture(SalonGame g, float yaw, Action<Texture2D> done)
        {
            Capturing = true;
            var cam = g.Cam;
            Vector3 t = cam.target; float y = cam.yaw, p = cam.pitch, d = cam.distance;
            cam.Set(g.HeadWorld + new Vector3(0, -0.04f, 0), yaw, 4f, 0.62f, true);
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            int h = Mathf.RoundToInt(Screen.height * 0.9f);
            int w = Mathf.RoundToInt(h * 0.8f);
            if (w > Screen.width * 0.9f) { w = Mathf.RoundToInt(Screen.width * 0.9f); h = Mathf.RoundToInt(w / 0.8f); }
            int x = (Screen.width - w) / 2, yy = (Screen.height - h) / 2;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(x, yy, w, h), 0, 0);
            tex.Apply();

            cam.Set(t, y, p, d, true);
            Capturing = false;
            g.Flash = 1f;
            g.Audio.OneShot(g.Audio.Shutter, 0.9f);
            if (done != null) done(tex);
        }

        /// <summary>Enregistre le montage avant / après en PNG. Renvoie le chemin.</summary>
        public string SaveBeforeAfter(Texture2D before, Texture2D after)
        {
            if (after == null) return null;
            int w = after.width, h = after.height, m = Mathf.Max(12, w / 25);
            bool two = before != null;
            int W = two ? w * 2 + m * 3 : w + m * 2, H = h + m * 2;
            var outTex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var bg = new Color[W * H];
            Color cream = SalonUtil.Hex("#F7EFE5"), gold = SalonUtil.Hex("#C9A45C");
            for (int i = 0; i < bg.Length; i++) bg[i] = cream;
            outTex.SetPixels(bg);
            int ox = m;
            if (two) { Blit(before, outTex, ox, m, w, h, gold); ox += w + m; }
            Blit(after, outTex, ox, m, w, h, gold);
            outTex.Apply();

            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "Salon_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            Destroy(outTex);
            return path;
        }

        static void Blit(Texture2D src, Texture2D dst, int ox, int oy, int w, int h, Color frame)
        {
            for (int y = -4; y < h + 4; y++)
                for (int x = -4; x < w + 4; x++)
                {
                    bool border = x < 0 || y < 0 || x >= w || y >= h;
                    Color c = border ? frame : src.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                    int px = ox + x, py = oy + y;
                    if (px >= 0 && py >= 0 && px < dst.width && py < dst.height) dst.SetPixel(px, py, c);
                }
        }

        public static List<string> ListPhotos()
        {
            var list = new List<string>();
            try
            {
                if (Directory.Exists(Folder)) list.AddRange(Directory.GetFiles(Folder, "*.png"));
                list.Sort();
                list.Reverse();
            }
            catch (Exception e) { Debug.LogWarning("[Salon] Galerie : " + e.Message); }
            return list;
        }

        public static Texture2D LoadPhoto(string path)
        {
            try
            {
                var t = new Texture2D(2, 2);
                if (t.LoadImage(File.ReadAllBytes(path))) return t;
            }
            catch (Exception e) { Debug.LogWarning("[Salon] Photo : " + e.Message); }
            return null;
        }
    }
}
