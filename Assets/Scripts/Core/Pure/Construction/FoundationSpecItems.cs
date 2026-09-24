using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class FoundationSpecItems
    {
        public const string Section = SpecSections.Foundation;
        public const string ExcavationName = "Выемка грунта";
        public const string SandName = "Подушка: песок";
        public const string GravelName = "Подушка: щебень";
        public const string ConcreteName = "Бетон ленты";
        public const string FormworkName = "Опалубка";
        public const string RebarName = "Арматура";

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
