using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class EdgeDetents
    {
        public const string FreeLabel = "своб";

        public static List<float> GrooveWallCoordsAlong(Face[] wallFaces, Vector3 axis)
        {
            var coords = new List<float>();
            foreach (var w in wallFaces)
            {
                if (Mathf.Abs(Vector3.Dot(w.normal, axis)) < Tolerance.ParallelDot) continue;
                float c = Vector3.Dot(w.center, axis);
                if (!coords.Contains(c)) coords.Add(c);
            }
            return coords;
        }

        public const string CentreLabel = "центр";

        public static float NearestDetentDelta(float aMin, float aMax, float bMin, float bMax,
            float threshold, List<float>? grooveCoords, out string label) =>
            NearestDetentDelta(aMin, aMax, bMin, bMax, threshold, grooveCoords, false, out label);

        public static float NearestDetentDelta(float aMin, float aMax, float bMin, float bMax,
            float threshold, List<float>? grooveCoords, bool preferCentre, out string label)
        {
            float aCenter = (aMin + aMax) * 0.5f;
            float bCenter = (bMin + bMax) * 0.5f;

            if (preferCentre && Mathf.Abs(bCenter - aCenter) <= threshold)
            {
                label = CentreLabel;
                return bCenter - aCenter;
            }

            var detents = new List<(float d, string name)>
            {
                (bMin - aMin, "кромка-"),
                (bMax - aMax, "кромка+"),
                (bCenter - aCenter, CentreLabel),
            };

            if (grooveCoords != null)
            {
                foreach (float g in grooveCoords)
                {
                    detents.Add((g - aMin, "паз-"));
                    detents.Add((g - aMax, "паз+"));
                }
            }

            float best = 0f;
            float bestAbs = float.MaxValue;
            label = FreeLabel;
            foreach (var c in detents)
            {
                float abs = Mathf.Abs(c.d);
                if (abs <= threshold && abs < bestAbs)
                {
                    bestAbs = abs;
                    best = c.d;
                    label = c.name;
                }
            }
            return best;
        }
    }
}
