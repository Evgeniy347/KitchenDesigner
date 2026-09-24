using System.Collections.Generic;
using KitchenDesigner.Core.Construction;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FoundationElement : KitchenElement, IQuantifies
    {
        public override string DisplayTypeName => "Фундамент";

        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart(
                "лента идёт по всему периметру несущих стен: у неё нет одной характерной стороны");

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public override bool ParticipatesInGapChecks => false;

        public const int DEFAULT_WIDTH_MM = 600;
        public const int DEFAULT_DEPTH_MM = 700;

        [SerializeField] private SoilKind _soilKind = KitchenSettings.Instance.ConstructionSoil;
        [SerializeField] private int _sandMm = KitchenSettings.Instance.ConstructionSandMm;
        [SerializeField] private int _gravelMm = KitchenSettings.Instance.ConstructionGravelMm;
        [SerializeField] private bool _compacted = KitchenSettings.Instance.ConstructionCompacted;
        [SerializeField] private ConcreteGrade _concreteGrade = KitchenSettings.Instance.ConstructionConcrete;
        [SerializeField] private int _rebarDiameterMm = FoundationRebarDefaults.DiameterMm;
        [SerializeField] private int _rebarStepMm = FoundationRebarDefaults.StepMm;
        [SerializeField] private int _coverMm = FoundationRebarDefaults.CoverMm;

        [Undoable]
        public SoilKind SoilKind
        {
            get => _soilKind;
            set => _soilKind = (SoilKind)Mathf.Clamp((int)value, 0, SoilKindTitles.All.Length - 1);
        }

        [Undoable]
        public int SandMm
        {
            get => _sandMm;
            set => _sandMm = Mathf.Clamp(value, 0, KitchenSettings.CONSTRUCTION_BEDDING_MAX_MM);
        }

        [Undoable]
        public int GravelMm
        {
            get => _gravelMm;
            set => _gravelMm = Mathf.Clamp(value, 0, KitchenSettings.CONSTRUCTION_BEDDING_MAX_MM);
        }

        [Undoable]
        public bool Compacted
        {
            get => _compacted;
            set => _compacted = value;
        }

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

        [Undoable]
        public int CoverMm
        {
            get => _coverMm;
            set => _coverMm = Mathf.Clamp(value,
                FoundationRebarDefaults.MinCoverMm, FoundationRebarDefaults.MaxCoverMm);
        }

        public string FrostDepthText =>
            FrostDepth.Read(KitchenSettings.Instance.ConstructionRegion, _soilKind).Value;

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            var centrelines = FoundationWallSurvey.LoadBearingCentrelines(allElements);
            var quantities = FoundationQuantities.OfLoadBearingWalls(_soilKind, centrelines,
                DimensionsMM.x, DimensionsMM.y, _sandMm, _gravelMm, _rebarDiameterMm,
                _rebarStepMm, _coverMm);
            return FoundationSpecItems.Of(quantities, ConcreteGradeTitles.Of(_concreteGrade));
        }

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;
            if (SuppressVisualRebuild) return;

            var centrelines = FoundationWallSurvey.LoadBearingCentrelines(PartRegistry.GetAll());
            var mesh = FoundationStripMesh.Build(centrelines, transform.position,
                DimensionsMM.x, DimensionsMM.y);
            AdoptOwnedMesh(mesh);

            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();

            ElementRoot.UseMeshCollider(gameObject, mesh);
            MaterialManager.RefreshTiling(this);
        }
    }
}
