using System;
using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>
    /// Icônes vectorielles dessinées par le code (formes à distance signée, anticrénelées).
    /// Blanc sur transparent : on les teinte à l'affichage.
    /// </summary>
    public static class IconGen
    {
        public static readonly string[] Names =
        {
            "ciseaux", "tondeuse", "douchette", "shampoing", "soin", "serviette", "pinceau", "seche",
            "tourner", "check", "etoile", "coeur", "maison", "photo", "reglages", "suivant",
            "piece", "plus", "moins", "accueil", "bac", "coupe", "couleur", "resultat", "galerie", "aide",
        };

        // ------------------------------------------------------------ Primitives SDF (unités : 0..1)
        static float Circle(float x, float y, float cx, float cy, float r) { return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r; }

        static float Ring(float x, float y, float cx, float cy, float r, float th) { return Mathf.Abs(Circle(x, y, cx, cy, r)) - th; }

        static float Seg(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy + 1e-9f));
            float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        static float Box(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x - cx) - hw + r, qy = Mathf.Abs(y - cy) - hh + r;
            return Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        static float Tri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            // distance signée approximative à un triangle (négative dedans)
            float e0 = Edge(x, y, ax, ay, bx, by), e1 = Edge(x, y, bx, by, cx, cy), e2 = Edge(x, y, cx, cy, ax, ay);
            return Mathf.Min(Mathf.Max(Mathf.Max(e0, e1), e2), Mathf.Max(Mathf.Max(-e0, -e1), -e2));
        }

        static float Edge(float x, float y, float ax, float ay, float bx, float by)
        {
            float nx = by - ay, ny = -(bx - ax);
            float l = Mathf.Sqrt(nx * nx + ny * ny) + 1e-9f;
            return ((x - ax) * nx + (y - ay) * ny) / l;
        }

        static float Arc(float x, float y, float cx, float cy, float r, float th, float a0, float a1)
        {
            float ang = Mathf.Atan2(y - cy, x - cx);
            float mid = (a0 + a1) / 2, half = (a1 - a0) / 2;
            float d = Mathf.Repeat(ang - mid + Mathf.PI, 2 * Mathf.PI) - Mathf.PI;
            if (Mathf.Abs(d) <= half) return Mathf.Abs(Circle(x, y, cx, cy, r)) - th;
            float ex = cx + Mathf.Cos(a0) * r, ey = cy + Mathf.Sin(a0) * r, fx = cx + Mathf.Cos(a1) * r, fy = cy + Mathf.Sin(a1) * r;
            return Mathf.Min(Circle(x, y, ex, ey, th), Circle(x, y, fx, fy, th));
        }

        static float U(float a, float b) { return Mathf.Min(a, b); }
        static float Sub(float a, float b) { return Mathf.Max(a, -b); }

        /// <summary>Distance signée de l'icône (y vers le haut, 0..1).</summary>
        public static float Shape(string name, float x, float y)
        {
            switch (name)
            {
                case "ciseaux":
                {
                    float d = Ring(x, y, 0.3f, 0.24f, 0.11f, 0.035f);
                    d = U(d, Ring(x, y, 0.7f, 0.24f, 0.11f, 0.035f));
                    d = U(d, Seg(x, y, 0.36f, 0.33f, 0.78f, 0.9f, 0.045f));
                    d = U(d, Seg(x, y, 0.64f, 0.33f, 0.22f, 0.9f, 0.045f));
                    d = Sub(d, Circle(x, y, 0.5f, 0.52f, 0.03f));
                    return d;
                }
                case "tondeuse":
                {
                    float d = Box(x, y, 0.5f, 0.42f, 0.17f, 0.33f, 0.12f);
                    d = U(d, Box(x, y, 0.5f, 0.83f, 0.2f, 0.06f, 0.02f));
                    for (int i = 0; i < 6; i++) d = U(d, Box(x, y, 0.33f + i * 0.068f, 0.92f, 0.018f, 0.05f, 0.01f));
                    d = Sub(d, Box(x, y, 0.5f, 0.45f, 0.06f, 0.12f, 0.05f));
                    return d;
                }
                case "douchette":
                {
                    float d = Seg(x, y, 0.22f, 0.12f, 0.52f, 0.55f, 0.07f);
                    d = U(d, Circle(x, y, 0.62f, 0.66f, 0.17f));
                    d = Sub(d, Circle(x, y, 0.62f, 0.66f, 0.1f));
                    for (int i = 0; i < 3; i++)
                    {
                        float a = -0.9f + i * 0.45f;
                        d = U(d, Circle(x, y, 0.62f + Mathf.Cos(a) * 0.3f, 0.66f + Mathf.Sin(a) * 0.3f - 0.05f, 0.035f));
                    }
                    d = U(d, Circle(x, y, 0.88f, 0.42f, 0.03f));
                    return d;
                }
                case "shampoing":
                {
                    float d = Box(x, y, 0.5f, 0.4f, 0.2f, 0.3f, 0.1f);
                    d = U(d, Box(x, y, 0.5f, 0.76f, 0.07f, 0.07f, 0.02f));
                    d = U(d, Box(x, y, 0.58f, 0.86f, 0.15f, 0.035f, 0.02f));
                    d = Sub(d, Box(x, y, 0.5f, 0.38f, 0.11f, 0.13f, 0.04f));
                    d = U(d, Circle(x, y, 0.5f, 0.38f, 0.05f));
                    return d;
                }
                case "soin":
                {
                    float d = Box(x, y, 0.5f, 0.36f, 0.3f, 0.2f, 0.08f);
                    d = U(d, Box(x, y, 0.5f, 0.62f, 0.33f, 0.07f, 0.04f));
                    d = Sub(d, Box(x, y, 0.5f, 0.35f, 0.18f, 0.06f, 0.03f));
                    d = U(d, Seg(x, y, 0.42f, 0.86f, 0.58f, 0.86f, 0.03f));
                    return d;
                }
                case "serviette":
                {
                    float d = Box(x, y, 0.5f, 0.5f, 0.33f, 0.28f, 0.1f);
                    d = Sub(d, Seg(x, y, 0.22f, 0.36f, 0.78f, 0.36f, 0.025f));
                    d = Sub(d, Seg(x, y, 0.22f, 0.64f, 0.78f, 0.64f, 0.025f));
                    return d;
                }
                case "pinceau":
                {
                    float d = Seg(x, y, 0.2f, 0.12f, 0.5f, 0.5f, 0.05f);
                    d = U(d, Tri(x, y, 0.42f, 0.62f, 0.62f, 0.42f, 0.88f, 0.88f));
                    d = U(d, Seg(x, y, 0.48f, 0.48f, 0.56f, 0.58f, 0.075f));
                    return d;
                }
                case "seche":
                {
                    float d = Box(x, y, 0.42f, 0.66f, 0.28f, 0.15f, 0.14f);
                    d = U(d, Box(x, y, 0.8f, 0.66f, 0.1f, 0.09f, 0.03f));
                    d = U(d, Box(x, y, 0.36f, 0.34f, 0.07f, 0.22f, 0.06f));
                    d = Sub(d, Circle(x, y, 0.34f, 0.66f, 0.06f));
                    return d;
                }
                case "tourner":
                {
                    float d = Arc(x, y, 0.5f, 0.5f, 0.3f, 0.05f, 0.4f, 2.8f);
                    d = U(d, Arc(x, y, 0.5f, 0.5f, 0.3f, 0.05f, 3.55f, 5.95f));
                    d = U(d, Tri(x, y, 0.76f, 0.75f, 0.94f, 0.62f, 0.7f, 0.5f));
                    d = U(d, Tri(x, y, 0.24f, 0.25f, 0.06f, 0.38f, 0.3f, 0.5f));
                    return d;
                }
                case "check": return U(Seg(x, y, 0.2f, 0.5f, 0.42f, 0.28f, 0.07f), Seg(x, y, 0.42f, 0.28f, 0.82f, 0.74f, 0.07f));
                case "etoile":
                {
                    // étoile à 5 branches : polygone à 10 sommets
                    float best = 9f; bool inside = false;
                    for (int i = 0; i < 10; i++)
                    {
                        float a0 = Mathf.PI / 2 + i * Mathf.PI / 5, a1 = Mathf.PI / 2 + (i + 1) * Mathf.PI / 5;
                        float r0 = (i % 2 == 0) ? 0.42f : 0.18f, r1 = ((i + 1) % 2 == 0) ? 0.42f : 0.18f;
                        float ax = 0.5f + Mathf.Cos(a0) * r0, ay = 0.47f + Mathf.Sin(a0) * r0;
                        float bx = 0.5f + Mathf.Cos(a1) * r1, by = 0.47f + Mathf.Sin(a1) * r1;
                        best = Mathf.Min(best, Seg(x, y, ax, ay, bx, by, 0f));
                        if (((ay > y) != (by > y)) && (x < (bx - ax) * (y - ay) / (by - ay + 1e-9f) + ax)) inside = !inside;
                    }
                    return (inside ? -best : best) - 0.015f;
                }
                case "coeur":
                {
                    float d = Circle(x, y, 0.35f, 0.62f, 0.18f);
                    d = U(d, Circle(x, y, 0.65f, 0.62f, 0.18f));
                    d = U(d, Tri(x, y, 0.18f, 0.58f, 0.82f, 0.58f, 0.5f, 0.15f));
                    return d;
                }
                case "maison":
                {
                    float d = Tri(x, y, 0.12f, 0.52f, 0.5f, 0.88f, 0.88f, 0.52f);
                    d = U(d, Box(x, y, 0.5f, 0.33f, 0.26f, 0.22f, 0.03f));
                    d = Sub(d, Box(x, y, 0.5f, 0.25f, 0.07f, 0.14f, 0.02f));
                    return d;
                }
                case "photo":
                {
                    float d = Box(x, y, 0.5f, 0.45f, 0.38f, 0.26f, 0.08f);
                    d = U(d, Box(x, y, 0.5f, 0.74f, 0.13f, 0.06f, 0.03f));
                    d = Sub(d, Circle(x, y, 0.5f, 0.45f, 0.15f));
                    d = U(d, Circle(x, y, 0.5f, 0.45f, 0.08f));
                    return d;
                }
                case "reglages":
                {
                    float ang = Mathf.Atan2(y - 0.5f, x - 0.5f);
                    float r = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
                    float teeth = 0.34f + 0.07f * Mathf.Clamp(Mathf.Sin(ang * 8f) * 3f, -1f, 1f);
                    return Sub(r - teeth, Circle(x, y, 0.5f, 0.5f, 0.13f));
                }
                case "suivant":
                {
                    float d = Seg(x, y, 0.18f, 0.5f, 0.72f, 0.5f, 0.07f);
                    d = U(d, Seg(x, y, 0.5f, 0.75f, 0.78f, 0.5f, 0.07f));
                    d = U(d, Seg(x, y, 0.5f, 0.25f, 0.78f, 0.5f, 0.07f));
                    return d;
                }
                case "piece":
                {
                    float d = Ring(x, y, 0.5f, 0.5f, 0.34f, 0.05f);
                    d = U(d, Seg(x, y, 0.5f, 0.3f, 0.5f, 0.7f, 0.05f));
                    return d;
                }
                case "plus": return U(Seg(x, y, 0.22f, 0.5f, 0.78f, 0.5f, 0.07f), Seg(x, y, 0.5f, 0.22f, 0.5f, 0.78f, 0.07f));
                case "moins": return Seg(x, y, 0.22f, 0.5f, 0.78f, 0.5f, 0.07f);
                case "accueil":
                {
                    // bulle de dialogue
                    float d = Box(x, y, 0.5f, 0.58f, 0.36f, 0.24f, 0.14f);
                    d = U(d, Tri(x, y, 0.3f, 0.4f, 0.48f, 0.4f, 0.24f, 0.16f));
                    for (int i = 0; i < 3; i++) d = Sub(d, Circle(x, y, 0.36f + i * 0.14f, 0.58f, 0.04f));
                    return d;
                }
                case "bac":
                {
                    // vasque (demi-anneau) + robinet col de cygne
                    float bowl = Sub(Circle(x, y, 0.5f, 0.5f, 0.34f), Box(x, y, 0.5f, 0.85f, 0.6f, 0.35f, 0f));
                    float d = Sub(bowl, Circle(x, y, 0.5f, 0.5f, 0.25f));
                    d = U(d, Seg(x, y, 0.12f, 0.5f, 0.88f, 0.5f, 0.04f));
                    d = U(d, Arc(x, y, 0.62f, 0.7f, 0.12f, 0.035f, 0f, 3.14f));
                    d = U(d, Seg(x, y, 0.74f, 0.7f, 0.74f, 0.52f, 0.035f));
                    d = U(d, Circle(x, y, 0.5f, 0.66f, 0.03f));
                    return d;
                }
                case "coupe": return Shape("ciseaux", x, y);
                case "couleur":
                {
                    // goutte
                    float d = Circle(x, y, 0.5f, 0.38f, 0.24f);
                    d = U(d, Tri(x, y, 0.28f, 0.45f, 0.72f, 0.45f, 0.5f, 0.92f));
                    d = Sub(d, Circle(x, y, 0.42f, 0.34f, 0.07f));
                    return d;
                }
                case "resultat": return Shape("etoile", x, y);
                case "galerie":
                {
                    float d = Box(x, y, 0.5f, 0.5f, 0.38f, 0.3f, 0.06f);
                    d = Sub(d, Box(x, y, 0.5f, 0.5f, 0.3f, 0.22f, 0.03f));
                    d = U(d, Tri(x, y, 0.22f, 0.3f, 0.45f, 0.62f, 0.62f, 0.3f));
                    d = U(d, Tri(x, y, 0.5f, 0.3f, 0.66f, 0.52f, 0.8f, 0.3f));
                    d = U(d, Circle(x, y, 0.68f, 0.64f, 0.06f));
                    return d;
                }
                case "aide":
                {
                    float d = Arc(x, y, 0.5f, 0.62f, 0.17f, 0.06f, -1.2f, 3.3f);
                    d = U(d, Seg(x, y, 0.5f, 0.45f, 0.5f, 0.36f, 0.06f));
                    d = U(d, Circle(x, y, 0.5f, 0.18f, 0.065f));
                    return d;
                }
            }
            return Circle(x, y, 0.5f, 0.5f, 0.3f);
        }

        public static Img Render(string name, int size = 128)
        {
            var img = new Img(size, size, new Color(1, 1, 1, 0));
            float px = 1f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float d = Shape(name, u, v);
                    float a = Mathf.Clamp01(0.5f - d / (px * 1.2f));
                    img.px[y * size + x] = new Color(1, 1, 1, a);
                }
            return img;
        }
    }
}
