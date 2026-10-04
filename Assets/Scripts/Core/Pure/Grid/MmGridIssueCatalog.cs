using System.Text;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class MmGridIssueCatalog
    {
        public const string CodeOffMillimetreGrid = "GRD-01";

        private static readonly string[] AxisNames = { "X", "Y", "Z" };

        public static string? OffMillimetreGrid(Vector3 minCornerMm, float toleranceMm)
        {
            if (!MmGridMath.TryMeasureOffGrid(minCornerMm, toleranceMm, out var shiftsMm))
                return null;
            return Loc.F("issue.grd01.message", Axes(minCornerMm, shiftsMm));
        }

        private static string Axes(Vector3 minCornerMm, Vector3 shiftsMm)
        {
            var text = new StringBuilder();
            for (int axis = 0; axis < 3; axis++)
            {
                if (shiftsMm[axis] == 0f) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(AxisNames[axis]).Append(' ').Append(Mm(minCornerMm[axis]))
                    .Append(Loc.T("issue.grd01.offBy")).Append(Mm(Mathf.Abs(shiftsMm[axis]))).Append(Loc.T("issue.grd01.mmSuffix"));
            }
            return text.ToString();
        }

        private static string Mm(float value) => NumberFormat.Compact(value, 3);
    }
}
