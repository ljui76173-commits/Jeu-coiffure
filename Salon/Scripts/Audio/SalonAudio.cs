using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>
    /// Musique lo-fi jazzy et bruitages, entièrement synthétisés par le code (aucun fichier audio).
    /// </summary>
    public class SalonAudio : MonoBehaviour
    {
        const int Rate = 22050;

        public AudioClip Music, Water, Dryer, Clipper, Squish, Towel, Snip, Ding, Shutter, Click;
        AudioSource music, loop, oneShot;
        AudioClip currentLoop;
        float sfxVol = 0.8f, loopTarget;

        public void Init(float musicVolume, float sfxVolume)
        {
            music = gameObject.AddComponent<AudioSource>();
            loop = gameObject.AddComponent<AudioSource>();
            oneShot = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { music, loop, oneShot }) { s.playOnAwake = false; s.spatialBlend = 0; }
            music.loop = true; loop.loop = true;

            System.Random rng = new System.Random(1234);
            Music = MakeMusic(rng);
            Water = MakeNoiseLoop("Eau", 2f, 0.55f, 0.25f, 0f, 0f, rng);
            Dryer = MakeNoiseLoop("SecheCheveux", 2f, 0.35f, 0.6f, 180f, 0.15f, rng);
            Clipper = MakeBuzz(rng);
            Squish = MakeSquish(rng);
            Towel = MakeNoiseLoop("Serviette", 2f, 0.15f, 0.12f, 0f, 0f, rng, 3f);
            Snip = MakeSnip(rng);
            Ding = MakeDing();
            Shutter = MakeShutter(rng);
            Click = MakeClick();

            SetVolumes(musicVolume, sfxVolume);
            music.clip = Music;
            music.Play();
        }

        public void SetVolumes(float m, float s)
        {
            sfxVol = s;
            if (music != null) music.volume = 0.6f * m;
        }

        public void Loop(AudioClip clip, float volume)
        {
            if (loop == null) return;
            if (currentLoop != clip)
            {
                currentLoop = clip;
                loop.clip = clip;
                loop.Play();
            }
            loopTarget = volume;
        }

        public void StopLoop() { loopTarget = 0; }

        public void OneShot(AudioClip clip, float volume)
        {
            if (oneShot != null && clip != null) oneShot.PlayOneShot(clip, volume * sfxVol);
        }

        void Update()
        {
            if (loop == null) return;
            float target = loopTarget * sfxVol;
            loop.volume = Mathf.MoveTowards(loop.volume, target, Time.deltaTime * 4f);
            if (loop.volume <= 0.001f && loopTarget <= 0 && loop.isPlaying) { loop.Stop(); currentLoop = null; }
        }

        // ------------------------------------------------------------------ Synthèse
        static float Midi(int n) { return 440f * Mathf.Pow(2f, (n - 69) / 12f); }

        static AudioClip Clip(string name, float[] data)
        {
            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float k = 0.85f / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Boucle lo-fi : accords de piano électrique, basse, batterie feutrée, craquements de vinyle.</summary>
        AudioClip MakeMusic(System.Random rng)
        {
            const float bpm = 72f;
            float beat = 60f / bpm, bar = beat * 4;
            int bars = 8;
            int n = (int)(bar * bars * Rate);
            var d = new float[n];
            // Progression : Fmaj9 – Em7 – Dm9 – Cmaj7 (x2, variations)
            int[][] chords =
            {
                new[] { 53, 57, 60, 64, 67 }, new[] { 52, 55, 59, 62, 66 },
                new[] { 50, 53, 57, 60, 64 }, new[] { 48, 52, 55, 59, 62 },
                new[] { 53, 57, 60, 64, 69 }, new[] { 52, 55, 59, 62, 67 },
                new[] { 50, 53, 57, 62, 65 }, new[] { 43, 50, 53, 57, 59 },
            };
            int[] bass = { 41, 40, 38, 36, 41, 40, 38, 31 };

            for (int b = 0; b < bars; b++)
            {
                float t0 = b * bar;
                // accords : temps 1 et « et » du 2
                AddChord(d, chords[b], t0, 2.6f, 0.16f);
                AddChord(d, chords[b], t0 + beat * 1.5f + 0.02f, 1.6f, 0.08f);
                // basse
                AddTone(d, Midi(bass[b]), t0, beat * 1.8f, 0.32f, 0f);
                AddTone(d, Midi(bass[b] + 7), t0 + beat * 2.5f, beat * 1.2f, 0.22f, 0f);
                // batterie (swing)
                for (int q = 0; q < 4; q++)
                {
                    float tb = t0 + q * beat;
                    if (q == 0 || q == 2) AddKick(d, tb, 0.55f);
                    if (q == 1 || q == 3) AddSnare(d, tb, 0.18f, rng);
                    AddHat(d, tb, 0.05f, rng);
                    AddHat(d, tb + beat * 0.62f, 0.035f, rng);
                }
            }
            // vinyle : souffle + craquements
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float wn = (float)rng.NextDouble() * 2 - 1;
                lp += (wn - lp) * 0.08f;
                d[i] += lp * 0.02f;
                if (rng.NextDouble() < 0.0006) d[i] += ((float)rng.NextDouble() - 0.5f) * 0.25f;
            }
            // filtre passe-bas doux (chaleur)
            float y = 0;
            for (int i = 0; i < n; i++) { y += (d[i] - y) * 0.35f; d[i] = y; }
            return Clip("MusiqueLoFi", d);
        }

        static void AddChord(float[] d, int[] notes, float t0, float dur, float amp)
        {
            for (int k = 0; k < notes.Length; k++)
                AddTone(d, Midi(notes[k]), t0 + k * 0.012f, dur, amp, 0.25f);
        }

        /// <summary>Note de piano électrique (sinus + harmonique qui s'éteint vite, trémolo léger).</summary>
        static void AddTone(float[] d, float f, float t0, float dur, float amp, float bell)
        {
            int s0 = (int)(t0 * Rate), len = (int)((dur + 0.4f) * Rate);
            for (int i = 0; i < len; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) idx -= d.Length; // boucle parfaite
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * 1.6f / dur);
                if (t > dur) env *= Mathf.Max(0, 1f - (t - dur) / 0.4f);
                float trem = 1f + 0.08f * Mathf.Sin(t * 2 * Mathf.PI * 4.5f);
                float w = Mathf.Sin(2 * Mathf.PI * f * t) + bell * Mathf.Exp(-t * 6f) * Mathf.Sin(2 * Mathf.PI * f * 2f * t)
                          + 0.15f * Mathf.Sin(2 * Mathf.PI * f * 3f * t) * Mathf.Exp(-t * 3f);
                d[idx] += w * env * amp * trem;
            }
        }

        static void AddKick(float[] d, float t0, float amp)
        {
            int s0 = (int)(t0 * Rate), len = (int)(0.3f * Rate);
            float ph = 0;
            for (int i = 0; i < len && s0 + i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 45f + 70f * Mathf.Exp(-t * 30f);
                ph += 2 * Mathf.PI * f / Rate;
                d[s0 + i] += Mathf.Sin(ph) * Mathf.Exp(-t * 12f) * amp;
            }
        }

        static void AddSnare(float[] d, float t0, float amp, System.Random rng)
        {
            int s0 = (int)(t0 * Rate), len = (int)(0.18f * Rate);
            float lp = 0;
            for (int i = 0; i < len && s0 + i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float wn = (float)rng.NextDouble() * 2 - 1;
                lp += (wn - lp) * 0.4f;
                d[s0 + i] += lp * Mathf.Exp(-t * 22f) * amp;
            }
        }

        static void AddHat(float[] d, float t0, float amp, System.Random rng)
        {
            int s0 = (int)(t0 * Rate), len = (int)(0.05f * Rate);
            float prev = 0;
            for (int i = 0; i < len && s0 + i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float wn = (float)rng.NextDouble() * 2 - 1;
                float hp = wn - prev; prev = wn;
                d[s0 + i] += hp * Mathf.Exp(-t * 70f) * amp;
            }
        }

        static AudioClip MakeNoiseLoop(string name, float seconds, float brightness, float amp, float humHz, float humAmp, System.Random rng, float wobbleHz = 0f)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float wn = (float)rng.NextDouble() * 2 - 1;
                lp += (wn - lp) * brightness;
                lp2 += (lp - lp2) * 0.5f;
                float v = lp2 * amp;
                if (humHz > 0) v += Mathf.Sin(2 * Mathf.PI * humHz * t) * humAmp + Mathf.Sin(2 * Mathf.PI * humHz * 2 * t) * humAmp * 0.4f;
                if (wobbleHz > 0) v *= 0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * wobbleHz * t);
                d[i] = v;
            }
            // fondu des bords pour une boucle sans clic
            int fade = Rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                float a = d[i], b = d[n - fade + i];
                d[n - fade + i] = Mathf.Lerp(b, a, k);
            }
            return Clip(name, d);
        }

        static AudioClip MakeBuzz(System.Random rng)
        {
            int n = Rate;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float saw = 2f * ((t * 120f) % 1f) - 1f;
                d[i] = saw * 0.4f + Mathf.Sin(2 * Mathf.PI * 240f * t) * 0.3f + ((float)rng.NextDouble() - 0.5f) * 0.15f;
            }
            float y = 0;
            for (int i = 0; i < n; i++) { y += (d[i] - y) * 0.3f; d[i] = y; }
            return Clip("Tondeuse", d);
        }

        static AudioClip MakeSquish(System.Random rng)
        {
            int n = Rate;
            var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float wn = (float)rng.NextDouble() * 2 - 1;
                lp += (wn - lp) * 0.12f;
                float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 3f * t), 3f);
                d[i] = lp * pulse;
            }
            return Clip("Mousse", d);
        }

        static AudioClip MakeSnip(System.Random rng)
        {
            int n = (int)(0.09f * Rate);
            var d = new float[n];
            float prev = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float wn = (float)rng.NextDouble() * 2 - 1;
                float hp = wn - prev; prev = wn;
                float click = Mathf.Exp(-t * 180f) + 0.6f * Mathf.Exp(-Mathf.Abs(t - 0.035f) * 260f);
                d[i] = hp * click * 0.7f + Mathf.Sin(2 * Mathf.PI * 3400f * t) * Mathf.Exp(-t * 60f) * 0.25f;
            }
            return Clip("Ciseaux", d);
        }

        static AudioClip MakeDing()
        {
            int n = (int)(1.4f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float e = Mathf.Exp(-t * 3f);
                d[i] = (Mathf.Sin(2 * Mathf.PI * 1318.5f * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 1975.5f * t) * Mathf.Exp(-t * 2f)
                        + (t > 0.12f ? 0.8f * Mathf.Sin(2 * Mathf.PI * 1760f * (t - 0.12f)) * Mathf.Exp(-(t - 0.12f) * 3f) : 0f)) * e;
            }
            return Clip("Ding", d);
        }

        static AudioClip MakeShutter(System.Random rng)
        {
            int n = (int)(0.2f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float wn = (float)rng.NextDouble() * 2 - 1;
                d[i] = wn * (Mathf.Exp(-t * 90f) + 0.7f * Mathf.Exp(-Mathf.Abs(t - 0.09f) * 120f));
            }
            return Clip("Photo", d);
        }

        static AudioClip MakeClick()
        {
            int n = (int)(0.05f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                d[i] = Mathf.Sin(2 * Mathf.PI * 900f * t) * Mathf.Exp(-t * 90f);
            }
            return Clip("Clic", d);
        }
    }
}
