using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class SlabSpecItems
    {
        public static string Section => SpecSections.Structures;
        public static string ConcreteName => Loc.T("spec.slab.concrete");
        public static string RebarName => Loc.T("spec.slab.rebar");

        public static IEnumerable<SpecItem> Of(SlabTechnology technology, float lengthMm,
            float widthMm, float thicknessMm, float rebarDiameterMm, float rebarStepMm,
            string concreteGradeTitle)
        {
            if (technology != SlabTechnology.Slab) yield break;

            double areaM2 = System.Math.Max(0f, lengthMm) * System.Math.Max(0f, widthMm) * 1e-6d;
            double concreteM3 = SlabQuantities.ConcreteM3(areaM2, thicknessMm);
            if (concreteM3 > 0d)
                yield return new SpecItem(Section, ConcreteName, concreteGradeTitle ?? "",
                    SpecUnit.VolumeM3, (float)concreteM3);

            double rebarKg = SlabQuantities.RebarKg(lengthMm, widthMm, rebarStepMm, rebarDiameterMm);
            if (rebarKg > 0d)
                yield return new SpecItem(Section, RebarName, "", SpecUnit.Kilograms, (float)rebarKg);
        }
    }
}
