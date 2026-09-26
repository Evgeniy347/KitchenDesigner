using System.Collections.Generic;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core
{
    public class CladdingElement : WallLayerElement, IQuantifies
    {
        public override string DisplayTypeName => "Облицовка";

        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("облицовка идёт по всей грани стены — лицевой детали нет");

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            var wall = ResolveHostWall();
            var openings = wall != null ? WallQuantitySurvey.Openings(wall) : new List<WallOpening>();
            double areaM2 = WallLayerQuantities.CladdingAreaM2(DimensionsMM.x, DimensionsMM.y, openings);

            yield return new SpecItem(SpecSections.Walls, "Облицовка стены", "", SpecUnit.AreaM2, (float)areaM2);
        }
    }
}
