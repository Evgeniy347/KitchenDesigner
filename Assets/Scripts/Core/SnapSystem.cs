using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct SnapResult
    {
        public bool snapped;
        public Vector3 position;
        public string targetName;
        public int faceIndex;
        public Vector3 snapPoint;
        public Vector3 targetPoint;
    }

    public static class SnapSystem
    {
        public static SnapResult TrySnap(KitchenElement moved, List<KitchenElement> others, Vector3 testPosition)
        {
            if (!KitchenSettings.Instance.SnapEnabled)
                return default;

            float threshold = KitchenSettings.Instance.SnapThreshold * AppConstants.MM_TO_UNITS;
            Vector3 prevPos = moved.transform.position;
            moved.transform.position = testPosition;
            KitchenElement.Face[] movedFaces = moved.GetFaces();
            moved.transform.position = prevPos;

            SnapResult best = default;
            float bestDist = float.MaxValue;

            foreach (var other in others)
            {
                if (other == moved || other == null) continue;
                if (ElementsIntersect(moved, other)) continue;

                KitchenElement.Face[] otherFaces = other.GetFaces();

                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        float dot = Vector3.Dot(movedFaces[i].normal, otherFaces[j].normal);
                        if (Mathf.Abs(dot) < 0.999f) continue;

                        Vector3 offset = otherFaces[j].center - movedFaces[i].center;
                        float planeDist = Mathf.Abs(Vector3.Dot(offset, movedFaces[i].normal));
                        if (planeDist > threshold) continue;

                        if (!FacesOverlap(movedFaces[i], otherFaces[j], out float overlapRatio))
                            continue;
                        if (overlapRatio < 0.3f) continue;

                        Vector3 snapPos = moved.transform.position + offset;
                        snapPos = GridManager.SnapToGrid(snapPos);

                        float dist = Vector3.Distance(snapPos, moved.transform.position);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = new SnapResult
                            {
                                snapped = true,
                                position = snapPos,
                                targetName = other.BoardName,
                                faceIndex = j,
                                snapPoint = movedFaces[i].center,
                                targetPoint = otherFaces[j].center
                            };
                        }
                    }
                }
            }

            return best;
        }

        private static bool FacesOverlap(KitchenElement.Face a, KitchenElement.Face b, out float overlapRatio)
        {
            Vector3 u = a.rightAxis;
            Vector3 v = a.upAxis;

            Rect aRect = GetFaceRect(a, u, v);
            Rect bRect = GetFaceRect(b, u, v);

            float interLeft = Mathf.Max(aRect.xMin, bRect.xMin);
            float interRight = Mathf.Min(aRect.xMax, bRect.xMax);
            float interBottom = Mathf.Max(aRect.yMin, bRect.yMin);
            float interTop = Mathf.Min(aRect.yMax, bRect.yMax);

            if (interLeft >= interRight || interBottom >= interTop)
            {
                overlapRatio = 0;
                return false;
            }

            float overlapArea = (interRight - interLeft) * (interTop - interBottom);
            float minArea = Mathf.Min(aRect.width * aRect.height, bRect.width * bRect.height);
            overlapRatio = minArea > 0 ? overlapArea / minArea : 0;
            return true;
        }

        private static Rect GetFaceRect(KitchenElement.Face face, Vector3 u, Vector3 v)
        {
            Vector2 center = new Vector2(
                Vector3.Dot(face.center, u),
                Vector3.Dot(face.center, v)
            );

            float halfU = Mathf.Abs(Vector3.Dot(face.rightAxis, u)) * face.size.x * 0.5f
                       + Mathf.Abs(Vector3.Dot(face.upAxis, u)) * face.size.y * 0.5f;
            float halfV = Mathf.Abs(Vector3.Dot(face.rightAxis, v)) * face.size.x * 0.5f
                       + Mathf.Abs(Vector3.Dot(face.upAxis, v)) * face.size.y * 0.5f;

            return new Rect(center.x - halfU, center.y - halfV, halfU * 2, halfV * 2);
        }

        private static bool ElementsIntersect(KitchenElement a, KitchenElement b)
        {
            Vector3[] va = a.GetVertices();
            Vector3[] vb = b.GetVertices();

            float aMinX = va[0].x, aMaxX = va[0].x;
            float aMinY = va[0].y, aMaxY = va[0].y;
            float aMinZ = va[0].z, aMaxZ = va[0].z;
            for (int i = 1; i < 8; i++)
            {
                aMinX = Mathf.Min(aMinX, va[i].x); aMaxX = Mathf.Max(aMaxX, va[i].x);
                aMinY = Mathf.Min(aMinY, va[i].y); aMaxY = Mathf.Max(aMaxY, va[i].y);
                aMinZ = Mathf.Min(aMinZ, va[i].z); aMaxZ = Mathf.Max(aMaxZ, va[i].z);
            }

            float bMinX = vb[0].x, bMaxX = vb[0].x;
            float bMinY = vb[0].y, bMaxY = vb[0].y;
            float bMinZ = vb[0].z, bMaxZ = vb[0].z;
            for (int i = 1; i < 8; i++)
            {
                bMinX = Mathf.Min(bMinX, vb[i].x); bMaxX = Mathf.Max(bMaxX, vb[i].x);
                bMinY = Mathf.Min(bMinY, vb[i].y); bMaxY = Mathf.Max(bMaxY, vb[i].y);
                bMinZ = Mathf.Min(bMinZ, vb[i].z); bMaxZ = Mathf.Max(bMaxZ, vb[i].z);
            }

            return aMinX < bMaxX && aMaxX > bMinX &&
                   aMinY < bMaxY && aMaxY > bMinY &&
                   aMinZ < bMaxZ && aMaxZ > bMinZ;
        }
    }
}
