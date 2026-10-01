using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core
{
    public class VentGapElement : WallLayerElement, IQuantifies
    {
        public override string DisplayTypeName => Loc.T("elementType.ventGap");

        public override int StackOrder => 1;

        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("обрешётка вентзазора идёт по всей грани стены — лицевой детали нет");

        [SerializeField] private int _battenStepMm = WallLayerDefaults.VentGapBattenStepMm;

        [Undoable]
        public int BattenStepMm
        {
            get => _battenStepMm;
            set => _battenStepMm = Mathf.Clamp(value,
                WallLayerDefaults.MinBattenStepMm, WallLayerDefaults.MaxBattenStepMm);
        }

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            double runningM = WallLayerQuantities.VentGapBattenRunningMetres(
                DimensionsMM.x, DimensionsMM.y, _battenStepMm);

            yield return new SpecItem(SpecSections.Walls, Loc.T("spec.item.ventGapBattens"), "", SpecUnit.LinearMeters,
                (float)runningM);
        }
    }
}
