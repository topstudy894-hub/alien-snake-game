using UnityEngine;

namespace AlienSnake
{
    /// Tiny generated sound effects (no audio files needed).
    public static class Sfx
    {
        public const int Eat = 0, Power = 1, Die = 2, Buy = 3, Click = 4;
        static AudioClip[] clips; static AudioSource src; static float lastT;
        public static bool Muted { get { return PlayerPrefs.GetInt("muted", 0) == 1; } set { PlayerPrefs.SetInt("muted", value ? 1 : 0); } }

        static AudioClip Tone(float f0, float f1, float dur)
        {
            int rate = 22050, n = (int)(rate * dur); var d = new float[n]; float ph = 0;
            for (int i = 0; i < n; i++) { float k = i / (float)n; ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, k) / rate; d[i] = Mathf.Sin(ph) * (1f - k) * .35f; }
            var c = AudioClip.Create("sfx", n, 1, rate, false); c.SetData(d, 0); return c;
        }
        static void Init()
        {
            if (clips != null) return;
            clips = new[] { Tone(660, 880, .07f), Tone(500, 1200, .18f), Tone(400, 60, .5f), Tone(700, 1000, .15f), Tone(500, 500, .04f) };
            var go = new GameObject("Sfx"); Object.DontDestroyOnLoad(go); src = go.AddComponent<AudioSource>();
        }
        public static void Play(int id)
        {
            if (Muted) return; Init();
            if (Time.unscaledTime - lastT < .04f) return; lastT = Time.unscaledTime; src.PlayOneShot(clips[id]);
        }
    }
}
