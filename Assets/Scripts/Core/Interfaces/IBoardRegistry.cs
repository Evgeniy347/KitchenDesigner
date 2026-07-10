using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public interface IBoardRegistry
    {
        IReadOnlyList<KitchenElement> All { get; }
        void Register(KitchenElement element);
        void Unregister(KitchenElement element);
        List<KitchenElement> GetAll();
        void Clear();
    }
}
