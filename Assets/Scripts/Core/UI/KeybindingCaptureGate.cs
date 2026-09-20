using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public sealed class KeybindingCaptureGate : MonoBehaviour
    {
        private TMP_InputField? _focusGuard;
        private Action<KeyChordCapture.Result>? _onDone;

        public bool IsCapturing { get; private set; }

        public void Build(Transform canvas)
        {
            var field = UIFactory.CreateInputField(
                "KeybindingCaptureGuard", canvas, string.Empty, Vector2.zero, Vector2.zero);
            field.readOnly = true;
            field.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            _focusGuard = field;
        }

        public void Begin(Action<KeyChordCapture.Result> onDone)
        {
            if (_focusGuard == null) return;
            var guard = _focusGuard;
            _onDone = onDone;
            IsCapturing = true;
            EventSystem.current?.SetSelectedGameObject(guard.gameObject);
        }

        public void CancelIfCapturing()
        {
            if (!IsCapturing) return;
            Finish(KeyChordCapture.Result.Cancelled);
        }

        private void OnGUI()
        {
            if (!IsCapturing) return;
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Escape)
            {
                e.Use();
                Finish(KeyChordCapture.Result.Cancelled);
                return;
            }

            var chord = KeyCaptureResolution.Resolve(e.keyCode, e.control, e.alt, e.shift);
            if (chord == null)
            {
                e.Use();
                return;
            }

            e.Use();
            Finish(KeyChordCapture.Result.Captured(chord.Value));
        }

        private void Finish(KeyChordCapture.Result result)
        {
            IsCapturing = false;
            if (EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == _focusGuard!.gameObject)
                EventSystem.current.SetSelectedGameObject(null);

            var callback = _onDone;
            _onDone = null;
            callback?.Invoke(result);
        }

        private void OnDisable() => CancelIfCapturing();
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
