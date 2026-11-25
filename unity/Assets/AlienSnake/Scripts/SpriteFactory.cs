using System;
using UnityEngine;

namespace AlienSnake
{
    /// Runtime-generated white sprites (1 unit wide) so the project needs no art files. Tint with SpriteRenderer.color.
    public static class SpriteFactory
    {
        static Sprite circle, square, tri, hex, glow; static Material mat;

        public static Material Mat
        {
            get
            {
                if (mat == null)
                {
                    var sh = Shader.Find("Sprites/Default");
                    if (sh == null) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (sh == null) Debug.LogError("AlienSnake: no unlit sprite shader found.");
                    mat = new Material(sh);
                }
                return mat;
            }
        }

        static Sprite Make(Func<float, float, float> alpha)
        {
            const int N = 64; var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha((x + .5f) / N - .5f, (y + .5f) / N - .5f)) * 255));
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(.5f, .5f), N);
        }

        public static Sprite Circle { get { if (circle == null) circle = Make((x, y) => (0.5f - Mathf.Sqrt(x * x + y * y)) * 64f); return circle; } }
        public static Sprite Square { get { if (square == null) square = Make((x, y) => (0.5f - Mathf.Max(Mathf.Abs(x), Mathf.Abs(y))) * 64f); return square; } }
        public static Sprite Tri { get { if (tri == null) tri = Make((x, y) => ((0.5f - y) * .5f - Mathf.Abs(x)) * 64f * (y > -.5f ? 1 : 0)); return tri; } }
        public static Sprite Hex { get { if (hex == null) hex = Make((x, y) => (0.433f - Mathf.Max(Mathf.Abs(x) * .866f + Mathf.Abs(y) * .5f, Mathf.Abs(y))) * 64f); return hex; } }
        public static Sprite Glow { get { if (glow == null) glow = Make((x, y) => { float d = Mathf.Sqrt(x * x + y * y) * 2f; return d >= 1 ? 0 : (1 - d) * (1 - d); }); return glow; } }
    }
}
