using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    public enum ToolType { Main, Douchette, Shampoing, Soin, Serviette, Ciseaux, Tondeuse, Pinceau, Seche }

    public enum Stage { Accueil, Coloration, Bac, Coupe, Sechage, Resultat }

    public class ToolDef
    {
        public ToolType type;
        public string name, hint, icon;
        public float radius;
        public ToolDef(ToolType t, string n, string ic, float r, string h) { type = t; name = n; icon = ic; radius = r; hint = h; }
    }

    public static class ToolCatalog
    {
        static readonly Dictionary<ToolType, ToolDef> defs = new Dictionary<ToolType, ToolDef>
        {
            { ToolType.Main, new ToolDef(ToolType.Main, "Regarder", "tourner", 0f, "Glisse pour tourner autour de la cliente.") },
            { ToolType.Douchette, new ToolDef(ToolType.Douchette, "Douchette", "douchette", 0.075f, "Passe sur les cheveux pour mouiller, rincer la mousse et la coloration.") },
            { ToolType.Shampoing, new ToolDef(ToolType.Shampoing, "Shampoing", "shampoing", 0.065f, "Frotte les cheveux mouillés pour faire mousser.") },
            { ToolType.Soin, new ToolDef(ToolType.Soin, "Soin", "soin", 0.07f, "Applique le soin sur les longueurs (cheveux mouillés).") },
            { ToolType.Serviette, new ToolDef(ToolType.Serviette, "Serviette", "serviette", 0.09f, "Essore en frottant.") },
            { ToolType.Ciseaux, new ToolDef(ToolType.Ciseaux, "Ciseaux", "ciseaux", 0.011f, "Trace un trait à travers les mèches : elles sont coupées le long du trait.") },
            { ToolType.Tondeuse, new ToolDef(ToolType.Tondeuse, "Tondeuse", "tondeuse", 0.035f, "Passe sur le crâne : coupe à la hauteur du sabot.") },
            { ToolType.Pinceau, new ToolDef(ToolType.Pinceau, "Pinceau", "pinceau", 0.03f, "Peins la coloration sur les mèches.") },
            { ToolType.Seche, new ToolDef(ToolType.Seche, "Séchoir", "seche", 0.1f, "Passe partout pour sécher et donner du volume.") },
        };

        public static ToolDef Get(ToolType t) { return defs[t]; }

        public static ToolType[] ForStage(Stage s)
        {
            switch (s)
            {
                case Stage.Coloration: return new[] { ToolType.Pinceau };
                case Stage.Bac: return new[] { ToolType.Douchette, ToolType.Shampoing, ToolType.Soin, ToolType.Serviette };
                case Stage.Coupe: return new[] { ToolType.Ciseaux, ToolType.Tondeuse };
                case Stage.Sechage: return new[] { ToolType.Seche, ToolType.Serviette };
                default: return new ToolType[0];
            }
        }
    }

    /// <summary>
    /// Applique l'outil choisi. Règle simple : si on appuie SUR la cliente, l'outil agit ;
    /// si on appuie à côté, on tourne la caméra.
    /// </summary>
    public class ToolController
    {
        public ToolType Current = ToolType.Main;
        public float SizeK = 1f;
        public int ZoneFilter = -1;
        public int SabotIndex = 3;
        public int DyeIndex = 6;

        public bool PointerActive;      // l'outil travaille
        public Vector2 PointerPos;      // écran, origine en bas à gauche
        public Vector2 HoverPos;
        public bool HoverValid;
        public bool OverClient;         // le pointeur survole la cliente
        public Vector3 HitPoint;

        public static readonly int[] SabotMm = { 1, 3, 6, 9, 12, 16, 19, 22, 25 };

        public string Feedback = "";
        public float FeedbackTime;

        float snipTimer, clipTimer, bubbleTimer, probeTimer;
        Vector2 lastCutPos;
        bool cutStarted;
        readonly List<Vector3> bubbleHits = new List<Vector3>();

        public ToolDef Def { get { return ToolCatalog.Get(Current); } }
        public float Radius { get { return Def.radius * SizeK; } }

        public void Say(string msg, float seconds = 2.6f)
        {
            Feedback = msg;
            FeedbackTime = seconds;
        }

        /// <summary>Le point écran vise-t-il la cliente (tête / cheveux) ?</summary>
        public bool IsOnClient(SalonGame g, Vector2 screenPos)
        {
            if (g.Client == null) return false;
            Ray ray = g.MainCam.ScreenPointToRay(screenPos);
            if (g.Client.Hair.RayHitsHead(ray, 0.05f)) return true;
            Vector3 hit;
            return g.Client.Hair.Probe(ray, 0.02f, out hit);
        }

        public void BeginStroke(Vector2 screenPos)
        {
            PointerActive = true;
            PointerPos = screenPos;
            lastCutPos = screenPos;
            cutStarted = false;
            snipTimer = 0;
        }

        public void EndStroke() { PointerActive = false; }

        public void Tick(SalonGame g, float dt)
        {
            FeedbackTime -= dt;
            var client = g.Client;
            float windAmp = 0.01f, windSpeed = 1.3f;
            Vector3 windDir = new Vector3(1f, 0f, 0.3f).normalized;
            Color dye = Shades.Dyes[DyeIndex].color;

            // Survol : position de l'outil 3D
            probeTimer -= dt;
            if (client != null && Current != ToolType.Main && (HoverValid || PointerActive) && !g.Busy)
            {
                Vector2 p = PointerActive ? PointerPos : HoverPos;
                Ray hr = g.MainCam.ScreenPointToRay(p);
                Vector3 hit;
                if (probeTimer <= 0 || PointerActive)
                {
                    probeTimer = 0.03f;
                    OverClient = client.Hair.Probe(hr, Mathf.Max(0.01f, Radius * 0.6f), out hit);
                    if (!OverClient && client.Hair.RayHitsHead(hr, 0.0f))
                    {
                        // vise le crâne : point approximatif sur l'ellipsoïde
                        hit = hr.origin + hr.direction * (Vector3.Distance(hr.origin, g.HeadWorld) - 0.09f);
                        OverClient = true;
                    }
                    if (OverClient) HitPoint = hit;
                }
                if (OverClient) g.Props.Show(Current, HitPoint, g.MainCam.transform.position, dye, PointerActive);
                else g.Props.Hide();
            }
            else g.Props.Hide();

            bool working = PointerActive && Current != ToolType.Main && client != null && !g.Busy;
            if (!working)
            {
                g.Audio.StopLoop();
                if (client != null) client.Hair.SetWind(windDir, windAmp, windSpeed);
                return;
            }

            var hair = client.Hair;
            Ray ray = g.MainCam.ScreenPointToRay(PointerPos);
            float r = Radius;

            switch (Current)
            {
                case ToolType.Douchette:
                {
                    int n = hair.Water(ray, r, 6f * dt, g.Develop);
                    g.Foam.RinseNear(ray, r);
                    g.Foam.Spray(HitPoint + (g.MainCam.transform.position - HitPoint).normalized * 0.07f, HitPoint);
                    g.Audio.Loop(g.Audio.Water, 0.5f);
                    if (n > 0 && g.PoseRunning)
                    {
                        g.PoseRunning = false;
                        Say("Rinçage avant la fin de la pose : la couleur sera moins intense.");
                    }
                    break;
                }
                case ToolType.Shampoing:
                {
                    bubbleHits.Clear();
                    int n = hair.Lather(ray, r, 2.6f * dt, bubbleHits);
                    if (n < 0) Say("Mouille d'abord les cheveux avec la douchette !");
                    bubbleTimer -= dt;
                    if (n > 0 && bubbleTimer <= 0)
                    {
                        bubbleTimer = 0.03f;
                        foreach (var p in bubbleHits) g.Foam.SpawnBubble(p, client.Head);
                    }
                    g.Audio.Loop(g.Audio.Squish, 0.35f);
                    break;
                }
                case ToolType.Soin:
                {
                    int n = hair.Condition(ray, r, 3f * dt);
                    if (n < 0) Say("Le soin s'applique sur cheveux mouillés.");
                    g.Audio.Loop(g.Audio.Squish, 0.25f);
                    break;
                }
                case ToolType.Serviette:
                    hair.Dry(ray, r, 2.2f * dt, 0.4f);
                    g.Audio.Loop(g.Audio.Towel, 0.4f);
                    break;
                case ToolType.Seche:
                    hair.Dry(ray, r, 1.3f * dt, 0f);
                    windAmp = 0.07f; windSpeed = 9f;
                    windDir = g.MainCam.transform.forward;
                    g.Audio.Loop(g.Audio.Dryer, 0.45f);
                    break;
                case ToolType.Ciseaux:
                {
                    g.Audio.StopLoop();
                    // trait continu : on échantillonne le geste tous les quelques pixels
                    Vector2 from = cutStarted ? lastCutPos : PointerPos;
                    float dist = Vector2.Distance(from, PointerPos);
                    int steps = Mathf.Clamp(Mathf.CeilToInt(dist / 4f), 1, 40);
                    int cut = 0;
                    for (int i = 1; i <= steps; i++)
                    {
                        Vector2 sp = Vector2.Lerp(from, PointerPos, (float)i / steps);
                        cut += hair.Cut(g.MainCam.ScreenPointToRay(sp), r, ZoneFilter, g.Clippings);
                    }
                    if (!cutStarted) cut += hair.Cut(ray, r, ZoneFilter, g.Clippings);
                    cutStarted = true;
                    lastCutPos = PointerPos;
                    snipTimer -= dt;
                    if (snipTimer <= 0 && (dist > 1f || cut > 0))
                    {
                        snipTimer = 0.11f;
                        g.Audio.OneShot(g.Audio.Snip, cut > 0 ? 0.85f : 0.4f);
                        g.Props.Snip();
                    }
                    break;
                }
                case ToolType.Tondeuse:
                    clipTimer -= dt;
                    if (clipTimer <= 0)
                    {
                        clipTimer = 0.04f;
                        hair.Clip(ray, r, ZoneFilter, SabotMm[SabotIndex] / 1000f, g.Clippings);
                    }
                    g.Audio.Loop(g.Audio.Clipper, 0.4f);
                    break;
                case ToolType.Pinceau:
                    g.Audio.StopLoop();
                    if (g.PoseRunning) { Say("Temps de pose en cours : patiente ou va rincer au bac."); break; }
                    if (g.Develop > 0) { Say("Rince d'abord la coloration précédente au bac."); break; }
                    hair.Paint(ray, r, ZoneFilter, dye, 6f * dt);
                    g.Audio.Loop(g.Audio.Squish, 0.15f);
                    break;
            }
            hair.SetWind(windDir, windAmp, windSpeed);
        }
    }
}
