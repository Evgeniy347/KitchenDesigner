using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    [RequireComponent(typeof(Button))]
    public class ConfirmDeleteButton : MonoBehaviour, IPointerDownHandler
    {
        private static ConfirmDeleteButton? _theOnlyArmedOne;

        private System.Action? _onConfirm;
        private TMP_Text? _label;
        private string _idleText = UIStyle.GlyphClose;
        private int _armedFrame = -1;
        private int _pointerDownFrame = -1;

        public bool Armed { get; private set; }

        public static ConfirmDeleteButton Attach(Button button, System.Action onConfirm)
        {
            var confirm = button.gameObject.AddComponent<ConfirmDeleteButton>();
            confirm._onConfirm = onConfirm;
            confirm._label = button.GetComponentInChildren<TMP_Text>();
            if (confirm._label != null) confirm._idleText = confirm._label.text;
            button.onClick.AddListener(confirm.OnlyTheSecondClickConfirms);
            return confirm;
        }

        private void OnlyTheSecondClickConfirms()
        {
            if (!Armed) { Arm(); return; }
            Disarm();
            _onConfirm?.Invoke();
        }

        private void Arm()
        {
            if (_theOnlyArmedOne != null && _theOnlyArmedOne != this) _theOnlyArmedOne.Disarm();
            _theOnlyArmedOne = this;
            Armed = true;
            _armedFrame = Time.frameCount;
            if (_label != null) _label.text = UIStyle.GlyphConfirm;
        }

        public static void DisarmAll() => _theOnlyArmedOne?.Disarm();

        public void Disarm()
        {
            if (_theOnlyArmedOne == this) _theOnlyArmedOne = null;
            if (!Armed) return;
            Armed = false;
            _armedFrame = -1;
            if (_label != null) _label.text = _idleText;
        }

        public void OnPointerDown(PointerEventData eventData) => _pointerDownFrame = Time.frameCount;

        public static bool ShouldDisarm(int frame, int armedFrame, int pointerDownFrame,
            bool mouseDown, bool cancelled) =>
            frame != armedFrame
            && (cancelled || (mouseDown && frame != pointerDownFrame));

        private void LateUpdate()
        {
            if (!Armed) return;
            bool cancelled = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);
            if (ShouldDisarm(Time.frameCount, _armedFrame, _pointerDownFrame,
                    Input.GetMouseButtonDown(0), cancelled))
                Disarm();
        }

        private void OnDisable() => Disarm();
    }
}
