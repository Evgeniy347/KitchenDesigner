using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class RoofSpecItems
    {
        public const string Section = SpecSections.Structures;
        public const string CoveringName = "Кровельное покрытие";
        public const string RafterName = "Стропила";
        public const string RidgeName = "Конёк";
        public const string EaveName = "Карниз";
        public const string GutterName = "Водосток";

        public static IEnumerable<SpecItem> Of(RoofFrame frame, float pitchDeg, float rafterStepMm,
            float wastePct)
        {
            double pitchAreaM2 = RoofQuantities.PitchAreaM2(frame, pitchDeg);
            double coveringM2 = RoofQuantities.CoveringAreaM2(pitchAreaM2, wastePct);
            if (coveringM2 > 0d)
                yield return new SpecItem(Section, CoveringName, "", SpecUnit.AreaM2,
                    (float)coveringM2);

            int rafterCount = RoofQuantities.RafterCount(frame, rafterStepMm);
            if (rafterCount > 0)
                yield return new SpecItem(Section, RafterName, "", SpecUnit.Pieces, rafterCount);

            double ridgeM = RoofQuantities.RidgeLengthM(frame);
            if (ridgeM > 0d)
                yield return new SpecItem(Section, RidgeName, "", SpecUnit.LinearMeters,
                    (float)ridgeM);

            double eaveM = RoofQuantities.EaveLengthM(frame);
            if (eaveM > 0d)
                yield return new SpecItem(Section, EaveName, "", SpecUnit.LinearMeters,
                    (float)eaveM);

            double gutterM = RoofQuantities.GutterLengthM(frame);
            if (gutterM > 0d)
                yield return new SpecItem(Section, GutterName, "", SpecUnit.LinearMeters,
                    (float)gutterM);
        }
    }
}
