using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Une teinte proposée (naturelle ou fantaisie).</summary>
    public struct Shade
    {
        public string name;
        public Color color;
        public Shade(string n, string hex) { name = n; color = SalonUtil.Hex(hex); }
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
            SalonUtil.Hex("#F8DDCB"), SalonUtil.Hex("#F0CCB0"), SalonUtil.Hex("#DDAB86"),
            SalonUtil.Hex("#C48E68"), SalonUtil.Hex("#9A6A4B"), SalonUtil.Hex("#6E4A35"),
        };

        public static readonly Color[] Eyes =
        {
            SalonUtil.Hex("#5B3A22"), SalonUtil.Hex("#3B2416"), SalonUtil.Hex("#7A6A3A"),
            SalonUtil.Hex("#4F7A5A"), SalonUtil.Hex("#4C7FB0"), SalonUtil.Hex("#6E7F8C"),
        };
    }

    public class CustomerProfile
    {
        public string firstName;
        public Color skin, eyes;
        public Shade naturalHair;
        public float hairLength, waviness;
        public int seed;
        public HairRequest request;

        static readonly string[] Names =
        {
            "Camille", "Léa", "Chloé", "Manon", "Inès", "Jade", "Louise", "Zoé", "Alice", "Lina",
            "Rose", "Anna", "Juliette", "Margaux", "Sarah", "Emma", "Clara", "Nina", "Mila", "Eva",
        };

        public static CustomerProfile Random(int seed)
        {
            var rng = new System.Random(seed);
            var p = new CustomerProfile();
            p.seed = seed;
            p.firstName = Names[rng.Next(Names.Length)];
            p.skin = Shades.Skins[rng.Next(Shades.Skins.Length)];
            p.eyes = Shades.Eyes[rng.Next(Shades.Eyes.Length)];
            p.naturalHair = Shades.Natural[rng.Next(Shades.Natural.Length)];
            p.hairLength = 0.36f + (float)rng.NextDouble() * 0.08f;
            p.waviness = rng.NextDouble() < 0.45 ? (float)rng.NextDouble() * 0.9f : 0f;
            p.request = HairRequest.Random(rng, p.naturalHair);
            return p;
        }

        public HairConfig HairConfig()
        {
            return new HairConfig { natural = naturalHair.color, length = hairLength, waviness = waviness, seed = seed };
        }
    }
}
