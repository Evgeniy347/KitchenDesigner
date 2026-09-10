using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ShowerColumnElement : KitchenElement, IWallMounted, IFixedSizeElement,
        IKeepsPlacementHeight, IPaintsItself, IQuantifies
    {
        public override string DisplayTypeName => "Душевая стойка";

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return PurchasedGoodsSpecItems.Piece(DisplayTypeName, DimensionsMM);
        }

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public bool HasFixedSize => true;

        [SerializeField] private int _columnHeightMM = ShowerColumnSpec.DefaultColumnHeightMM;
        [SerializeField] private int _riserDiameterMM = ShowerColumnSpec.DefaultRiserDiameterMM;
        [SerializeField] private int _headDiameterMM = ShowerColumnSpec.DefaultHeadDiameterMM;
        [SerializeField] private int _headThicknessMM = ShowerColumnSpec.DefaultHeadThicknessMM;
        [SerializeField] private int _armReachMM = ShowerColumnSpec.DefaultArmReachMM;
        [SerializeField] private int _wallOffsetMM = ShowerColumnSpec.DefaultWallOffsetMM;
        [SerializeField] private int _handShowerDiameterMM =
            ShowerColumnSpec.DefaultHandShowerDiameterMM;
        [SerializeField] private int _hoseLengthMM = ShowerColumnSpec.DefaultHoseLengthMM;

        private readonly RebuildGuard _rebuild = new RebuildGuard();
        private OwnedMeshBody? _body;

        private OwnedMeshBody Body =>
            _body ??= new OwnedMeshBody(gameObject, AdoptOwnedMesh, false);

        public ShowerColumnSpec Spec => ShowerColumnSpec.Clamped(_columnHeightMM,
            _riserDiameterMM, _headDiameterMM, _headThicknessMM, _armReachMM, _wallOffsetMM,
            _handShowerDiameterMM, _hoseLengthMM);

        [Undoable]
        public int ColumnHeightMM
        {
            get => _columnHeightMM;
            set => Set(ref _columnHeightMM,
                ShowerColumnSpec.ClampColumnHeightMM(value, _riserDiameterMM));
        }

        [Undoable]
        public int RiserDiameterMM
        {
            get => _riserDiameterMM;
            set => Set(ref _riserDiameterMM, ShowerColumnSpec.ClampRiserDiameterMM(value));
        }

        [Undoable]
        public int HeadDiameterMM
        {
            get => _headDiameterMM;
            set => Set(ref _headDiameterMM, ShowerColumnSpec.ClampHeadDiameterMM(value));
        }

        [Undoable]
        public int HeadThicknessMM
        {
            get => _headThicknessMM;
            set => Set(ref _headThicknessMM, ShowerColumnSpec.ClampHeadThicknessMM(value));
        }

        [Undoable]
        public int ArmReachMM
        {
            get => _armReachMM;
            set => Set(ref _armReachMM,
                ShowerColumnSpec.ClampArmReachMM(value, _riserDiameterMM, _wallOffsetMM));
        }

        [Undoable]
        public int WallOffsetMM
        {
            get => _wallOffsetMM;
            set => Set(ref _wallOffsetMM, ShowerColumnSpec.ClampWallOffsetMM(value));
        }

        [Undoable]
        public int HandShowerDiameterMM
        {
            get => _handShowerDiameterMM;
            set => Set(ref _handShowerDiameterMM,
                ShowerColumnSpec.ClampHandShowerDiameterMM(value));
        }

        [Undoable]
        public int HoseLengthMM
        {
            get => _hoseLengthMM;
            set => Set(ref _hoseLengthMM, ShowerColumnSpec.ClampHoseLengthMM(value));
        }

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        public void SnapToWall() => WallSeating.Seat(this, DimensionsMM.z);

        private void Start() => SnapToWall();

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Set(ref int field, int clamped)
        {
            if (field == clamped) return;
            field = clamped;
            ApplyDimensions();
        }

        private void Rebuild()
        {
            var spec = Spec;
            _columnHeightMM = spec.ColumnHeightMM;
            _riserDiameterMM = spec.RiserDiameterMM;
            _headDiameterMM = spec.HeadDiameterMM;
            _headThicknessMM = spec.HeadThicknessMM;
            _armReachMM = spec.ArmReachMM;
            _wallOffsetMM = spec.WallOffsetMM;
            _handShowerDiameterMM = spec.HandShowerDiameterMM;
            _hoseLengthMM = spec.HoseLengthMM;

            Data.DimensionsMM = ShowerColumnLayout.DimensionsMM(spec);
            transform.localScale = Vector3.one;
            if (SuppressVisualRebuild) return;

            Body.SetMaterial(Skin());
            Body.Rebuild(BuildMesh(spec));
            MaterialManager.RefreshTiling(this);
        }

        private Material Skin()
        {
            if (SanitaryDecor.IsFactoryLook(MaterialId)) return SanitaryMaterials.MatteBlack;
            var decor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));
            return decor != null ? decor! : SanitaryMaterials.MatteBlack;
        }

        public void SetMaterial(Material material) => Body.SetMaterial(
            SanitaryDecor.ChosenOrFactory(MaterialId, material, SanitaryMaterials.MatteBlack));

        private static Mesh BuildMesh(ShowerColumnSpec spec)
        {
            var builder = new PlumbingMesh(ShowerColumnLayout.BoundsMM(spec).center);
            builder.AddSegments(ShowerColumnLayout.Parts(spec));
            builder.AddTube(ShowerColumnLayout.RiserPath(spec),
                ShowerColumnLayout.RiserRadiusMM(spec));
            builder.AddTube(ShowerColumnLayout.HosePath(spec),
                ShowerColumnLayout.HoseDiameterMM * 0.5f);

            var diverter = ShowerColumnLayout.DiverterBox(spec);
            builder.AddRoundedBox(diverter.CentreMM, diverter.SizeMM, diverter.RadiusMM);

            return builder.Build();
        }
    }
}
