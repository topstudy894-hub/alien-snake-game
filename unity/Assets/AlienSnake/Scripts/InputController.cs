using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace AlienSnake
{
    /// Steering + boost. Desktop: aim with the mouse, boost = Space or right mouse button.
    /// Phone: floating joystick (drag anywhere), boost = on-screen button (GameUI sets UiBoost).
    public class InputController : MonoBehaviour
    {
        public static InputController I;
        public Vector2 Dir = Vector2.right;   // screen-space direction, y up
        public bool Boost, UiBoost;
        Vector2 origin; bool wasTouch;

        void Awake() { I = this; }

        void Update()
        {
            Vector2 pos; bool touch, alt;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            touch = false; alt = false; pos = Vector2.zero;
            var ts = Touchscreen.current; var m = Mouse.current; var k = Keyboard.current;
            if (ts != null && ts.primaryTouch.press.isPressed) { touch = true; pos = ts.primaryTouch.position.ReadValue(); }
            else if (m != null) { pos = m.position.ReadValue(); alt = m.rightButton.isPressed; }
            if (k != null && k.spaceKey.isPressed) alt = true;
#else
            touch = Input.touchCount > 0;
            pos = touch ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            alt = Input.GetMouseButton(1) || Input.GetKey(KeyCode.Space);
#endif
            if (touch)
            {
                if (!wasTouch) origin = pos;
                Vector2 d = pos - origin; if (d.magnitude > 12f) Dir = d.normalized;
            }
            else
            {
                Vector2 d = pos - new Vector2(Screen.width / 2f, Screen.height / 2f); if (d.magnitude > 8f) Dir = d.normalized;
            }
            wasTouch = touch; Boost = alt || UiBoost;
        }
    }
}
