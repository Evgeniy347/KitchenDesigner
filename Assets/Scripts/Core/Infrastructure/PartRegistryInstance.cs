using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class PartRegistryInstance : IPartRegistry
    {
        private readonly List<KitchenElement> _all = new List<KitchenElement>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int _getAllCalls;

        /// <summary>Сколько раз вызвали <see cref="GetAll"/> с прошлого опроса, и сброс.
        /// Каждый вызов — копия всего списка, так что счётчик показывает масштаб
        /// покадрового перебора сцены.</summary>
        public static int TakeGetAllCalls()
        {
            int n = _getAllCalls;
            _getAllCalls = 0;
            return n;
        }
#endif

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
            using var _ = PerfMarkers.PartRegistryGetAll.Auto();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _getAllCalls++;
#endif
            return new List<KitchenElement>(_all);
        }

        public void Clear()
        {
            _all.Clear();
        }
    }
}
