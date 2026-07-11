using UnityEngine;

namespace KitchenDesigner.Core
{
    public class InputCapture : MonoBehaviour
    {
        private bool _prevAlt;
        private bool _prevCtrl;
        private bool _prevShift;
        private bool _prevLMB;
        private bool _prevMMB;
        private bool _prevRMB;
        private bool _prevScrollNonZero;

        private static readonly KeyCode[] _watchKeys = new KeyCode[]
        {
            KeyCode.LeftAlt, KeyCode.RightAlt,
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.F, KeyCode.Escape, KeyCode.Space, KeyCode.Return,
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
            KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
        };

        private void Start()
        {
            _prevAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            _prevCtrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            _prevShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            _prevLMB = Input.GetMouseButton(0);
            _prevMMB = Input.GetMouseButton(2);
            _prevRMB = Input.GetMouseButton(1);
            Debug.Log("[Input] Start monitoring");
        }

        private void Update()
        {
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool lmb = Input.GetMouseButton(0);
            bool mmb = Input.GetMouseButton(2);
            bool rmb = Input.GetMouseButton(1);

            if (alt != _prevAlt) { Debug.Log("[Input] Alt=" + alt); _prevAlt = alt; }
            if (ctrl != _prevCtrl) { Debug.Log("[Input] Ctrl=" + ctrl); _prevCtrl = ctrl; }
            if (shift != _prevShift) { Debug.Log("[Input] Shift=" + shift); _prevShift = shift; }
            if (lmb != _prevLMB) { Debug.Log("[Input] LMB=" + lmb); _prevLMB = lmb; }
            if (mmb != _prevMMB) { Debug.Log("[Input] MMB=" + mmb); _prevMMB = mmb; }
            if (rmb != _prevRMB) { Debug.Log("[Input] RMB=" + rmb); _prevRMB = rmb; }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Debug.Log("[Input] Scroll=" + scroll.ToString("F3"));
            }

            foreach (KeyCode code in _watchKeys)
            {
                if (Input.GetKeyDown(code))
                {
                    Debug.Log("[Input] KeyDown: " + code);
                    break;
                }
            }
        }
    }
}
