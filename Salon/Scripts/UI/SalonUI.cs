using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>
    /// Interface (IMGUI) façon jeu mobile : barre d'étapes en haut, fiche cliente à gauche,
    /// objectifs et réglages à droite, dock d'outils rond en bas.
    /// Règle des gestes : appuyer sur la cliente = utiliser l'outil ; appuyer à côté = tourner la vue.
    /// </summary>
    public class SalonUI : MonoBehaviour
    {
        enum Panel { None, Options, Galerie, Aide }

        SalonGame g;
        readonly UIStyles st = new UIStyles();
        float scale = -1;
        readonly List<Rect> blockers = new List<Rect>();
        Panel panel = Panel.None;

        bool orbitDrag;
        Vector2 lastMouse, hoverPos;
        bool hoverValid;

        Vector2 rightScroll, resultScroll, galleryScroll;
        List<string> gallery;
        readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
        Texture2D galleryView;
        string galleryViewPath;

        static readonly Stage[] StageOrder = { Stage.Accueil, Stage.Coloration, Stage.Bac, Stage.Coupe, Stage.Sechage, Stage.Resultat };
        static readonly string[] StageNames = { "Accueil", "Couleur", "Bac", "Coupe", "Séchage", "Résultat" };
        static readonly string[] StageIcons = { "accueil", "couleur", "bac", "coupe", "seche", "resultat" };

        static readonly Color Green = UIStyles.Green, Gold = UIStyles.Gold, Cream = UIStyles.Cream, Ink = UIStyles.Ink;

        public void Init(SalonGame game) { g = game; }

        float S(float v) { return v * scale; }

        void EnsureStyles()
        {
            float s = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            if (Application.isMobilePlatform) s *= 1.1f;
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

        Vector2 ToScreen(Vector2 gui) { return new Vector2(gui.x, Screen.height - gui.y); }

        // ------------------------------------------------------------------ Gestes
        void HandlePointer(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    hoverPos = e.mousePosition; hoverValid = true;
                    UpdateHover();
                    if (IsBlocked(e.mousePosition) || g.Mode != GameMode.Libre || g.Busy) return;
                    if (e.button == 0 && g.Tools.Current != ToolType.Main && g.Tools.IsOnClient(g, ToScreen(e.mousePosition)))
                    {
                        g.Tools.BeginStroke(ToScreen(e.mousePosition));
                    }
                    else
                    {
                        orbitDrag = true;
                        lastMouse = e.mousePosition;
                    }
                    break;
                case EventType.MouseDrag:
                    hoverPos = e.mousePosition; hoverValid = true;
                    UpdateHover();
                    if (orbitDrag)
                    {
                        Vector2 d = e.mousePosition - lastMouse;
                        g.Cam.Orbit(d.x * 0.35f, d.y * 0.22f);
                        lastMouse = e.mousePosition;
                    }
                    if (g.Tools.PointerActive) g.Tools.PointerPos = ToScreen(e.mousePosition);
                    break;
                case EventType.MouseUp:
                    orbitDrag = false;
                    g.Tools.EndStroke();
                    if (Application.isMobilePlatform) { hoverValid = false; UpdateHover(); }
                    break;
                case EventType.MouseMove:
                    hoverPos = e.mousePosition; hoverValid = true;
                    UpdateHover();
                    break;
                case EventType.ScrollWheel:
                    if (!IsBlocked(e.mousePosition) && g.Mode == GameMode.Libre) g.Cam.Zoom(1f + e.delta.y * 0.04f);
                    break;
            }
        }

        void UpdateHover()
        {
            g.Tools.HoverValid = hoverValid && !IsBlocked(hoverPos);
            g.Tools.HoverPos = ToScreen(hoverPos);
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

        void Shadowed(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(r.x - S(10), r.y - S(6), r.width + S(20), r.height + S(22)), st.shadow);
        }

        void Card(Rect r, GUIStyle style)
        {
            Shadowed(r);
            GUI.Box(r, GUIContent.none, style);
        }

        void DrawOverlay()
        {
            if (g.Fade > 0.001f)
            {
                GUI.color = new Color(0.08f, 0.06f, 0.06f, g.Fade);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), st.white);
            }
            if (g.Loading)
            {
                GUI.color = Color.white;
                var big = new GUIStyle(st.title) { fontSize = Mathf.RoundToInt(S(34)) };
                big.normal.textColor = Cream;
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "La cliente arrive…", big);
            }
            if (g.Flash > 0.001f)
            {
                GUI.color = new Color(1, 1, 1, g.Flash * 0.85f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), st.white);
            }
            GUI.color = Color.white;
        }

        bool IconButton(Rect r, string icon, Color bg, Color fg, bool ring = false, string tooltip = null)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color(0, 0, 0, 0.18f);
                GUI.DrawTexture(new Rect(r.x + S(1), r.y + S(3), r.width, r.height), st.circle);
                if (ring)
                {
                    GUI.color = Gold;
                    GUI.DrawTexture(new Rect(r.x - S(4), r.y - S(4), r.width + S(8), r.height + S(8)), st.circle);
                }
                GUI.color = hover ? Color.Lerp(bg, Color.white, 0.15f) : bg;
                GUI.DrawTexture(r, st.circle);
                GUI.color = Color.white;
                float pad = r.width * 0.24f;
                Icons.Draw(new Rect(r.x + pad, r.y + pad, r.width - pad * 2, r.height - pad * 2), icon, fg);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // ---------------------------------------------------------------- Menu
        void DrawMenu()
        {
            float w = S(500), h = S(560);
            var r = Block(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h));
            Card(r, st.panel);
            GUILayout.BeginArea(r, st.panel);
            GUILayout.Space(S(10));
            GUILayout.Label(g.Save.salonName, st.title);
            Rect line = GUILayoutUtility.GetRect(w, S(14));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = Gold;
                GUI.DrawTexture(new Rect(line.x + w * 0.25f, line.y + S(6), w * 0.5f - S(20), S(2)), st.white);
                GUI.color = Color.white;
            }
            GUILayout.Label("Salon de coiffure · Paris", st.smallCenter);
            GUILayout.Space(S(18));
            if (MenuButton("Ouvrir le salon", "ciseaux", true)) { Click(); g.StartFreeMode(); }
            GUI.enabled = false;
            MenuButton("Mode Gestion (bientôt)", "piece", false);
            GUI.enabled = true;
            if (MenuButton("Galerie photos", "galerie", false)) { Click(); OpenGallery(); }
            if (MenuButton("Options", "reglages", false)) { Click(); panel = Panel.Options; }
            if (MenuButton("Comment jouer ?", "aide", false)) { Click(); panel = Panel.Aide; }
            if (!Application.isMobilePlatform && !Application.isEditor)
                if (GUILayout.Button("Quitter", st.buttonSmall)) Application.Quit();
            GUILayout.FlexibleSpace();
            GUILayout.Label(g.Save.clientsServed + " clientes  ·  " + g.Save.totalEarned + " €  ·  " + g.Save.fiveStars + " × 5 étoiles", st.smallCenter);
            GUILayout.EndArea();
        }

        bool MenuButton(string text, string icon, bool primary)
        {
            var style = primary ? st.bigNext : st.buttonBig;
            Rect r = GUILayoutUtility.GetRect(new GUIContent(text), style);
            bool click = GUI.Button(r, "      " + text, style);
            Icons.Draw(new Rect(r.x + S(18), r.y + (r.height - S(28)) / 2, S(28), S(28)), icon, primary ? Color.white : Green);
            return click;
        }

        // ---------------------------------------------------------------- Jeu
        void DrawGame()
        {
            if (g.Stage == Stage.Resultat && g.Report != null && !g.Busy) { DrawTopBar(); DrawResult(); return; }
            DrawTopBar();
            DrawClientCard();
            if (g.Stage != Stage.Accueil) DrawRightPanel();
            DrawDock();
            DrawViewPill();
            if (g.Stage == Stage.Accueil && !g.Busy) DrawWelcome();
            DrawCursor();
        }

        void DrawTopBar()
        {
            float h = S(68);
            var r = Block(new Rect(0, 0, Screen.width, h));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color(Cream.r, Cream.g, Cream.b, 0.94f);
                GUI.DrawTexture(r, st.white);
                GUI.color = Gold;
                GUI.DrawTexture(new Rect(0, h - S(2), Screen.width, S(2)), st.white);
                GUI.color = Color.white;
            }
            GUI.Label(new Rect(S(20), 0, S(260), h), g.Save.salonName, st.topTitle);

            // Suivi des étapes
            int n = StageOrder.Length;
            float node = S(40), gap = S(88);
            float total = gap * (n - 1);
            float x0 = Screen.width / 2f - total / 2f;
            float cy = S(26);
            int cur = System.Array.IndexOf(StageOrder, g.Stage);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
                GUI.DrawTexture(new Rect(x0, cy - S(1), total, S(2)), st.white);
                GUI.color = Color.white;
            }
            for (int i = 0; i < n; i++)
            {
                bool skipped = StageOrder[i] == Stage.Coloration && !g.ColorRequested;
                float cx = x0 + gap * i;
                var nr = new Rect(cx - node / 2, cy - node / 2, node, node);
                bool on = i == cur;
                bool done = i < cur;
                Color bg = on ? Green : (done ? Gold : new Color(0.93f, 0.89f, 0.84f));
                Color fg = on || done ? Color.white : (skipped ? new Color(0.7f, 0.65f, 0.6f) : Green);
                if (IconButton(nr, done ? "check" : StageIcons[i], bg, fg, on) && !on && !g.Busy) { Click(); g.GoToStage(StageOrder[i]); }
                var lr = new Rect(cx - S(45), cy + node / 2 + S(1), S(90), S(16));
                GUI.Label(lr, StageNames[i], st.dockLabel);
            }

            // À droite : recettes + boutons
            float bx = Screen.width - S(14) - S(44);
            if (IconButton(new Rect(bx, S(12), S(44), S(44)), "maison", Color.white, Green)) { Click(); g.BackToMenu(); }
            bx -= S(52);
            if (IconButton(new Rect(bx, S(12), S(44), S(44)), "aide", Color.white, Green)) { Click(); panel = Panel.Aide; }
            bx -= S(52);
            if (IconButton(new Rect(bx, S(12), S(44), S(44)), "galerie", Color.white, Green)) { Click(); OpenGallery(); }
            bx -= S(120);
            Icons.Draw(new Rect(bx, S(22), S(24), S(24)), "piece", Gold);
            GUI.Label(new Rect(bx + S(30), S(14), S(90), S(40)), "<b>" + g.Save.totalEarned + " €</b>", new GUIStyle(st.label) { alignment = TextAnchor.MiddleLeft });
        }

        void DrawClientCard()
        {
            var p = g.Client.Profile;
            float w = S(300);
            float h = g.Stage == Stage.Accueil ? S(150) : S(170);
            var r = Block(new Rect(S(14), S(82), w, h));
            Card(r, st.card);
            // portrait (photo avant)
            var pr = new Rect(r.x + S(14), r.y + S(14), S(64), S(80));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = Gold;
                GUI.DrawTexture(new Rect(pr.x - S(2), pr.y - S(2), pr.width + S(4), pr.height + S(4)), st.round);
                GUI.color = Color.white;
                if (g.PhotoBefore != null) GUI.DrawTexture(pr, g.PhotoBefore, ScaleMode.ScaleAndCrop);
                else GUI.DrawTexture(pr, st.barBg);
            }
            GUI.Label(new Rect(pr.xMax + S(12), r.y + S(10), w - S(100), S(30)), p.firstName, st.h1);
            // humeur (cœurs)
            for (int i = 0; i < 3; i++)
                Icons.Draw(new Rect(pr.xMax + S(12) + i * S(20), r.y + S(42), S(17), S(17)), "coeur", i < 3 ? UIStyles.Rose : new Color(0.8f, 0.8f, 0.8f));
            var req = p.request;
            var tr = new Rect(pr.xMax + S(12), r.y + S(62), w - S(104), h - S(70));
            GUI.Label(tr, "« " + req.phrase + " »", st.small);
            if (req.colorKind != ColorKind.Aucune && Event.current.type == EventType.Repaint)
            {
                GUI.color = req.dye.color;
                GUI.DrawTexture(new Rect(r.x + S(14), pr.yMax + S(10), S(64), S(18)), st.round);
                GUI.color = Color.white;
            }
        }

        void DrawRightPanel()
        {
            float w = S(290);
            float top = S(82);
            float h = Screen.height - top - S(130);
            var r = Block(new Rect(Screen.width - w - S(14), top, w, h));
            Card(r, st.panel);
            GUILayout.BeginArea(r, st.panel);
            rightScroll = GUILayout.BeginScrollView(rightScroll);

            GUILayout.Label("Objectifs", st.h1);
            foreach (var o in g.Objectives) ObjectiveRow(o);
            if (g.Objectives.Count == 0) GUILayout.Label("Prends ton temps et fais-toi plaisir !", st.small);

            var hair = g.Client.Hair;
            switch (g.Stage)
            {
                case Stage.Coloration: ColorOptions(hair); break;
                case Stage.Coupe: CutOptions(); break;
                case Stage.Bac:
                    if (hair.HasPaste()) GUILayout.Label("<b>Coloration à rincer</b> : passe la douchette partout.", st.label);
                    break;
            }
            if (g.Tools.Current != ToolType.Main)
            {
                GUILayout.Label("Taille de l'outil", st.h2);
                g.Tools.SizeK = GUILayout.HorizontalSlider(g.Tools.SizeK, 0.5f, 2.2f);
                GUILayout.Space(S(10));
            }
            GUILayout.EndScrollView();

            // Étape suivante
            bool done = g.StageComplete;
            Stage next = g.NextStageAfter(g.Stage);
            string label = next == Stage.Resultat ? "Terminer" : "Étape suivante";
            var style = done ? st.bigNext : st.button;
            if (GUILayout.Button(label + "  →", style)) { Click(); g.GoNext(); }
            GUILayout.EndArea();

            // halo doré quand l'étape est terminée
            if (done && Event.current.type == EventType.Repaint)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
                GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.35f * pulse);
                GUI.DrawTexture(new Rect(r.x - S(6), r.yMax - S(74), r.width + S(12), S(70)), st.round);
                GUI.color = Color.white;
            }
        }

        void ObjectiveRow(Objective o)
        {
            var content = new GUIContent(o.text);
            Rect r = GUILayoutUtility.GetRect(content, st.objective);
            r.height = Mathf.Max(r.height, S(26));
            if (Event.current.type == EventType.Repaint)
            {
                var box = new Rect(r.x + S(2), r.y + (r.height - S(20)) / 2, S(20), S(20));
                GUI.color = o.Done ? new Color(0.31f, 0.55f, 0.38f) : new Color(0.88f, 0.84f, 0.79f);
                GUI.DrawTexture(box, st.circle);
                GUI.color = Color.white;
                if (o.Done) Icons.Draw(new Rect(box.x + S(3), box.y + S(3), S(14), S(14)), "check", Color.white);
                else if (o.progress > 0.01f)
                {
                    GUI.color = Gold;
                    GUI.DrawTexture(new Rect(r.x + S(30), r.yMax - S(4), (r.width - S(34)) * o.progress, S(3)), st.white);
                    GUI.color = Color.white;
                }
            }
            GUI.Label(r, o.text + (o.optional && !o.Done ? "" : ""), o.Done ? st.objectiveDone : st.objective);
        }

        void ZoneSelector()
        {
            GUILayout.Label("Zone", st.h2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Toutes", g.Tools.ZoneFilter < 0 ? st.buttonOn : st.buttonSmall)) g.Tools.ZoneFilter = -1;
            if (GUILayout.Button(HairCore.ZoneNames[0], g.Tools.ZoneFilter == 0 ? st.buttonOn : st.buttonSmall)) g.Tools.ZoneFilter = 0;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int z = 1; z < 4; z++)
                if (GUILayout.Button(HairCore.ZoneNames[z], g.Tools.ZoneFilter == z ? st.buttonOn : st.buttonSmall)) g.Tools.ZoneFilter = z;
            GUILayout.EndHorizontal();
        }

        void ColorOptions(HairSystem hair)
        {
            GUILayout.Label("Teinte : " + Shades.Dyes[g.Tools.DyeIndex].name, st.h2);
            int perRow = 6;
            for (int i = 0; i < Shades.Dyes.Length; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int k = i; k < Mathf.Min(i + perRow, Shades.Dyes.Length); k++)
                {
                    Rect rr = GUILayoutUtility.GetRect(S(36), S(36), st.swatch);
                    if (Event.current.type == EventType.Repaint)
                    {
                        if (k == g.Tools.DyeIndex)
                        {
                            GUI.color = Green;
                            GUI.DrawTexture(new Rect(rr.x - S(3), rr.y - S(3), rr.width + S(6), rr.height + S(6)), st.circle);
                        }
                        GUI.color = Shades.Dyes[k].color;
                        GUI.DrawTexture(rr, st.circle);
                        GUI.color = Color.white;
                    }
                    if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) { g.Tools.DyeIndex = k; Click(); }
                }
                GUILayout.EndHorizontal();
            }
            ZoneSelector();
            GUILayout.Space(S(6));
            if (g.PoseRunning)
            {
                GUILayout.Label("Pose en cours… " + Mathf.RoundToInt(g.Develop * 100) + " %", st.label);
                bool fast = g.PoseSpeed > 1f;
                if (GUILayout.Button(fast ? "Vitesse normale" : "Accélérer ×4", st.buttonSmall)) g.PoseSpeed = fast ? 1f : 4f;
            }
            else if (g.Develop >= 1f) GUILayout.Label("Pose terminée : direction le bac pour rincer !", st.label);
            else if (g.Develop > 0f) GUILayout.Label("Pose interrompue (" + Mathf.RoundToInt(g.Develop * 100) + " %).", st.small);
            else if (GUILayout.Button("Lancer le temps de pose", st.buttonOn)) { Click(); g.StartPose(); }
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
            bool guides = GUILayout.Toggle(g.ShowGuides, "  Repères dorés de longueur");
            if (guides != g.ShowGuides) { g.ShowGuides = guides; if (g.Guides != null) g.Guides.SetVisible(guides); }
            GUILayout.Label("Astuce : coupe le long des repères dorés, et tourne autour de la tête pour la nuque.", st.small);
        }

        // ---------------------------------------------------------------- Dock d'outils
        void DrawDock()
        {
            var tools = ToolCatalog.ForStage(g.Stage);
            if (tools.Length == 0) return;
            float b = S(70), gap = S(26);
            float w = tools.Length * b + (tools.Length - 1) * gap + S(48);
            float h = b + S(38);
            var r = Block(new Rect((Screen.width - w) / 2, Screen.height - h - S(14), w, h));
            Card(r, st.panel);
            float x = r.x + S(24);
            foreach (var t in tools)
            {
                var def = ToolCatalog.Get(t);
                bool on = g.Tools.Current == t;
                var br = new Rect(x, r.y + S(8) - (on ? S(4) : 0), b, b);
                if (IconButton(br, def.icon, on ? Green : Color.white, on ? Color.white : Green, on)) { Click(); g.Tools.Current = t; }
                GUI.Label(new Rect(x - S(10), r.y + b + S(10), b + S(20), S(18)), def.name, st.dockLabel);
                x += b + gap;
            }
        }

        void DrawViewPill()
        {
            string[] labels = { "Face", "Gauche", "Dos", "Droite" };
            float[] yaws = { 0f, 90f, 180f, -90f };
            float bw = S(68), bh = S(38);
            float w = bw * 4 + S(16) + S(2 * 44) + S(12);
            var r = Block(new Rect(S(14), Screen.height - bh - S(30), w, bh + S(16)));
            Card(r, st.panel);
            float x = r.x + S(8);
            for (int i = 0; i < 4; i++)
            {
                if (GUI.Button(new Rect(x, r.y + S(8), bw - S(4), bh), labels[i], st.buttonSmall)) { Click(); g.ViewFrom(g.Stage == Stage.Bac ? yaws[i] + 180f : yaws[i]); }
                x += bw;
            }
            x += S(6);
            if (GUI.RepeatButton(new Rect(x, r.y + S(8), S(40), bh), "+", st.buttonSmall)) g.Cam.Zoom(1f - 1.2f * Time.unscaledDeltaTime);
            x += S(44);
            if (GUI.RepeatButton(new Rect(x, r.y + S(8), S(40), bh), "−", st.buttonSmall)) g.Cam.Zoom(1f + 1.2f * Time.unscaledDeltaTime);
        }

        void DrawWelcome()
        {
            var p = g.Client.Profile;
            float w = Mathf.Min(S(560), Screen.width - S(40));
            var r = Block(new Rect((Screen.width - w) / 2, Screen.height - S(250), w, S(200)));
            Card(r, st.bubble);
            GUILayout.BeginArea(r, st.bubble);
            GUILayout.Label("Bonjour, je m'appelle " + p.firstName + " !", st.h1);
            GUILayout.Label("« " + p.request.phrase + " »", st.label);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Installez-vous, c'est parti !  →", st.bigNext, GUILayout.Width(S(360))))
            {
                Click();
                g.WelcomeDone = true;
                g.GoNext();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawCursor()
        {
            if (!hoverValid || g.Tools.Current == ToolType.Main || g.Busy || !g.Tools.OverClient) return;
            if (IsBlocked(hoverPos) || Event.current.type != EventType.Repaint) return;
            float dist = Vector3.Distance(g.MainCam.transform.position, g.HeadWorld);
            float px = g.Tools.Radius / (2f * dist * Mathf.Tan(g.MainCam.fieldOfView * 0.5f * Mathf.Deg2Rad)) * Screen.height;
            px = Mathf.Max(px, S(6));
            GUI.color = g.Tools.PointerActive ? new Color(1f, 0.85f, 0.45f, 0.9f) : new Color(1f, 1f, 1f, 0.65f);
            GUI.DrawTexture(new Rect(hoverPos.x - px, hoverPos.y - px, px * 2, px * 2), st.ring);
            GUI.color = Color.white;
        }

        void DrawToast()
        {
            if (g.Tools.FeedbackTime <= 0 || string.IsNullOrEmpty(g.Tools.Feedback)) return;
            var content = new GUIContent(g.Tools.Feedback);
            float w = Mathf.Min(st.toast.CalcSize(content).x + S(10), Screen.width - S(40));
            float h = st.toast.CalcHeight(content, w);
            var r = new Rect((Screen.width - w) / 2, Screen.height - S(170) - h, w, h);
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(g.Tools.FeedbackTime * 2f));
            GUI.Label(r, content, st.toast);
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- Résultat
        void DrawResult()
        {
            var rep = g.Report;
            float w = Mathf.Min(Screen.width - S(40), S(1100)), h = Mathf.Min(Screen.height - S(110), S(600));
            var r = Block(new Rect((Screen.width - w) / 2, S(84) + (Screen.height - S(84) - h) / 2, w, h));
            Card(r, st.panel);
            GUILayout.BeginArea(r, st.panel);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(w * 0.56f));
            float ph = h - S(70);
            float pw = ph * 0.8f;
            if (pw * 2 + S(16) > w * 0.54f) { pw = (w * 0.54f - S(16)) / 2; ph = pw / 0.8f; }
            GUILayout.BeginHorizontal();
            Photo(g.PhotoBefore, "Avant", pw, ph);
            GUILayout.Space(S(12));
            Photo(g.PhotoAfter, "Après", pw, ph);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(g.Client.Profile.firstName, st.h1);
            Rect sr = GUILayoutUtility.GetRect(w * 0.4f, S(52));
            float t = Time.time - g.ReportTime;
            for (int i = 0; i < 5; i++)
            {
                float appear = Mathf.Clamp01((t - i * 0.18f) / 0.25f);
                float sz = S(42) * (appear < 1f ? Mathf.Lerp(0.3f, 1.15f, appear) : 1f);
                var star = new Rect(sr.x + S(8) + i * S(50) + (S(42) - sz) / 2, sr.y + (S(46) - sz) / 2, sz, sz);
                Icons.Draw(star, "etoile", i < rep.stars && appear > 0 ? Gold : new Color(0.86f, 0.82f, 0.77f));
            }
            GUILayout.Label("« " + rep.mood + " »", st.h2);
            resultScroll = GUILayout.BeginScrollView(resultScroll, GUILayout.Height(h * 0.34f));
            foreach (var line in rep.lines) GUILayout.Label(line, st.label);
            GUILayout.EndScrollView();
            GUILayout.Label("Prestation : <b>" + rep.price + " €</b>     Pourboire : <b>" + rep.tip + " €</b>", st.label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(g.LastSavedPhoto == null ? "Enregistrer la photo avant / après" : "Photo enregistrée ✓", st.button)) { Click(); if (g.LastSavedPhoto == null) g.SavePhoto(); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Retoucher", st.buttonSmall)) { Click(); g.GoToStage(Stage.Coupe); }
            if (GUILayout.Button("Cliente suivante  →", st.bigNext)) { Click(); g.NextClient(); }
            GUILayout.EndHorizontal();
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
                GUI.color = Gold;
                GUI.DrawTexture(new Rect(r.x - S(3), r.y - S(3), r.width + S(6), r.height + S(6)), st.round);
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
            Card(r, st.panel);
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
            Card(r, st.panel);
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
                GUILayout.Label("Aucune photo pour l'instant. Termine une cliente et enregistre la photo avant / après !", st.labelCenter);
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
            Card(r, st.panel);
            GUILayout.BeginArea(r, st.panel);
            GUILayout.Label("Comment jouer ?", st.h1);
            GUILayout.Label(
                "• <b>Appuie sur la cliente</b> pour utiliser l'outil choisi en bas. <b>Appuie à côté</b> et glisse pour tourner autour d'elle.\n" +
                "• Les <b>objectifs</b> à droite se cochent tout seuls. Quand tout est vert, le bouton doré passe à l'étape suivante.\n" +
                "• <b>Couleur</b> : choisis une teinte, peins les mèches, lance le temps de pose, puis rince au bac.\n" +
                "• <b>Bac</b> : douchette, shampoing (frotte !), rinçage, soin, serviette.\n" +
                "• <b>Coupe</b> : trace un trait avec les ciseaux le long des <b>repères dorés</b>. Filtre par zone pour être précise.\n" +
                "• <b>Séchage</b> : passe le séchoir partout.\n" +
                "• <b>Résultat</b> : étoiles, pourboire et photo avant / après pour la galerie.\n\n" +
                "Caméra : boutons Face / Gauche / Dos / Droite, molette ou +/− pour zoomer.",
                st.label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Compris !", st.bigNext)) { Click(); panel = Panel.None; }
            GUILayout.EndArea();
        }

        void Click() { if (g.Audio != null) g.Audio.OneShot(g.Audio.Click, 0.5f); }
    }
}
