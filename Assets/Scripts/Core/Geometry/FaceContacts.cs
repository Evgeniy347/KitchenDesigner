using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class FaceContacts
    {
        public static bool AreInFaceToFaceContact(Face[] facesA, Face[] facesB, float contactDist)
        {
            foreach (var _ in new FaceContactScan(facesA, facesB, FaceAlignment.ParallelEitherWay,
                         FaceContactScan.NoLowerGapBound, contactDist, Tolerance.MinSupportOverlap))
                return true;
            return false;
        }

        public static float MinParallelGap(Face[] fa, Face[] fb, float contactDist, float maxGap)
        {
            float best = 0f;
            foreach (var hit in new FaceContactScan(fa, fb, FaceAlignment.ParallelEitherWay,
                         contactDist, maxGap, Tolerance.MinSupportOverlap))
            {
                if (best == 0f || hit.PlaneGap < best) best = hit.PlaneGap;
            }
            return best;
        }

        public static float SumParallelGaps(Face[] fa, Face[] fb, float contactDist, float maxGap)
        {
            float sum = 0f;
            foreach (var hit in new FaceContactScan(fa, fb, FaceAlignment.FacingEachOther,
                         contactDist, maxGap, Tolerance.MinSupportOverlap))
            {
                sum += hit.PlaneGap;
            }
            return sum;
        }

        public static bool FacesOverlap(Face a, Face b, out float overlapArea, out float overlapRatio)
        {
            Vector3 u = a.rightAxis;
            Vector3 v = a.upAxis;

            Rect aRect = FaceRects.Of(a, u, v);
            Rect bRect = FaceRects.Of(b, u, v);
            FaceRects.SignedOverlap(aRect, bRect, out float overlapU, out float overlapV);

            if (overlapU <= 0f || overlapV <= 0f)
            {
                overlapArea = 0;
                overlapRatio = 0;
                return false;
            }

            overlapArea = overlapU * overlapV;
            overlapRatio =
                FaceRects.RatioOfSmallerSide(overlapU, Mathf.Min(aRect.width, bRect.width)) *
                FaceRects.RatioOfSmallerSide(overlapV, Mathf.Min(aRect.height, bRect.height));
            return true;
        }

        public static bool OverlapAllowingEdgeTouch(Face a, Face b,
            out float overlapRatio, out bool hasLineContact)
        {
            overlapRatio = 0;
            hasLineContact = false;
            Vector3 u = a.rightAxis;
            Vector3 v = a.upAxis;

            Rect aRect = FaceRects.Of(a, u, v);
            Rect bRect = FaceRects.Of(b, u, v);
            FaceRects.SignedOverlap(aRect, bRect, out float signedU, out float signedV);

            if (signedU < -Tolerance.SnapEpsilon || signedV < -Tolerance.SnapEpsilon) return false;

            float overlapU = Mathf.Max(0, signedU);
            float overlapV = Mathf.Max(0, signedV);
            float minW = Mathf.Min(aRect.width, bRect.width);
            float minH = Mathf.Min(aRect.height, bRect.height);

            float ratioU = FaceRects.RatioOfSmallerSide(overlapU, minW);
            float ratioV = FaceRects.RatioOfSmallerSide(overlapV, minH);

            bool lineU = overlapU <= Tolerance.SnapEpsilon && minW > 0;
            bool lineV = overlapV <= Tolerance.SnapEpsilon && minH > 0;
            if (lineU) ratioU = 1.0f;
            if (lineV) ratioV = 1.0f;
            hasLineContact = lineU || lineV;

            overlapRatio = ratioU * ratioV;
            return true;
        }

        public static bool AABBsIntersect(in ElementGeometry a, in ElementGeometry b, float margin) =>
            Tolerance.IntervalsOverlap(a.Min.x, a.Max.x, b.Min.x, b.Max.x, margin) &&
            Tolerance.IntervalsOverlap(a.Min.y, a.Max.y, b.Min.y, b.Max.y, margin) &&
            Tolerance.IntervalsOverlap(a.Min.z, a.Max.z, b.Min.z, b.Max.z, margin);

        public static int FaceIndexByNormal(Face[] faces, Vector3 normal)
        {
            int best = 0;
            float bestDot = float.MinValue;
            for (int i = 0; i < faces.Length; i++)
            {
                float d = Vector3.Dot(faces[i].normal, normal);
                if (d > bestDot) { bestDot = d; best = i; }
            }
            return best;
        }
    }
}
