using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public class PipeElement : KitchenElement, ISnapPorts
    {
        public override string DisplayTypeName => "Труба";

        [SerializeField] private string _sizeId = PipeSpec.DEFAULT_SIZE;
        [SerializeField] private int _lengthMM = PipeElementSpec.DEFAULT_LENGTH_MM;

        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [Undoable]
        public string SizeId
        {
            get => _sizeId;
            set
            {
                var normalized = PipeSpec.NormalizeSize(value);
                if (normalized == _sizeId) return;
                _sizeId = normalized;
                SyncDimensions();
                ApplyDimensions();
            }
        }

        [Undoable]
        public int LengthMM
        {
            get => _lengthMM;
            set
            {
                var clamped = PipeElementSpec.ClampLengthMM(value);
                if (clamped == _lengthMM) return;
                _lengthMM = clamped;
                SyncDimensions();
                ApplyDimensions();
            }
        }

        public PipeSize Size => PipeSpec.Get(_sizeId);

        public string Designation => Size.Designation;

        public int NominalBoreMM => Size.NominalBoreMm;

        public float OuterDiameterMm => Size.OuterDiameterMm;

        public float InnerDiameterMm => Size.InnerDiameterMm;

        public float WallThicknessMm => Size.WallThicknessMm;

        public int SectionMM => PipeElementSpec.SectionMM(_sizeId);

        public int SnapPortCount => PipeNodePorts.CountOf(PipeNodeKind.Pipe);

        public SnapPort SnapPortAt(int index, Vector3 transformPosition)
        {
            var along = ValidationRotation * Vector3.up;
            var half = along * (_lengthMM * 0.5f * AppConstants.MM_TO_UNITS);
            var centre = ValidationPositionAt(transformPosition);
            return index == 0
                ? new SnapPort(centre - half, -along)
                : new SnapPort(centre + half, along);
        }

        public Vector3 EndAUnits => SnapPortAt(0, transform.position).Position;

        public Vector3 EndBUnits => SnapPortAt(1, transform.position).Position;

        public Vector3 RunAxis => SnapPortAt(1, transform.position).Outward;

        public override Vector2Int DecorSurfaceMM => new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(OuterDiameterMm * Mathf.PI)), _lengthMM);

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        public override bool ParticipatesInGapChecks => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void SyncDimensions() =>
            Data.DimensionsMM = new Vector3Int(SectionMM, _lengthMM, SectionMM);

        private void Rebuild()
        {
            _sizeId = PipeSpec.NormalizeSize(_sizeId);
            _lengthMM = PipeElementSpec.ClampLengthMM(Data.DimensionsMM.y);
            SyncDimensions();

            transform.localScale = Vector3.one;
            if (SuppressVisualRebuild) return;

            float toU = AppConstants.MM_TO_UNITS;
            var mesh = PipeMesh.Build(OuterDiameterMm * 0.5f * toU, _lengthMM * toU);
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
