using System.Collections.Generic;
using KitchenDesigner.Core.Construction;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class RoofElement : KitchenElement, IQuantifies
    {
        public override string DisplayTypeName => "Крыша";

        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart(
                "крыша — несколько скатов вокруг конька: у неё нет одной характерной стороны");

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public override bool ParticipatesInGapChecks => false;

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        [SerializeField] private RoofType _type = RoofDefaults.Type;
        [SerializeField] private RoofRidgeAxis _ridgeAxis = RoofDefaults.RidgeAxis;
        [SerializeField] private float _pitchDeg = RoofDefaults.PitchDeg;
        [SerializeField] private int _overhangMm = RoofDefaults.OverhangMm;
        [SerializeField] private int _rafterStepMm = RoofDefaults.RafterStepMm;

        private static readonly int RoofTypeCount = System.Enum.GetValues(typeof(RoofType)).Length;
        private static readonly int RoofRidgeAxisCount = System.Enum.GetValues(typeof(RoofRidgeAxis)).Length;

        private RoofFrame _builtFrame;

        public RoofFrame BuiltFrame => _builtFrame;

        [Undoable]
        public RoofType Type
        {
            get => _type;
            set
            {
                _type = (RoofType)Mathf.Clamp((int)value, 0, RoofTypeCount - 1);
                ApplyDimensions();
            }
        }

        [Undoable]
        public RoofRidgeAxis RidgeAxis
        {
            get => _ridgeAxis;
            set
            {
                _ridgeAxis = (RoofRidgeAxis)Mathf.Clamp((int)value, 0, RoofRidgeAxisCount - 1);
                ApplyDimensions();
            }
        }

        [Undoable]
        public float PitchDeg
        {
            get => _pitchDeg;
            set
            {
                _pitchDeg = Mathf.Clamp(value, RoofDefaults.MinPitchDeg, RoofDefaults.MaxPitchDeg);
                ApplyDimensions();
            }
        }

        [Undoable]
        public int OverhangMm
        {
            get => _overhangMm;
            set
            {
                _overhangMm = Mathf.Clamp(value, RoofDefaults.MinOverhangMm,
                    RoofDefaults.MaxOverhangMm);
                ApplyDimensions();
            }
        }

        [Undoable]
        public int RafterStepMm
        {
            get => _rafterStepMm;
            set
            {
                _rafterStepMm = Mathf.Clamp(value, RoofDefaults.MinRafterStepMm,
                    RoofDefaults.MaxRafterStepMm);
                ApplyDimensions();
            }
        }

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements) =>
            RoofSpecItems.Of(_builtFrame, _pitchDeg, _rafterStepMm, RoofDefaults.CoveringWastePct);

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;
            if (SuppressVisualRebuild) return;

            var topLevel = LevelResolution.TopLevel(LevelRegistry.Snapshot());
            var topLevelElements = new List<KitchenElement>();
            foreach (var e in PartRegistry.GetAll())
                if (e != null && LevelRegistry.LevelOf(e).id == topLevel.id) topLevelElements.Add(e);

            var centrelines = FoundationWallSurvey.LoadBearingCentrelines(topLevelElements);
            var footprint = RoofContour.BoundingFootprint(centrelines);
            bool hasFootprint = footprint.WidthXMm > 0f || footprint.LengthZMm > 0f;
            _builtFrame = hasFootprint
                ? RoofPitchPlanes.Build(footprint, _type, _ridgeAxis, _overhangMm)
                : default;

            float runMm = _builtFrame.Planes != null && _builtFrame.Planes.Count > 0
                ? _builtFrame.Planes[0].RunMm
                : 0f;
            float thicknessMm = RafterSectionTable.ForSpan(runMm).HeightMm;
            float riseMm = runMm * Mathf.Tan(_pitchDeg * Mathf.Deg2Rad);

            float toU = AppConstants.MM_TO_UNITS;
            float halfRiseUnits = riseMm * 0.5f * toU;

            var mesh = RoofMesh.Build(footprint, _type, _ridgeAxis, _overhangMm, _pitchDeg,
                thicknessMm, -halfRiseUnits);
            AdoptOwnedMesh(mesh);

            float centreXUnits = (footprint.MinXMm + footprint.MaxXMm) * 0.5f * toU;
            float centreZUnits = (footprint.MinZMm + footprint.MaxZMm) * 0.5f * toU;
            float eaveYUnits = (topLevel.floorElevationMm + topLevel.heightMm) * toU;
            float boxCentreYUnits = eaveYUnits + halfRiseUnits;
            transform.position = new Vector3(centreXUnits, boxCentreYUnits, centreZUnits);

            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();

            ElementRoot.UseMeshCollider(gameObject, mesh);
            MaterialManager.RefreshTiling(this);

            RoofPitchPlanes.ExtendedSpanAndSlope(footprint, _type, _ridgeAxis, _overhangMm,
                out float spanMm, out float slopeMm);
            bool ridgeAlongX = RoofPitchPlanes.RidgeAlongX(footprint,
                RoofPitchPlanes.EffectiveRidgeAxis(_type, _ridgeAxis));
            float widthXMm = ridgeAlongX ? spanMm : slopeMm;
            float lengthZMm = ridgeAlongX ? slopeMm : spanMm;
            Data.DimensionsMM = new Vector3Int(Mathf.Max(1, (int)widthXMm),
                Mathf.Max(1, (int)riseMm), Mathf.Max(1, (int)lengthZMm));
        }
    }
}
