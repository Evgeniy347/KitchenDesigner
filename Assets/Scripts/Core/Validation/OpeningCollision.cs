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

    public sealed class OpeningScanMemory
    {
        private OrientedBox[] _moving = System.Array.Empty<OrientedBox>();
        private OrientedBox[] _obstacles = System.Array.Empty<OrientedBox>();
        private float _answer = 1f;
        private bool _known;

        public float Answer => _known ? _answer : 1f;

        public void Forget()
        {
            _known = false;
            _moving = System.Array.Empty<OrientedBox>();
            _obstacles = System.Array.Empty<OrientedBox>();
            _answer = 1f;
        }

        public bool Matches(List<OrientedBox> moving, List<OpeningObstacle> obstacles)
        {
            if (!_known) return false;
            if (_moving.Length != moving.Count || _obstacles.Length != obstacles.Count) return false;
            for (int i = 0; i < _moving.Length; i++)
                if (!SameBox(_moving[i], moving[i])) return false;
            for (int i = 0; i < _obstacles.Length; i++)
                if (!SameBox(_obstacles[i], obstacles[i].Box)) return false;
            return true;
        }

        public void Keep(List<OrientedBox> moving, List<OpeningObstacle> obstacles, float answer)
        {
            if (_moving.Length != moving.Count) _moving = new OrientedBox[moving.Count];
            for (int i = 0; i < _moving.Length; i++) _moving[i] = moving[i];

            if (_obstacles.Length != obstacles.Count) _obstacles = new OrientedBox[obstacles.Count];
            for (int i = 0; i < _obstacles.Length; i++) _obstacles[i] = obstacles[i].Box;

            _answer = answer;
            _known = true;
        }

        private static bool SameBox(in OrientedBox a, in OrientedBox b) =>
            SamePoint(a.Center, b.Center) && SameTurn(a.Rotation, b.Rotation)
            && SamePoint(a.Half, b.Half);

        private static bool SamePoint(Vector3 a, Vector3 b) =>
            a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z);

        private static bool SameTurn(Quaternion a, Quaternion b) =>
            a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z) && a.w.Equals(b.w);
    }

    public static class OpeningCollision
    {
        internal const int ScanSteps = 128;

        internal const float TouchGapMm = 5f;

        public const float MinBlockingPenetrationMm = 1f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int _buildObstacleCalls;
        private static int _scanForBlockCalls;

        public static int ObstaclesInLastScan { get; private set; }

        public static int TakeBuildObstacleCalls()
        {
            int n = _buildObstacleCalls;
            _buildObstacleCalls = 0;
            return n;
        }

        public static int TakeScanForBlockCalls()
        {
            int n = _scanForBlockCalls;
            _scanForBlockCalls = 0;
            return n;
        }
#endif

        public static float FindMaxProgress(
            KitchenElement self,
            OpenBoxes getBoxes,
            List<KitchenElement>? exclude = null,
            float precision = 0.001f,
            OpeningScanMemory? memory = null)
        {
            using var _ = PerfMarkers.OpeningFindMaxProgress.Auto();

            var moving = new List<OrientedBox>();
            getBoxes(0f, moving);
            if (moving.Count == 0) return Free(memory);

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
            KeepOnlyWhatTheSweepCanReach(getBoxes, obstacles);
            if (obstacles.Count == 0) return Free(memory);

            if (memory != null && memory.Matches(moving, obstacles)) return memory.Answer;

            float answer = ScanForBlock(getBoxes, new List<OrientedBox>(moving.Count),
                obstacles, precision);
            memory?.Keep(moving, obstacles, answer);
            return answer;
        }

        public static void KeepOnlyWhatTheSweepCanReach(
            OpenBoxes getBoxes, List<OpeningObstacle> obstacles)
        {
            if (obstacles.Count == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ObstaclesInLastScan = 0;
#endif
                return;
            }

            var reach = SweptWorldBounds(getBoxes);
            int kept = 0;
            for (int i = 0; i < obstacles.Count; i++)
                if (reach.Intersects(WorldAabbOf(obstacles[i].Box)))
                    obstacles[kept++] = obstacles[i];

            obstacles.RemoveRange(kept, obstacles.Count - kept);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ObstaclesInLastScan = kept;
#endif
        }

        private static Bounds SweptWorldBounds(OpenBoxes getBoxes)
        {
            var buffer = new List<OrientedBox>();
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i <= ScanSteps; i++)
            {
                buffer.Clear();
                getBoxes(i / (float)ScanSteps, buffer);
                for (int b = 0; b < buffer.Count; b++)
                {
                    var aabb = WorldAabbOf(buffer[b]);
                    min = Vector3.Min(min, aabb.min);
                    max = Vector3.Max(max, aabb.max);
                }
            }

            if (min.x > max.x) return new Bounds(Vector3.zero, Vector3.zero);

            float pad = TouchGapMm * AppConstants.MM_TO_UNITS;
            var padding = new Vector3(pad, pad, pad);
            var bounds = new Bounds();
            bounds.SetMinMax(min - padding, max + padding);
            return bounds;
        }

        private static Bounds WorldAabbOf(in OrientedBox box) =>
            new Bounds(box.Center, 2f * new Vector3(
                box.RadiusAlong(Vector3.right),
                box.RadiusAlong(Vector3.up),
                box.RadiusAlong(Vector3.forward)));

        private static float Free(OpeningScanMemory? memory)
        {
            memory?.Forget();
            return 1f;
        }

        private static float ScanForBlock(
            OpenBoxes getBoxes, List<OrientedBox> buffer,
            List<OpeningObstacle> obstacles, float precision)
        {
            using var __ = PerfMarkers.OpeningScanForBlock.Auto();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _scanForBlockCalls++;
#endif

            float prevFree = 0f, hit = -1f;
            for (int i = 1; i <= ScanSteps; i++)
            {
                float t = i / (float)ScanSteps;
                if (Blocked(getBoxes, buffer, obstacles, t)) { hit = t; break; }
                prevFree = t;
            }
            if (hit < 0f) return 1f;

            float lo = prevFree, hi = hit;
            int steps = Mathf.CeilToInt(Mathf.Log((hi - lo) / precision, 2f));
            for (int iter = 0; iter < steps; iter++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Blocked(getBoxes, buffer, obstacles, mid))
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
            using var _ = PerfMarkers.OpeningBuildObstacles.Auto();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _buildObstacleCalls++;
#endif

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
