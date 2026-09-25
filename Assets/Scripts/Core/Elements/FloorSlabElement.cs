using System.Collections.Generic;
using KitchenDesigner.Core.Construction;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FloorSlabElement : KitchenElement, IKeepsPlacementHeight, IQuantifies
    {
        public override string DisplayTypeName => "Перекрытие";

        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart(
                "плита перекрытия горизонтальна: у неё нет одной характерной стороны");

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public override bool ParticipatesInGapChecks => false;

        public override Vector2Int DecorSurfaceMM => new Vector2Int(DimensionsMM.x, DimensionsMM.z);

        public const int DEFAULT_LENGTH_MM = 3000;
        public const int DEFAULT_WIDTH_MM = 3000;

        [SerializeField] private SlabTechnology _technology = SlabTechnology.Slab;
        [SerializeField] private ConcreteGrade _concreteGrade = KitchenSettings.Instance.ConstructionConcrete;
        [SerializeField] private int _rebarDiameterMm = FoundationRebarDefaults.DiameterMm;
        [SerializeField] private int _rebarStepMm = FoundationRebarDefaults.StepMm;

        [Undoable]
        public SlabTechnology Technology
        {
            get => _technology;
            set => _technology = (SlabTechnology)Mathf.Clamp((int)value, 0, 1);
        }

        public int ThicknessMm => DimensionsMM.y;

        [Undoable]
        public ConcreteGrade ConcreteGrade
        {
            get => _concreteGrade;
            set => _concreteGrade = (ConcreteGrade)Mathf.Clamp((int)value,
                0, ConcreteGradeTitles.All.Length - 1);
        }

        [Undoable]
        public int RebarDiameterMm
        {
            get => _rebarDiameterMm;
            set => _rebarDiameterMm = Mathf.Clamp(value,
                FoundationRebarDefaults.MinDiameterMm, FoundationRebarDefaults.MaxDiameterMm);
        }

        [Undoable]
        public int RebarStepMm
        {
            get => _rebarStepMm;
            set => _rebarStepMm = Mathf.Clamp(value,
                FoundationRebarDefaults.MinStepMm, FoundationRebarDefaults.MaxStepMm);
        }

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements) =>
            SlabSpecItems.Of(_technology, DimensionsMM.x, DimensionsMM.z, ThicknessMm,
                _rebarDiameterMm, _rebarStepMm, ConcreteGradeTitles.Of(_concreteGrade));
    }
}
