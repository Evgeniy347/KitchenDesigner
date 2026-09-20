using System;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public sealed class KeybindingCaptureGate : MonoBehaviour
    {
        private Action<KeyChordCapture.Result>? _onDone;

        public bool IsCapturing => InputMap.IsMutedBy(this);

        public void Build(Transform canvas)
        {
            enabled = false;
        }

        public void Begin(Action<KeyChordCapture.Result> onDone)
        {
            CancelIfCapturing();

            _onDone = onDone;
            enabled = true;
            InputMap.MuteSceneInput(this);
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

        private void Finish(KeyChordCapture.Result result)
        {
            var callback = _onDone;
            _onDone = null;
            StopCapturing();
            callback?.Invoke(result);
        }

        private void StopCapturing()
        {
            enabled = false;
            InputMap.UnmuteSceneInput(this);
        }

        private void OnDisable()
        {
            _onDone = null;
            StopCapturing();
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
