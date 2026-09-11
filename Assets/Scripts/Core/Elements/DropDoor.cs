using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class DropDoor
    {
        public const float OPEN_ANGLE_DEG = 90f;
        public const float OPEN_SECONDS = AppConstants.OPENING_ANIM_DURATION_SECONDS;
        public const float KEEP_AWAKE_MARGIN_SECONDS = AppConstants.OPENING_KEEP_AWAKE_MARGIN_SECONDS;

        public static readonly Vector3 DROP_HINGE_AXIS = Vector3.right;

        private readonly ApplianceBoxes _boxes;
        private readonly int _firstChild;
        private readonly Func<(Vector3 centerMM, Vector3 sizeMM)[]> _closedParts;
        private readonly Func<Vector3> _hingeLocalMM;
        private readonly Vector3 _hingeAxis;

        private float _progress;

        public DropDoor(ApplianceBoxes boxes, int firstChild,
            Func<(Vector3 centerMM, Vector3 sizeMM)[]> closedParts, Func<Vector3> hingeLocalMM)
            : this(boxes, firstChild, closedParts, hingeLocalMM, DROP_HINGE_AXIS)
        {
        }

        public DropDoor(ApplianceBoxes boxes, int firstChild,
            Func<(Vector3 centerMM, Vector3 sizeMM)[]> closedParts, Func<Vector3> hingeLocalMM,
            Vector3 hingeAxis)
        {
            _boxes = boxes;
            _firstChild = firstChild;
            _closedParts = closedParts;
            _hingeLocalMM = hingeLocalMM;
            _hingeAxis = hingeAxis;
        }

        public float Progress => _progress;

        public Vector3 HingeAxis => _hingeAxis;

        public bool IsAnimatingTowards(bool open) => !Mathf.Approximately(_progress, open ? 1f : 0f);

        public static Quaternion RotationAbout(Vector3 hingeAxis, float progress) =>
            Quaternion.AngleAxis(OPEN_ANGLE_DEG * Mathf.Clamp01(progress), hingeAxis);

        public static Quaternion LocalRotation(float progress) =>
            RotationAbout(DROP_HINGE_AXIS, progress);

        public Quaternion LocalRotationAt(float progress) => RotationAbout(_hingeAxis, progress);

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
            var rotation = LocalRotationAt(_progress);
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

        public void WorldBoxes(Transform root, float progress, List<OrientedBox> into)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var hinge = _hingeLocalMM() * toU;
            var localRot = LocalRotationAt(progress);
            var parts = _closedParts();

            for (int i = 0; i < parts.Length; i++)
            {
                var closed = parts[i].centerMM * toU;
                var localPos = hinge + localRot * (closed - hinge);
                into.Add(new OrientedBox(root.TransformPoint(localPos), root.rotation * localRot,
                    parts[i].sizeMM * toU * 0.5f));
            }
        }

        public static OrientedBox RiderBox(
            Transform root, Quaternion doorRotation, Vector3 hingeLocalUnits,
            Vector3 closedWorldPosition, Quaternion closedWorldRotation, Vector3 halfExtents)
        {
            var (worldPos, worldRot) = RiderPose(
                root, doorRotation, hingeLocalUnits, closedWorldPosition, closedWorldRotation);

            return new OrientedBox(worldPos, worldRot, halfExtents);
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
