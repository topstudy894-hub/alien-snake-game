using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AlienSnake
{
    /// Talks to server.js, owns the arena, camera and every SnakeView. The server is authoritative; this only draws it and sends {angle, boost}.
    public class MatchController : MonoBehaviour
    {
        public static MatchController I;
        public Camera Cam;
        public int MyId = -1, Online, Kills; public bool InRoom, Dead, Private; public string Code = ""; public float StartTime;
        public Snapshot Last;
        public event Action Joined, Left, Awakened; public event Action<string> Feed, Error; public event Action<int> Died;

        class Dot { public Transform t; public SpriteRenderer dot, halo; }
        readonly Dictionary<int, SnakeView> views = new Dictionary<int, SnakeView>();
        readonly List<Dot> foods = new List<Dot>(), pups = new List<Dot>();
        Transform world; float zoom = 1f, sendT, snapT, shake; JObject rejoin; SimRoom sim; public bool Local; int lastScore; int[] lastMine = new int[3];
        static readonly Color[] FRUIT = { new Color(.9f, .15f, .15f), new Color(.6f, .2f, .8f), new Color(.95f, .2f, .35f), new Color(.85f, .1f, .25f), new Color(1f, .6f, .1f), new Color(1f, .9f, .2f), new Color(.2f, .8f, .4f), new Color(1f, .55f, .55f) };
        static readonly Color[] PUC = { new Color(1f, .47f, .78f), new Color(.47f, .86f, 1f), new Color(1f, .82f, .25f) };

        void Awake() { I = this; }
        public void Init(Camera c) { Cam = c; BuildArena(); }
        void OnEnable() { NetClient.I.OnMessage += Handle; NetClient.I.OnStatus += OnStatus; }
        void OnDisable() { if (NetClient.I != null) { NetClient.I.OnMessage -= Handle; NetClient.I.OnStatus -= OnStatus; } }

        void BuildArena()
        {
            world = new GameObject("World").transform;
            Cam.orthographic = true; Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = new Color(.03f, .03f, .09f);
            Sprite Sq = SpriteFactory.Square;
            Node("Floor", Sq, new Vector2(75, -75), new Vector2(150, 150), new Color(.07f, .06f, .2f), -10000);
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) px[y * 32 + x] = (x == 0 || y == 0) ? new Color32(255, 255, 255, 22) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px); tex.Apply();
            var grid = new GameObject("Grid").AddComponent<SpriteRenderer>(); grid.transform.SetParent(world, false);
            grid.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0, 1), 32f / 5f, 0, SpriteMeshType.FullRect);
            grid.drawMode = SpriteDrawMode.Tiled; grid.size = new Vector2(150, 150); grid.sharedMaterial = SpriteFactory.Mat; grid.sortingOrder = -9999; grid.transform.position = Vector3.zero;
            var red = new Color(1f, .3f, .43f);
            Node("B1", Sq, new Vector2(75, 0), new Vector2(150.6f, .3f), red, -9000); Node("B2", Sq, new Vector2(75, -150), new Vector2(150.6f, .3f), red, -9000);
            Node("B3", Sq, new Vector2(0, -75), new Vector2(.3f, 150.6f), red, -9000); Node("B4", Sq, new Vector2(150, -75), new Vector2(.3f, 150.6f), red, -9000);
            Cam.transform.position = new Vector3(75, -75, -10);
        }
        void Node(string n, Sprite sp, Vector2 pos, Vector2 scale, Color c, int order)
        {
            var sr = new GameObject(n).AddComponent<SpriteRenderer>(); sr.transform.SetParent(world, false);
            sr.sprite = sp; sr.color = c; sr.sharedMaterial = SpriteFactory.Mat; sr.sortingOrder = order; sr.transform.position = pos; sr.transform.localScale = new Vector3(scale.x, scale.y, 1);
        }

        // ---- messages ----
        JObject Hello() { return new JObject { { "t", "hello" }, { "name", PlayerProfile.Name }, { "av", "\U0001F451" }, { "h", 0 }, { "rb", 0 }, { "sp", PlayerProfile.Selected } }; }
        void Msg(string t) { NetClient.I.Send(Hello()); NetClient.I.Send(new JObject { { "t", t } }); }
        public void QuickPlay() { Msg("quick"); }
        public void CreateRoom() { Msg("create"); }
        public void JoinRoom(string code) { NetClient.I.Send(Hello()); NetClient.I.Send(new JObject { { "t", "join" }, { "code", code.ToUpper() } }); }
        public void Respawn() { if (Local) { sim.Respawn(); Dead = false; Kills = 0; StartTime = Time.time; return; } NetClient.I.Send(Hello()); NetClient.I.Send(new JObject { { "t", "respawn" } }); Dead = false; Kills = 0; StartTime = Time.time; }
        public void Leave()
        {
            if (!Local) NetClient.I.Send(new JObject { { "t", "leave" } }); Local = false; sim = null; InRoom = false; Dead = false; rejoin = null; ClearViews();
            var h = Left; if (h != null) h();
        }

        void OnStatus(bool connected)
        {
            if (!connected && InRoom && !Local) rejoin = Private ? new JObject { { "t", "join" }, { "code", Code } } : new JObject { { "t", "quick" } };
            if (connected) { NetClient.I.Send(Hello()); if (rejoin != null) { NetClient.I.Send(rejoin); rejoin = null; } }
        }

        void Handle(JObject j)
        {
            switch (j.Value<string>("t"))
            {
                case "online": Online = j.Value<int>("n"); break;
                case "err": { var h = Error; if (h != null) h(j.Value<string>("m")); break; }
                case "joined":
                    MyId = j.Value<int>("id"); Code = j.Value<string>("code"); Private = j.Value<bool>("priv");
                    InRoom = true; Dead = false; Kills = 0; StartTime = Time.time; ClearViews();
                    { var h = Joined; if (h != null) h(); } break;
                case "s": if (InRoom && !Local) Apply(Snapshot.Parse(j)); break;
                case "kill":
                    {
                        string v = j.Value<string>("v"), k = j.Value<string>("k");
                        var h = Feed; if (h != null) h(k != null ? k + " ate " + v : v + " crashed");
                        if (k != null && k == PlayerProfile.Name) Kills++; break;
                    }
                case "dead":
                    {
                        Dead = true; int sc = j.Value<int>("sc"); PlayerProfile.AddCoins(sc / 2); shake = 1f; Sfx.Play(Sfx.Die);
                        var h = Died; if (h != null) h(sc); break;
                    }
            }
        }

        SnakeView NewView(PlayerSnap p)
        {
            var go = new GameObject("Snake" + p.id); go.transform.SetParent(world, false);
            var v = go.AddComponent<SnakeView>(); v.Init(p.id, SkinDatabase.All[Mathf.Clamp(p.skin, 0, SkinDatabase.All.Length - 1)], p.id == MyId ? 500 : p.id % 400); return v;
        }

        void Apply(Snapshot s)
        {
            Last = s; Online = s.online;
            var seen = new HashSet<int>();
            foreach (var p in s.players)
            {
                seen.Add(p.id); SnakeView v; var skin = SkinDatabase.All[Mathf.Clamp(p.skin, 0, SkinDatabase.All.Length - 1)];
                bool fresh = false; if (!views.TryGetValue(p.id, out v) || v.Skin != skin) { if (v != null) Destroy(v.gameObject); v = NewView(p); views[p.id] = v; fresh = true; }
                bool show = p.alive && p.segs.Length > 0;
                if (show && (fresh || !v.gameObject.activeSelf)) { v.Snap(); if (p.id == MyId && skin.legendary) DoEntrance(p.pos); }
                v.gameObject.SetActive(show);
                if (show) v.SetTarget(p.segs, p.angle, p.boost, p.eating);
                if (p.id == MyId) { if (show && p.score > lastScore) Sfx.Play(Sfx.Eat); lastScore = p.score; }
            }
            var gone = new List<int>(); foreach (var id in views.Keys) if (!seen.Contains(id)) gone.Add(id);
            foreach (var id in gone) { Destroy(views[id].gameObject); views.Remove(id); }
            if (s.mine.Length > 2 && (s.mine[0] > lastMine[0] || s.mine[2] > lastMine[2] || (s.mine[1] > lastMine[1] && s.mine[1] > 100))) Sfx.Play(Sfx.Power);
            lastMine = s.mine; Sync(foods, s.foods, 4, false); Sync(pups, s.pups, 3, true);
        }

        void Sync(List<Dot> pool, int[] flat, int stride, bool power)
        {
            int n = flat.Length / stride;
            while (pool.Count < n)
            {
                var g = new GameObject(power ? "Pu" : "Food"); g.transform.SetParent(world, false);
                var d = new Dot { t = g.transform };
                d.halo = new GameObject("h").AddComponent<SpriteRenderer>(); d.halo.transform.SetParent(g.transform, false); d.halo.sprite = SpriteFactory.Glow; d.halo.sharedMaterial = SpriteFactory.Mat; d.halo.sortingOrder = -500;
                d.dot = new GameObject("d").AddComponent<SpriteRenderer>(); d.dot.transform.SetParent(g.transform, false); d.dot.sprite = power ? SpriteFactory.Hex : SpriteFactory.Circle; d.dot.sharedMaterial = SpriteFactory.Mat; d.dot.sortingOrder = -400;
                pool.Add(d);
            }
            for (int i = 0; i < pool.Count; i++)
            {
                var d = pool[i]; d.t.gameObject.SetActive(i < n); if (i >= n) continue;
                d.t.position = Snapshot.Net2U(flat[i * stride], flat[i * stride + 1]);
                if (power) { var c = PUC[Mathf.Clamp(flat[i * stride + 2], 0, 2)]; d.dot.color = c; d.halo.color = new Color(c.r, c.g, c.b, .5f); d.dot.transform.localScale = Vector3.one * .9f; d.halo.transform.localScale = Vector3.one * 2.6f; }
                else { int v = flat[i * stride + 2]; var c = FRUIT[flat[i * stride + 3] % FRUIT.Length]; float sz = v >= 3 ? .8f : .5f; d.dot.color = c; d.halo.color = new Color(1f, .93f, .6f, .35f); d.dot.transform.localScale = Vector3.one * sz; d.halo.transform.localScale = Vector3.one * sz * 2.6f; }
            }
        }

        public void StartPractice()
        {
            sim = new SimRoom(8, PlayerProfile.Selected, PlayerProfile.Name);
            sim.OnDead = sc => { Dead = true; PlayerProfile.AddCoins(sc / 2); shake = 1f; Sfx.Play(Sfx.Die); var h = Died; if (h != null) h(sc); };
            sim.OnKill = (v, k) => { var h = Feed; if (h != null) h(k != null ? k + " ate " + v : v + " crashed"); if (k != null && k == PlayerProfile.Name) Kills++; };
            Local = true; MyId = sim.MyId; Private = false; Code = ""; InRoom = true; Dead = false; Kills = 0; StartTime = Time.time; lastScore = 0; ClearViews();
            var j = Joined; if (j != null) j();
        }

        void DoEntrance(Vector2 pos)   // Gangrage legendary entrance: rings, shake, sound, screen flash
        {
            shake = 1.2f; Sfx.Play(Sfx.Power);
            for (int i = 0; i < 4; i++) Burst.Spawn(pos, i % 2 == 0 ? new Color(.7f, .23f, 1f) : new Color(.3f, .8f, 1f), 1.5f + i * 1.2f, .8f + i * .1f);
            var h = Awakened; if (h != null) h();
        }

        void ClearViews() { foreach (var v in views.Values) if (v != null) Destroy(v.gameObject); views.Clear(); Last = null; }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            foreach (var v in views.Values) if (v.gameObject.activeSelf) v.Tick(dt, Time.time);
            SnakeView my; bool has = views.TryGetValue(MyId, out my) && my.gameObject.activeSelf;
            float target = 1f;
            if (has)
            {
                Vector2 p = Vector2.Lerp(Cam.transform.position, my.HeadPos, 1f - Mathf.Exp(-dt * 12f)); Cam.transform.position = new Vector3(p.x, p.y, -10f);
                target = Mathf.Max(.65f, 1f - (my.Length - 10) / 600f);     // zoom out as you grow
            }
            zoom += (target - zoom) * (1f - Mathf.Exp(-dt * 1.2f));
            Cam.orthographicSize = Mathf.Max(11f, 15f / Cam.aspect) / zoom;
            if (shake > .01f) { Cam.transform.position += (Vector3)(UnityEngine.Random.insideUnitCircle * shake * .4f); shake *= Mathf.Exp(-dt * 6f); }

            sendT += dt;
            if (Local && sim != null)
            {
                var ld = InputController.I.Dir; sim.SetInput(Mathf.Atan2(-ld.y, ld.x), InputController.I.Boost); sim.Update(dt);
                snapT += dt; if (snapT >= 1f / 20f) { snapT = 0; Apply(sim.Build()); }
            }
            if (InRoom && !Dead && !Local && sendT >= .05f && NetClient.I.Connected)
            {
                sendT = 0; var d = InputController.I.Dir;
                NetClient.I.Send(new JObject { { "t", "in" }, { "a", Mathf.Atan2(-d.y, d.x) }, { "b", InputController.I.Boost ? 1 : 0 } });
            }
        }
    }

    /// One expanding, fading glow ring (used for the legendary entrance).
    public class Burst : MonoBehaviour
    {
        float t, dur, maxScale; Color col; SpriteRenderer sr;
        public static void Spawn(Vector2 p, Color c, float maxScale, float dur)
        {
            var go = new GameObject("Burst"); go.transform.position = p; var b = go.AddComponent<Burst>();
            b.sr = go.AddComponent<SpriteRenderer>(); b.sr.sprite = SpriteFactory.Glow; b.sr.sharedMaterial = SpriteFactory.Mat; b.sr.sortingOrder = 4000; b.col = c; b.dur = dur; b.maxScale = maxScale;
        }
        void Update()
        {
            t += Time.deltaTime; float k = t / dur; if (k >= 1f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * Mathf.Lerp(.5f, maxScale * 6f, k); sr.color = new Color(col.r, col.g, col.b, (1f - k) * .9f);
        }
    }
}
