using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Styles de l'interface : crème, doré et vert anglais, coins arrondis.</summary>
    public class UIStyles
    {
        public static readonly Color Cream = SalonUtil.Hex("#FBF6EF");
        public static readonly Color Ink = SalonUtil.Hex("#3B2E2A");
        public static readonly Color Green = SalonUtil.Hex("#2F4A40");
        public static readonly Color Gold = SalonUtil.Hex("#C9A45C");
        public static readonly Color Rose = SalonUtil.Hex("#D9A5A0");
        public static readonly Color Muted = SalonUtil.Hex("#8A7A70");

        static Font serif, sans;

        public static Font SerifFont()
        {
            if (serif == null)
                serif = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Didot", "Baskerville", "Times New Roman", "Noto Serif", "DejaVu Serif", "serif", "Segoe UI Symbol", "Arial Unicode MS", "Noto Sans Symbols2", "Apple Symbols" }, 48);
            return serif;
        }

        public static Font SansFont()
        {
            if (sans == null)
                sans = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Helvetica Neue", "Avenir", "Roboto", "Arial", "DejaVu Sans", "sans-serif", "Segoe UI Symbol", "Arial Unicode MS", "Noto Sans Symbols2", "Apple Symbols" }, 32);
            return sans;
        }

        public float scale;
        public GUIStyle panel, card, bubble, button, buttonOn, buttonSmall, buttonBig, title, h1, h2, label, labelCenter, small, smallCenter, chip, toast, stars, swatch;
        public Texture2D white, round, ring, barBg;

        public void Build(float s)
        {
            scale = s;
            int r = 64;
            if (white == null)
            {
                white = ProcTex.Solid(Color.white);
                round = ProcTex.RoundRect(r, 14, Color.white, Color.white, 0);
                ring = ProcTex.Ring(128, 6, Color.white);
                barBg = ProcTex.RoundRect(r, 30, new Color(0.88f, 0.84f, 0.79f, 1), new Color(0, 0, 0, 0), 0);
            }
            var panelTex = ProcTex.RoundRect(r, 18, new Color(Cream.r, Cream.g, Cream.b, 0.95f), Gold, 2.2f);
            var cardTex = ProcTex.RoundRect(r, 18, new Color(1f, 0.99f, 0.97f, 0.97f), new Color(Gold.r, Gold.g, Gold.b, 0.8f), 1.6f);
            var btnN = ProcTex.RoundRect(r, 16, SalonUtil.Hex("#F3E6DA"), SalonUtil.Hex("#D8C2A2"), 1.6f);
            var btnH = ProcTex.RoundRect(r, 16, SalonUtil.Hex("#F9EFE6"), Gold, 1.8f);
            var btnA = ProcTex.RoundRect(r, 16, SalonUtil.Hex("#E7D3BF"), Gold, 1.8f);
            var btnOn = ProcTex.RoundRect(r, 16, Green, Gold, 1.8f);
            var toastTex = ProcTex.RoundRect(r, 24, new Color(Green.r, Green.g, Green.b, 0.93f), Gold, 1.6f);
            var chipTex = ProcTex.RoundRect(r, 24, SalonUtil.Hex("#EFE3D6"), new Color(0, 0, 0, 0), 0);

            Font fs = SansFont(), fz = SerifFont();
            var border = new RectOffset(20, 20, 20, 20);

            panel = new GUIStyle { border = border, padding = Pad(14, 14, 12, 12) };
            panel.normal.background = panelTex;
            card = new GUIStyle(panel);
            card.normal.background = cardTex;
            bubble = new GUIStyle(card) { padding = Pad(20, 20, 14, 14) };

            button = new GUIStyle
            {
                border = border, padding = Pad(12, 12, 8, 8), margin = Pad(2, 2, 3, 3),
                alignment = TextAnchor.MiddleCenter, font = fs, fontSize = Fs(17), fixedHeight = s * 44f, wordWrap = true,
            };
            SetBg(button, btnN, btnH, btnA, Ink, Ink, Ink);
            buttonOn = new GUIStyle(button);
            SetBg(buttonOn, btnOn, btnOn, btnOn, Cream, Cream, Cream);
            buttonSmall = new GUIStyle(button) { fontSize = Fs(15), fixedHeight = s * 36f, padding = Pad(8, 8, 4, 4) };
            buttonBig = new GUIStyle(button) { font = fz, fontSize = Fs(22), fixedHeight = s * 58f };

            title = new GUIStyle { font = fz, fontSize = Fs(46), fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            title.normal.textColor = Green;
            h1 = new GUIStyle { font = fz, fontSize = Fs(26), alignment = TextAnchor.MiddleLeft, wordWrap = true, margin = Pad(2, 2, 4, 6) };
            h1.normal.textColor = Green;
            h2 = new GUIStyle(h1) { fontSize = Fs(19), margin = Pad(2, 2, 8, 4) };
            label = new GUIStyle { font = fs, fontSize = Fs(16), wordWrap = true, richText = true, margin = Pad(2, 2, 2, 2) };
            label.normal.textColor = Ink;
            labelCenter = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(label) { fontSize = Fs(13) };
            small.normal.textColor = Muted;
            smallCenter = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            chip = new GUIStyle(label) { fontSize = Fs(14), border = border, padding = Pad(10, 10, 4, 4), margin = Pad(2, 2, 2, 2) };
            chip.normal.background = chipTex;
            toast = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = Fs(17), border = border, padding = Pad(22, 22, 12, 12) };
            toast.normal.background = toastTex;
            toast.normal.textColor = Cream;
            stars = new GUIStyle { font = fz, fontSize = Fs(40), alignment = TextAnchor.MiddleCenter };
            stars.normal.textColor = Gold;
            swatch = new GUIStyle { margin = Pad(3, 3, 3, 3), fixedWidth = s * 38f, fixedHeight = s * 38f };
        }

        int Fs(float px) { return Mathf.Max(8, Mathf.RoundToInt(px * scale)); }

        RectOffset Pad(float l, float r, float t, float b)
        {
            return new RectOffset(Mathf.RoundToInt(l * scale), Mathf.RoundToInt(r * scale), Mathf.RoundToInt(t * scale), Mathf.RoundToInt(b * scale));
        }

        static void SetBg(GUIStyle st, Texture2D n, Texture2D h, Texture2D a, Color tn, Color th, Color ta)
        {
            st.normal.background = n; st.normal.textColor = tn;
            st.hover.background = h; st.hover.textColor = th;
            st.active.background = a; st.active.textColor = ta;
            st.focused.background = n; st.focused.textColor = tn;
            st.onNormal.background = n; st.onNormal.textColor = tn;
        }
    }
}
