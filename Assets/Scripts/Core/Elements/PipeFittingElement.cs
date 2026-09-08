using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public abstract class PipeFittingElement : KitchenElement, IPaintsItself, ISnapPorts,
        IAutoSeated
    {
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        private string?[] _boreSizeIds = new string?[0];

        public abstract PipeNodeKind NodeKind { get; }

        public override string DisplayTypeName => PipeFittingNames.Title(NodeKind);

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => false;

        public string PortFrameSizeId => PipeSpec.DEFAULT_SIZE;

        public IReadOnlyList<string?> BoreSizeIds => _boreSizeIds;

        public string BoreSizeId => PipeSizes.Widest(_boreSizeIds);

        public int PortCount => PipeFittingSpec.PortCount(NodeKind);

        public Vector3Int DerivedDimensionsMM
        {
            get
            {
                var cover = PipeFittingLayout.CoverSizeMM(NodeKind, PortFrameSizeId, _boreSizeIds);
                return new Vector3Int(
                    PipeFittingSpec.RoundedMm(cover.x),
                    PipeFittingSpec.RoundedMm(cover.y),
                    PipeFittingSpec.RoundedMm(cover.z));
            }
        }

        public virtual Material FactoryMaterial => SanitaryMaterials.Chrome;

        public int SnapPortCount => PortCount;

        public SnapPort SnapPortAt(int index, Vector3 transformPosition) => new SnapPort(
            ValidationPositionAt(transformPosition) + ValidationRotation * LocalPortUnits(index),
            ValidationRotation * LocalPortAxis(index));

        public void SeatAfterMove(IReadOnlyList<KitchenElement> scene,
            SnapCursor cursor = default) =>
            PipeDocking.Seat(this, ValidationPositionAt(transform.position), ValidationRotation,
                scene, cursor);

        public void RepairJointAfterGridSnap(IReadOnlyList<KitchenElement> scene) =>
            PipeDocking.RepairAfterGridSnap(this, scene);

        public void SeatOnPipeEnd(PipeElement pipe, int end, IReadOnlyList<KitchenElement> scene) =>
            PipeDocking.SeatFittingOnPipeEnd(this, pipe, end, scene);

        public Vector3 HubPositionUnits =>
            ValidationPositionAt(transform.position) + ValidationRotation * LocalHubUnits;

        public Vector3 PortPositionUnits(int portIndex) =>
            SnapPortAt(portIndex, transform.position).Position;

        public Vector3 PortDirection(int portIndex) =>
            SnapPortAt(portIndex, transform.position).Outward;

        public override Vector2Int DecorSurfaceMM => new Vector2Int(
            Mathf.Max(1, PipeFittingSpec.RoundedMm(
                PipeFittingSpec.BodyDiameterMm(BoreSizeId) * Mathf.PI)),
            Mathf.Max(1, DerivedDimensionsMM.y));

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        public override bool ParticipatesInGapChecks => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        public bool TakeBoreSizes(IReadOnlyList<string?>? sizes)
        {
            int count = PortCount;
            var taken = new string?[count];
            for (int i = 0; i < count; i++)
                taken[i] = sizes != null && i < sizes.Count ? sizes[i] : null;

            if (_boreSizeIds.Length == count)
            {
                bool same = true;
                for (int i = 0; i < count; i++)
                    if (!string.Equals(_boreSizeIds[i], taken[i], StringComparison.Ordinal))
                    {
                        same = false;
                        break;
                    }

                if (same) return false;
            }

            _boreSizeIds = taken;
            return true;
        }

        private Vector3 LocalHubUnits => ToUnits(
            PipeFittingSpec.HubOffsetMm(NodeKind, PortFrameSizeId));

        private Vector3 LocalPortUnits(int portIndex) => ToUnits(
            PipeFittingSpec.PortOffsetMm(NodeKind, PortFrameSizeId, portIndex));

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
            Data.DimensionsMM = DerivedDimensionsMM;

            transform.localScale = Vector3.one;
            if (SuppressVisualRebuild) return;

            var mesh = PipeFittingMesh.Build(NodeKind, PortFrameSizeId, _boreSizeIds);
            AdoptOwnedMesh(mesh);

            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Skin();

            ElementRoot.UseMeshCollider(gameObject, mesh);
            MaterialManager.RefreshTiling(this);
        }

        public void SetMaterial(Material material)
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) return;
            renderer.sharedMaterial =
                SanitaryDecor.ChosenOrFactory(MaterialId, material, FactoryMaterial);
        }

        private Material Skin()
        {
            if (SanitaryDecor.IsFactoryLook(MaterialId)) return FactoryMaterial;
            var decor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));
            return decor != null ? decor! : FactoryMaterial;
        }
    }
}
