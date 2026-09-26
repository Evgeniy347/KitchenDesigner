using System.Collections.Generic;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core
{
    public class InsulationElement : WallLayerElement, IQuantifies
    {
        public override string DisplayTypeName => "Утеплитель";

        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("утеплитель идёт по всей грани стены под облицовкой — лицевой детали нет");

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            var wall = ResolveHostWall();
            var openings = wall != null ? WallQuantitySurvey.Openings(wall) : new List<WallOpening>();
            double areaM2 = WallLayerQuantities.InsulationAreaM2(DimensionsMM.x, DimensionsMM.y, openings);
            double volumeM3 = WallLayerQuantities.InsulationVolumeM3(
                DimensionsMM.x, DimensionsMM.y, DimensionsMM.z, openings);

            yield return new SpecItem(SpecSections.Walls, "Утепление стены", "", SpecUnit.AreaM2, (float)areaM2);
            yield return new SpecItem(SpecSections.Walls, "Утепление стены, объём", "", SpecUnit.VolumeM3,
                (float)volumeM3);
        }
    }
}
