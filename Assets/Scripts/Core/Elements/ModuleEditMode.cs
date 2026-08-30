using System;

namespace KitchenDesigner.Core
{
    public static class ModuleEditMode
    {
        public static LinkGroup? Active { get; private set; }

        public static bool IsActive => Active != null;

        public static event Action? Changed;

        public static void Enter(LinkGroup module)
        {
            if (module == null || Active == module) return;
            Active = module;
            Changed?.Invoke();
        }

        public static void Exit()
        {
            if (Active == null) return;
            Active = null;
            Changed?.Invoke();
        }

        public static bool IsEditable(KitchenElement e)
        {
            if (!IsActive) return true;
            return e != null && e.GroupId == Active!.id;
        }
    }
}
