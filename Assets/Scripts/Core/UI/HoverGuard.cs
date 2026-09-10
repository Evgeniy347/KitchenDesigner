using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class HoverGuard : MonoBehaviour
    {
        public System.Action? Clear;

        public static HoverGuard Attach(GameObject target, System.Action clear)
        {
            var self = target.GetComponent<HoverGuard>() ?? target.AddComponent<HoverGuard>();
            self.Clear = clear;
            return self;
        }

        private void OnDisable() => Clear?.Invoke();
    }
}
