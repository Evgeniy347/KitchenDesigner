using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SceneMembership
    {
        public static void Leave(GameObject? go, KitchenElement? element)
        {
            if (go == null) return;
            if (element != null && element is ICutsItsHost guest) guest.ReleaseHostCutout();
            go.SetActive(false);
            if (element != null) PartRegistry.Unregister(element);
        }

        public static void Return(GameObject? go, KitchenElement? element)
        {
            if (go == null) return;
            go.SetActive(true);
            if (element != null) PartRegistry.Register(element);
            if (element != null && element is ICutsItsHost guest) guest.RestoreHostCutout();
        }
    }
}
