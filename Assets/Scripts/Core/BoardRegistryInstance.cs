using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class BoardRegistryInstance : IBoardRegistry
    {
        private readonly List<KitchenElement> _all = new List<KitchenElement>();

        public IReadOnlyList<KitchenElement> All => _all;

        public void Register(KitchenElement element)
        {
            if (element != null && !_all.Contains(element))
                _all.Add(element);
        }

        public void Unregister(KitchenElement element)
        {
            _all.Remove(element);
        }

        public List<KitchenElement> GetAll()
        {
            return new List<KitchenElement>(_all);
        }

        public void Clear()
        {
            _all.Clear();
        }
    }
}
