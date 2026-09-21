using System;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public sealed class KeybindingGestureGate : MonoBehaviour
    {
        public const string BlockerName = "KeybindingGestureBlocker";
        public const float MoveEpsilonPx = 3f;
        public const float WheelEpsilon = 0.01f;

        private static readonly MouseButtonKind[] ButtonByUnityIndex =
        {
            MouseButtonKind.Left,
            MouseButtonKind.Right,
            MouseButtonKind.Middle,
            MouseButtonKind.XButton1,
            MouseButtonKind.XButton2,
        };

        private Transform? _canvas;
        private GameObject? _blocker;
        private MouseGestureCapture? _capture;
        private Action<GestureCapture.Result>? _onDone;
        private Vector3 _lastPointer;

        public bool IsCapturing => _blocker != null;

        public void Build(Transform canvas)
        {
            _canvas = canvas;
            enabled = false;
        }

        public void Begin(Action<GestureCapture.Result> onDone)
        {
            if (_canvas == null) return;
            var canvas = _canvas;
            CancelIfCapturing();

            var blocker = UIFactory.CreateRect(BlockerName, canvas);
            blocker.anchorMin = Vector2.zero;
            blocker.anchorMax = Vector2.one;
            blocker.offsetMin = blocker.offsetMax = Vector2.zero;
            var catcher = blocker.gameObject.AddComponent<Image>();
            catcher.color = UIStyle.RaycastOnly;
            catcher.raycastTarget = true;
            blocker.SetAsLastSibling();

            _blocker = blocker.gameObject;
            _capture = new MouseGestureCapture();
            _onDone = onDone;
            _lastPointer = Input.mousePosition;
            enabled = true;
        }

        public void CancelIfCapturing()
        {
            if (!IsCapturing) return;
            Finish(GestureCapture.Result.Cancelled);
        }

        private void Update()
        {
            if (!IsCapturing) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Finish(GestureCapture.Result.Cancelled);
                return;
            }

            var gesture = _capture!.Step(Poll());
            if (gesture != null) Finish(GestureCapture.Result.Captured(gesture.Value));
        }

        private MousePoll Poll()
        {
            bool anyHeld = false;
            var pressed = MouseButtonKind.None;
            var released = MouseButtonKind.None;

            for (int i = 0; i < ButtonByUnityIndex.Length; i++)
            {
                if (Input.GetMouseButton(i)) anyHeld = true;
                if (Input.GetMouseButtonDown(i) && pressed == MouseButtonKind.None)
                    pressed = ButtonByUnityIndex[i];
                if (Input.GetMouseButtonUp(i) && released == MouseButtonKind.None)
                    released = ButtonByUnityIndex[i];
            }

            var pointer = Input.mousePosition;
            bool moved = (pointer - _lastPointer).sqrMagnitude > MoveEpsilonPx * MoveEpsilonPx;
            _lastPointer = pointer;

            return new MousePoll(
                anyHeld, pressed, released,
                wheelMoved: Mathf.Abs(Input.mouseScrollDelta.y) > WheelEpsilon,
                pointerMoved: moved,
                ctrl: Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                alt: Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt),
                shift: Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        }

        private void Finish(GestureCapture.Result result)
        {
            var callback = _onDone;
            _onDone = null;
            _capture = null;
            DropTheBlocker();
            callback?.Invoke(result);
        }

        private void DropTheBlocker()
        {
            var blocker = _blocker;
            _blocker = null;
            if (blocker == null) return;
            enabled = false;

            DestroyNow.The(blocker);
        }

        private void OnDisable()
        {
            _onDone = null;
            _capture = null;
            DropTheBlocker();
        }
    }

    public static class GestureCapture
    {
        public readonly struct Result
        {
            public readonly bool WasCancelled;
            public readonly MouseGesture Gesture;

            private Result(bool cancelled, MouseGesture gesture)
            {
                WasCancelled = cancelled;
                Gesture = gesture;
            }

            public static Result Cancelled => new Result(true, MouseGesture.Empty);

            public static Result Captured(MouseGesture gesture) => new Result(false, gesture);
        }
    }
}
