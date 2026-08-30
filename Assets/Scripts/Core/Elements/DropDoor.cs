using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class DropDoor
    {
        public const float OPEN_ANGLE_DEG = 90f;
        public const float OPEN_SECONDS = 0.4f;
        public const float KEEP_AWAKE_MARGIN_SECONDS = 0.2f;

        private readonly ApplianceBoxes _boxes;
        private readonly int _firstChild;
        private readonly Func<(Vector3 centerMM, Vector3 sizeMM)[]> _closedParts;
        private readonly Func<Vector3> _hingeLocalMM;

        private float _progress;

        public DropDoor(ApplianceBoxes boxes, int firstChild,
            Func<(Vector3 centerMM, Vector3 sizeMM)[]> closedParts, Func<Vector3> hingeLocalMM)
        {
            _boxes = boxes;
            _firstChild = firstChild;
            _closedParts = closedParts;
            _hingeLocalMM = hingeLocalMM;
        }

        public float Progress => _progress;

        public bool IsAnimatingTowards(bool open) => !Mathf.Approximately(_progress, open ? 1f : 0f);

        public static Quaternion LocalRotation(float progress) =>
            Quaternion.AngleAxis(OPEN_ANGLE_DEG * Mathf.Clamp01(progress), Vector3.right);

        public bool ForceClose()
        {
            if (_progress <= 0f) return false;
            _progress = 0f;
            return true;
        }

        public bool Step(float dt, bool open, Func<float>? maxSafeProgress)
        {
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(_progress, target)) return false;

            float step = OPEN_SECONDS > 0f ? dt / OPEN_SECONDS : 1f;
            _progress = Mathf.MoveTowards(_progress, target, step);

            if (open && _progress > 0f && maxSafeProgress != null)
            {
                float safe = maxSafeProgress();
                if (safe < _progress) _progress = Mathf.Max(_progress - step, safe);
            }
            return true;
        }

        public Quaternion ApplyPose()
        {
            var rotation = LocalRotation(_progress);
            var parts = _closedParts();
            var hingeMM = _hingeLocalMM();

            for (int i = 0; i < parts.Length; i++)
            {
                var closedMM = parts[i].centerMM;
                var centerMM = hingeMM + rotation * (closedMM - hingeMM);
                _boxes.Place(_firstChild + i, centerMM, parts[i].sizeMM, rotation);
            }
            return rotation;
        }

        public Vector3 HingeLocalUnits => _hingeLocalMM() * AppConstants.MM_TO_UNITS;

        public (Vector3 min, Vector3 max) WorldBounds(Transform root, float progress)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var hinge = _hingeLocalMM() * toU;
            var localRot = LocalRotation(progress);
            var parts = _closedParts();

            var world = new Vector3[parts.Length * 8];
            int n = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                var closed = parts[i].centerMM * toU;
                var localPos = hinge + localRot * (closed - hinge);
                var worldPos = root.TransformPoint(localPos);
                var worldRot = root.rotation * localRot;
                var half = parts[i].sizeMM * toU * 0.5f;
                for (int c = 0; c < 8; c++)
                    world[n++] = worldPos + worldRot * new Vector3(
                        (c & 1) == 0 ? -half.x : half.x,
                        (c & 2) == 0 ? -half.y : half.y,
                        (c & 4) == 0 ? -half.z : half.z);
            }
            return OpeningCollision.MinMax(world);
        }

        public static (Vector3 min, Vector3 max) RiderBounds(
            Transform root, Quaternion doorRotation, Vector3 hingeLocalUnits,
            Vector3 closedWorldPosition, Quaternion closedWorldRotation, Vector3 halfExtents)
        {
            var (worldPos, worldRot) = RiderPose(
                root, doorRotation, hingeLocalUnits, closedWorldPosition, closedWorldRotation);

            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = worldPos + worldRot * new Vector3(
                    (i & 1) == 0 ? -halfExtents.x : halfExtents.x,
                    (i & 2) == 0 ? -halfExtents.y : halfExtents.y,
                    (i & 4) == 0 ? -halfExtents.z : halfExtents.z);
            return OpeningCollision.MinMax(corners);
        }

        public static (Vector3 position, Quaternion rotation) RiderPose(
            Transform root, Quaternion doorRotation, Vector3 hingeLocalUnits,
            Vector3 closedWorldPosition, Quaternion closedWorldRotation)
        {
            var local = root.InverseTransformPoint(closedWorldPosition);
            var localRot = Quaternion.Inverse(root.rotation) * closedWorldRotation;

            var rotatedLocal = hingeLocalUnits + doorRotation * (local - hingeLocalUnits);
            return (root.TransformPoint(rotatedLocal), root.rotation * (doorRotation * localRot));
        }
    }
}
