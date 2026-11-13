using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Une teinte proposée (naturelle ou fantaisie).</summary>
    public struct Shade
    {
        public string name;
        public Color color;
        public Shade(string n, string hex) { name = n; color = SalonMath.Hex(hex); }
    }

    public static class Shades
    {
        public static readonly Shade[] Natural =
        {
            new Shade("Noir", "#16110E"),
            new Shade("Brun foncé", "#33231A"),
            new Shade("Châtain", "#5C3D27"),
            new Shade("Châtain clair", "#85613F"),
            new Shade("Blond foncé", "#A6814F"),
            new Shade("Blond", "#D3B37A"),
            new Shade("Roux", "#A2481F"),
            new Shade("Auburn", "#6B2A16"),
        };

        public static readonly Shade[] Dyes =
        {
            new Shade("Noir corbeau", "#141018"),
            new Shade("Chocolat", "#4A2C1E"),
            new Shade("Châtain doré", "#7A5532"),
            new Shade("Caramel", "#B07A45"),
            new Shade("Blond miel", "#D6AE6C"),
            new Shade("Blond platine", "#ECE2CB"),
            new Shade("Cuivré", "#B85C2A"),
            new Shade("Rouge cerise", "#8E1426"),
            new Shade("Rose pastel", "#F0A8BC"),
            new Shade("Lavande", "#B49FDB"),
            new Shade("Bleu nuit", "#202A5A"),
            new Shade("Menthe", "#8FD8BE"),
        };

        public static readonly Color[] Skins =
        {
            SalonMath.Hex("#F8DDCB"), SalonMath.Hex("#F0CCB0"), SalonMath.Hex("#DDAB86"),
            SalonMath.Hex("#C48E68"), SalonMath.Hex("#9A6A4B"), SalonMath.Hex("#6E4A35"),
        };

        public static readonly Color[] Eyes =
        {
            SalonMath.Hex("#5B3A22"), SalonMath.Hex("#3B2416"), SalonMath.Hex("#7A6A3A"),
            SalonMath.Hex("#4F7A5A"), SalonMath.Hex("#4C7FB0"), SalonMath.Hex("#6E7F8C"),
        };
    }

    public class CustomerProfile
    {
        public string firstName;
        public Color skin, eyes;
        public Shade naturalHair;
        public float hairLength, waviness;
        public bool bangs;
        public int seed;
        public HairRequest request;
        public FaceParams face;

        static readonly string[] Names =
        {
            "Camille", "Léa", "Chloé", "Manon", "Inès", "Jade", "Louise", "Zoé", "Alice", "Lina",
            "Rose", "Anna", "Juliette", "Margaux", "Sarah", "Emma", "Clara", "Nina", "Mila", "Eva",
        };

        static readonly string[] LipColors = { "#C77079", "#B85A66", "#D4847F", "#A84E5E", "#C9787A", "#9E4A55" };
        static readonly string[] Shadows = { "#B98A7A", "#A88AA0", "#B08F72", "#C79A93", "#9A8478" };

        public static CustomerProfile Random(int seed)
        {
            var rng = new System.Random(seed);
            var p = new CustomerProfile();
            p.seed = seed;
            p.firstName = Names[rng.Next(Names.Length)];
            int skinIdx = rng.Next(Shades.Skins.Length);
            p.skin = Shades.Skins[skinIdx];
            p.eyes = Shades.Eyes[rng.Next(Shades.Eyes.Length)];
            // cheveux naturels plausibles selon le teint
            int hairIdx = skinIdx >= 4 ? rng.Next(0, 3) : rng.Next(Shades.Natural.Length);
            p.naturalHair = Shades.Natural[hairIdx];
            p.hairLength = 0.36f + (float)rng.NextDouble() * 0.08f;
            p.waviness = rng.NextDouble() < 0.4 ? 0.35f + (float)rng.NextDouble() * 0.6f : 0f;
            p.bangs = rng.NextDouble() < 0.25;
            p.request = HairRequest.Random(rng, p.naturalHair);

            var f = new FaceParams();
            f.seed = seed;
            f.skin = p.skin;
            f.iris = p.eyes;
            f.brows = Color.Lerp(SalonMath.Mul(p.naturalHair.color, 0.75f), new Color(0.2f, 0.14f, 0.1f), 0.3f);
            f.scalp = SalonMath.Mul(p.naturalHair.color, 0.8f);
            Color lip = SalonMath.Hex(LipColors[rng.Next(LipColors.Length)]);
            f.lips = Color.Lerp(lip, SalonMath.Mul(p.skin, 0.7f), skinIdx >= 4 ? 0.45f : 0.15f);
            f.shadow = Color.Lerp(SalonMath.Hex(Shadows[rng.Next(Shadows.Length)]), p.skin, 0.25f);
            f.blush = Color.Lerp(SalonMath.Hex("#F08C8C"), SalonMath.Hex("#E07A6A"), (float)rng.NextDouble());
            f.blushAmount = 0.2f + (float)rng.NextDouble() * 0.25f;
            f.liner = rng.NextDouble() < 0.7 ? 0.7f + 0.3f * (float)rng.NextDouble() : 0.25f;
            f.freckles = (skinIdx <= 1 && rng.NextDouble() < 0.35) ? 0.6f + 0.4f * (float)rng.NextDouble() : 0f;
            f.jaw = 0.18f + (float)rng.NextDouble() * 0.1f;
            f.nose = 0.85f + (float)rng.NextDouble() * 0.25f;
            f.lipFull = 0.9f + (float)rng.NextDouble() * 0.2f;
            f.eyeSize = 1.04f + (float)rng.NextDouble() * 0.1f;
            p.face = f;
            return p;
        }

        public HairConfig HairConfig()
        {
            return new HairConfig { natural = naturalHair.color, length = hairLength, waviness = waviness, bangs = bangs, seed = seed };
        }
    }
}
