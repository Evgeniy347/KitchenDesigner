using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class AttachMove
    {
        public static void Follow(Vector3 childPos, Quaternion childRot,
            Vector3 posBefore, Quaternion rotBefore, Vector3 posAfter, Quaternion rotAfter,
            out Vector3 newPos, out Quaternion newRot)
        {
            var delta = rotAfter * Quaternion.Inverse(rotBefore);
            newPos = posAfter + delta * (childPos - posBefore);
            newRot = delta * childRot;
        }

        public static void ExpandWithDescendants(List<KitchenElement> set)
        {
            if (set == null || set.Count == 0) return;
            var seen = new HashSet<KitchenElement>(set);
            var buffer = new List<KitchenElement>();
            for (int i = 0; i < set.Count; i++)
            {
                buffer.Clear();
                AttachLinks.Descendants(set[i], buffer);
                foreach (var d in buffer)
                    if (d != null && seen.Add(d)) set.Add(d);
            }
        }

        public static void AppendFollowers(List<IUndoCommand> commands, KitchenElement root,
            Vector3 posBefore, Quaternion rotBefore, Vector3 posAfter, Quaternion rotAfter,
            ICollection<KitchenElement>? skip = null)
        {
            if (commands == null || root == null) return;
            if ((posAfter - posBefore).sqrMagnitude <= Tolerance.EpsilonSqr
                && Quaternion.Angle(rotBefore, rotAfter) <= 0.01f) return;

            foreach (var child in AttachLinks.Descendants(root))
            {
                if (child == null || (skip != null && skip.Contains(child))) continue;
                var childPos = child.AttachRestPosition;
                var childRot = child.AttachRestRotation;
                Follow(childPos, childRot, posBefore, rotBefore, posAfter, rotAfter,
                    out var newPos, out var newRot);
                commands.Add(new MoveCommand(child, childPos, newPos, childRot, newRot));
            }
        }

        public static IUndoCommand? FollowersCommand(KitchenElement root,
            Vector3 posBefore, Quaternion rotBefore, Vector3 posAfter, Quaternion rotAfter)
        {
            var cmds = new List<IUndoCommand>();
            AppendFollowers(cmds, root, posBefore, rotBefore, posAfter, rotAfter);
            if (cmds.Count == 0) return null;
            return cmds.Count == 1 ? cmds[0] : new CompositeCommand("Move attached", cmds);
        }
    }
}
