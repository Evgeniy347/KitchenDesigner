using System;

namespace KitchenDesigner.Core
{
    [Flags]
    public enum ElementKind
    {
        None = 0,
        Anchor = 1 << 0,
        FloorAnchor = 1 << 1,
        Opening = 1 << 2,
        Drawer = 1 << 3,
        Decor = 1 << 4,
        Recessed = 1 << 5,
        FloatingFacade = 1 << 6,
        Facade = 1 << 7,
        ScrewLeg = 1 << 8,
    }
}
