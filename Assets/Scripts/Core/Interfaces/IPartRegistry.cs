using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public interface IPartRegistry
    {
        IReadOnlyList<KitchenElement> All { get; }
        void Register(KitchenElement element);
        void Unregister(KitchenElement element);
        List<KitchenElement> GetAll(
            [System.Runtime.CompilerServices.CallerMemberName] string? scannedBy = null,
            [System.Runtime.CompilerServices.CallerFilePath] string? scannedIn = null);
        void Clear();

        IReadOnlyList<Wall> Walls { get; }
        void RegisterWall(Wall wall);
        void UnregisterWall(Wall wall);
    }
}
