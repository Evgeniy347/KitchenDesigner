using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class ScrewLegHostLink
    {
        private static readonly List<ElementGeometry> _candidates = new List<ElementGeometry>();
        private static readonly List<KitchenElement> _owners = new List<KitchenElement>();

        private static readonly List<ElementGeometry> _batchCandidates = new List<ElementGeometry>();
        private static readonly List<KitchenElement> _batchOwners = new List<KitchenElement>();
        private static readonly List<ScrewLegElement> _batchLegs = new List<ScrewLegElement>();

        internal static int HostGeometriesBuiltByLastApplyAll { get; private set; }

        public static string Derive(ScrewLegElement leg, IReadOnlyList<KitchenElement> scene)
        {
            if (leg == null || scene == null) return "";

            CollectHosts(scene, _owners, _candidates);
            return HostNameFor(leg, _owners, _candidates);
        }

        public static bool Apply(ScrewLegElement leg, IReadOnlyList<KitchenElement> scene)
        {
            if (leg == null) return false;
            return Take(leg, Derive(leg, scene));
        }

        public static int ApplyAll(IReadOnlyList<KitchenElement> scene)
        {
            if (scene == null) return 0;

            using var _ = PerfMarkers.ScrewLegHostLinkApplyAll.Auto();

            HostGeometriesBuiltByLastApplyAll = 0;
            _batchLegs.Clear();
            _batchOwners.Clear();
            _batchCandidates.Clear();
            for (int i = 0; i < scene.Count; i++)
                if (scene[i] is ScrewLegElement leg) _batchLegs.Add(leg);
            if (_batchLegs.Count == 0) return 0;

            CollectHosts(scene, _batchOwners, _batchCandidates);
            HostGeometriesBuiltByLastApplyAll = _batchCandidates.Count;

            int changed = 0;
            for (int i = 0; i < _batchLegs.Count; i++)
                if (Take(_batchLegs[i], HostNameFor(_batchLegs[i], _batchOwners, _batchCandidates))) changed++;

            _batchLegs.Clear();
            _batchOwners.Clear();
            _batchCandidates.Clear();
            return changed;
        }

        private static void CollectHosts(IReadOnlyList<KitchenElement> scene,
            List<KitchenElement> owners, List<ElementGeometry> candidates)
        {
            owners.Clear();
            candidates.Clear();
            for (int i = 0; i < scene.Count; i++)
            {
                var e = scene[i];
                if (e == null || string.IsNullOrEmpty(e.PartName)) continue;
                if (!CanHost(e)) continue;
                owners.Add(e);
                candidates.Add(e.ToGeometry());
            }
        }

        private static string HostNameFor(ScrewLegElement leg,
            List<KitchenElement> owners, List<ElementGeometry> candidates)
        {
            if (!AttachLinks.CanBeChild(leg)) return "";
            int host = ScrewLegHosting.HostIndex(leg.ThreadBody, candidates,
                Tolerance.ContactMm * AppConstants.MM_TO_UNITS);
            return host == ScrewLegHosting.NoHost ? "" : owners[host].PartName;
        }

        private static bool Take(ScrewLegElement leg, string derived)
        {
            if (derived == leg.AttachedToName) return false;
            leg.AttachedToName = derived;
            return true;
        }

        private static bool CanHost(KitchenElement candidate) =>
            !(candidate is FloorElement) && AttachLinks.CanBeParent(candidate);
    }
}
