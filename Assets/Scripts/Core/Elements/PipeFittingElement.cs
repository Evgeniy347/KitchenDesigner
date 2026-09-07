using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public abstract class PipeFittingElement : KitchenElement
    {
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        public abstract PipeNodeKind NodeKind { get; }

        public override string DisplayTypeName => PipeFittingNames.Title(NodeKind);

        public string NominalSizeId => PipeSpec.DEFAULT_SIZE;

        public int PortCount => PipeFittingSpec.PortCount(NodeKind);

        public Vector3Int NominalDimensionsMM => new Vector3Int(
            PipeFittingSpec.RoundedMm(PipeFittingSpec.WidthMm(NodeKind, NominalSizeId)),
            PipeFittingSpec.RoundedMm(PipeFittingSpec.HeightMm(NodeKind, NominalSizeId)),
            PipeFittingSpec.RoundedMm(PipeFittingSpec.DepthMm(NodeKind, NominalSizeId)));

        public Vector3 HubPositionUnits => transform.TransformPoint(LocalHubUnits);

        public Vector3 PortPositionUnits(int portIndex) =>
            transform.TransformPoint(LocalPortUnits(portIndex));

        public Vector3 PortDirection(int portIndex) =>
            transform.TransformDirection(LocalPortAxis(portIndex)).normalized;

        public override Vector2Int DecorSurfaceMM => new Vector2Int(
            Mathf.Max(1, PipeFittingSpec.RoundedMm(
                PipeFittingSpec.BodyDiameterMm(NominalSizeId) * Mathf.PI)),
            Mathf.Max(1, NominalDimensionsMM.y));

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        public override bool ParticipatesInGapChecks => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private Vector3 LocalHubUnits =>
            ToUnits(PipeFittingSpec.HubOffsetMm(NodeKind, NominalSizeId));

        private Vector3 LocalPortUnits(int portIndex) =>
            ToUnits(PipeFittingSpec.PortOffsetMm(NodeKind, NominalSizeId, portIndex));

        private static Vector3 ToUnits(in PointMm offset)
        {
            float toU = AppConstants.MM_TO_UNITS;
            return new Vector3(offset.XMm * toU, offset.YMm * toU, offset.ZMm * toU);
        }

        private Vector3 LocalPortAxis(int portIndex)
        {
            var axis = PipeFittingSpec.PortAxis(NodeKind, portIndex);
            return new Vector3(axis.X, axis.Y, axis.Z);
        }

        private void Rebuild()
        {
            Data.DimensionsMM = NominalDimensionsMM;

            transform.localScale = Vector3.one;
            if (SuppressVisualRebuild) return;

            var mesh = PipeFittingMesh.Build(NodeKind, NominalSizeId);
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
