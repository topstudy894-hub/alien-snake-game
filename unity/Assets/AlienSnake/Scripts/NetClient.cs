using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AlienSnake
{
    /// WebSocket client for the existing Node.js server (server.js, unchanged). Reconnects automatically.
    public class NetClient : MonoBehaviour
    {
        public static NetClient I;
        public string Url = "ws://localhost:3000";
        public event Action<JObject> OnMessage;
        public event Action<bool> OnStatus;
        public bool Connected { get { return ws != null && ws.State == WebSocketState.Open; } }

        ClientWebSocket ws; CancellationTokenSource cts;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        bool connecting, lastState; float nextTry; int tries;

        void Awake() { I = this; }

        void Update()
        {
            string m; int n = 0;
            while (n++ < 30 && inbox.TryDequeue(out m))
            {
                try { var h = OnMessage; if (h != null) h(JObject.Parse(m)); } catch (Exception e) { Debug.LogException(e); }
            }
            bool c = Connected;
            if (c != lastState) { lastState = c; var h = OnStatus; if (h != null) h(c); }
            if (!c && !connecting && Time.unscaledTime >= nextTry) Connect();
        }

        async void Connect()
        {
            connecting = true;
            cts = new CancellationTokenSource();
            var sock = new ClientWebSocket(); ws = sock; var tk = cts.Token;
            try { await sock.ConnectAsync(new Uri(Url), tk); tries = 0; await Receive(sock, tk); }
            catch (Exception e) { if (!tk.IsCancellationRequested) Debug.LogWarning("WebSocket: " + e.Message); }
            tries++; nextTry = Time.unscaledTime + Mathf.Min(5f, tries);   // 1s,2s,3s..5s back-off
            connecting = false;
        }

        async System.Threading.Tasks.Task Receive(ClientWebSocket sock, CancellationToken tk)
        {
            var buf = new byte[1 << 14]; var ms = new MemoryStream();
            while (sock.State == WebSocketState.Open && !tk.IsCancellationRequested)
            {
                ms.SetLength(0); WebSocketReceiveResult r;
                do
                {
                    r = await sock.ReceiveAsync(new ArraySegment<byte>(buf), tk);
                    if (r.MessageType == WebSocketMessageType.Close) { await sock.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); return; }
                    ms.Write(buf, 0, r.Count);
                } while (!r.EndOfMessage);
                inbox.Enqueue(Encoding.UTF8.GetString(ms.ToArray()));
            }
        }

        public async void Send(JObject o)
        {
            if (!Connected) return;
            var b = Encoding.UTF8.GetBytes(o.ToString(Newtonsoft.Json.Formatting.None));
            await sendLock.WaitAsync();
            try { await ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, CancellationToken.None); }
            catch (Exception e) { Debug.LogWarning("WS send: " + e.Message); }
            finally { sendLock.Release(); }
        }

        public void SetUrl(string u) { Url = u; if (cts != null) cts.Cancel(); try { if (ws != null) ws.Abort(); } catch { } }
        void OnDestroy() { if (cts != null) cts.Cancel(); if (ws != null) ws.Dispose(); }
    }
}
