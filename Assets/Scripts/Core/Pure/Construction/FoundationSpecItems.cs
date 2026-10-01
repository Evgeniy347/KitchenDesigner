using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class FoundationSpecItems
    {
        public static string Section => SpecSections.Foundation;
        public static string ExcavationName => Loc.T("spec.foundation.excavation");
        public static string SandName => Loc.T("spec.foundation.sand");
        public static string GravelName => Loc.T("spec.foundation.gravel");
        public static string ConcreteName => Loc.T("spec.foundation.concrete");
        public static string FormworkName => Loc.T("spec.foundation.formwork");
        public static string RebarName => Loc.T("spec.foundation.rebar");

        public static IEnumerable<SpecItem> Of(FoundationQuantitiesResult quantities,
            string concreteGradeTitle)
        {
            if (quantities.ExcavationLooseM3 > 0d)
                yield return new SpecItem(Section, ExcavationName, "", SpecUnit.VolumeM3,
                    (float)quantities.ExcavationLooseM3);

            if (quantities.SandM3 > 0d)
                yield return new SpecItem(Section, SandName, "", SpecUnit.VolumeM3,
                    (float)quantities.SandM3);

            if (quantities.GravelM3 > 0d)
                yield return new SpecItem(Section, GravelName, "", SpecUnit.VolumeM3,
                    (float)quantities.GravelM3);

            if (quantities.ConcreteM3 > 0d)
                yield return new SpecItem(Section, ConcreteName, concreteGradeTitle ?? "",
                    SpecUnit.VolumeM3, (float)quantities.ConcreteM3);

            if (quantities.FormworkM2 > 0d)
                yield return new SpecItem(Section, FormworkName, "", SpecUnit.AreaM2,
                    (float)quantities.FormworkM2);

            if (quantities.RebarKg > 0d)
                yield return new SpecItem(Section, RebarName, "", SpecUnit.Kilograms,
                    (float)quantities.RebarKg);
        }
    }
}
