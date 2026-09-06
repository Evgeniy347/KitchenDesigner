using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class ScrewLegHostLink
    {
        private static readonly List<ElementGeometry> _candidates = new List<ElementGeometry>();
        private static readonly List<KitchenElement> _owners = new List<KitchenElement>();

        public static string Derive(ScrewLegElement leg, IReadOnlyList<KitchenElement> scene)
        {
            if (leg == null || scene == null) return "";

            _candidates.Clear();
            _owners.Clear();
            for (int i = 0; i < scene.Count; i++)
            {
                var e = scene[i];
                if (e == null || ReferenceEquals(e, leg) || string.IsNullOrEmpty(e.PartName)) continue;
                if (!CanHost(leg, e)) continue;
                _owners.Add(e);
                _candidates.Add(e.ToGeometry());
            }

            int host = ScrewLegHosting.HostIndex(leg.ThreadBody, _candidates,
                Tolerance.ContactMm * AppConstants.MM_TO_UNITS);
            return host == ScrewLegHosting.NoHost ? "" : _owners[host].PartName;
        }

        public static bool Apply(ScrewLegElement leg, IReadOnlyList<KitchenElement> scene)
        {
            if (leg == null) return false;
            var derived = Derive(leg, scene);
            if (derived == leg.AttachedToName) return false;
            leg.AttachedToName = derived;
            return true;
        }

        public static int ApplyAll(IReadOnlyList<KitchenElement> scene)
        {
            if (scene == null) return 0;
            int changed = 0;
            for (int i = 0; i < scene.Count; i++)
                if (scene[i] is ScrewLegElement leg && Apply(leg, scene)) changed++;
            return changed;
        }

        private static bool CanHost(ScrewLegElement leg, KitchenElement candidate) =>
            !(candidate is FloorElement)
            && AttachLinks.CanBeChild(leg) && AttachLinks.CanBeParent(candidate);
    }
}
