using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienSnake
{
    /// Coins, unlocked skins, selected skin, name. Saved in PlayerPrefs. All rules (no negative coins, no duplicate buys) live here.
    public static class PlayerProfile
    {
        [Serializable] class Data { public int coins = 300; public List<int> owned = new List<int> { 0 }; public int selected; public string name = ""; }
        static Data d;
        public static event Action Changed;

        static void Load()
        {
            if (d != null) return;
            try { d = JsonUtility.FromJson<Data>(PlayerPrefs.GetString("alien_profile", "")); } catch { }
            if (d == null) d = new Data();
            var set = new HashSet<int>();
            foreach (var i in d.owned) if (i >= 0 && i < SkinDatabase.All.Length) set.Add(i);
            set.Add(0); d.owned = new List<int>(set);
            if (!set.Contains(d.selected)) d.selected = 0;
            d.coins = Mathf.Max(0, d.coins);
            if (string.IsNullOrEmpty(d.name)) d.name = "Player" + UnityEngine.Random.Range(100, 1000);
        }
        static void Save() { PlayerPrefs.SetString("alien_profile", JsonUtility.ToJson(d)); PlayerPrefs.Save(); var h = Changed; if (h != null) h(); }

        public static int Coins { get { Load(); return d.coins; } }
        public static int Selected { get { Load(); return d.selected; } }
        public static bool Owns(int i) { Load(); return d.owned.Contains(i); }
        public static string Name
        {
            get { Load(); return d.name; }
            set
            {
                Load(); var s = new string(Array.FindAll((value ?? "").ToCharArray(), c => "<>&\"'`".IndexOf(c) < 0)).Trim();   // same rules as server.js
                if (s.Length > 10) s = s.Substring(0, 10);
                d.name = s.Length == 0 ? "Player" : s; Save();
            }
        }
        public static void AddCoins(int n) { Load(); d.coins = Mathf.Max(0, d.coins + Mathf.Max(0, n)); Save(); }
        public static bool Select(int i) { Load(); if (!Owns(i)) return false; d.selected = i; Save(); return true; }

        /// Returns null on success, otherwise the reason it failed.
        public static string Buy(int i)
        {
            Load();
            if (i < 0 || i >= SkinDatabase.All.Length) return "Invalid skin";
            if (Owns(i)) return "Already owned";
            int price = SkinDatabase.All[i].price;
            if (d.coins < price) return "Need " + (price - d.coins) + " more coins";
            d.coins = Mathf.Max(0, d.coins - price); d.owned.Add(i); Save(); return null;
        }
    }
}

namespace AlienSnake
{
    /// 7-day login streak: 50,100,150,200,250,300 then 500 coins. Uses the device date.
    public static class DailyReward
    {
        static readonly int[] Rewards = { 50, 100, 150, 200, 250, 300, 500 };
        static string Today() { return System.DateTime.Now.ToString("yyyy-MM-dd"); }
        static string Yesterday() { return System.DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"); }
        public static bool CanClaim { get { return UnityEngine.PlayerPrefs.GetString("daily_last", "") != Today(); } }
        public static int Streak { get { string l = UnityEngine.PlayerPrefs.GetString("daily_last", ""); return (l == Today() || l == Yesterday()) ? UnityEngine.PlayerPrefs.GetInt("daily_streak", 0) : 0; } }
        public static int NextReward { get { return Rewards[Streak % 7]; } }
        public static int Claim()
        {
            if (!CanClaim) return 0;
            int r = NextReward, streak = Streak + 1;
            UnityEngine.PlayerPrefs.SetString("daily_last", Today()); UnityEngine.PlayerPrefs.SetInt("daily_streak", streak); UnityEngine.PlayerPrefs.Save();
            PlayerProfile.AddCoins(r); return r;
        }
    }
}
