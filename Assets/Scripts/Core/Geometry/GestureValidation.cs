using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class GestureValidation
    {
        private readonly List<ValidationElement> _posedAtFreeze = new List<ValidationElement>();
        private readonly List<int> _movers = new List<int>();
        private FrozenValidation? _frozen;
        private bool _refused;

        public int Freezes { get; private set; }

        public int MoverCount => _movers.Count;

        public int PairsInLastFrame => _frozen?.PairsInLastPass ?? 0;

        public bool IsFrozen => _frozen != null;

        public void Reset()
        {
            _frozen = null;
            _refused = false;
            _movers.Clear();
            _posedAtFreeze.Clear();
        }

        public bool TryValidate(IReadOnlyList<ValidationElement> all, CoreValidationResult result)
        {
            if (all == null || all.Count == 0) return false;
            if (_frozen != null && _frozen.Count != all.Count) Reset();
            if (_refused) return false;

            if (_frozen == null)
            {
                if (!Freeze(all)) return false;
            }
            else if (TakeDriftersAsMovers(all) && !Freeze(all))
            {
                return false;
            }

            _frozen!.Revalidate(all, result);
            return true;
        }

        private bool Freeze(IReadOnlyList<ValidationElement> all)
        {
            _frozen = FrozenValidation.Freeze(all, _movers);
            if (_frozen == null)
            {
                _refused = true;
                _posedAtFreeze.Clear();
                return false;
            }

            _posedAtFreeze.Clear();
            for (int i = 0; i < all.Count; i++) _posedAtFreeze.Add(all[i]);
            Freezes++;
            return true;
        }

        private bool TakeDriftersAsMovers(IReadOnlyList<ValidationElement> all)
        {
            bool drifted = false;
            for (int i = 0; i < all.Count; i++)
            {
                if (_frozen!.IsMover(i)) continue;
                if (all[i].PosedLike(_posedAtFreeze[i])) continue;
                _movers.Add(i);
                drifted = true;
            }
            return drifted;
        }
    }
}
