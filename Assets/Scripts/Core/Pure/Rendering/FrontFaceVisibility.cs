using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct FacePart
    {
        public readonly string Name;
        public readonly Bounds Box;

        public FacePart(string name, Bounds box)
        {
            Name = name;
            Box = box;
        }
    }

    public static class FrontFaceVisibility
    {
        private const float ClearanceUnits = 1e-4f;

        private const float AxisParallelEpsilon = 1e-9f;

        public const string MissingPartSuffix = " — такой детали в сцене нет";

        public static bool IsVisible(IReadOnlyList<FacePart> parts, int index, Vector3 viewDirection)
        {
            var view = viewDirection.normalized;
            var target = parts[index].Box;
            Vector3 towardsPart = -view;
            Vector3 origin = target.center + view * StandOffFor(parts);
            if (!EntryDistance(target, origin, towardsPart, out float ownDistance)) return false;

            float targetDepth = Vector3.Dot(target.center, view);
            for (int i = 0; i < parts.Count; i++)
            {
                if (i == index) continue;
                if (FrontmostDepth(parts[i].Box, view) <= targetDepth) continue;
                if (!EntryDistance(parts[i].Box, origin, towardsPart, out float otherDistance))
                    continue;
                if (otherDistance < ownDistance - ClearanceUnits) return false;
            }

            return true;
        }

        public static List<string> Hidden(IReadOnlyList<FacePart> parts,
            IReadOnlyList<string> declaredPartNames, Vector3 viewDirection)
        {
            var hidden = new List<string>();
            foreach (var declared in declaredPartNames)
            {
                bool found = false;
                for (int i = 0; i < parts.Count; i++)
                {
                    if (!parts[i].Name.StartsWith(declared, StringComparison.Ordinal)) continue;
                    found = true;
                    if (!IsVisible(parts, i, viewDirection)) hidden.Add(parts[i].Name);
                }
                if (!found) hidden.Add(declared + MissingPartSuffix);
            }
            return hidden;
        }

        private static float FrontmostDepth(Bounds box, Vector3 view)
        {
            var e = box.extents;
            return Vector3.Dot(box.center, view)
                + Mathf.Abs(e.x * view.x) + Mathf.Abs(e.y * view.y) + Mathf.Abs(e.z * view.z);
        }

        private static bool EntryDistance(Bounds box, Vector3 origin, Vector3 direction,
            out float distance)
        {
            float near = float.NegativeInfinity;
            float far = float.PositiveInfinity;
            var min = box.min;
            var max = box.max;

            for (int axis = 0; axis < 3; axis++)
            {
                float d = direction[axis];
                float o = origin[axis];
                if (Mathf.Abs(d) < AxisParallelEpsilon)
                {
                    if (o < min[axis] || o > max[axis])
                    {
                        distance = 0f;
                        return false;
                    }
                    continue;
                }

                float t1 = (min[axis] - o) / d;
                float t2 = (max[axis] - o) / d;
                if (t1 > t2) (t1, t2) = (t2, t1);
                if (t1 > near) near = t1;
                if (t2 < far) far = t2;
            }

            distance = Mathf.Max(near, 0f);
            return near <= far && far >= 0f;
        }

        private static float StandOffFor(IReadOnlyList<FacePart> parts)
        {
            var whole = parts[0].Box;
            for (int i = 1; i < parts.Count; i++) whole.Encapsulate(parts[i].Box);
            return whole.size.magnitude + 1f;
        }
    }
}
