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

        public static float CentreDelta(float aMin, float aMax, float bMin, float bMax,
            float threshold)
        {
            float delta = (bMin + bMax - aMin - aMax) * 0.5f;
            return Mathf.Abs(delta) <= threshold ? delta : 0f;
        }

        public const string EdgeMinusLabel = "кромка-";

        public const string EdgePlusLabel = "кромка+";

        public const string FromEdgeLabel = "от кромки";

        public static float MountDetentDelta(float aMin, float aMax, float bMin, float bMax,
            float threshold, float fromEdge, out string label)
        {
            float aCentre = (aMin + aMax) * 0.5f;
            float bCentre = (bMin + bMax) * 0.5f;

            float best = 0f;
            float bestAbs = float.MaxValue;
            label = FreeLabel;

            Closer(bMin - aMin, EdgeMinusLabel, threshold, ref best, ref bestAbs, ref label);
            Closer(bMax - aMax, EdgePlusLabel, threshold, ref best, ref bestAbs, ref label);
            Closer(bCentre - aCentre, CentreLabel, threshold, ref best, ref bestAbs, ref label);

            if (fromEdge > 0f && bMin + fromEdge <= bCentre + Tolerance.EpsilonUnits)
            {
                Closer(bMin + fromEdge - aCentre, FromEdgeLabel,
                    threshold, ref best, ref bestAbs, ref label);
                Closer(bMax - fromEdge - aCentre, FromEdgeLabel,
                    threshold, ref best, ref bestAbs, ref label);
            }

            return best;
        }

        private static void Closer(float delta, string name, float threshold,
            ref float best, ref float bestAbs, ref string label)
        {
            float abs = delta < 0f ? -delta : delta;
            if (abs > threshold || abs >= bestAbs) return;
            bestAbs = abs;
            best = delta;
            label = name;
        }

        public static float NearestDetentDelta(float aMin, float aMax, float bMin, float bMax,
            float threshold, List<float>? grooveCoords, out string label)
        {
            float aCenter = (aMin + aMax) * 0.5f;
            float bCenter = (bMin + bMax) * 0.5f;

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
