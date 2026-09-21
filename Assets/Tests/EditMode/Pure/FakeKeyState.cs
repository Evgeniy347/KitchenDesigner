using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;

public sealed class FakeKeyState : IInputState
{
    private readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
    private readonly HashSet<KeyCode> _down = new HashSet<KeyCode>();
    private readonly HashSet<KeyCode> _up = new HashSet<KeyCode>();

    private readonly HashSet<MouseButtonKind> _buttonHeld = new HashSet<MouseButtonKind>();
    private readonly HashSet<MouseButtonKind> _buttonDown = new HashSet<MouseButtonKind>();
    private readonly HashSet<MouseButtonKind> _buttonUp = new HashSet<MouseButtonKind>();
    private bool _wheelMoved;

    public FakeKeyState Press(KeyCode key)
    {
        _held.Add(key);
        _down.Add(key);
        return this;
    }

    public FakeKeyState Hold(KeyCode key)
    {
        _held.Add(key);
        return this;
    }

    public FakeKeyState Release(KeyCode key)
    {
        _held.Remove(key);
        _up.Add(key);
        return this;
    }

    public FakeKeyState PressButton(MouseButtonKind button)
    {
        _buttonHeld.Add(button);
        _buttonDown.Add(button);
        return this;
    }

    public FakeKeyState HoldButton(MouseButtonKind button)
    {
        _buttonHeld.Add(button);
        return this;
    }

    public FakeKeyState ReleaseButton(MouseButtonKind button)
    {
        _buttonHeld.Remove(button);
        _buttonUp.Add(button);
        return this;
    }

    public FakeKeyState MoveWheel()
    {
        _wheelMoved = true;
        return this;
    }

    public bool IsKeyDown(KeyCode key) => _down.Contains(key);
    public bool IsKeyHeld(KeyCode key) => _held.Contains(key);
    public bool IsKeyUp(KeyCode key) => _up.Contains(key);

    public bool IsButtonDown(MouseButtonKind button) => _buttonDown.Contains(button);
    public bool IsButtonHeld(MouseButtonKind button) => _buttonHeld.Contains(button);
    public bool IsButtonUp(MouseButtonKind button) => _buttonUp.Contains(button);
    public bool WheelMoved => _wheelMoved;
}
