using System;

namespace HomewardBeam.Core
{
    /// <summary>Procedurally synthesised mono sound effects (no audio files needed). Samples are in [-1, 1].</summary>
    public static class SoundSynth
    {
        public const int Rate = 22050;

        public static float[] Make(Sfx s)
        {
            switch (s)
            {
                case Sfx.Step: return Tone(110, 70, 0.25f, wave: 2, slide: -20);
                case Sfx.Bump: return Tone(70, 90, 0.35f, wave: 1, slide: -10);
                case Sfx.Door: return Tone(180, 260, 0.3f, wave: 1, slide: -120);
                case Sfx.Hit: return Noise(120, 0.5f);
                case Sfx.Miss: return Tone(500, 120, 0.2f, wave: 0, slide: -300);
                case Sfx.Hurt: return Tone(220, 200, 0.45f, wave: 1, slide: -150);
                case Sfx.Pickup: return Arp(new[] { 660, 880, 1320 }, 60, 0.3f);
                case Sfx.Stairs: return Arp(new[] { 523, 440, 349, 262 }, 90, 0.3f);
                case Sfx.Eat: return Arp(new[] { 300, 360, 300 }, 60, 0.25f);
                case Sfx.Magic: return Tone(880, 260, 0.3f, wave: 0, slide: 900);
                case Sfx.Death: return Arp(new[] { 392, 330, 262, 196, 131 }, 160, 0.35f);
                case Sfx.Victory: return Arp(new[] { 523, 659, 784, 1047 }, 110, 0.3f);
                default: return Tone(1000, 40, 0.2f, wave: 0, slide: 0);
            }
        }

        // wave: 0 sine, 1 square, 2 triangle
        static float[] Tone(float hz, int ms, float vol, int wave, float slide)
        {
            int n = Rate * ms / 1000; var d = new float[n]; double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n; double f = hz + slide * t; ph += f / Rate;
                double p = ph - Math.Floor(ph);
                double v = wave == 0 ? Math.Sin(2 * Math.PI * p) : wave == 1 ? (p < 0.5 ? 1 : -1) : 1 - 4 * Math.Abs(p - 0.5);
                d[i] = (float)(v * vol * Env(t));
            }
            return d;
        }

        static float[] Noise(int ms, float vol)
        {
            int n = Rate * ms / 1000; var d = new float[n]; var r = new Rng(42); float last = 0;
            for (int i = 0; i < n; i++) { float raw = (r.Range(0, 2000) - 1000) / 1000f; last = last * 0.6f + raw * 0.4f; d[i] = last * vol * Env((float)i / n); }
            return d;
        }

        static float[] Arp(int[] notes, int msEach, float vol)
        {
            int per = Rate * msEach / 1000; var d = new float[per * notes.Length];
            for (int k = 0; k < notes.Length; k++) { var t = Tone(notes[k], msEach, vol, 1, 0); Array.Copy(t, 0, d, k * per, Math.Min(per, t.Length)); }
            return d;
        }

        static float Env(float t) => t < 0.05f ? t / 0.05f : 1f - (t - 0.05f) / 0.95f;
    }
}
