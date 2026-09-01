using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ScrewLegElement : KitchenElement, IAutoSeated
    {
        public override string DisplayTypeName => "Винтовая опора";

        [SerializeField] private string _thread = ScrewLegSpec.DEFAULT_THREAD;
        [SerializeField] private int _threadLengthMM = ScrewLegSpec.DEFAULT_THREAD_LENGTH_MM;
        [SerializeField] private int _insertionMM = ScrewLegSpec.DEFAULT_INSERTION_MM;
        [SerializeField] private int _baseDiameterMM = ScrewLegSpec.DEFAULT_BASE_DIAMETER_MM;
        [SerializeField] private int _baseHeightMM = ScrewLegSpec.DEFAULT_BASE_HEIGHT_MM;

        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [Undoable]
        public string Thread
        {
            get => _thread;
            set
            {
                var normalized = ScrewLegSpec.NormalizeThread(value);
                if (normalized == _thread) return;
                _thread = normalized;
                ApplyDimensions();
            }
        }

        [Undoable]
        public int ThreadLengthMM
        {
            get => _threadLengthMM;
            set
            {
                var clamped = ScrewLegSpec.ClampThreadLengthMM(value);
                if (clamped == _threadLengthMM) return;
                _threadLengthMM = clamped;
                SyncDimensions();
                ApplyDimensions();
            }
        }

        [Undoable]
        public int InsertionDepthMM
        {
            get => _insertionMM;
            set
            {
                var clamped = ScrewLegSpec.ClampInsertionMM(value, _threadLengthMM);
                if (clamped == _insertionMM) return;
                _insertionMM = clamped;
                ApplyDimensions();
            }
        }

        [Undoable]
        public int BaseDiameterMM
        {
            get => _baseDiameterMM;
            set
            {
                var clamped = ScrewLegSpec.ClampBaseDiameterMM(value);
                if (clamped == _baseDiameterMM) return;
                _baseDiameterMM = clamped;
                SyncDimensions();
                ApplyDimensions();
            }
        }

        [Undoable]
        public int BaseHeightMM
        {
            get => _baseHeightMM;
            set
            {
                var clamped = ScrewLegSpec.ClampBaseHeightMM(value);
                if (clamped == _baseHeightMM) return;
                _baseHeightMM = clamped;
                SyncDimensions();
                ApplyDimensions();
            }
        }

        public int ThreadDiameterMM => ScrewLegSpec.ThreadDiameterMM(_thread);

        public int BodyHeightMM => ScrewLegSpec.BodyHeightMM(_threadLengthMM, _baseHeightMM);

        public int HeightAboveFloorMM =>
            ScrewLegSpec.HeightAboveFloorMM(_threadLengthMM, _insertionMM, _baseHeightMM);

        public string? HostPartName => string.IsNullOrEmpty(AttachedToName) ? null : AttachedToName;

        public override bool CanCarryAttachedParts => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public float MountYUnits =>
            transform.position.y
            + (BodyHeightMM * 0.5f - _insertionMM) * AppConstants.MM_TO_UNITS;

        public float FloorYUnits =>
            transform.position.y - BodyHeightMM * 0.5f * AppConstants.MM_TO_UNITS;

        public void SetHeightAboveFloorMM(int heightMM) =>
            ThreadLengthMM = ScrewLegSpec.ThreadLengthForHeightMM(heightMM, _insertionMM, _baseHeightMM);

        public void SeatAfterMove(System.Collections.Generic.IReadOnlyList<KitchenElement> scene) =>
            ScrewLegAutoFit.Seat(this, scene);

        private void SyncDimensions() =>
            Data.DimensionsMM = new Vector3Int(_baseDiameterMM, BodyHeightMM, _baseDiameterMM);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            _thread = ScrewLegSpec.NormalizeThread(_thread);
            _baseDiameterMM = ScrewLegSpec.ClampBaseDiameterMM(Data.DimensionsMM.x);
            _baseHeightMM = ScrewLegSpec.ClampBaseHeightMM(_baseHeightMM);
            _threadLengthMM = ScrewLegSpec.ClampThreadLengthMM(
                Data.DimensionsMM.y - _baseHeightMM);
            _insertionMM = ScrewLegSpec.ClampInsertionMM(_insertionMM, _threadLengthMM);
            SyncDimensions();

            transform.localScale = Vector3.one;
            if (SuppressVisualRebuild) return;

            float toU = AppConstants.MM_TO_UNITS;
            var mesh = ScrewLegMesh.Build(
                _baseDiameterMM * 0.5f * toU, _baseHeightMM * toU,
                ThreadDiameterMM * 0.5f * toU, _threadLengthMM * toU);
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
