using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienSnake
{
    /// Draws one alien snake from a skin: pooled body circles, outline, effect overlays, procedural head, trail.
    public class SnakeView : MonoBehaviour
    {
        public int Id;
        SkinDef skin; Transform head; TrailRenderer trail;
        readonly List<SpriteRenderer> fill = new List<SpriteRenderer>(), line = new List<SpriteRenderer>(), over = new List<SpriteRenderer>();
        Vector2[] cur = new Vector2[0], tgt = new Vector2[0];
        float ang, tAng, radius = 0.4f; bool boosting, eating, inited; int ovUsed;

        public SkinDef Skin { get { return skin; } }
        public int Length { get { return tgt.Length; } }
        public Vector2 HeadPos { get { return cur.Length > 0 ? cur[0] : (Vector2)transform.position; } }
        bool Has(Fx f) { return (skin.fx & f) != 0; }
        public void Snap() { inited = false; }

        public void Init(int id, SkinDef s, int sortIndex)
        {
            Id = id; skin = s;
            gameObject.AddComponent<SortingGroup>().sortingOrder = sortIndex;
            head = new GameObject("Head").transform; head.SetParent(transform, false);
            BuildHead();
            trail = head.gameObject.AddComponent<TrailRenderer>();
            trail.material = SpriteFactory.Mat; trail.time = 0.45f; trail.minVertexDistance = 0.1f; trail.sortingOrder = -200; trail.emitting = false;
            var g = s.glow; trail.startColor = new Color(g.r, g.g, g.b, 0.8f); trail.endColor = new Color(g.r, g.g, g.b, 0f);
        }

        public void SetTarget(Vector2[] segs, float angleDeg, bool boost, bool eat)
        {
            tgt = segs; tAng = angleDeg; boosting = boost; eating = eat;
            if (!inited) { cur = (Vector2[])segs.Clone(); ang = angleDeg; inited = true; return; }
            if (cur.Length != segs.Length)
            {
                var n = new Vector2[segs.Length];
                for (int i = 0; i < n.Length; i++) n[i] = i < cur.Length ? cur[i] : segs[i];
                cur = n;
            }
        }

        public void Tick(float dt, float t)
        {
            int n = cur.Length; if (n == 0) return;
            float k = 1f - Mathf.Exp(-dt / 0.07f);
            for (int i = 0; i < n; i++) cur[i] = (cur[i] - tgt[i]).sqrMagnitude > 25f ? tgt[i] : Vector2.Lerp(cur[i], tgt[i], k);
            ang = Mathf.LerpAngle(ang, tAng, k);
            radius = (8f + Mathf.Min(10f, n / 40f)) * Snapshot.U;
            Grow(fill, n - 1); Grow(line, n - 1);
            for (int i = 1; i < n; i++)
            {
                var f = fill[i - 1]; f.transform.localPosition = cur[i]; f.transform.localScale = Vector3.one * radius * 2f;
                f.color = skin.pal[(i / skin.step) % skin.pal.Length]; f.sortingOrder = 1000 + (n - i);
                var l = line[i - 1]; l.transform.localPosition = cur[i]; l.transform.localScale = Vector3.one * (radius * 2f + 0.07f);
                l.color = skin.dark; l.sortingOrder = n - i;
            }
            Overlays(n, t);
            head.localPosition = cur[0]; head.localRotation = Quaternion.Euler(0, 0, ang - 90f);
            head.localScale = Vector3.one * radius * 1.45f * (eating ? 1.12f : 1f);
            trail.emitting = boosting || skin.legendary; trail.widthMultiplier = radius * (skin.legendary ? 1.6f : 1.1f);
        }

        void Grow(List<SpriteRenderer> l, int n)
        {
            while (l.Count < n)
            {
                var go = new GameObject("s"); go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = SpriteFactory.Circle; sr.sharedMaterial = SpriteFactory.Mat; l.Add(sr);
            }
            for (int i = 0; i < l.Count; i++) l[i].enabled = i < n;
        }

        void Ov(Sprite sp, Vector2 p, float sx, float sy, float rot, Color c, int order)
        {
            SpriteRenderer sr;
            if (ovUsed < over.Count) sr = over[ovUsed];
            else { var go = new GameObject("o"); go.transform.SetParent(transform, false); sr = go.AddComponent<SpriteRenderer>(); sr.sharedMaterial = SpriteFactory.Mat; over.Add(sr); }
            ovUsed++; sr.enabled = true; sr.sprite = sp; sr.color = c; sr.sortingOrder = order;
            var tr = sr.transform; tr.localPosition = p; tr.localRotation = Quaternion.Euler(0, 0, rot); tr.localScale = new Vector3(sx, sy, 1);
        }

        void Overlays(int n, float t)
        {
            ovUsed = 0;
            if ((skin.fx & (Fx.Spike | Fx.Node | Fx.Crack | Fx.Star | Fx.Plate | Fx.Aura)) != 0)
            {
                for (int i = 1; i < n; i++)
                {
                    Vector2 a = cur[Mathf.Min(n - 1, i + 1)], b = cur[i - 1], tan = (b - a).normalized, nrm = new Vector2(-tan.y, tan.x), p = cur[i];
                    var g = skin.glow;
                    if (Has(Fx.Aura) && i % 2 == 0) Ov(SpriteFactory.Glow, p, radius * 4.4f, radius * 4.4f, 0, new Color(g.r, g.g, g.b, 0.14f + 0.06f * Mathf.Sin(t * 4f)), -50);
                    if (Has(Fx.Spike) && i % 3 == 0)
                    {
                        float side = i % 6 == 0 ? 1f : -1f; Vector2 o = nrm * side;
                        Ov(SpriteFactory.Tri, p + o * radius * 1.15f, radius * 1.3f, radius * 1.5f, Mathf.Atan2(o.y, o.x) * Mathf.Rad2Deg - 90f, new Color(.04f, .05f, .09f), 2000);
                    }
                    if (Has(Fx.Node) && i % 3 == 1) Ov(SpriteFactory.Circle, p, radius * .8f, radius * .8f, 0, new Color(g.r, g.g, g.b, .4f + .5f * Mathf.Sin(t * 6f - i * .6f)), 2000);
                    if (Has(Fx.Crack) && i % 2 == 0) Ov(SpriteFactory.Circle, p + nrm * radius * .3f, radius * .56f, radius * .56f, 0, new Color(g.r, g.g, g.b, .5f + .5f * Mathf.Sin(t * 5f + i)), 2000);
                    if (Has(Fx.Star)) Ov(SpriteFactory.Circle, p + new Vector2(((i * 37) % 7 - 3) / 3f, ((i * 53) % 7 - 3) / 3f) * radius * .6f, radius * .16f, radius * .16f, 0, new Color(1, 1, 1, .5f + .5f * Mathf.Sin(t * 4f + i * 2f)), 2000);
                    if (Has(Fx.Plate) && i % 2 == 0) Ov(SpriteFactory.Circle, p, radius * .3f, radius * .3f, 0, g, 2000);
                }
            }
            for (int j = ovUsed; j < over.Count; j++) over[j].enabled = false;
        }

        // ---- head: built once in "head radius = 1" units; +Y is the direction the snake faces ----
        SpriteRenderer Add(Sprite sp, float x, float y, float sx, float sy, float rot, Color c, int order)
        {
            var go = new GameObject("p"); go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(x, y, 0); go.transform.localRotation = Quaternion.Euler(0, 0, rot); go.transform.localScale = new Vector3(sx, sy, 1);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.color = c; sr.sharedMaterial = SpriteFactory.Mat; sr.sortingOrder = 3000 + order; return sr;
        }

        void BuildHead()
        {
            var S = skin; var T = SpriteFactory.Tri; var Ci = SpriteFactory.Circle; var Sq = SpriteFactory.Square;
            var gold = new Color(1f, .85f, .29f); var pink = new Color(1f, .82f, .93f); var red = new Color(.89f, .14f, .14f);
            if (Has(Fx.Fin)) { var ice = new Color(.62f, .9f, 1f); Add(T, -1.05f, .15f, .9f, 1.1f, 55, ice, 0); Add(T, 1.05f, .15f, .9f, 1.1f, -55, ice, 0); }
            if (Has(Fx.Mane)) { var m = new[] { new Color(1, .48f, .78f), new Color(1, .82f, .48f), new Color(.48f, 1, .78f), new Color(.48f, .72f, 1), new Color(.78f, .6f, 1) }; for (int i = 0; i < 5; i++) Add(Ci, -.8f, .75f - i * .4f, .6f, .6f, 0, m[i], 0); }
            if (Has(Fx.Ant)) { Add(Sq, 0, 1.15f, .1f, .6f, 0, S.dark, 0); Add(Ci, 0, 1.5f, .32f, .32f, 0, new Color(1, .23f, .23f), 1); }
            if (Has(Fx.Horn)) { Add(T, -.72f, 1.0f, .5f, .8f, 25, pink, 0); Add(T, .72f, 1.0f, .5f, .8f, -25, pink, 0); Add(T, 0, 1.45f, .42f, 1.1f, 0, gold, 1); }
            if (Has(Fx.Flame)) { Add(T, -.45f, 1.1f, .5f, .9f, 10, new Color(1, .54f, .1f), 0); Add(T, .45f, 1.1f, .5f, .9f, -10, new Color(1, .54f, .1f), 0); Add(T, 0, 1.25f, .6f, 1.2f, 0, new Color(1, .82f, .23f), 1); }
            if (S.headType == 2)
            {
                Add(T, -.85f, 1.25f, .72f, 1.6f, 18, S.glow, 0); Add(T, .85f, 1.25f, .72f, 1.6f, -18, S.glow, 0);
                Add(T, -.85f, 1.25f, .6f, 1.5f, 18, new Color(.04f, .05f, .09f), 1); Add(T, .85f, 1.25f, .6f, 1.5f, -18, new Color(.04f, .05f, .09f), 1);
                for (int i = -1; i <= 1; i++) Add(T, i * .3f, 1.15f, .3f, .55f, 0, new Color(.04f, .05f, .09f), 1);
            }
            if (Has(Fx.Crown)) { Add(Sq, 0, .95f, 1.4f, .3f, 0, gold, 6); for (int k = -1; k <= 1; k++) Add(T, k * .5f, 1.3f, .45f, .6f, 0, gold, 6); }
            Sprite shape = S.headType == 1 ? Sq : (S.headType == 2 ? SpriteFactory.Hex : Ci);
            float w = S.headType == 1 ? 1.9f : (S.headType == 2 ? 2.3f : 2f), h = S.headType == 1 ? 1.7f : 2f;
            Add(shape, 0, 0, w + .14f, h + .14f, 0, S.headType == 2 ? S.glow : S.dark, 2); Add(shape, 0, 0, w, h, 0, S.head, 3);
            if (S.headType == 2) Add(Sq, 0, .55f, 1.2f, .16f, 0, new Color(.16f, .35f, .8f), 4);
            // eyes
            if (S.headType == 1) { Add(Sq, 0, .2f, 1.5f, .5f, 0, new Color(.06f, .13f, .25f), 4); Add(Ci, -.35f, .2f, .34f, .34f, 0, S.eye, 5); Add(Ci, .35f, .2f, .34f, .34f, 0, S.eye, 5); }
            else if (S.headType == 2) { Add(Sq, -.5f, .15f, .75f, .3f, -28, S.eye, 5); Add(Sq, .5f, .15f, .75f, .3f, 28, S.eye, 5); Add(Ci, -.62f, .26f, .14f, .14f, 0, Color.white, 6); Add(Ci, .38f, .26f, .14f, .14f, 0, Color.white, 6); }
            else
                for (int s = -1; s <= 1; s += 2)
                {
                    Add(Ci, s * .42f, .1f, .62f, .78f, s * 17f, S.eye, 5);
                    if (S.hasPupil) Add(Ci, s * .42f, .1f, .3f, .4f, 0, S.pupil, 6);
                    Add(Ci, s * .42f - .12f, .3f, .17f, .17f, 0, Color.white, 7);
                }
            // mouth
            var mouth = new Color(.35f, .04f, .11f);
            if (Has(Fx.Fang) || S.headType == 2 || Has(Fx.Santa))
            {
                float mw = S.headType == 2 ? 1.1f : .8f; Add(Sq, 0, -.4f, mw, .3f, 0, mouth, 4);
                Add(T, -.25f, -.58f, .2f, .3f, 180, Color.white, 5); Add(T, .25f, -.58f, .2f, .3f, 180, Color.white, 5);
                if (S.headType == 2) { Add(T, -.5f, -.55f, .16f, .26f, 180, Color.white, 5); Add(T, .5f, -.55f, .16f, .26f, 180, Color.white, 5); }
            }
            else if (S.headType == 1) Add(Sq, 0, -.4f, .8f, .14f, 0, new Color(.06f, .13f, .25f), 4);
            else Add(Sq, 0, -.38f, .5f, .07f, 0, S.dark, 4);
            if (Has(Fx.Santa)) { Add(T, 0, 1.0f, 1.9f, 1.5f, 10, red, 6); Add(Sq, 0, .55f, 2.1f, .38f, 0, Color.white, 7); Add(Ci, .55f, 1.75f, .45f, .45f, 0, Color.white, 7); }
        }
    }
}
