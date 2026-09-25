using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core
{
    public class CladdingElement : WallLayerElement, IQuantifies
    {
        public override string DisplayTypeName => "Облицовка";

        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("облицовка идёт по всей грани стены — лицевой детали нет");

        [Undoable]
        public int ThicknessMm
        {
            get => DimensionsMM.z;
            set
            {
                var d = DimensionsMM;
                d.z = Mathf.Clamp(value, WallLayerDefaults.MinThicknessMm, WallLayerDefaults.MaxThicknessMm);
                DimensionsMM = d;
            }
        }

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            var wall = ResolveHostWall();
            var openings = wall != null ? WallQuantitySurvey.Openings(wall) : new List<WallOpening>();
            double areaM2 = WallLayerQuantities.CladdingAreaM2(DimensionsMM.x, DimensionsMM.y, openings);

            yield return new SpecItem(SpecSections.Walls, "Облицовка стены", "", SpecUnit.AreaM2, (float)areaM2);
        }
    }
}
