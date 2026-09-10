using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ElementFacing
    {
        public static readonly Vector3 LocalFront = Vector3.forward;

        public const float QuarterTurnDeg = 90f;

        public const int QuarterTurnsInFullTurn = 4;

        private const float DegenerateDirectionSqr = 1e-8f;

        public static float YawTowardsCameraDeg(Vector3 cameraDirection)
        {
            var horizontal = new Vector3(cameraDirection.x, 0f, cameraDirection.z);
            if (horizontal.sqrMagnitude < DegenerateDirectionSqr) return 0f;

            float bestYaw = 0f;
            float bestDot = float.NegativeInfinity;
            for (int turn = 0; turn < QuarterTurnsInFullTurn; turn++)
            {
                float yaw = turn * QuarterTurnDeg;
                float dot = Vector3.Dot(TurnedAroundVertical(LocalFront, yaw), horizontal);
                if (dot <= bestDot) continue;
                bestDot = dot;
                bestYaw = yaw;
            }

            return bestYaw;
        }

        public static Vector3 CameraDirection(Vector3 isoDirection) =>
            TurnedAroundVertical(isoDirection, -YawTowardsCameraDeg(isoDirection));

        public static Vector3 TurnedAroundVertical(Vector3 direction, float yawDeg)
        {
            float rad = yawDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector3(direction.x * cos + direction.z * sin, direction.y,
                direction.z * cos - direction.x * sin);
        }
    }
}
