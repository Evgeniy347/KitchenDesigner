using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SceneChangeTracker : MonoBehaviour
    {
        private static bool _membershipChanged;
        private static readonly HashSet<KitchenElement> _selfAnimated = new HashSet<KitchenElement>();

        private void LateUpdate() => Poll();

        public static void NoteMembershipChanged() => _membershipChanged = true;

        public static void NoteSelfAnimated(KitchenElement element)
        {
            if (element != null) _selfAnimated.Add(element);
        }

        public static void Poll()
        {
            using var _ = PerfMarkers.SceneChangeTrackerPoll.Auto();

            var all = PartRegistry.All;
            bool any = _membershipChanged;
            bool hostSetChanged = _membershipChanged;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null) continue;

                var t = e.transform;
                if (!t.hasChanged) continue;
                if (_selfAnimated.Contains(e)) continue;

                t.hasChanged = false;
                e.BumpPoseVersion();
                if (e.SupportsGrooves) hostSetChanged = true;
                any = true;
            }

            _selfAnimated.Clear();

            if (hostSetChanged) WakeCutoutGuests();
            if (any) SettleDerivedLinks();
        }

        public static void WakeCutoutGuests()
        {
            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] is PartCutoutElement guest && guest != null)
                    guest.WakeForSceneChange();
        }

        public static void SettleDerivedLinks()
        {
            using var _ = PerfMarkers.SettleDerivedLinks.Auto();

            _membershipChanged = false;
            ScrewLegHostLink.ApplyAll(PartRegistry.All);
            Analysis.PipeFittingSizeLink.ApplyAll(PartRegistry.All);
            WakeWhoeverParkedAtAGestureLimit();
            SceneRevision.Bump();
        }

        private static void WakeWhoeverParkedAtAGestureLimit()
        {
            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e != null && e is IParksAtAGestureLimit parked && parked.IsParkedAtALimit)
                    e.enabled = true;
            }
        }
    }
}
