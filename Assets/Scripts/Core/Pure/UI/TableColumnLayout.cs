using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public static class TableColumnLayout
    {
        public static float[] Widths(IReadOnlyList<float> requested, float total)
        {
            var widths = new float[requested.Count];
            float fixedSum = 0f;
            int flexible = 0;
            for (int i = 0; i < requested.Count; i++)
            {
                if (requested[i] > 0f) fixedSum += requested[i];
                else flexible++;
            }

            float share = flexible > 0 ? System.Math.Max(0f, total - fixedSum) / flexible : 0f;
            for (int i = 0; i < requested.Count; i++)
                widths[i] = requested[i] > 0f ? requested[i] : share;
            return widths;
        }

        public static float[] Lefts(IReadOnlyList<float> widths)
        {
            var lefts = new float[widths.Count];
            float x = 0f;
            for (int i = 0; i < widths.Count; i++)
            {
                lefts[i] = x;
                x += widths[i];
            }
            return lefts;
        }
    }
}
