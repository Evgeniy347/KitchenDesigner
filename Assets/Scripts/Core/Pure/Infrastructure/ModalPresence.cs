using System;

namespace KitchenDesigner.Core
{
    public static class ModalPresence
    {
        public static Func<bool>? Probe { get; set; }

        public static bool IsOpen => Probe != null && Probe();
    }
}
