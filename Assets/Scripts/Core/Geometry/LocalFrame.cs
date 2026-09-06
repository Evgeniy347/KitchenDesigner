using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class LocalFrame
    {
        public static Bounds BoundsOf(Vector3[] points, Vector3 origin, Quaternion frame)
        {
            var right = frame * Vector3.right;
            var up = frame * Vector3.up;
            var forward = frame * Vector3.forward;

            var first = points[0] - origin;
            var min = new Vector3(Vector3.Dot(first, right), Vector3.Dot(first, up),
                Vector3.Dot(first, forward));
            var max = min;

            for (int i = 1; i < points.Length; i++)
            {
                var local = points[i] - origin;
                var projected = new Vector3(Vector3.Dot(local, right), Vector3.Dot(local, up),
                    Vector3.Dot(local, forward));
                min = Vector3.Min(min, projected);
                max = Vector3.Max(max, projected);
            }

            return FromMinMax(min, max);
        }

        public static Bounds BoundsOf(in OrientedBox box, Vector3 origin, Quaternion frame)
        {
            var right = frame * Vector3.right;
            var up = frame * Vector3.up;
            var forward = frame * Vector3.forward;

            var offset = box.Center - origin;
            var center = new Vector3(Vector3.Dot(offset, right), Vector3.Dot(offset, up),
                Vector3.Dot(offset, forward));
            var extents = new Vector3(box.RadiusAlong(right), box.RadiusAlong(up),
                box.RadiusAlong(forward));

            return new Bounds(center, extents * 2f);
        }

        public static Bounds BoundsOf(List<OrientedBox> boxes, Vector3 origin, Quaternion frame)
        {
            var result = BoundsOf(boxes[0], origin, frame);
            for (int i = 1; i < boxes.Count; i++)
            {
                var next = BoundsOf(boxes[i], origin, frame);
                result.SetMinMax(Vector3.Min(result.min, next.min), Vector3.Max(result.max, next.max));
            }
            return result;
        }

        public static OrientedBox ToWorld(Bounds local, Vector3 origin, Quaternion frame) =>
            new OrientedBox(origin + frame * local.center, frame, local.extents);

        private static Bounds FromMinMax(Vector3 min, Vector3 max)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }
}
