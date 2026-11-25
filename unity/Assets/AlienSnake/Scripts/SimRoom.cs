using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienSnake
{
    public class SimPlayer
    {
        public int id, skin, score, eat, drain, mg, sh, dx, respawnIn; public string name; public bool bot, alive, boost;
        public Vector2 pos; public float a, ta; public List<Vector2> seg = new List<Vector2>();
    }

    /// Offline copy of the server.js rules (same speeds, radii, power-ups) plus bot players. Used by Practice mode, no server needed.
    public class SimRoom
    {
        const float WS = 3000f; const int SP = 8, FOODN = 350;
        class Food { public Vector2 p; public int v, e; }
        class Pup { public Vector2 p; public int t; }
        public readonly List<SimPlayer> ps = new List<SimPlayer>();
        readonly List<Food> foods = new List<Food>(); readonly List<Pup> pups = new List<Pup>();
        public int MyId = 1; public SimPlayer Me;
        public Action<int> OnDead; public Action<string, string> OnKill;   // my score | victim name, killer name
        float acc; int tick, nextId = 2;
        static readonly string[] Names = { "Zorg", "Blip", "Xeno", "Glorp", "Mork", "Zap", "Nebu", "Quark", "Vex", "Pip", "Rax", "Juno" };
        static float Rnd(float a, float b) { return UnityEngine.Random.Range(a, b); }
        static float Rad(SimPlayer p) { return 8f + Mathf.Min(10f, p.seg.Count / 40f); }

        public SimRoom(int bots, int skin, string name)
        {
            for (int i = 0; i < FOODN; i++) AddFood(new Vector2(Rnd(20, WS - 20), Rnd(20, WS - 20)), 0);
            for (int i = 0; i < 6; i++) pups.Add(new Pup { p = new Vector2(Rnd(100, WS - 100), Rnd(100, WS - 100)), t = UnityEngine.Random.Range(0, 3) });
            Me = Spawn(new SimPlayer { id = MyId, name = name, skin = skin }); ps.Add(Me);
            for (int i = 0; i < bots; i++) ps.Add(Spawn(new SimPlayer { id = nextId++, name = Names[i % Names.Length], skin = UnityEngine.Random.Range(0, 16), bot = true }));
        }

        void AddFood(Vector2 p, int v) { foods.Add(new Food { p = p, v = v == 0 ? (UnityEngine.Random.value < .15f ? 3 : 1) : v, e = UnityEngine.Random.Range(0, 40) }); }

        SimPlayer Spawn(SimPlayer p)
        {
            float a = Rnd(0, 6.283f); var pos = new Vector2(Rnd(300, WS - 300), Rnd(300, WS - 300));
            p.a = p.ta = a; p.pos = pos; p.score = 0; p.alive = true; p.boost = false; p.eat = 0; p.drain = 0; p.mg = 0; p.sh = 90; p.dx = 0;   // sh = 3 s spawn shield
            p.seg.Clear(); for (int i = 0; i < 10; i++) p.seg.Add(pos - new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * i * SP);
            return p;
        }

        public void SetInput(float angle, bool boost) { if (Me.alive) { Me.ta = angle; Me.boost = boost; } }
        public void Respawn() { if (!Me.alive) Spawn(Me); }
        public void Update(float dt) { acc += dt; int n = 0; while (acc >= 1f / 30f && n++ < 4) { acc -= 1f / 30f; Tick(); } if (n >= 4) acc = 0; }

        void Tick()
        {
            tick++;
            foreach (var p in ps)
            {
                if (!p.alive) { if (p.bot && --p.respawnIn <= 0) Spawn(p); continue; }
                if (p.bot) Think(p);
                Step(p);
            }
            if (foods.Count < FOODN) for (int i = 0; i < 4; i++) AddFood(new Vector2(Rnd(20, WS - 20), Rnd(20, WS - 20)), 0);
            if (pups.Count < 6 && UnityEngine.Random.value < .02f) pups.Add(new Pup { p = new Vector2(Rnd(100, WS - 100), Rnd(100, WS - 100)), t = UnityEngine.Random.Range(0, 3) });
        }

        void Think(SimPlayer b)
        {
            if (tick % 15 == b.id % 15)
            {
                float best = 450f * 450f; Food bf = null;
                foreach (var f in foods) { float d = (f.p - b.pos).sqrMagnitude; if (d < best) { best = d; bf = f; } }
                if (bf != null) { var v = bf.p - b.pos; b.ta = Mathf.Atan2(v.y, v.x); } else b.ta += Rnd(-.8f, .8f);
            }
            if (tick % 4 == 0)
            {
                var ahead = b.pos + new Vector2(Mathf.Cos(b.a), Mathf.Sin(b.a)) * 70f; bool hit = false;
                foreach (var o in ps)
                {
                    if (o == b || !o.alive || hit) continue;
                    for (int i = 0; i < o.seg.Count; i += 2) if ((o.seg[i] - ahead).sqrMagnitude < 900f) { hit = true; break; }
                }
                if (hit) b.ta = b.a + 1.6f;
            }
            if (b.pos.x < 200 || b.pos.y < 200 || b.pos.x > WS - 200 || b.pos.y > WS - 200) { var c = new Vector2(WS / 2, WS / 2) - b.pos; b.ta = Mathf.Atan2(c.y, c.x); }
        }

        void Step(SimPlayer p)
        {
            if (p.mg > 0) p.mg--; if (p.sh > 0) p.sh--; if (p.dx > 0) p.dx--;
            float d = Mathf.Repeat(p.ta - p.a + Mathf.PI * 3f, Mathf.PI * 2f) - Mathf.PI;
            p.a += Mathf.Clamp(d, -.15f, .15f);
            bool bo = p.boost && p.seg.Count > 12; float sp = bo ? 9.2f : 5.4f;
            p.pos += new Vector2(Mathf.Cos(p.a), Mathf.Sin(p.a)) * sp;
            if (bo && ++p.drain >= 4) { p.drain = 0; if (p.score > 0) p.score--; AddFood(p.seg[p.seg.Count - 1], 1); }
            p.seg[0] = p.pos;
            int tl = 10 + Mathf.FloorToInt(p.score * .8f);
            while (p.seg.Count < tl) p.seg.Add(p.seg[p.seg.Count - 1]);
            while (p.seg.Count > tl && p.seg.Count > 10) p.seg.RemoveAt(p.seg.Count - 1);
            for (int i = 1; i < p.seg.Count; i++) { Vector2 v = p.seg[i - 1] - p.seg[i]; float dd = v.magnitude; if (dd > SP) p.seg[i] += v * ((dd - SP) / dd); }
            float rd = Rad(p);
            for (int i = foods.Count - 1; i >= 0; i--)
            {
                var f = foods[i]; Vector2 dv = f.p - p.pos; float d2 = dv.sqrMagnitude, rr = rd + (f.v >= 3 ? 13 : 9) + 4;
                if (d2 < rr * rr) { p.score += f.v * (p.dx > 0 ? 2 : 1); p.eat = 6; foods.RemoveAt(i); }
                else { float pr = rd + (p.mg > 0 ? 260f : 80f); if (d2 < pr * pr) f.p -= dv * (p.mg > 0 ? .3f : .2f); }
            }
            for (int i = pups.Count - 1; i >= 0; i--)
                if ((pups[i].p - p.pos).magnitude < rd + 18) { int t = pups[i].t; if (t == 0) p.mg = 300; else if (t == 1) p.sh = 300; else p.dx = 300; pups.RemoveAt(i); }
            if (p.eat > 0) p.eat--;
            if (p.pos.x < 0 || p.pos.y < 0 || p.pos.x > WS || p.pos.y > WS) { Die(p, null); return; }
            if (p.sh > 0) return;
            foreach (var o in ps)
            {
                if (o == p || !o.alive) continue;
                float lim = rd + Rad(o) * .8f;
                for (int i = 1; i < o.seg.Count; i++) if ((o.seg[i] - p.pos).sqrMagnitude < lim * lim) { Die(p, o); return; }
            }
        }

        void Die(SimPlayer p, SimPlayer killer)
        {
            p.alive = false; p.boost = false;
            for (int i = 0; i < p.seg.Count; i += 2) AddFood(p.seg[i] + new Vector2(Rnd(-8, 8), Rnd(-8, 8)), 2);
            if (p == Me) { var h = OnDead; if (h != null) h(p.score); } else p.respawnIn = 150;
            var k = OnKill; if (k != null) k(p.name, killer != null ? killer.name : null);
        }

        /// Same shape as a server snapshot, so MatchController draws it with the exact same code.
        public Snapshot Build()
        {
            var s = new Snapshot { online = 1 };
            foreach (var p in ps)
            {
                var q = new PlayerSnap { id = p.id, name = p.name, score = p.score, skin = p.skin, fx = (p.mg > 0 ? 1 : 0) | (p.sh > 0 ? 2 : 0) | (p.dx > 0 ? 4 : 0), pos = Snapshot.Net2U(p.pos.x, p.pos.y), angle = -p.a * Mathf.Rad2Deg, alive = p.alive, boost = p.boost && p.seg.Count > 12, eating = p.eat > 0 };
                if (p.alive) { var sg = new Vector2[p.seg.Count]; for (int i = 0; i < sg.Length; i++) sg[i] = Snapshot.Net2U(p.seg[i].x, p.seg[i].y); q.segs = sg; }
                s.players.Add(q);
            }
            var f = new List<int>(); foreach (var o in foods) if (Mathf.Abs(o.p.x - Me.pos.x) < 1050 && Mathf.Abs(o.p.y - Me.pos.y) < 800) { f.Add((int)o.p.x); f.Add((int)o.p.y); f.Add(o.v); f.Add(o.e); }
            var u = new List<int>(); foreach (var o in pups) if (Mathf.Abs(o.p.x - Me.pos.x) < 1050 && Mathf.Abs(o.p.y - Me.pos.y) < 800) { u.Add((int)o.p.x); u.Add((int)o.p.y); u.Add(o.t); }
            s.foods = f.ToArray(); s.pups = u.ToArray(); s.mine = new[] { Me.mg, Me.sh, Me.dx };
            return s;
        }
    }
}
