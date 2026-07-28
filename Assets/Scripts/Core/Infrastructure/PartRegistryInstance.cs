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
            if (element == null || _all.Contains(element)) return;
            _all.Add(element);
            SceneRevision.Bump();
            SceneVisibilityManager.Invalidate();
        }

        public void Unregister(KitchenElement element)
        {
            if (_all.Remove(element)) { SceneRevision.Bump(); SceneVisibilityManager.Invalidate(); }
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
            if (_all.Count == 0) return;
            _all.Clear();
            SceneRevision.Bump();
        }
    }
}
