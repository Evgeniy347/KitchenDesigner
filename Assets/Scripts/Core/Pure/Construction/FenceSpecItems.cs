using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class FenceSpecItems
    {
        public static string Section => SpecSections.Structures;
        public static string PostsName => Loc.T("spec.fence.posts");
        public static string ConcreteName => Loc.T("spec.fence.postConcrete");
        public static string SheetName => Loc.T("spec.fence.sheet");
        public static string RailName => Loc.T("spec.fence.rails");

        public const float HoleDiameterToPostSectionRatio = 2.5f;

        public static IEnumerable<SpecItem> Of(float lengthMm, float heightMm, float postStepMm,
            float postSectionMm, float pitDepthMm, float sheetWorkingWidthMm, string sheetMark)
        {
            var runs = new[] { new FenceRun("run", lengthMm) };

            int postCount = FenceQuantities.PostCount(runs, postStepMm);
            if (postCount > 0)
                yield return new SpecItem(Section, PostsName,
                    ((int)postSectionMm) + "x" + ((int)postSectionMm), SpecUnit.Pieces, postCount);

            float holeDiameterMm = postSectionMm * HoleDiameterToPostSectionRatio;
            double concreteM3 = FenceQuantities.PostConcreteM3(postCount, holeDiameterMm, pitDepthMm);
            if (concreteM3 > 0d)
                yield return new SpecItem(Section, ConcreteName, "", SpecUnit.VolumeM3, (float)concreteM3);

            int sheetCount = FenceQuantities.SheetCount(runs, sheetWorkingWidthMm);
            if (sheetCount > 0)
                yield return new SpecItem(Section, SheetName, sheetMark ?? "", SpecUnit.Pieces, sheetCount);

            int railCount = FenceRailPlan.RailCountFor(heightMm);
            double railM = FenceQuantities.RailRunningMetres(runs, railCount);
            if (railM > 0d)
                yield return new SpecItem(Section, RailName, "", SpecUnit.LinearMeters, (float)railM);
        }
    }
}
