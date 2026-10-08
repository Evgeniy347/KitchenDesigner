using System;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    public readonly struct PlanBounds
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float MinV;
        public readonly float MaxV;

        private PlanBounds(float minX, float maxX, float minV, float maxV)
        {
            MinX = minX;
            MaxX = maxX;
            MinV = minV;
            MaxV = maxV;
        }

        public double Width => Math.Max(MaxX - MinX, PlanLayout.MinExtentMm);

        public double Height => Math.Max(MaxV - MinV, PlanLayout.MinExtentMm);

        public static float VOf(PlanView view, Vector3 point) => view == PlanView.Top ? point.z : point.y;

        public static PlanBounds Of(DigestInput input, PlanView view)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (var entry in input.Entries)
            {
                Include(ref minX, ref maxX, entry.Box.Min.x, entry.Box.Max.x);
                Include(ref minV, ref maxV, VOf(view, entry.Box.Min), VOf(view, entry.Box.Max));
            }
            if (view == PlanView.Top)
                foreach (var room in input.Rooms)
                    IncludeRoom(room.PolygonXz, ref minX, ref maxX, ref minV, ref maxV);
            if (minX > maxX) { minX = 0f; maxX = PlanLayout.EmptySceneExtentMm; }
            if (minV > maxV) { minV = 0f; maxV = PlanLayout.EmptySceneExtentMm; }
            return new PlanBounds(minX, maxX, minV, maxV);
        }

        private static void IncludeRoom(int[]? polygon, ref float minX, ref float maxX, ref float minV, ref float maxV)
        {
            if (polygon == null) return;
            for (int i = 0; i + 1 < polygon.Length; i += 2)
            {
                Include(ref minX, ref maxX, polygon[i], polygon[i]);
                Include(ref minV, ref maxV, polygon[i + 1], polygon[i + 1]);
            }
        }

        private static void Include(ref float min, ref float max, float low, float high)
        {
            min = Math.Min(min, low);
            max = Math.Max(max, high);
        }
    }
}
