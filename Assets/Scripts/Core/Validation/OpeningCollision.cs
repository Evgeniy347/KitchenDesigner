using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public delegate void OpenBoxes(float progress, List<OrientedBox> into);

    public readonly struct OpeningObstacle
    {
        public readonly KitchenElement Owner;
        public readonly OrientedBox Box;

        public OpeningObstacle(KitchenElement owner, OrientedBox box)
        {
            Owner = owner;
            Box = box;
        }
    }

    public static class OpeningCollision
    {
        internal const int ScanSteps = 128;

        internal const float TouchGapMm = 5f;

        public const float MinBlockingPenetrationMm = 1f;

        public static float FindMaxProgress(
            KitchenElement self,
            OpenBoxes getBoxes,
            List<KitchenElement>? exclude = null,
            float precision = 0.001f)
        {
            var moving = new List<OrientedBox>();
            getBoxes(0f, moving);
            if (moving.Count == 0) return 1f;

            var origin = moving[0].Center;
            var frame = moving[0].Rotation;
            var closed = LocalFrame.BoundsOf(moving, origin, frame);

            var others = new List<KitchenElement>();
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == self) continue;
                if (exclude != null && exclude.Contains(el)) continue;
                others.Add(el);
            }

            var obstacles = new List<OpeningObstacle>();
            BuildObstacles(closed, origin, frame, others, obstacles);
            if (obstacles.Count == 0) return 1f;

            float prevFree = 0f, hit = -1f;
            for (int i = 1; i <= ScanSteps; i++)
            {
                float t = i / (float)ScanSteps;
                if (Blocked(getBoxes, moving, obstacles, t)) { hit = t; break; }
                prevFree = t;
            }
            if (hit < 0f) return 1f;

            float lo = prevFree, hi = hit;
            int steps = Mathf.CeilToInt(Mathf.Log((hi - lo) / precision, 2f));
            for (int iter = 0; iter < steps; iter++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Blocked(getBoxes, moving, obstacles, mid))
                    hi = mid;
                else
                    lo = mid;
            }
            return lo;
        }

        public static void BuildObstacles(
            Bounds closed, Vector3 origin, Quaternion frame,
            IReadOnlyList<KitchenElement> others,
            List<OpeningObstacle> into)
        {
            float touchGap = TouchGapMm * AppConstants.MM_TO_UNITS;
            var pieces = new List<Bounds>();

            for (int i = 0; i < others.Count; i++)
            {
                var el = others[i];
                if (el == null) continue;

                pieces.Clear();
                ContactShadow.ActivePieces(closed,
                    LocalFrame.BoundsOf(el.GetVertices(), origin, frame), touchGap, pieces);

                foreach (var piece in pieces)
                    into.Add(new OpeningObstacle(el, LocalFrame.ToWorld(piece, origin, frame)));
            }
        }

        public static float Penetration(in OrientedBox moving, in OrientedBox obstacle) =>
            BoxOverlap.PenetrationUnits(moving, obstacle) / AppConstants.MM_TO_UNITS;

        public static bool Blocks(in OrientedBox moving, in OrientedBox obstacle, out float penetrationMm)
        {
            penetrationMm = Penetration(moving, obstacle);
            return penetrationMm > MinBlockingPenetrationMm;
        }

        private static bool Blocked(
            OpenBoxes getBoxes, List<OrientedBox> buffer,
            List<OpeningObstacle> obstacles, float progress)
        {
            buffer.Clear();
            getBoxes(progress, buffer);
            foreach (var box in buffer)
                foreach (var obstacle in obstacles)
                    if (Blocks(box, obstacle.Box, out _))
                        return true;

            return false;
        }
    }
}
