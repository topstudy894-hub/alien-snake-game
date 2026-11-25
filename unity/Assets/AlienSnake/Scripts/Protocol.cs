using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AlienSnake
{
    /// One player as sent by server.js inside a snapshot ('s' message).
    public class PlayerSnap
    {
        public int id, score, skin, fx;      // fx bitmask: 1 magnet, 2 shield, 4 x2
        public string name;
        public Vector2 pos; public float angle;   // angle in Unity degrees
        public bool alive, boost, eating;
        public Vector2[] segs = new Vector2[0];   // head first, Unity units
    }

    public class Snapshot
    {
        public const float U = 1f / 20f;               // 1 web pixel = 0.05 Unity units
        public List<PlayerSnap> players = new List<PlayerSnap>();
        public int[] foods = new int[0], pups = new int[0], mine = new int[3];
        public int online;

        public static Vector2 Net2U(float x, float y) { return new Vector2(x * U, -y * U); } // server y is down

        public static Snapshot Parse(JObject j)
        {
            var s = new Snapshot { online = j.Value<int?>("n") ?? 0 };
            if (j["f"] != null) s.foods = j["f"].ToObject<int[]>();
            if (j["pu"] != null) s.pups = j["pu"].ToObject<int[]>();
            if (j["m"] != null) s.mine = j["m"].ToObject<int[]>();
            foreach (JObject p in (JArray)j["p"])
            {
                var k = (JArray)p["k"]; var g = p["g"].ToObject<int[]>();
                var segs = new Vector2[g.Length / 2];
                for (int i = 0; i < segs.Length; i++) segs[i] = Net2U(g[2 * i], g[2 * i + 1]);
                s.players.Add(new PlayerSnap
                {
                    id = (int)p["i"], name = (string)p["n"], score = (int)p["s"],
                    skin = k.Count > 2 ? (int)k[2] : 0, fx = p.Value<int?>("u") ?? 0,
                    pos = Net2U((float)p["x"], (float)p["y"]), angle = -(float)p["a"] * Mathf.Rad2Deg,
                    alive = (int)p["l"] == 1, boost = (p.Value<int?>("b") ?? 0) == 1, eating = (p.Value<int?>("e") ?? 0) == 1,
                    segs = segs
                });
            }
            return s;
        }
    }
}
