using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class WallRules
    {
        public const float ThicknessToleranceMm = 0.5f;

        public static bool ThicknessFitsFormat(MasonryTechnology technology,
            float thicknessMm, float jointMm)
        {
            var series = MasonryUnit.ThicknessSeries(technology, jointMm);
            if (series.Count == 0) return true;
            foreach (var standard in series)
                if (System.Math.Abs(standard - thicknessMm) <= ThicknessToleranceMm) return true;
            return false;
        }

        public static IReadOnlyList<ConstructionFinding> Collect(
            IReadOnlyList<WallSurvey>? walls)
        {
            var findings = new List<ConstructionFinding>();
            if (walls == null) return findings;

            foreach (var wall in walls)
            {
                if (ThicknessFitsFormat(wall.Technology, wall.ThicknessMm, wall.JointMm)) continue;
                findings.Add(ConstructionIssueCatalog.WallThicknessOffFormat(
                    wall.ElementId, MasonryUnit.Of(wall.Technology), wall.ThicknessMm,
                    MasonryUnit.ThicknessSeries(wall.Technology, wall.JointMm)));
            }

            return findings;
        }
    }
}
