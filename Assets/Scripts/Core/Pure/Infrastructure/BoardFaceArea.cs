using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BoardFaceArea
    {
        public static float FaceAreaM2(Vector3Int dimsMM)
        {
            int a = dimsMM.x, b = dimsMM.y, c = dimsMM.z;
            int thickness = Mathf.Min(a, Mathf.Min(b, c));
            int largest = Mathf.Max(a, Mathf.Max(b, c));
            int mid = a + b + c - thickness - largest;
            float wM = mid * AppConstants.MM_TO_UNITS;
            float hM = largest * AppConstants.MM_TO_UNITS;
            return wM * hM;
        }
    }
}
