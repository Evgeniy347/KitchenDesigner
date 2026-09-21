namespace KitchenDesigner.Core.Keybinding
{
    public interface IMouseState
    {
        bool IsButtonDown(MouseButtonKind button);
        bool IsButtonHeld(MouseButtonKind button);
        bool IsButtonUp(MouseButtonKind button);
        bool WheelMoved { get; }
    }
}
