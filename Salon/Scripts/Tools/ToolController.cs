using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    public enum ToolType { Main, Douchette, Shampoing, Soin, Serviette, Ciseaux, Tondeuse, Pinceau, Seche }

    public enum Stage { Accueil, Coloration, Bac, Coupe, Sechage, Resultat }

    public class ToolDef
    {
        public ToolType type;
        public string name, hint;
        public float radius;
        public ToolDef(ToolType t, string n, float r, string h) { type = t; name = n; radius = r; hint = h; }
    }

    public static class ToolCatalog
    {
        static readonly Dictionary<ToolType, ToolDef> defs = new Dictionary<ToolType, ToolDef>
        {
            { ToolType.Main, new ToolDef(ToolType.Main, "Tourner la vue", 0f, "Glisse pour tourner autour de la cliente.") },
            { ToolType.Douchette, new ToolDef(ToolType.Douchette, "Douchette", 0.055f, "Mouille et rince les cheveux. Rince aussi la coloration.") },
            { ToolType.Shampoing, new ToolDef(ToolType.Shampoing, "Shampoing", 0.045f, "Masse les cheveux mouillés pour faire mousser.") },
            { ToolType.Soin, new ToolDef(ToolType.Soin, "Après-shampoing", 0.05f, "Applique le soin sur les longueurs mouillées.") },
            { ToolType.Serviette, new ToolDef(ToolType.Serviette, "Serviette", 0.07f, "Essore les cheveux en douceur.") },
            { ToolType.Ciseaux, new ToolDef(ToolType.Ciseaux, "Ciseaux", 0.012f, "Coupe les mèches exactement là où tu passes.") },
            { ToolType.Tondeuse, new ToolDef(ToolType.Tondeuse, "Tondeuse", 0.03f, "Passe sur le crâne : coupe à la hauteur du sabot.") },
            { ToolType.Pinceau, new ToolDef(ToolType.Pinceau, "Pinceau", 0.022f, "Peins la coloration sur les mèches.") },
            { ToolType.Seche, new ToolDef(ToolType.Seche, "Sèche-cheveux", 0.08f, "Sèche les cheveux et leur redonne du volume.") },
        };

        public static ToolDef Get(ToolType t) { return defs[t]; }

        public static ToolType[] ForStage(Stage s)
        {
            switch (s)
            {
                case Stage.Coloration: return new[] { ToolType.Pinceau, ToolType.Main };
                case Stage.Bac: return new[] { ToolType.Douchette, ToolType.Shampoing, ToolType.Soin, ToolType.Serviette, ToolType.Main };
                case Stage.Coupe: return new[] { ToolType.Ciseaux, ToolType.Tondeuse, ToolType.Main };
                case Stage.Sechage: return new[] { ToolType.Seche, ToolType.Serviette, ToolType.Main };
                default: return new[] { ToolType.Main };
            }
        }
    }

    /// <summary>Applique l'outil choisi là où le doigt / la souris appuie.</summary>
    public class ToolController
    {
        public ToolType Current = ToolType.Main;
        public float SizeK = 1f;
        public int ZoneFilter = -1;
        public int SabotIndex = 3;
        public int DyeIndex = 6;
        public bool PointerActive;
        public Vector2 PointerPos; // écran, origine en bas à gauche
        public bool PointerOverHair;

        public static readonly int[] SabotMm = { 1, 3, 6, 9, 12, 16, 19, 22, 25 };

        public string Feedback = "";
        public float FeedbackTime;

        float snipTimer, clipTimer, bubbleTimer, scalpTimer;
        readonly List<Vector3> bubbleHits = new List<Vector3>();

        public ToolDef Def { get { return ToolCatalog.Get(Current); } }
        public float Radius { get { return Def.radius * SizeK; } }

        public void Say(string msg, float seconds = 2.5f)
        {
            Feedback = msg;
            FeedbackTime = seconds;
        }

        public void Tick(SalonGame g, float dt)
        {
            FeedbackTime -= dt;
            var client = g.Client;
            bool working = PointerActive && Current != ToolType.Main && client != null && !g.Busy;
            float windAmp = 0.012f, windSpeed = 1.3f;
            Vector3 windDir = new Vector3(1f, 0f, 0.3f).normalized;

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
                    bool hadPaste = g.PoseRunning || g.Develop > 0;
                    int n = hair.Water(ray, r, 2.5f * dt, g.Develop);
                    g.Foam.RinseNear(ray, r);
                    Vector3 hit;
                    if (hair.Probe(ray, r, out hit))
                        g.Foam.Spray(g.MainCam.transform.position + g.MainCam.transform.right * 0.12f - g.MainCam.transform.up * 0.05f, hit);
                    g.Audio.Loop(g.Audio.Water, 0.5f);
                    if (n > 0 && g.PoseRunning)
                    {
                        g.PoseRunning = false;
                        Say("Rinçage avant la fin de la pose : la couleur sera moins intense.");
                    }
                    scalpTimer -= dt;
                    if (hadPaste && scalpTimer <= 0) { scalpTimer = 0.4f; client.SetScalpColor(hair.AverageRootColor()); }
                    break;
                }
                case ToolType.Shampoing:
                {
                    bubbleHits.Clear();
                    int n = hair.Lather(ray, r, 1.1f * dt, bubbleHits);
                    if (n < 0) Say("Mouille d'abord les cheveux avec la douchette !");
                    bubbleTimer -= dt;
                    if (n > 0 && bubbleTimer <= 0)
                    {
                        bubbleTimer = 0.04f;
                        foreach (var p in bubbleHits) g.Foam.SpawnBubble(p, client.Head);
                    }
                    g.Audio.Loop(g.Audio.Squish, 0.35f);
                    break;
                }
                case ToolType.Soin:
                {
                    int n = hair.Condition(ray, r, 1.4f * dt);
                    if (n < 0) Say("Le soin s'applique sur cheveux mouillés.");
                    g.Audio.Loop(g.Audio.Squish, 0.25f);
                    break;
                }
                case ToolType.Serviette:
                    hair.Dry(ray, r, 1.0f * dt, 0.45f);
                    g.Audio.Loop(g.Audio.Towel, 0.4f);
                    break;
                case ToolType.Seche:
                    hair.Dry(ray, r, 0.45f * dt, 0f);
                    windAmp = 0.07f; windSpeed = 9f;
                    windDir = g.MainCam.transform.forward;
                    g.Audio.Loop(g.Audio.Dryer, 0.45f);
                    break;
                case ToolType.Ciseaux:
                    g.Audio.StopLoop();
                    snipTimer -= dt;
                    if (snipTimer <= 0)
                    {
                        snipTimer = 0.14f;
                        hair.Cut(ray, r, ZoneFilter, g.Clippings);
                        g.Audio.OneShot(g.Audio.Snip, 0.8f);
                    }
                    break;
                case ToolType.Tondeuse:
                    clipTimer -= dt;
                    if (clipTimer <= 0)
                    {
                        clipTimer = 0.06f;
                        hair.Clip(ray, r, ZoneFilter, SabotMm[SabotIndex] / 1000f, g.Clippings);
                    }
                    g.Audio.Loop(g.Audio.Clipper, 0.4f);
                    break;
                case ToolType.Pinceau:
                    g.Audio.StopLoop();
                    if (g.PoseRunning) { Say("Temps de pose en cours… patiente ou rince au bac."); break; }
                    if (g.Develop > 0) { Say("Rince d'abord la coloration précédente au bac."); break; }
                    hair.Paint(ray, r, ZoneFilter, Shades.Dyes[DyeIndex].color, 2.5f * dt);
                    break;
            }
            hair.SetWind(windDir, windAmp, windSpeed);
        }
    }
}
