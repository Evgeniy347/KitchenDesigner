using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public sealed class KeybindingCaptureGate : MonoBehaviour
    {
        public const string GuardName = "KeybindingCaptureGuard";

        private Transform? _canvas;
        private TMP_InputField? _focusGuard;
        private Action<KeyChordCapture.Result>? _onDone;

        public bool IsCapturing => _focusGuard != null;

        public void Build(Transform canvas)
        {
            _canvas = canvas;
            enabled = false;
        }

        public void Begin(Action<KeyChordCapture.Result> onDone)
        {
            if (_canvas == null) return;
            var canvas = _canvas;
            CancelIfCapturing();

            var field = UIFactory.CreateInputField(
                GuardName, canvas, string.Empty, Vector2.zero, Vector2.zero);
            field.readOnly = true;
            field.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

            _focusGuard = field;
            _onDone = onDone;
            enabled = true;
            EventSystem.current?.SetSelectedGameObject(field.gameObject);
        }

        public void CancelIfCapturing()
        {
            if (!IsCapturing) return;
            Finish(KeyChordCapture.Result.Cancelled);
        }

        internal bool Read(KeyCode key, bool ctrl, bool alt, bool shift)
        {
            if (!IsCapturing) return false;

            if (key == KeyCode.Escape)
            {
                Finish(KeyChordCapture.Result.Cancelled);
                return true;
            }

            var chord = KeyCaptureResolution.Resolve(key, ctrl, alt, shift);
            if (chord == null) return true;

            Finish(KeyChordCapture.Result.Captured(chord.Value));
            return true;
        }

        private void OnGUI()
        {
            if (!IsCapturing) return;
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (Read(e.keyCode, e.control, e.alt, e.shift)) e.Use();
        }

        private void Update()
        {
            if (!IsCapturing) return;
            var es = EventSystem.current;
            if (es == null) return;
            if (es.currentSelectedGameObject == _focusGuard!.gameObject) return;
            CancelIfCapturing();
        }

        private void Finish(KeyChordCapture.Result result)
        {
            var callback = _onDone;
            _onDone = null;
            DropTheFocusGuard();
            callback?.Invoke(result);
        }

        private void DropTheFocusGuard()
        {
            var guard = _focusGuard;
            _focusGuard = null;
            if (guard == null) return;
            enabled = false;

            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == guard.gameObject)
                es.SetSelectedGameObject(null);

            DestroyNow.The(guard.gameObject);
        }

        private void OnDisable()
        {
            _onDone = null;
            DropTheFocusGuard();
        }
    }

    public static class KeyChordCapture
    {
        public readonly struct Result
        {
            public readonly bool WasCancelled;
            public readonly KeyChord Chord;

            private Result(bool cancelled, KeyChord chord)
            {
                WasCancelled = cancelled;
                Chord = chord;
            }

            public static Result Cancelled => new Result(true, KeyChord.Empty);
            public static Result Captured(KeyChord chord) => new Result(false, chord);
        }
    }
}
