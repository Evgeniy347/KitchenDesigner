using System;

namespace KitchenDesigner.Core.UI
{
    [Flags]
    internal enum ElementFacet
    {
        None = 0,
        Facade = 1 << 0,
        Assembled = 1 << 1,
        Radial = 1 << 2,
        Drawer = 1 << 3,
        Table = 1 << 4,
        Pillar = 1 << 5,
        Window = 1 << 6,
        Door = 1 << 7,
        Part = 1 << 8,
        Light = 1 << 9,
        Oven = 1 << 10,
        Dishwasher = 1 << 11,
        Stool = 1 << 12,
        Chair = 1 << 13,
        Sofa = 1 << 14,
        ScrewLeg = 1 << 15,
        Bed = 1 << 16,
        Pouffe = 1 << 17,
        Pipe = 1 << 18,
        PipeFitting = 1 << 19,
        PipeFittingSecondPort = 1 << 20,
        PipeFittingThirdPort = 1 << 21,
        LaundryMachine = 1 << 22,
    }
}
