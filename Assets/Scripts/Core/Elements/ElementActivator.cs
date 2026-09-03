using System.Collections.Generic;
using KitchenDesigner.Core.Lighting;

namespace KitchenDesigner.Core
{
    public static class ElementActivator
    {
        public static ActivationKind KindOf(object? target)
        {
            if (target is ILightSwitch) return ActivationKind.LightSwitch;
            if (target is IOpenable) return ActivationKind.Openable;
            return ActivationKind.None;
        }

        public static bool Activate(object? target)
        {
            switch (KindOf(target))
            {
                case ActivationKind.LightSwitch:
                    var source = (ILightSwitch)target!;
                    SwitchPower.Set(source, !source.IsOn);
                    return true;
                case ActivationKind.Openable:
                    ((IOpenable)target!).CycleOpenState();
                    return true;
                default:
                    return false;
            }
        }

        public static int ActivateAll(IReadOnlyList<KitchenElement> elements)
        {
            if (elements == null) return 0;
            int count = 0;
            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (element == null) continue;
                if (!ActivationRules.RespondsToHotkey(KindOf(element))) continue;
                if (Activate(element)) count++;
            }
            return count;
        }
    }
}
