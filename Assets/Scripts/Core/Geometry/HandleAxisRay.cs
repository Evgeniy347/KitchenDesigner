using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    public static class HandleAxisRay
    {
        public static float ParamAlongAxis(Vector3 rayOrigin, Vector3 rayDirection,
            Vector3 axisPoint, Vector3 axisDirection)
        {
            Vector3 lineDir = axisDirection.normalized;
            Vector3 rayDir = rayDirection.normalized;
            float b = Vector3.Dot(lineDir, rayDir);
            float denom = 1f - b * b;
            bool lookingAlongTheAxis = Mathf.Abs(denom) < Tolerance.EpsilonUnits;
            if (lookingAlongTheAxis) return float.NaN;

            Vector3 w0 = axisPoint - rayOrigin;
            float dW = Vector3.Dot(lineDir, w0);
            float eW = Vector3.Dot(rayDir, w0);
            return (b * eW - dW) / denom;
        }
    }
}
