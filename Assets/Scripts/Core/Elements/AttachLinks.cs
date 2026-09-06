using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class AttachLinks
    {
        public static bool CanBeChild(KitchenElement? e) =>
            e != null && e.CanFollowAnAttachParent && !(e is IPartCutout)
            && e.GetComponent<Wall>() == null && e.GetComponent<BasePlate>() == null;

        public static bool CanChooseParent(KitchenElement? e) =>
            CanBeChild(e) && !e!.AttachIsDerived;

        public static bool CanBeParent(KitchenElement? e) =>
            e != null && e.CanCarryAttachedParts && !(e is IPartCutout)
            && e.GetComponent<Wall>() == null && e.GetComponent<BasePlate>() == null;

        public static KitchenElement? Parent(KitchenElement? e)
        {
            if (e == null || string.IsNullOrEmpty(e.AttachedToName)) return null;
            foreach (var other in PartRegistry.All)
                if (other != null && other != e && other.PartName == e.AttachedToName)
                    return CanBeParent(other) ? other : null;
            return null;
        }

        public static void Children(KitchenElement? parent, List<KitchenElement> into)
        {
            if (parent == null || string.IsNullOrEmpty(parent.PartName)) return;
            foreach (var e in PartRegistry.All)
                if (e != null && e != parent && CanBeChild(e) && e.AttachedToName == parent.PartName)
                    into.Add(e);
        }

        public static List<KitchenElement> Children(KitchenElement? parent)
        {
            var list = new List<KitchenElement>();
            Children(parent, list);
            return list;
        }

        public static void Descendants(KitchenElement? root, List<KitchenElement> into)
        {
            if (root == null) return;
            var seen = new HashSet<KitchenElement> { root };
            var queue = new Queue<KitchenElement>();
            queue.Enqueue(root);
            var buffer = new List<KitchenElement>();
            while (queue.Count > 0)
            {
                buffer.Clear();
                Children(queue.Dequeue(), buffer);
                foreach (var child in buffer)
                {
                    if (!seen.Add(child)) continue;
                    into.Add(child);
                    queue.Enqueue(child);
                }
            }
        }

        public static List<KitchenElement> Descendants(KitchenElement? root)
        {
            var list = new List<KitchenElement>();
            Descendants(root, list);
            return list;
        }

        public static bool WouldCycle(KitchenElement child, KitchenElement parent)
        {
            if (child == null || parent == null) return false;
            if (child == parent) return true;
            foreach (var d in Descendants(child))
                if (d == parent) return true;
            return false;
        }

        public static bool CanAttach(KitchenElement? child, KitchenElement? parent) =>
            CanBeChild(child) && CanBeParent(parent) && !WouldCycle(child!, parent!);

        public static bool InContact(KitchenElement? child, KitchenElement? parent)
        {
            if (child == null || parent == null) return false;
            return ConstraintValidator.AreInFaceToFaceContact(child, parent);
        }

        public static bool IsDetached(KitchenElement? child)
        {
            var parent = Parent(child);
            return parent != null && !InContact(child, parent);
        }

        public static Vector3 RestPosition(KitchenElement e) => e.AttachRestPosition;

        public static Quaternion RestRotation(KitchenElement e) => e.AttachRestRotation;

        public static void ForceRest(KitchenElement? e)
        {
            var parent = Parent(e);
            for (int guard = 0; parent != null && guard < 16; guard++)
            {
                if (parent is IOpenable openable) openable.ForceClose();
                parent = Parent(parent);
            }
            AttachRider.Step();
        }

        public static bool IsDisplaced(KitchenElement e)
        {
            if (e == null) return false;
            var restPos = RestPosition(e);
            var restRot = RestRotation(e);
            return (e.transform.position - restPos).sqrMagnitude > Tolerance.EpsilonSqr
                || Quaternion.Angle(e.transform.rotation, restRot) > 0.01f;
        }
    }
}
