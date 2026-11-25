using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlienSnake
{
    /// Whole UI built from code (uGUI + TextMeshPro): lobby, skin shop, HUD, dialogs, toast. Anchors are percentages, so it adapts to phone/desktop.
    public class GameUI : MonoBehaviour
    {
        GameObject lobby, shop, hud, dlg, root;
        TextMeshProUGUI coinsLbl, shopCoins, statusLbl, scoreLbl, boardLbl, feedLbl, infoLbl, fxLbl, toastLbl, dlgText, pvName, pvPrice, pvBtnLbl;
        TMP_InputField nameIn, codeIn; UnityEngine.UI.Image pvBtnImg; UnityEngine.UI.Button dlgOk, dlgNo;
        readonly List<string> feed = new List<string>(); readonly List<float> feedAt = new List<float>();
        float toastT, boardT, flashT; int pvIndex; bool portrait; Action okCb, noCb; UnityEngine.UI.Image flash; TextMeshProUGUI dailyLbl, soundLbl;
        class Card { public int i; public UnityEngine.UI.Image bg, btnImg; public TextMeshProUGUI price, btn; public GameObject lockObj; }
        readonly List<Card> cards = new List<Card>();
        Camera pvCam; RenderTexture pvRt; SnakeView pvView; readonly Vector2 pvOrigin = new Vector2(400, 400);

        // ---------- tiny uGUI helpers (percent anchors) ----------
        static RectTransform R(string n, Transform p, float x0, float y0, float x1, float y1)
        {
            var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false);
            var r = g.GetComponent<RectTransform>(); r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero; return r;
        }
        static UnityEngine.UI.Image Im(Transform p, string n, Color c, float x0, float y0, float x1, float y1)
        { var i = R(n, p, x0, y0, x1, y1).gameObject.AddComponent<UnityEngine.UI.Image>(); i.color = c; return i; }
        static TextMeshProUGUI T(Transform p, string s, float sz, Color c, TextAlignmentOptions a, float x0, float y0, float x1, float y1)
        { var t = R("T", p, x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>(); t.text = s; t.fontSize = sz; t.color = c; t.alignment = a; t.raycastTarget = false; return t; }
        static UnityEngine.UI.Button B(Transform p, string s, Color c, float x0, float y0, float x1, float y1, UnityAction cb, float fs = 40)
        {
            var im = Im(p, "Btn_" + s, c, x0, y0, x1, y1); var b = im.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = im; b.onClick.AddListener(() => Sfx.Play(Sfx.Click)); b.onClick.AddListener(cb);
            T(im.transform, s, fs, Color.white, TextAlignmentOptions.Center, 0, 0, 1, 1); return b;
        }
        static TMP_InputField Field(Transform p, string ph, int lim, TMP_InputField.ContentType ct, float x0, float y0, float x1, float y1)
        {
            var im = Im(p, "Input", new Color(1, 1, 1, .18f), x0, y0, x1, y1); var f = im.gameObject.AddComponent<TMP_InputField>();
            var area = R("Area", im.transform, .05f, 0, .95f, 1); area.gameObject.AddComponent<RectMask2D>();
            var pl = T(area, ph, 36, new Color(1, 1, 1, .45f), TextAlignmentOptions.Center, 0, 0, 1, 1); var tx = T(area, "", 36, Color.white, TextAlignmentOptions.Center, 0, 0, 1, 1);
            f.textViewport = area; f.textComponent = tx; f.placeholder = pl; f.characterLimit = lim; f.contentType = ct; f.targetGraphic = im; return f;
        }

        static readonly Color Green = new Color(.2f, .72f, .15f), Blue = new Color(.12f, .4f, .85f), Orange = new Color(1f, .6f, .05f), Navy = new Color(.05f, .12f, .35f, .9f), Gold = new Color(1f, .8f, .2f);

        void Start()
        {
            portrait = Screen.height > Screen.width;
            var cg = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); cg.transform.SetParent(transform, false);
            var cv = cg.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = .5f;
            var safe = Screen.safeArea; var r = R("Safe", cg.transform, safe.xMin / Screen.width, safe.yMin / Screen.height, safe.xMax / Screen.width, safe.yMax / Screen.height); root = r.gameObject;
            BuildLobby(r); BuildShop(r); BuildHud(r); BuildDialog(r);
            toastLbl = T(r, "", 44, Color.white, TextAlignmentOptions.Center, .2f, .85f, .8f, .93f); toastLbl.alpha = 0;
            var m = MatchController.I;
            m.Joined += () => Show(hud); m.Left += () => Show(lobby);
            m.Awakened += () => { flashT = 1f; Toast("GANGRAGE AWAKENS!"); };
            flash = Im(r, "Flash", new Color(.7f, .4f, 1f, 0), 0, 0, 1, 1); flash.raycastTarget = false; m.Died += OnDied; m.Error += Toast;
            m.Feed += s => { feed.Add(s); feedAt.Add(Time.time); if (feed.Count > 4) { feed.RemoveAt(0); feedAt.RemoveAt(0); } };
            Show(lobby);
        }

        void Show(GameObject g) { lobby.SetActive(g == lobby); shop.SetActive(g == shop); hud.SetActive(g == hud); dlg.SetActive(false); if (pvCam != null) pvCam.enabled = g == shop; }
        public void Toast(string s) { toastLbl.text = s; toastT = 2.5f; }

        // ---------- lobby ----------
        void BuildLobby(RectTransform p)
        {
            var bg = Im(p, "Lobby", new Color(.1f, .35f, .8f), 0, 0, 1, 1); lobby = bg.gameObject; var t = lobby.transform;
            T(t, "Snake.io", 170, new Color(1f, .85f, .1f), TextAlignmentOptions.Center, 0, .76f, 1, .98f);
            T(t, "Eat  -  Grow  -  Survive", 44, new Color(1, .93f, .7f), TextAlignmentOptions.Center, 0, .7f, 1, .77f);
            coinsLbl = T(t, "", 46, Gold, TextAlignmentOptions.Left, .02f, .93f, .4f, 1f);
            statusLbl = T(t, "", 36, Color.white, TextAlignmentOptions.Right, .5f, .93f, .98f, 1f);
            nameIn = Field(t, "Your name", 10, TMP_InputField.ContentType.Standard, .3f, .6f, .7f, .69f); nameIn.text = PlayerProfile.Name;
            nameIn.onEndEdit.AddListener(s => { PlayerProfile.Name = s; nameIn.text = PlayerProfile.Name; });
            B(t, "PLAY", Green, .27f, .44f, .73f, .57f, () => { if (!Ready()) return; PlayerProfile.Name = nameIn.text; MatchController.I.QuickPlay(); }, 80);
            B(t, "Create Room", Orange, .27f, .31f, .46f, .41f, () => { if (!Ready()) return; MatchController.I.CreateRoom(); }, 36);
            codeIn = Field(t, "CODE", 5, TMP_InputField.ContentType.Alphanumeric, .48f, .31f, .6f, .41f);
            B(t, "Join Room", Blue, .62f, .31f, .73f, .41f, () => { if (!Ready()) return; if (codeIn.text.Length != 5) { Toast("Enter the 5-letter room code"); return; } MatchController.I.JoinRoom(codeIn.text); }, 36);
            B(t, "Skins & Shop", new Color(.55f, .25f, .85f), .27f, .17f, .73f, .27f, () => { Show(shop); EnsurePreview(); ShowPreview(PlayerProfile.Selected); }, 44);
            B(t, "Practice (offline)", Orange, .27f, .07f, .49f, .16f, () => { PlayerProfile.Name = nameIn.text; MatchController.I.StartPractice(); }, 34);
            var db = B(t, "Daily Reward", new Color(.2f, .7f, .4f), .51f, .07f, .73f, .16f, OnDaily, 34); dailyLbl = db.GetComponentInChildren<TextMeshProUGUI>();
            var sb = B(t, "", new Color(1, 1, 1, .2f), .02f, .84f, .14f, .91f, () => { Sfx.Muted = !Sfx.Muted; }, 30); soundLbl = sb.GetComponentInChildren<TextMeshProUGUI>();
            T(t, "Server: " + NetClient.I.Url, 28, new Color(1, 1, 1, .6f), TextAlignmentOptions.Center, 0, 0, 1, .06f);
        }
        bool Ready() { if (NetClient.I.Connected) return true; Toast("Not connected to the server yet..."); return false; }

        // ---------- shop ----------
        void BuildShop(RectTransform p)
        {
            var bg = Im(p, "Shop", new Color(.12f, .08f, .3f), 0, 0, 1, 1); shop = bg.gameObject; var t = shop.transform;
            B(t, "Back", Blue, .01f, .91f, .13f, .99f, () => Show(lobby), 36);
            T(t, "Alien Skins", 66, Gold, TextAlignmentOptions.Center, .3f, .91f, .7f, 1f);
            shopCoins = T(t, "", 46, Gold, TextAlignmentOptions.Right, .7f, .91f, .99f, 1f);
            float px0 = 0, px1 = portrait ? 1 : .4f, py0 = portrait ? .62f : .36f, py1 = portrait ? .9f : .9f;
            var raw = R("Preview", t, px0 + .02f, py0, px1 - .02f, py1).gameObject.AddComponent<RawImage>(); raw.color = Color.white; raw.raycastTarget = false; raw.name = "PreviewImage";
            raw.texture = null; previewImage = raw;
            float iy = portrait ? .5f : .0f;
            pvName = T(t, "", 50, Color.white, TextAlignmentOptions.Center, px0, portrait ? .55f : .28f, px1, portrait ? .62f : .36f);
            pvPrice = T(t, "", 40, Gold, TextAlignmentOptions.Center, px0, portrait ? .5f : .21f, px1, portrait ? .55f : .28f);
            var b = B(t, "BUY", Green, px0 + .08f, portrait ? .43f : .08f, px1 - .08f, portrait ? .5f : .19f, () => OnAction(pvIndex), 48);
            pvBtnImg = b.GetComponent<UnityEngine.UI.Image>(); pvBtnLbl = b.GetComponentInChildren<TextMeshProUGUI>();
            // scroll grid of 16 cards
            var sv = Im(t, "Scroll", new Color(0, 0, 0, .25f), portrait ? 0f : .4f, .01f, 1f, portrait ? .42f : .9f); var sr = sv.gameObject.AddComponent<ScrollRect>();
            var vp = R("Viewport", sv.transform, 0, 0, 1, 1); vp.gameObject.AddComponent<RectMask2D>();
            var content = R("Content", vp, 0, 1, 1, 1); content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(320, 340); grid.spacing = new Vector2(14, 14); grid.padding = new RectOffset(14, 14, 14, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = portrait ? 3 : 3;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.viewport = vp; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30;
            for (int i = 0; i < SkinDatabase.All.Length; i++) MakeCard(content, i);
        }
        RawImage previewImage;

        void MakeCard(Transform parent, int i)
        {
            var s = SkinDatabase.All[i]; var card = new Card { i = i };
            var bg = R("Card" + i, parent, 0, 0, 1, 1).gameObject.AddComponent<UnityEngine.UI.Image>(); bg.color = new Color(.1f, .22f, .6f); card.bg = bg;
            var open = bg.gameObject.AddComponent<UnityEngine.UI.Button>(); open.targetGraphic = bg; int idx = i; open.onClick.AddListener(() => ShowPreview(idx));
            if (s.legendary) { var o = bg.gameObject.AddComponent<UnityEngine.UI.Outline>(); o.effectColor = Gold; o.effectDistance = new Vector2(6, 6); }
            for (int k = 0; k < Mathf.Min(4, s.pal.Length); k++) Im(bg.transform, "c", s.pal[k], .1f + k * .2f, .62f, .1f + k * .2f + .18f, .86f).raycastTarget = false;
            T(bg.transform, s.shortName, 34, Color.white, TextAlignmentOptions.Center, 0, .42f, 1, .6f);
            card.price = T(bg.transform, "", 32, Gold, TextAlignmentOptions.Center, 0, .28f, 1, .42f);
            var b = B(bg.transform, "BUY", Green, .1f, .04f, .9f, .25f, () => OnAction(idx), 34); card.btnImg = b.GetComponent<UnityEngine.UI.Image>(); card.btn = b.GetComponentInChildren<TextMeshProUGUI>();
            card.lockObj = T(bg.transform, "LOCKED", 26, new Color(1, .5f, .5f), TextAlignmentOptions.Left, .06f, .88f, .6f, 1f).gameObject;
            if (s.legendary) T(bg.transform, "LEGENDARY", 24, Gold, TextAlignmentOptions.Right, .3f, .88f, .96f, 1f);
            cards.Add(card);
        }

        void RefreshShop()
        {
            foreach (var c in cards)
            {
                bool own = PlayerProfile.Owns(c.i), sel = own && PlayerProfile.Selected == c.i;
                c.lockObj.SetActive(!own); c.price.text = own ? (c.i == 0 ? "Starter" : "Owned") : SkinDatabase.All[c.i].price + " coins";
                c.btn.text = sel ? "SELECTED" : (own ? "SELECT" : "BUY"); c.btnImg.color = sel ? new Color(.3f, .35f, .45f) : (own ? Blue : Green);
                c.bg.color = c.i == pvIndex ? new Color(.2f, .35f, .8f) : new Color(.1f, .22f, .6f);
            }
            var d = SkinDatabase.All[pvIndex]; bool o = PlayerProfile.Owns(pvIndex), s = o && PlayerProfile.Selected == pvIndex;
            pvName.text = d.name + (d.legendary ? "\n<size=60%><color=#FFCC33>LEGENDARY</color></size>" : "");
            pvPrice.text = o ? (pvIndex == 0 ? "Starter alien" : "Unlocked") : d.price + " coins";
            pvBtnLbl.text = s ? "SELECTED" : (o ? "SELECT" : "BUY"); pvBtnImg.color = s ? new Color(.3f, .35f, .45f) : (o ? Blue : Green);
        }

        void OnAction(int i)
        {
            var d = SkinDatabase.All[i];
            if (PlayerProfile.Owns(i)) { if (PlayerProfile.Select(i)) Toast(d.name + " selected"); RefreshShop(); return; }
            if (PlayerProfile.Coins < d.price) { Toast("Need " + (d.price - PlayerProfile.Coins) + " more coins"); return; }
            Dlg("Buy " + d.name + "\nfor " + d.price + " coins?\n<size=60%>Balance: " + PlayerProfile.Coins + " -> " + (PlayerProfile.Coins - d.price) + "</size>", "BUY", () =>
            { var err = PlayerProfile.Buy(i); if (err == null) Sfx.Play(Sfx.Buy); Toast(err ?? ("Unlocked " + d.name + "! Tap SELECT")); RefreshShop(); }, "CANCEL", null);
        }

        // ---------- shop preview (render texture + a real SnakeView) ----------
        void EnsurePreview()
        {
            if (pvCam != null) return;
            pvRt = new RenderTexture(640, 480, 16); previewImage.texture = pvRt;
            var g = new GameObject("PreviewCam"); pvCam = g.AddComponent<Camera>(); pvCam.orthographic = true; pvCam.orthographicSize = 2.6f;
            pvCam.clearFlags = CameraClearFlags.SolidColor; pvCam.backgroundColor = new Color(.18f, .1f, .4f); pvCam.targetTexture = pvRt; pvCam.enabled = false;
            g.transform.position = new Vector3(pvOrigin.x, pvOrigin.y, -10);
        }
        void ShowPreview(int i)
        {
            pvIndex = i; if (pvView != null) Destroy(pvView.gameObject);
            pvView = new GameObject("PreviewSnake").AddComponent<SnakeView>(); pvView.Init(-1, SkinDatabase.All[i], 0);
            if (pvCam != null) pvCam.enabled = true; RefreshShop();
        }
        void UpdatePreview()
        {
            if (pvView == null) return; float t = Time.time; const int n = 36; var pts = new Vector2[n];
            for (int i = 0; i < n; i++) { float u = i / (float)(n - 1); pts[i] = pvOrigin + new Vector2(1.7f - i * .11f, .5f - u * .5f + Mathf.Sin(i * .3f - t * 3f) * .3f * u + Mathf.Sin(t * 1.5f) * .05f); }
            pvView.SetTarget(pts, 0f, false, false); pvView.Tick(Time.deltaTime, t);
        }

        // ---------- HUD ----------
        void BuildHud(RectTransform p)
        {
            hud = R("HUD", p, 0, 0, 1, 1).gameObject; var t = hud.transform;
            scoreLbl = T(t, "", 48, Color.white, TextAlignmentOptions.Left, .01f, .92f, .3f, 1f);
            infoLbl = T(t, "", 34, new Color(1, 1, 1, .85f), TextAlignmentOptions.Center, .35f, .93f, .65f, 1f);
            boardLbl = T(t, "", 30, Color.white, TextAlignmentOptions.TopRight, .74f, .6f, .99f, .99f);
            feedLbl = T(t, "", 28, new Color(1, .95f, .75f), TextAlignmentOptions.TopRight, .6f, .42f, .99f, .59f);
            fxLbl = T(t, "", 36, Color.white, TextAlignmentOptions.Center, .3f, .02f, .7f, .12f);
            B(t, "Lobby", new Color(1, 1, 1, .2f), .01f, .83f, .12f, .91f, () => MatchController.I.Leave(), 32);
            var boost = Im(t, "Boost", new Color(1, 1, 1, .25f), .03f, .05f, .13f, .2f); T(boost.transform, "BOOST", 34, Color.white, TextAlignmentOptions.Center, 0, 0, 1, 1);
            var tr = boost.gameObject.AddComponent<EventTrigger>();
            var dn = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown }; dn.callback.AddListener(_ => InputController.I.UiBoost = true); tr.triggers.Add(dn);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp }; up.callback.AddListener(_ => InputController.I.UiBoost = false); tr.triggers.Add(up);
        }

        void UpdateHud()
        {
            var m = MatchController.I; var s = m.Last; if (s == null) return;
            PlayerSnap me = null; foreach (var p in s.players) if (p.id == m.MyId) me = p;
            scoreLbl.text = "Score: " + (me != null ? me.score : 0);
            infoLbl.text = (m.Private ? "Room " + m.Code + "   " : "") + "Players: " + s.players.Count + "   Online: " + m.Online;
            string[] nm = { "Magnet", "Shield", "x2 Score" }; var sb = new StringBuilder();
            for (int i = 0; i < 3; i++) if (s.mine.Length > i && s.mine[i] > 0) sb.AppendLine(nm[i] + " " + Mathf.CeilToInt(s.mine[i] / 30f) + "s");
            fxLbl.text = sb.ToString();
            boardT -= Time.deltaTime;
            if (boardT <= 0)
            {
                boardT = .25f; var l = new List<PlayerSnap>(s.players); l.Sort((a, b) => b.score.CompareTo(a.score));
                var b2 = new StringBuilder("<color=#FFD54A>Top Aliens</color>\n");
                for (int i = 0; i < Mathf.Min(10, l.Count); i++) b2.AppendLine((l[i].id == m.MyId ? "<color=#7CFF7C>" : "") + (i + 1) + ". " + l[i].name + "  " + l[i].score + (l[i].id == m.MyId ? "</color>" : ""));
                boardLbl.text = b2.ToString();
                var fb = new StringBuilder(); for (int i = 0; i < feed.Count; i++) if (Time.time - feedAt[i] < 6f) fb.AppendLine(feed[i]); feedLbl.text = fb.ToString();
            }
        }

        // ---------- dialog / death ----------
        void BuildDialog(RectTransform p)
        {
            var dim = Im(p, "Dialog", new Color(0, 0, 0, .75f), 0, 0, 1, 1); dlg = dim.gameObject; var box = Im(dlg.transform, "Box", new Color(.15f, .3f, .65f), .25f, .3f, .75f, .7f);
            dlgText = T(box.transform, "", 50, Color.white, TextAlignmentOptions.Center, .05f, .35f, .95f, .95f);
            dlgOk = B(box.transform, "OK", Green, .08f, .06f, .47f, .3f, () => { dlg.SetActive(false); var a = okCb; if (a != null) a(); }, 44);
            dlgNo = B(box.transform, "NO", Blue, .53f, .06f, .92f, .3f, () => { dlg.SetActive(false); var a = noCb; if (a != null) a(); }, 44);
            dlg.SetActive(false);
        }
        void Dlg(string text, string ok, Action okA, string no, Action noA)
        {
            dlgText.text = text; okCb = okA; noCb = noA; dlgOk.GetComponentInChildren<TextMeshProUGUI>().text = ok; dlgNo.GetComponentInChildren<TextMeshProUGUI>().text = no; dlg.SetActive(true);
        }
        void OnDied(int score)
        {
            var m = MatchController.I; int t = Mathf.Max(0, (int)(Time.time - m.StartTime));
            Dlg("You died!\nScore " + score + "  Kills " + m.Kills + "  Time " + t / 60 + ":" + (t % 60).ToString("00") + "\n<size=60%>+" + score / 2 + " coins</size>", "RESPAWN", () => m.Respawn(), "LOBBY", () => m.Leave());
        }

        void OnDaily()
        {
            if (!DailyReward.CanClaim) { Toast("Come back tomorrow for the next reward"); return; }
            int day = DailyReward.Streak + 1; int r = DailyReward.Claim(); Sfx.Play(Sfx.Buy); Toast("Daily reward (day " + day + "): +" + r + " coins!");
        }

        void Update()
        {
            coinsLbl.text = shopCoins.text = "Coins: " + PlayerProfile.Coins;
            var net = NetClient.I; statusLbl.text = (net.Connected ? "<color=#7CFF7C>Online</color>" : "<color=#FF9A9A>Connecting...</color>") + "   Players online: " + MatchController.I.Online;
            if (hud.activeSelf) UpdateHud();
            if (shop.activeSelf) UpdatePreview();
            dailyLbl.text = DailyReward.CanClaim ? "Daily Reward  !" : "Daily Reward"; soundLbl.text = Sfx.Muted ? "Sound: Off" : "Sound: On";
            if (flashT > 0) { flashT -= Time.deltaTime; flash.color = new Color(.7f, .4f, 1f, Mathf.Clamp01(flashT) * .6f); }
            if (toastT > 0) { toastT -= Time.deltaTime; toastLbl.alpha = Mathf.Clamp01(toastT); }
        }
    }
}
