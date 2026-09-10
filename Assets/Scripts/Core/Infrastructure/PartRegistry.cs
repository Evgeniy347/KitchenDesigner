using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class PartRegistry
    {
        internal static IPartRegistry Instance
        {
            get
            {
                if (GameContext.Services != null)
                    return GameContext.Services.PartRegistry;
                if (_fallback == null)
                    _fallback = new PartRegistryInstance();
                return _fallback!;
            }
            set => _fallback = value;
        }
        private static IPartRegistry? _fallback;

        public static IReadOnlyList<KitchenElement> All => Instance.All;

        public static void Register(KitchenElement element) => Instance.Register(element);

        public static void Unregister(KitchenElement element) => Instance.Unregister(element);

        public static List<KitchenElement> GetAll() => Instance.GetAll();

        public static void Clear() => Instance.Clear();

        public static IReadOnlyList<Wall> Walls => Instance.Walls;

        public static void RegisterWall(Wall wall) => Instance.RegisterWall(wall);

        public static void UnregisterWall(Wall wall) => Instance.UnregisterWall(wall);
    }
}
