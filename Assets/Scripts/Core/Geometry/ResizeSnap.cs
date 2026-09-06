using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ResizeSnap
    {
        private const float ThresholdEpsilon = Tolerance.SnapEpsilon;

        public static bool SnapDelta(
            Vector3 faceCenter, Vector3 normal, Vector3 uAxis, Vector3 vAxis, Vector2 faceSize,
            IReadOnlyList<ElementGeometry> others, in ElementGeometry self, float threshold, out float gap)
        {
            gap = 0f;
            if (others == null) return false;

            Rect mRect = RectFor(faceCenter, uAxis, vAxis, uAxis, vAxis, faceSize.x, faceSize.y);
            float bestAbs = float.MaxValue;
            bool found = false;

            foreach (var o in others)
            {
                if (o.IsEmpty || ReferenceEquals(o.Faces, self.Faces)) continue;

                var faces = o.Faces;
                for (int j = 0; j < faces.Length; j++)
                {
                    var g = faces[j];
                    if (Mathf.Abs(Vector3.Dot(g.normal, normal)) < Tolerance.ParallelDot) continue;

                    float d = Vector3.Dot(g.center - faceCenter, normal);
                    if (Mathf.Abs(d) > threshold + ThresholdEpsilon) continue;

                    Rect oRect = RectFor(g.center, uAxis, vAxis, g.rightAxis, g.upAxis, g.size.x, g.size.y);
                    if (!Overlap(mRect, oRect)) continue;

                    if (Mathf.Abs(d) < bestAbs)
                    {
                        bestAbs = Mathf.Abs(d);
                        gap = d;
                        found = true;
                    }
                }

                if (self.IsPanel)
                {
                    foreach (var seat in o.GrooveSeatFaces)
                    {
                        if (Vector3.Dot(seat.normal, normal) > -Tolerance.ParallelDot) continue;
                        float d = Vector3.Dot(seat.center - faceCenter, normal);
                        if (Mathf.Abs(d) > threshold + ThresholdEpsilon) continue;

                        Rect sRect = RectFor(seat.center, uAxis, vAxis, seat.rightAxis, seat.upAxis,
                            seat.size.x, seat.size.y);
                        if (!Overlap(mRect, sRect)) continue;

                        if (Mathf.Abs(d) < bestAbs)
                        {
                            bestAbs = Mathf.Abs(d);
                            gap = d;
                            found = true;
                        }
                    }
                }

                foreach (var wall in o.GrooveWallFaces)
                {
                    if (Mathf.Abs(Vector3.Dot(wall.normal, normal)) < Tolerance.ParallelDot) continue;

                    float d = Vector3.Dot(wall.center - faceCenter, normal);
                    if (Mathf.Abs(d) > threshold + ThresholdEpsilon) continue;
                    if (!OverlapsAlongGroove(faceCenter, uAxis, vAxis, faceSize, wall)) continue;

                    if (Mathf.Abs(d) < bestAbs)
                    {
                        bestAbs = Mathf.Abs(d);
                        gap = d;
                        found = true;
                    }
                }
            }
            return found;
        }

        private static bool OverlapsAlongGroove(Vector3 faceCenter, Vector3 uAxis, Vector3 vAxis,
            Vector2 faceSize, Face wall)
        {
            Vector3 lengthAxis = wall.rightAxis;
            float faceHalf = Mathf.Abs(Vector3.Dot(uAxis, lengthAxis)) * faceSize.x * 0.5f
                           + Mathf.Abs(Vector3.Dot(vAxis, lengthAxis)) * faceSize.y * 0.5f;
            float wallHalf = wall.size.x * 0.5f;
            float centreGap = Mathf.Abs(Vector3.Dot(wall.center - faceCenter, lengthAxis));
            return centreGap < faceHalf + wallHalf;
        }

        private static Rect RectFor(Vector3 center, Vector3 u, Vector3 v,
            Vector3 rightAxis, Vector3 upAxis, float sizeX, float sizeY)
        {
            var face = new Face(center, Vector3.zero, new Vector2(sizeX, sizeY), rightAxis, upAxis);
            return FaceRects.Of(face, u, v);
        }

        private static bool Overlap(Rect a, Rect b)
        {
            float left = Mathf.Max(a.xMin, b.xMin);
            float right = Mathf.Min(a.xMax, b.xMax);
            float bottom = Mathf.Max(a.yMin, b.yMin);
            float top = Mathf.Min(a.yMax, b.yMax);
            return left <= right + Tolerance.SnapEpsilon
                && bottom <= top + Tolerance.SnapEpsilon;
        }
    }
}
