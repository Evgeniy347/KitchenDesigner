using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SceneChangeTracker : MonoBehaviour
    {
        private void LateUpdate() => Poll();

        public static void Poll()
        {
            bool any = false;
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
            ScrewLegHostLink.ApplyAll(PartRegistry.All);
            SceneRevision.Bump();
        }
    }
}
