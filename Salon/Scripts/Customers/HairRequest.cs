using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    public enum TargetKind { Aucune, Hauteur, Longueur, Pointes }
    public enum ColorKind { Aucune, Complete, Meches }

    public struct ZoneTarget
    {
        public TargetKind kind;
        public float value;
        public ZoneTarget(TargetKind k, float v) { kind = k; value = v; }
    }

    /// <summary>Ce qui s'est passé pendant la séance (pour la note finale).</summary>
    public struct SessionInfo
    {
        public float maxFoam;
        public float maxCare;
        public float colorDevelop;
    }

    public class ScoreReport
    {
        public float score;
        public int stars;
        public int price;
        public int tip;
        public string mood;
        public readonly List<string> lines = new List<string>();
    }

    /// <summary>Demande de la cliente : coupe par zone + coloration éventuelle, et notation.</summary>
    public class HairRequest
    {
        public string cutName;
        public string phrase;
        public readonly ZoneTarget[] zones = new ZoneTarget[4];
        public ColorKind colorKind;
        public Shade dye;
        public Shade natural;

        // Repères de hauteur (mètres, centre de la tête = 0)
        const float Menton = -0.12f, Machoire = -0.15f, Epaules = -0.22f, Pommettes = 0.0f;

        public static HairRequest Random(System.Random rng, Shade natural)
        {
            var r = new HairRequest { natural = natural };
            int k = rng.Next(6);
            switch (k)
            {
                case 0:
                    r.cutName = "un carré court au niveau du menton";
                    r.SetAll(TargetKind.Hauteur, Menton);
                    r.zones[(int)HairZone.Devant] = new ZoneTarget(TargetKind.Hauteur, Menton + 0.01f);
                    break;
                case 1:
                    r.cutName = "juste rafraîchir les pointes, je garde ma longueur";
                    r.SetAll(TargetKind.Pointes, 0.03f);
                    break;
                case 2:
                    r.cutName = "un carré plongeant : court derrière, plus long devant";
                    r.zones[(int)HairZone.Nuque] = new ZoneTarget(TargetKind.Hauteur, -0.08f);
                    r.zones[(int)HairZone.Cotes] = new ZoneTarget(TargetKind.Hauteur, Machoire);
                    r.zones[(int)HairZone.Devant] = new ZoneTarget(TargetKind.Hauteur, Machoire);
                    r.zones[(int)HairZone.Dessus] = new ZoneTarget(TargetKind.Hauteur, Menton);
                    break;
                case 3:
                    r.cutName = "un mi-long aux épaules avec une frange rideau";
                    r.SetAll(TargetKind.Hauteur, Epaules);
                    r.zones[(int)HairZone.Devant] = new ZoneTarget(TargetKind.Hauteur, Pommettes);
                    break;
                case 4:
                    r.cutName = "une coupe courte façon pixie";
                    r.zones[(int)HairZone.Devant] = new ZoneTarget(TargetKind.Longueur, 0.07f);
                    r.zones[(int)HairZone.Dessus] = new ZoneTarget(TargetKind.Longueur, 0.07f);
                    r.zones[(int)HairZone.Cotes] = new ZoneTarget(TargetKind.Longueur, 0.04f);
                    r.zones[(int)HairZone.Nuque] = new ZoneTarget(TargetKind.Longueur, 0.03f);
                    break;
                default:
                    r.cutName = "un dégradé long avec des mèches qui encadrent le visage";
                    r.zones[(int)HairZone.Devant] = new ZoneTarget(TargetKind.Hauteur, -0.06f);
                    r.zones[(int)HairZone.Cotes] = new ZoneTarget(TargetKind.Hauteur, -0.18f);
                    r.zones[(int)HairZone.Dessus] = new ZoneTarget(TargetKind.Hauteur, -0.14f);
                    r.zones[(int)HairZone.Nuque] = new ZoneTarget(TargetKind.Hauteur, -0.24f);
                    break;
            }

            double c = rng.NextDouble();
            if (c < 0.4) r.colorKind = ColorKind.Aucune;
            else
            {
                r.colorKind = c < 0.75 ? ColorKind.Complete : ColorKind.Meches;
                do { r.dye = Shades.Dyes[rng.Next(Shades.Dyes.Length)]; }
                while (Dist(r.dye.color, natural.color) < 0.15f);
            }

            string col = "";
            if (r.colorKind == ColorKind.Complete) col = " Et je voudrais une couleur " + r.dye.name.ToLower() + " sur toute la chevelure !";
            if (r.colorKind == ColorKind.Meches) col = " Avec quelques mèches " + r.dye.name.ToLower() + " (balayage).";
            r.phrase = "J'aimerais " + r.cutName + "." + col;
            return r;
        }

        void SetAll(TargetKind k, float v)
        {
            for (int i = 0; i < 4; i++) zones[i] = new ZoneTarget(k, v);
        }

        static float Dist(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        public string ZoneHint(int z)
        {
            var t = zones[z];
            switch (t.kind)
            {
                case TargetKind.Hauteur:
                    if (t.value >= -0.02f) return "à hauteur des pommettes";
                    if (t.value >= -0.1f) return "au-dessus du menton";
                    if (t.value >= -0.135f) return "au menton";
                    if (t.value >= -0.17f) return "à la mâchoire";
                    if (t.value >= -0.2f) return "au cou";
                    return "aux épaules";
                case TargetKind.Longueur: return "environ " + Mathf.RoundToInt(t.value * 100) + " cm";
                case TargetKind.Pointes: return "couper 2 à 5 cm";
            }
            return "-";
        }

        public ScoreReport Evaluate(HairSystem hair, SessionInfo info, System.Random rng)
        {
            var rep = new ScoreReport();
            int n = hair.StrandCount;

            // ---------- Coupe ----------
            float cutScore = 0; int zonesCount = 0;
            for (int z = 0; z < 4; z++)
            {
                var t = zones[z];
                if (t.kind == TargetKind.Aucune) continue;
                float sumAdj = 0, sumSigned = 0; int cnt = 0;
                for (int s = 0; s < n; s++)
                {
                    if ((int)hair.Zone(s) != z) continue;
                    float tooShort; // > 0 : trop court, < 0 : trop long
                    switch (t.kind)
                    {
                        case TargetKind.Hauteur: tooShort = hair.TipHeight(s) - t.value; break;
                        case TargetKind.Longueur: tooShort = t.value - hair.Length(s); break;
                        default:
                            float cut = hair.OriginalLength(s) - hair.Length(s);
                            tooShort = cut < 0.015f ? cut - 0.03f : (cut > 0.06f ? cut - 0.06f : 0f);
                            break;
                    }
                    sumSigned += tooShort;
                    sumAdj += tooShort > 0 ? tooShort * 1.6f : -tooShort;
                    cnt++;
                }
                if (cnt == 0) continue;
                float mean = sumAdj / cnt, signed = sumSigned / cnt;
                float zs = Mathf.Clamp01(1f - mean / 0.05f);
                cutScore += zs; zonesCount++;
                string zn = HairSystem.ZoneNames[z];
                if (zs > 0.8f) rep.lines.Add("✓ " + zn + " : parfait !");
                else if (signed < 0) rep.lines.Add("✗ " + zn + " : encore trop long (" + ZoneHint(z) + ").");
                else rep.lines.Add("✗ " + zn + " : un peu trop court…");
            }
            cutScore = zonesCount > 0 ? cutScore / zonesCount : 1f;

            // ---------- Couleur ----------
            float colorScore = 1f;
            Color expected = Color.Lerp(natural.color, dye.color, 0.88f);
            if (colorKind == ColorKind.Complete)
            {
                float d = 0;
                for (int s = 0; s < n; s++) d += Dist(hair.StrandColor(s), expected);
                d /= Mathf.Max(1, n);
                colorScore = Mathf.Clamp01(1f - (d - 0.04f) / 0.25f);
                rep.lines.Add(colorScore > 0.75f ? "✓ Couleur " + dye.name.ToLower() + " : magnifique !" : "✗ La couleur n'est pas uniforme ou pas assez posée.");
            }
            else if (colorKind == ColorKind.Meches)
            {
                int dyed = 0;
                for (int s = 0; s < n; s++)
                {
                    Color c = hair.StrandColor(s);
                    if (Dist(c, expected) < Dist(c, natural.color)) dyed++;
                }
                float f = (float)dyed / Mathf.Max(1, n);
                colorScore = f < 0.15f ? f / 0.15f : (f > 0.55f ? Mathf.Clamp01(1f - (f - 0.55f) / 0.35f) : 1f);
                rep.lines.Add(colorScore > 0.75f ? "✓ Balayage réussi, très naturel !" : (f < 0.15f ? "✗ Il manque des mèches." : "✗ Trop de mèches, c'est presque une couleur complète."));
            }
            else
            {
                float d = 0;
                for (int s = 0; s < n; s++) d += Dist(hair.StrandColor(s), natural.color);
                d /= Mathf.Max(1, n);
                if (d > 0.1f) { colorScore = 0.3f; rep.lines.Add("✗ Je n'avais pas demandé de couleur !"); }
            }

            // ---------- Soins ----------
            float care = 1f;
            if (info.maxFoam < 0.45f) { care -= 0.3f; rep.lines.Add("✗ Le shampoing était un peu rapide."); }
            else rep.lines.Add("✓ Shampoing très agréable.");
            if (hair.AverageFoam() > 0.05f) { care -= 0.3f; rep.lines.Add("✗ Il reste de la mousse !"); }
            if (hair.HasPaste()) { care -= 0.5f; colorScore *= 0.5f; rep.lines.Add("✗ Il reste de la coloration non rincée !"); }
            if (hair.AverageWet() > 0.2f) { care -= 0.25f; rep.lines.Add("✗ Mes cheveux sont encore mouillés."); }
            if (info.maxCare > 0.4f) { care += 0.1f; rep.lines.Add("✓ Le soin rend mes cheveux tout doux."); }
            care = Mathf.Clamp01(care);

            float wColor = colorKind == ColorKind.Aucune ? 0.1f : 0.3f;
            rep.score = cutScore * (0.65f - wColor * 0.5f) + colorScore * wColor + care * (0.35f - wColor * 0.5f);
            rep.score = Mathf.Clamp01(rep.score);
            rep.stars = Mathf.Clamp(Mathf.RoundToInt(rep.score * 5f), 1, 5);
            rep.price = 35 + (colorKind == ColorKind.Complete ? 45 : colorKind == ColorKind.Meches ? 55 : 0) + (info.maxCare > 0.4f ? 5 : 0);
            rep.tip = rep.stars >= 4 ? (rep.stars - 3) * 3 + rng.Next(0, 4) : 0;
            string[] moods = { "", "Très déçue…", "Pas convaincue.", "Ça va, merci.", "Ravie !", "Absolument enchantée !" };
            rep.mood = moods[rep.stars];
            return rep;
        }
    }
}
