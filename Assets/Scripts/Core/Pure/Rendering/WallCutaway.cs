using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class WallCutaway
    {
        public static bool ShouldLower(Vector3 wallCenter, Vector3 sceneCenter, Vector3 cameraForward)
        {
            Vector3 offsetFromSceneCenter = FlattenToGround(wallCenter - sceneCenter);
            Vector3 viewDirection = FlattenToGround(cameraForward);

            if (viewDirection.sqrMagnitude < 1e-6f || offsetFromSceneCenter.sqrMagnitude < 1e-8f)
                return false;

            float alignmentWithViewDirection =
                Vector3.Dot(offsetFromSceneCenter.normalized, viewDirection.normalized);
            return alignmentWithViewDirection < -0.1f;
        }

        private static Vector3 FlattenToGround(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
