using UnityEngine;

namespace AlienSnake
{
    [System.Flags]
    public enum Fx { None = 0, Spike = 1, Node = 2, Crack = 4, Star = 8, Plate = 16, Aura = 32, Fang = 64, Fin = 128, Crown = 256, Horn = 512, Mane = 1024, Santa = 2048, Flame = 4096, Ant = 8192 }

    public class SkinDef
    {
        public string name, shortName; public int price;
        public Color[] pal; public int step;             // body colour bands: pal[(segment / step) % pal.Length]
        public Color head, dark, glow, eye, pupil; public bool hasPupil;
        public Fx fx; public int headType; public bool legendary;   // headType 0 round, 1 box (robot), 2 dragon
    }

    /// The 16 characters. Ids 0..15 are what the server stores/sends (clamped to 0..15).
    public static class SkinDatabase
    {
        static Color C(string h) { Color c; ColorUtility.TryParseHtmlString("#" + h, out c); return c; }
        static Color[] P(string s) { var a = s.Split(','); var r = new Color[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = C(a[i]); return r; }
        static SkinDef S(string n, string sn, int price, string pal, int st, string head, string dark, string glow, string eye, string pupil, Fx fx, int ht = 0, bool leg = false)
        {
            return new SkinDef { name = n, shortName = sn, price = price, pal = P(pal), step = st, head = C(head), dark = C(dark), glow = C(glow), eye = C(eye), hasPupil = pupil != null, pupil = pupil != null ? C(pupil) : Color.white, fx = fx, headType = ht, legendary = leg };
        }

        public static readonly SkinDef[] All =
        {
            S("Alien Basic","Alien Basic",0,"43e03a,16401a",2,"4cf040","0d3010","7fff6a","06150a",null,Fx.None),
            S("Red Alien","Red Alien",250,"e02424,190606",2,"ee2a2a","2b0707","ff6a5a","150303",null,Fx.Fang),
            S("Ice Alien","Ice Alien",500,"f2fbff,69c7ff",2,"bfeaff","1a5f9a","aeeeff","082a55",null,Fx.Fin),
            S("Dark Alien","Dark Alien",1000,"1b1625,3a2358",2,"241a34","07050c","b36bff","0a0818","58a8ff",Fx.Node),
            S("Royal Alien","Royal Alien",1500,"ffd84a,c88a14,ffe98a,c88a14",1,"ffd84a","6a4208","ffe27a","2a1a04",null,Fx.Crown),
            S("Radioactive Alien","Radioactive",2000,"b8ff1a,141a0a,8fe01a,141a0a",2,"b8ff1a","10200a","d8ff3a","101a05","eaff6a",Fx.Node|Fx.Fang),
            S("Fire Alien","Fire Alien",2500,"ff9a14,3a1608,ff5a0a,3a1608",2,"ff7a10","2a0c04","ffb43a","200800","ffe45a",Fx.Flame|Fx.Fang),
            S("Electric Alien","Electric Alien",3000,"2f8cff,9be3ff,1a58d8,9be3ff",2,"3fa0ff","0a2a6a","7ff2ff","04122a","c8f6ff",Fx.Node),
            S("Santa Alien","Santa Alien",3500,"e32323,ffffff",2,"fff4f0","7a1010","ff6a6a","150303",null,Fx.Santa|Fx.Fang),
            S("Toxic Alien","Toxic Alien",4000,"4cff1a,22b80a,7cff3a,22b80a",1,"58ff22","0f4a05","9bff4a","06200a","d8ff7a",Fx.Node),
            S("Unicorn Alien","Unicorn Alien",5000,"ff9ad8,ffc89a,fff59a,a8ffb8,9ad8ff,c8a8ff",2,"fff6fc","b05a9a","ffd0f4","2a1a4a",null,Fx.Horn|Fx.Mane),
            S("Lava Alien","Lava Alien",6000,"2a1410,4a1c0c",2,"2c1612","0c0402","ff8a20","ff9a1a",null,Fx.Crack|Fx.Fang),
            S("Galaxy Alien","Galaxy Alien",7500,"1c1a6a,3a1c8a,14307a,5a2aa8",1,"2a2a9a","080a2a","8aa8ff","06081a","9fd0ff",Fx.Star),
            S("Robot Alien","Robot Alien",10000,"c4ccd6,6a7480,aab4c0,6a7480",1,"b8c2ce","2a323c","7fd8ff","ff2a2a",null,Fx.Plate|Fx.Ant,1),
            S("Shadow Alien","Shadow Alien",12500,"14101e,241a34",1,"181222","050308","7a5ac8","f4f0ff",null,Fx.None),
            S("Gangrage Legendary Snake","Gangrage",15000,"10121c,1a3a8a,0a0c14,2a5acc",2,"14182a","04050a","b43aff","d84aff",null,Fx.Spike|Fx.Node|Fx.Aura,2,true),
        };
    }
}
