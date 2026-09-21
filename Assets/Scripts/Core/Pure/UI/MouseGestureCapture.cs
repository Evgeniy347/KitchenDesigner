using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public readonly struct MousePoll
    {
        public readonly bool AnyButtonHeld;
        public readonly MouseButtonKind Pressed;
        public readonly MouseButtonKind Released;
        public readonly bool WheelMoved;
        public readonly bool PointerMoved;
        public readonly bool Ctrl;
        public readonly bool Alt;
        public readonly bool Shift;

        public MousePoll(bool anyButtonHeld, MouseButtonKind pressed, MouseButtonKind released,
            bool wheelMoved, bool pointerMoved, bool ctrl = false, bool alt = false, bool shift = false)
        {
            AnyButtonHeld = anyButtonHeld;
            Pressed = pressed;
            Released = released;
            WheelMoved = wheelMoved;
            PointerMoved = pointerMoved;
            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
        }
    }

    public sealed class MouseGestureCapture
    {
        private MouseButtonKind _held = MouseButtonKind.None;
        private bool _movedWhileHeld;
        private bool _ctrl;
        private bool _alt;
        private bool _shift;

        public bool Armed { get; private set; }

        public MouseGesture? Step(MousePoll poll)
        {
            if (!Armed)
            {
                if (poll.AnyButtonHeld) return null;
                Armed = true;
                return null;
            }

            if (_held == MouseButtonKind.None && poll.WheelMoved)
                return MouseGesture.Wheel(poll.Ctrl, poll.Alt, poll.Shift);

            if (_held == MouseButtonKind.None && poll.Pressed != MouseButtonKind.None)
            {
                _held = poll.Pressed;
                _movedWhileHeld = false;
                _ctrl = poll.Ctrl;
                _alt = poll.Alt;
                _shift = poll.Shift;
                return null;
            }

            if (_held == MouseButtonKind.None) return null;

            if (poll.PointerMoved) _movedWhileHeld = true;

            if (poll.Released != _held) return null;

            var captured = new MouseGesture(_held, _movedWhileHeld, _ctrl, _alt, _shift);
            _held = MouseButtonKind.None;
            _movedWhileHeld = false;
            return captured;
        }
    }
}
