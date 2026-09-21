using System;

namespace KitchenDesigner.Core.Keybinding
{
    public readonly struct InputBinding : IEquatable<InputBinding>
    {
        public static readonly InputBinding Empty = default;

        private readonly bool _isGesture;
        private readonly KeyChord _key;
        private readonly MouseGesture _gesture;

        private InputBinding(bool isGesture, KeyChord key, MouseGesture gesture)
        {
            _isGesture = isGesture;
            _key = key;
            _gesture = gesture;
        }

        public static InputBinding FromKey(KeyChord chord) =>
            new InputBinding(false, chord, MouseGesture.Empty);

        public static InputBinding FromGesture(MouseGesture gesture) =>
            new InputBinding(true, KeyChord.Empty, gesture);

        public bool IsGesture => _isGesture && !_gesture.IsEmpty;

        public bool IsKey => !_isGesture && !_key.IsEmpty;

        public bool IsEmpty => _isGesture ? _gesture.IsEmpty : _key.IsEmpty;

        public KeyChord Key => _key;

        public MouseGesture Gesture => _gesture;

        public bool Equals(InputBinding other)
        {
            if (IsEmpty && other.IsEmpty) return true;
            if (_isGesture != other._isGesture) return false;
            return _isGesture ? _gesture.Equals(other._gesture) : _key.Equals(other._key);
        }

        public override bool Equals(object? obj) => obj is InputBinding other && Equals(other);

        public override int GetHashCode()
        {
            if (IsEmpty) return 0;
            int hash = _isGesture ? 1 : 2;
            hash = hash * 31 + (_isGesture ? _gesture.GetHashCode() : _key.GetHashCode());
            return hash;
        }

        public static bool operator ==(InputBinding a, InputBinding b) => a.Equals(b);
        public static bool operator !=(InputBinding a, InputBinding b) => !a.Equals(b);

        public static string Format(InputBinding binding)
        {
            if (binding.IsEmpty) return "";
            return binding._isGesture ? MouseGesture.Format(binding._gesture) : KeyChord.Format(binding._key);
        }

        public static bool TryParse(string? text, out InputBinding binding)
        {
            binding = Empty;
            if (string.IsNullOrEmpty(text)) return true;

            if (MouseGesture.TryParse(text, out var gesture) && !gesture.IsEmpty)
            {
                binding = FromGesture(gesture);
                return true;
            }

            if (KeyChord.TryParse(text, out var chord) && !chord.IsEmpty)
            {
                binding = FromKey(chord);
                return true;
            }

            return false;
        }

        public static InputBinding Parse(string? text) => TryParse(text, out var binding) ? binding : Empty;
    }
}
