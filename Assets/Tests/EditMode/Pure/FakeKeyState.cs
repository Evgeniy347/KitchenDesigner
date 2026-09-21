using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;

internal sealed class FakeKeyState : IKeyState
{
    private readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
    private readonly HashSet<KeyCode> _down = new HashSet<KeyCode>();
    private readonly HashSet<KeyCode> _up = new HashSet<KeyCode>();

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

    public bool IsKeyDown(KeyCode key) => _down.Contains(key);
    public bool IsKeyHeld(KeyCode key) => _held.Contains(key);
    public bool IsKeyUp(KeyCode key) => _up.Contains(key);
}
