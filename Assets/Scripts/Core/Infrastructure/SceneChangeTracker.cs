using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SceneChangeTracker : MonoBehaviour
    {
        private static bool _membershipChanged;

        private void LateUpdate() => Poll();

        public static void NoteMembershipChanged() => _membershipChanged = true;

        public static void Poll()
        {
            bool any = _membershipChanged;
            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null) continue;

                var t = e.transform;
                if (!t.hasChanged) continue;

                t.hasChanged = false;
                e.BumpPoseVersion();
                any = true;
            }

            if (any) SettleDerivedLinks();
        }

        public static void SettleDerivedLinks()
        {
            _membershipChanged = false;
            ScrewLegHostLink.ApplyAll(PartRegistry.All);
            SceneRevision.Bump();
        }
    }
}
