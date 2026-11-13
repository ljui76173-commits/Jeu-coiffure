using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>
    /// Interface du jeu (IMGUI) : menu, étapes, outils, demande de la cliente, résultat, options, galerie.
    /// Fonctionne à la souris et au doigt ; tout est mis à l'échelle selon l'écran.
    /// </summary>
    public class SalonUI : MonoBehaviour
    {
        enum Panel { None, Options, Galerie, Aide }

        SalonGame g;
        readonly UIStyles st = new UIStyles();
        float scale = -1;
        readonly List<Rect> blockers = new List<Rect>();
        Panel panel = Panel.None;

        bool orbitDrag, toolDrag;
        Vector2 lastMouse, hoverPos;
        bool hoverValid;

        Vector2 toolScroll, resultScroll, galleryScroll;
        List<string> gallery;
        readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
        Texture2D galleryView;
        string galleryViewPath;

        static readonly Stage[] StageOrder = { Stage.Accueil, Stage.Coloration, Stage.Bac, Stage.Coupe, Stage.Sechage, Stage.Resultat };
        static readonly string[] StageNames = { "Accueil", "Coloration", "Bac à shampoing", "Coupe", "Séchage", "Résultat & photo" };

        public void Init(SalonGame game) { g = game; }

        float S(float v) { return v * scale; }

        void EnsureStyles()
        {
            float s = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            if (Application.isMobilePlatform) s *= 1.15f;
            s = Mathf.Clamp(s, 0.55f, 3.5f);
            if (Mathf.Abs(s - scale) > 0.01f)
            {
                scale = s;
                st.Build(s);
            }
        }

        Rect Block(Rect r) { blockers.Add(r); return r; }

        bool IsBlocked(Vector2 p)
        {
            if (panel != Panel.None) return true;
            for (int i = 0; i < blockers.Count; i++) if (blockers[i].Contains(p)) return true;
            return false;
        }

        // ------------------------------------------------------------------ Entrées
        void HandlePointer(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    hoverPos = e.mousePosition; hoverValid = true;
                    if (IsBlocked(e.mousePosition) || g.Mode != GameMode.Libre || g.Busy) return;
                    if (e.button != 0 || g.Tools.Current == ToolType.Main)
                    {
                        orbitDrag = true;
                        lastMouse = e.mousePosition;
                    }
                    else
                    {
                        toolDrag = true;
                        g.Tools.PointerActive = true;
                        SetPointer(e.mousePosition);
                    }
                    break;
                case EventType.MouseDrag:
                    hoverPos = e.mousePosition; hoverValid = true;
                    if (orbitDrag)
                    {
                        Vector2 d = e.mousePosition - lastMouse;
                        g.Cam.Orbit(d.x * 0.35f, d.y * 0.25f);
                        lastMouse = e.mousePosition;
                    }
                    if (toolDrag) SetPointer(e.mousePosition);
                    break;
                case EventType.MouseUp:
                    orbitDrag = false; toolDrag = false;
                    g.Tools.PointerActive = false;
                    if (Application.isMobilePlatform) hoverValid = false;
                    break;
                case EventType.MouseMove:
                    hoverPos = e.mousePosition; hoverValid = true;
                    break;
                case EventType.ScrollWheel:
                    if (!IsBlocked(e.mousePosition) && g.Mode == GameMode.Libre) g.Cam.Zoom(1f + e.delta.y * 0.04f);
                    break;
            }
        }

        void SetPointer(Vector2 guiPos)
        {
            g.Tools.PointerPos = new Vector2(guiPos.x, Screen.height - guiPos.y);
        }

        // ------------------------------------------------------------------ Dessin
        void OnGUI()
        {
            if (g == null) return;
            EnsureStyles();
            Event e = Event.current;
            if (e.type == EventType.Layout) blockers.Clear();
            HandlePointer(e);
            if (g.Photo.Capturing) return;

            if (g.Mode == GameMode.Menu) DrawMenu();
            else DrawGame();

            if (panel != Panel.None) DrawPanel();
            DrawToast();
            DrawOverlay();
        }

        void DrawOverlay()
        {
            if (g.Fade > 0.001f)
            {
                GUI.color = new Color(0.08f, 0.06f, 0.06f, g.Fade);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), st.white);
            }
            if (g.Flash > 0.001f)
            {
                GUI.color = new Color(1, 1, 1, g.Flash * 0.85f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), st.white);
            }
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- Menu
        void DrawMenu()
        {
            float w = S(520), h = S(560);
            var r = Block(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h));
            GUILayout.BeginArea(r, st.panel);
            GUILayout.Space(S(6));
            GUILayout.Label(g.Save.salonName, st.title);
            GUILayout.Label("Salon de coiffure · Paris", st.smallCenter);
            GUILayout.Space(S(14));
            if (GUILayout.Button("Mode Libre", st.buttonBig)) { Click(); g.StartFreeMode(); }
            GUI.enabled = false;
            GUILayout.Button("Mode Gestion — bientôt (V3)", st.button);
            GUI.enabled = true;
            if (GUILayout.Button("Galerie photos", st.button)) { Click(); OpenGallery(); }
            if (GUILayout.Button("Options", st.button)) { Click(); panel = Panel.Options; }
            if (GUILayout.Button("Comment jouer ?", st.button)) { Click(); panel = Panel.Aide; }
            if (!Application.isMobilePlatform && !Application.isEditor)
                if (GUILayout.Button("Quitter", st.buttonSmall)) Application.Quit();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Clientes coiffées : " + g.Save.clientsServed + "   ·   Recettes : " + g.Save.totalEarned + " €   ·   ★★★★★ : " + g.Save.fiveStars, st.smallCenter);
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- Jeu
        void DrawGame()
        {
            if (g.Stage == Stage.Resultat && g.Report != null && !g.Busy) { DrawResult(); return; }
            DrawSteps();
            DrawTools();
            DrawRequest();
            DrawCameraBar();
            if (g.Stage == Stage.Accueil && !g.Busy) DrawWelcome();
            DrawCursor();
        }

        void DrawSteps()
        {
            float w = S(215);
            var r = Block(new Rect(S(12), S(12), w, S(470)));
            GUILayout.BeginArea(r, st.panel);
            GUILayout.Label("Étapes", st.h1);
            for (int i = 0; i < StageOrder.Length; i++)
            {
                bool on = g.Stage == StageOrder[i];
                string label = (i + 1) + ". " + StageNames[i];
                if (GUILayout.Button(label, on ? st.buttonOn : st.button) && !on) { Click(); g.GoToStage(StageOrder[i]); }
            }
            GUILayout.Space(S(8));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Menu", st.buttonSmall)) { Click(); g.BackToMenu(); }
            if (GUILayout.Button("Aide", st.buttonSmall)) { Click(); panel = Panel.Aide; }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Nouvelle cliente", st.buttonSmall)) { Click(); g.NextClient(); }
            GUILayout.EndArea();
        }

        void DrawTools()
        {
            float w = S(260);
            var r = Block(new Rect(Screen.width - w - S(12), S(12), w, Screen.height - S(24) - S(70)));
            GUILayout.BeginArea(r, st.panel);
            toolScroll = GUILayout.BeginScrollView(toolScroll);
            GUILayout.Label("Outils", st.h1);
            var tools = ToolCatalog.ForStage(g.Stage);
            foreach (var t in tools)
            {
                bool on = g.Tools.Current == t;
                if (GUILayout.Button(ToolCatalog.Get(t).name, on ? st.buttonOn : st.button)) { Click(); g.Tools.Current = t; }
            }
            GUILayout.Label(g.Tools.Def.hint, st.small);

            var hair = g.Client.Hair;
            switch (g.Stage)
            {
                case Stage.Coloration: ColorOptions(hair); break;
                case Stage.Bac: BacOptions(hair); break;
                case Stage.Coupe: CutOptions(); break;
                case Stage.Sechage:
                    GUILayout.Label("Humidité", st.h2);
                    Bar(hair.AverageWet(), new Color(0.45f, 0.65f, 0.9f));
                    GUILayout.Label(hair.AverageWet() < 0.15f ? "Cheveux secs ✓" : "Sèche bien toutes les zones.", st.small);
                    break;
                case Stage.Accueil:
                    GUILayout.Label("Prends le temps d'écouter la demande de ta cliente. Une photo « avant » est prise automatiquement.", st.small);
                    break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void SizeSlider()
        {
            GUILayout.Label("Taille de l'outil", st.h2);
            g.Tools.SizeK = GUILayout.HorizontalSlider(g.Tools.SizeK, 0.5f, 2.2f);
            GUILayout.Space(S(6));
        }

        void ZoneSelector()
        {
            GUILayout.Label("Zone", st.h2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Toutes", g.Tools.ZoneFilter < 0 ? st.buttonOn : st.buttonSmall)) g.Tools.ZoneFilter = -1;
            if (GUILayout.Button(HairSystem.ZoneNames[0], g.Tools.ZoneFilter == 0 ? st.buttonOn : st.buttonSmall)) g.Tools.ZoneFilter = 0;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int z = 1; z < 4; z++)
                if (GUILayout.Button(HairSystem.ZoneNames[z], g.Tools.ZoneFilter == z ? st.buttonOn : st.buttonSmall)) g.Tools.ZoneFilter = z;
            GUILayout.EndHorizontal();
        }

        void ColorOptions(HairSystem hair)
        {
            var req = g.Client.Profile.request;
            if (req.colorKind != ColorKind.Aucune)
                GUILayout.Label("Demandé : <b>" + req.dye.name + "</b>" + (req.colorKind == ColorKind.Meches ? " (mèches)" : ""), st.label);
            else
                GUILayout.Label("Pas de couleur demandée.", st.small);

            GUILayout.Label("Teinte : " + Shades.Dyes[g.Tools.DyeIndex].name, st.h2);
            int perRow = 5;
            for (int i = 0; i < Shades.Dyes.Length; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int k = i; k < Mathf.Min(i + perRow, Shades.Dyes.Length); k++)
                {
                    Rect rr = GUILayoutUtility.GetRect(S(38), S(38), st.swatch);
                    if (Event.current.type == EventType.Repaint)
                    {
                        if (k == g.Tools.DyeIndex)
                        {
                            GUI.color = UIStyles.Green;
                            GUI.DrawTexture(new Rect(rr.x - S(3), rr.y - S(3), rr.width + S(6), rr.height + S(6)), st.round);
                        }
                        GUI.color = Shades.Dyes[k].color;
                        GUI.DrawTexture(rr, st.round);
                        GUI.color = Color.white;
                    }
                    if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) { g.Tools.DyeIndex = k; Click(); }
                }
                GUILayout.EndHorizontal();
            }
            ZoneSelector();
            SizeSlider();
            GUILayout.Label("Couverture", st.h2);
            Bar(hair.PasteCoverage(), Shades.Dyes[g.Tools.DyeIndex].color);

            GUILayout.Label("Temps de pose", st.h2);
            if (g.PoseRunning)
            {
                Bar(g.Develop, UIStyles.Gold);
                int minutes = Mathf.CeilToInt((1f - g.Develop) * 35f);
                GUILayout.Label("Encore ~" + minutes + " min (temps du salon)", st.small);
                bool fast = g.PoseSpeed > 1f;
                if (GUILayout.Button(fast ? "Vitesse normale" : "Accélérer ×4", st.buttonSmall)) g.PoseSpeed = fast ? 1f : 4f;
            }
            else if (g.Develop >= 1f)
            {
                GUILayout.Label("Pose terminée ✓ — rince au bac !", st.label);
                if (GUILayout.Button("Aller au bac", st.button)) { Click(); g.GoToStage(Stage.Bac); }
            }
            else if (g.Develop > 0f)
            {
                GUILayout.Label("Pose interrompue (" + Mathf.RoundToInt(g.Develop * 100) + " %). Rince au bac.", st.small);
            }
            else
            {
                if (GUILayout.Button("Lancer le temps de pose", st.button)) { Click(); g.StartPose(); }
            }
        }

        void BacOptions(HairSystem hair)
        {
            GUILayout.Label("Mouillés", st.h2);
            Bar(hair.AverageWet(), new Color(0.45f, 0.65f, 0.9f));
            GUILayout.Label("Mousse", st.h2);
            Bar(hair.AverageFoam(), new Color(0.92f, 0.92f, 0.98f));
            GUILayout.Label("Soin", st.h2);
            Bar(hair.AverageCare(), UIStyles.Rose);
            GUILayout.Space(S(6));
            GUILayout.Label("1. Douchette pour mouiller\n2. Shampoing : masse pour faire mousser\n3. Douchette pour rincer\n4. Après-shampoing, puis rinçage\n5. Serviette pour essorer", st.small);
            if (hair.HasPaste())
                GUILayout.Label("<b>Coloration à rincer</b> : passe la douchette partout.", st.label);
        }

        void CutOptions()
        {
            if (g.Tools.Current == ToolType.Tondeuse)
            {
                GUILayout.Label("Sabot", st.h2);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("−", st.buttonSmall, GUILayout.Width(S(44)))) g.Tools.SabotIndex = Mathf.Max(0, g.Tools.SabotIndex - 1);
                GUILayout.Label("n°" + g.Tools.SabotIndex + "  (" + ToolController.SabotMm[g.Tools.SabotIndex] + " mm)", st.labelCenter, GUILayout.Height(S(36)));
                if (GUILayout.Button("+", st.buttonSmall, GUILayout.Width(S(44)))) g.Tools.SabotIndex = Mathf.Min(ToolController.SabotMm.Length - 1, g.Tools.SabotIndex + 1);
                GUILayout.EndHorizontal();
            }
            ZoneSelector();
            SizeSlider();
            var req = g.Client.Profile.request;
            GUILayout.Label("Objectif", st.h2);
            for (int z = 0; z < 4; z++)
                GUILayout.Label("<b>" + HairSystem.ZoneNames[z] + "</b> : " + req.ZoneHint(z), st.chip);
            GUILayout.Label("Astuce : tourne autour de la tête pour couper la nuque et les côtés.", st.small);
        }

        void Bar(float v, Color c)
        {
            Rect r = GUILayoutUtility.GetRect(S(200), S(14));
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = Color.white;
            GUI.DrawTexture(r, st.barBg);
            GUI.color = c;
            float w = Mathf.Max(r.height, r.width * Mathf.Clamp01(v));
            if (v > 0.005f) GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), st.round);
            GUI.color = Color.white;
        }

        void DrawRequest()
        {
            float left = S(12) + S(215) + S(12), right = Screen.width - S(260) - S(24);
            float w = Mathf.Min(right - left, S(620));
            if (w < S(200)) return;
            var p = g.Client.Profile;
            var r = Block(new Rect(left + (right - left - w) / 2, S(12), w, S(92)));
            GUILayout.BeginArea(r, st.card);
            GUILayout.Label(p.firstName + " — « " + p.request.phrase + " »", st.label);
            GUILayout.EndArea();
        }

        void DrawWelcome()
        {
            var p = g.Client.Profile;
            float w = Mathf.Min(S(620), Screen.width - S(560));
            if (w < S(300)) w = Screen.width - S(40);
            var r = Block(new Rect((Screen.width - w) / 2, Screen.height - S(270), w, S(190)));
            GUILayout.BeginArea(r, st.bubble);
            GUILayout.Label("Bonjour, je m'appelle " + p.firstName + " !", st.h1);
            GUILayout.Label("« " + p.request.phrase + " »", st.label);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Stage first = p.request.colorKind != ColorKind.Aucune ? Stage.Coloration : Stage.Bac;
            if (GUILayout.Button(first == Stage.Coloration ? "C'est parti : coloration" : "C'est parti : shampoing", st.button, GUILayout.Width(S(300))))
            {
                Click();
                g.GoToStage(first);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawCameraBar()
        {
            float bw = S(58), h = S(52);
            string[] labels = { "←", "→", "↑", "↓", "+", "−", "Face" };
            float w = bw * labels.Length + S(16) + S(30);
            var r = Block(new Rect((Screen.width - w) / 2, Screen.height - h - S(12), w, h));
            GUI.Box(r, GUIContent.none, st.panel);
            float x = r.x + S(8);
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < labels.Length; i++)
            {
                float ww = i == 6 ? bw + S(30) : bw;
                var br = new Rect(x, r.y + S(5), ww - S(4), h - S(10));
                x += ww;
                var style = st.buttonSmall;
                if (i == 6)
                {
                    if (GUI.Button(br, labels[i], style)) g.ApplyCameraPreset(g.Stage, false);
                    continue;
                }
                if (GUI.RepeatButton(br, labels[i], style))
                {
                    switch (i)
                    {
                        case 0: g.Cam.Orbit(90f * dt, 0); break;
                        case 1: g.Cam.Orbit(-90f * dt, 0); break;
                        case 2: g.Cam.Orbit(0, 50f * dt); break;
                        case 3: g.Cam.Orbit(0, -50f * dt); break;
                        case 4: g.Cam.Zoom(1f - 1.2f * dt); break;
                        case 5: g.Cam.Zoom(1f + 1.2f * dt); break;
                    }
                }
            }
        }

        void DrawCursor()
        {
            if (!hoverValid || g.Tools.Current == ToolType.Main || g.Busy) return;
            if (IsBlocked(hoverPos)) return;
            if (Event.current.type != EventType.Repaint) return;
            float dist = Vector3.Distance(g.MainCam.transform.position, g.HeadWorld);
            float px = g.Tools.Radius / (2f * dist * Mathf.Tan(g.MainCam.fieldOfView * 0.5f * Mathf.Deg2Rad)) * Screen.height;
            px = Mathf.Max(px, S(8));
            GUI.color = g.Tools.PointerActive ? new Color(1f, 0.85f, 0.45f, 0.95f) : new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(hoverPos.x - px, hoverPos.y - px, px * 2, px * 2), st.ring);
            GUI.color = Color.white;
            var lr = new Rect(hoverPos.x + px + S(6), hoverPos.y - S(12), S(220), S(24));
            GUI.Label(lr, g.Tools.Def.name, st.small);
        }

        void DrawToast()
        {
            if (g.Tools.FeedbackTime <= 0 || string.IsNullOrEmpty(g.Tools.Feedback)) return;
            var content = new GUIContent(g.Tools.Feedback);
            float w = Mathf.Min(st.toast.CalcSize(content).x + S(10), Screen.width - S(40));
            float h = st.toast.CalcHeight(content, w);
            var r = new Rect((Screen.width - w) / 2, Screen.height - S(140) - h, w, h);
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(g.Tools.FeedbackTime * 2f));
            GUI.Label(r, content, st.toast);
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- Résultat
        void DrawResult()
        {
            var rep = g.Report;
            float w = Mathf.Min(Screen.width - S(40), S(1100)), h = Mathf.Min(Screen.height - S(40), S(660));
            var r = Block(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h));
            GUILayout.BeginArea(r, st.panel);
            GUILayout.BeginHorizontal();

            // Photos avant / après
            GUILayout.BeginVertical(GUILayout.Width(w * 0.56f));
            float ph = h - S(110);
            float pw = ph * 0.8f;
            if (pw * 2 + S(16) > w * 0.54f) { pw = (w * 0.54f - S(16)) / 2; ph = pw / 0.8f; }
            GUILayout.BeginHorizontal();
            Photo(g.PhotoBefore, "Avant", pw, ph);
            GUILayout.Space(S(10));
            Photo(g.PhotoAfter, "Après", pw, ph);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Avis
            GUILayout.BeginVertical();
            GUILayout.Label(g.Client.Profile.firstName, st.h1);
            GUILayout.Label(new string('★', rep.stars) + new string('☆', 5 - rep.stars), st.stars);
            GUILayout.Label("« " + rep.mood + " »", st.labelCenter);
            resultScroll = GUILayout.BeginScrollView(resultScroll, GUILayout.Height(h * 0.36f));
            foreach (var line in rep.lines) GUILayout.Label(line, st.label);
            GUILayout.EndScrollView();
            GUILayout.Label("Prestation : <b>" + rep.price + " €</b>    Pourboire : <b>" + rep.tip + " €</b>", st.label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(g.LastSavedPhoto == null ? "Enregistrer la photo avant/après" : "Photo enregistrée ✓", st.button)) { Click(); if (g.LastSavedPhoto == null) g.SavePhoto(); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Retoucher", st.buttonSmall)) { Click(); g.GoToStage(Stage.Coupe); }
            if (GUILayout.Button("Cliente suivante", st.buttonOn)) { Click(); g.NextClient(); }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Menu", st.buttonSmall)) { Click(); g.BackToMenu(); }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void Photo(Texture2D tex, string caption, float w, float h)
        {
            GUILayout.BeginVertical(GUILayout.Width(w));
            Rect r = GUILayoutUtility.GetRect(w, h);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = UIStyles.Gold;
                GUI.DrawTexture(new Rect(r.x - S(3), r.y - S(3), r.width + S(6), r.height + S(6)), st.white);
                GUI.color = Color.white;
                if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.ScaleAndCrop);
                else GUI.DrawTexture(r, st.barBg);
            }
            GUILayout.Label(caption, st.smallCenter);
            GUILayout.EndVertical();
        }

        // ---------------------------------------------------------------- Fenêtres
        void DrawPanel()
        {
            GUI.color = new Color(0, 0, 0, 0.35f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), st.white);
            GUI.color = Color.white;
            switch (panel)
            {
                case Panel.Options: DrawOptions(); break;
                case Panel.Galerie: DrawGallery(); break;
                case Panel.Aide: DrawHelp(); break;
            }
        }

        void DrawOptions()
        {
            float w = S(520), h = S(520);
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUILayout.BeginArea(r, st.panel);
            GUILayout.Label("Options", st.h1);
            GUILayout.Label("Nom du salon (enseigne)", st.h2);
            string n = GUILayout.TextField(g.Save.salonName, 32, GUILayout.Height(S(36)));
            if (n != g.Save.salonName) { g.Save.salonName = n; g.World.SetSalonName(n); }
            GUILayout.Label("Musique", st.h2);
            float mv = GUILayout.HorizontalSlider(g.Save.musicVolume, 0f, 1f);
            GUILayout.Space(S(8));
            GUILayout.Label("Bruitages", st.h2);
            float sv = GUILayout.HorizontalSlider(g.Save.sfxVolume, 0f, 1f);
            if (!Mathf.Approximately(mv, g.Save.musicVolume) || !Mathf.Approximately(sv, g.Save.sfxVolume))
            {
                g.Save.musicVolume = mv; g.Save.sfxVolume = sv;
                g.Audio.SetVolumes(mv, sv);
            }
            GUILayout.Space(S(8));
            GUILayout.Label("Qualité des cheveux (prochaine cliente)", st.h2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto", g.Save.quality < 0 ? st.buttonOn : st.buttonSmall)) g.Save.quality = -1;
            for (int q = 0; q < 3; q++)
                if (GUILayout.Button(SaveData.QualityNames[q], g.Save.quality == q ? st.buttonOn : st.buttonSmall)) g.Save.quality = q;
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Fermer", st.button)) { Click(); g.ApplyOptions(); panel = Panel.None; }
            GUILayout.EndArea();
        }

        void OpenGallery()
        {
            gallery = PhotoBooth.ListPhotos();
            galleryView = null;
            galleryViewPath = null;
            panel = Panel.Galerie;
        }

        Texture2D Thumb(string path)
        {
            Texture2D t;
            if (!thumbs.TryGetValue(path, out t))
            {
                t = PhotoBooth.LoadPhoto(path);
                thumbs[path] = t;
            }
            return t;
        }

        void DrawGallery()
        {
            float w = Mathf.Min(Screen.width - S(40), S(1000)), h = Mathf.Min(Screen.height - S(40), S(640));
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUILayout.BeginArea(r, st.panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Galerie de créations", st.h1);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Fermer", st.buttonSmall, GUILayout.Width(S(120)))) { Click(); panel = Panel.None; }
            GUILayout.EndHorizontal();

            if (galleryView != null)
            {
                Rect pr = GUILayoutUtility.GetRect(w - S(40), h - S(150));
                if (Event.current.type == EventType.Repaint) GUI.DrawTexture(pr, galleryView, ScaleMode.ScaleToFit);
                if (GUILayout.Button("Retour à la galerie", st.buttonSmall)) { galleryView = null; galleryViewPath = null; }
            }
            else if (gallery == null || gallery.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Aucune photo pour l'instant. Termine une cliente et enregistre la photo avant/après !", st.labelCenter);
                GUILayout.FlexibleSpace();
            }
            else
            {
                galleryScroll = GUILayout.BeginScrollView(galleryScroll);
                int perRow = 3;
                float tw = (w - S(80)) / perRow, th = tw * 0.55f;
                for (int i = 0; i < gallery.Count; i += perRow)
                {
                    GUILayout.BeginHorizontal();
                    for (int k = i; k < Mathf.Min(i + perRow, gallery.Count); k++)
                    {
                        Rect tr = GUILayoutUtility.GetRect(tw, th, GUILayout.Width(tw), GUILayout.Height(th));
                        var tex = Thumb(gallery[k]);
                        if (Event.current.type == EventType.Repaint && tex != null) GUI.DrawTexture(tr, tex, ScaleMode.ScaleToFit);
                        if (GUI.Button(tr, GUIContent.none, GUIStyle.none)) { galleryView = tex; galleryViewPath = gallery[k]; }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }
            GUILayout.Label(galleryViewPath ?? PhotoBooth.Folder, st.small);
            if (!Application.isMobilePlatform && GUILayout.Button("Ouvrir le dossier des photos", st.buttonSmall))
            {
                System.IO.Directory.CreateDirectory(PhotoBooth.Folder);
                Application.OpenURL("file://" + PhotoBooth.Folder);
            }
            GUILayout.EndArea();
        }

        void DrawHelp()
        {
            float w = Mathf.Min(Screen.width - S(40), S(720)), h = Mathf.Min(Screen.height - S(40), S(560));
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUILayout.BeginArea(r, st.panel);
            GUILayout.Label("Comment jouer ?", st.h1);
            GUILayout.Label(
                "• <b>Écoute la demande</b> de la cliente (en haut de l'écran).\n" +
                "• <b>Coloration</b> : choisis une teinte, peins les mèches au pinceau, lance le temps de pose.\n" +
                "• <b>Bac</b> : mouille à la douchette, fais mousser le shampoing, rince (ça révèle la couleur !), applique le soin, essore à la serviette.\n" +
                "• <b>Coupe</b> : les ciseaux coupent là où tu passes. Filtre par zone pour plus de précision. La tondeuse coupe à la hauteur du sabot.\n" +
                "• <b>Séchage</b> : passe le sèche-cheveux partout.\n" +
                "• <b>Résultat</b> : note de la cliente, pourboire et photo avant/après.\n\n" +
                "<b>Caméra</b> : outil « Tourner la vue », clic droit + glisser, molette, ou les flèches en bas.",
                st.label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Compris !", st.button)) { Click(); panel = Panel.None; }
            GUILayout.EndArea();
        }

        void Click() { if (g.Audio != null) g.Audio.OneShot(g.Audio.Click, 0.5f); }
    }
}
