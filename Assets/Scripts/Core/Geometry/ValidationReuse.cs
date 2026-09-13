using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class ValidationReuse
    {
        private readonly List<ValidationElement> _kept = new List<ValidationElement>();

        private bool _holdsAnAnswer;

        public int FullValidations { get; private set; }

        public int TakeFullValidations()
        {
            int n = FullValidations;
            FullValidations = 0;
            return n;
        }

        public bool NeedsAFreshAnswer(IReadOnlyList<ValidationElement>? all)
        {
            if (AnswerStillStands(all)) return false;

            _kept.Clear();
            if (all != null)
                for (int i = 0; i < all.Count; i++) _kept.Add(all[i]);
            _holdsAnAnswer = all != null;
            FullValidations++;
            return true;
        }

        private bool AnswerStillStands(IReadOnlyList<ValidationElement>? all)
        {
            if (!_holdsAnAnswer || all == null || all.Count != _kept.Count) return false;
            for (int i = 0; i < _kept.Count; i++)
                if (!all[i].ValidatesTheSameAs(_kept[i])) return false;
            return true;
        }
    }
}
