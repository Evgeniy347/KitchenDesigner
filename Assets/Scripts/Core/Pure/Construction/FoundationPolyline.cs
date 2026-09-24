using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Construction
{
    public readonly struct FoundationPolyline
    {
        public readonly IReadOnlyList<Vector3> Points;
        public readonly float LengthMm;

        public FoundationPolyline(IReadOnlyList<Vector3> points, float lengthMm)
        {
            Points = points;
            LengthMm = lengthMm;
        }
    }
}
