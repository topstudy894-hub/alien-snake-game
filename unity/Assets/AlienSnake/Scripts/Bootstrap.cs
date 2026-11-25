using UnityEngine;
using UnityEngine.EventSystems;

namespace AlienSnake
{
    /// Builds the whole game at runtime, so you only need to press Play in ANY scene (even an empty one). No manual scene wiring.
    public static class Bootstrap
    {
        // >>> Set your server here (the same Node.js server.js from the web version). Use wss:// for a hosted server. <<<
        public const string DefaultServerUrl = "ws://localhost:3000";
        static bool done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (done) return; done = true;
            Application.targetFrameRate = 60;
            var cam = Camera.main;
            if (cam == null) { cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>(); }
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            if (Object.FindObjectsOfType<EventSystem>().Length == 0)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
            var go = new GameObject("AlienSnake"); Object.DontDestroyOnLoad(go);
            var net = go.AddComponent<NetClient>(); net.Url = PlayerPrefs.GetString("server_url", DefaultServerUrl);
            go.AddComponent<InputController>();
            var match = go.AddComponent<MatchController>(); match.Init(cam);
            go.AddComponent<GameUI>();
        }
    }
}
