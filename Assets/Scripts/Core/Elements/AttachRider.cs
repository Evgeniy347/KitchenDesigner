using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class AttachRider : MonoBehaviour
    {
        private void LateUpdate() => Step();

        private static readonly Dictionary<string, List<KitchenElement>> _byParent =
            new Dictionary<string, List<KitchenElement>>();
        private static readonly List<KitchenElement> _roots = new List<KitchenElement>();

        public static void Step()
        {
            using var _ = PerfMarkers.AttachRiderStep.Auto();
            var all = PartRegistry.All;
            if (all == null || all.Count == 0) return;

            _byParent.Clear();
            bool anyLink = false;
            foreach (var e in all)
            {
                if (e == null || !AttachLinks.CanBeChild(e)) continue;
                var parentName = e.AttachedToName;
                if (string.IsNullOrEmpty(parentName)) continue;
                anyLink = true;
                if (!_byParent.TryGetValue(parentName, out var list))
                    _byParent[parentName] = list = new List<KitchenElement>();
                list.Add(e);
            }
            if (!anyLink) return;

            _roots.Clear();
            foreach (var e in all)
            {
                if (e == null || string.IsNullOrEmpty(e.PartName)) continue;
                if (!_byParent.ContainsKey(e.PartName)) continue;
                if (!string.IsNullOrEmpty(e.AttachedToName) && AttachLinks.Parent(e) != null) continue;
                _roots.Add(e);
            }
            foreach (var root in _roots) Drive(root, 0);
        }

        private const int MaxDepth = 16;

        private static void Drive(KitchenElement parent, int depth)
        {
            if (depth >= MaxDepth || parent == null) return;
            if (string.IsNullOrEmpty(parent.PartName)) return;
            if (!_byParent.TryGetValue(parent.PartName, out var children)) return;

            var restPos = AttachLinks.RestPosition(parent);
            var restRot = AttachLinks.RestRotation(parent);
            bool displaced = AttachLinks.IsDisplaced(parent);
            var delta = parent.transform.rotation * Quaternion.Inverse(restRot);

            foreach (var child in children)
            {
                if (child == null || child == parent) continue;
                if (displaced)
                {
                    if (!child.IsAttachRidden)
                        child.BeginAttachRide(child.transform.position, child.transform.rotation);
                    child.transform.SetPositionAndRotation(
                        parent.transform.position + delta * (child.AttachRestPosition - restPos),
                        delta * child.AttachRestRotation);
                }
                else if (child.IsAttachRidden)
                {
                    child.transform.SetPositionAndRotation(child.AttachRestPosition, child.AttachRestRotation);
                    child.EndAttachRide();
                }
                Drive(child, depth + 1);
            }
        }
    }
}
