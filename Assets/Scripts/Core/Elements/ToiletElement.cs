using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ToiletElement : KitchenElement, IHasTwoDecorSlots, IFixedSizeElement, IStandsOnFloor
    {
        public override string DisplayTypeName => "Унитаз-компакт";

        public const int DefaultWidthMM = ToiletLayout.WidthMM;
        public const int DefaultHeightMM = ToiletLayout.HeightMM;
        public const int DefaultDepthMM = ToiletLayout.DepthMM;
        public const int DefaultSeatHeightMM = ToiletLayout.DefaultSeatHeightMM;

        private FurniturePartSet? _ceramic;
        private FurniturePartSet? _chrome;
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [SerializeField] private int _seatHeightMM = ToiletLayout.DefaultSeatHeightMM;
        [SerializeField] private string _ceramicMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _buttonMaterialId = MaterialCatalog.DefaultId;

        public static Vector3Int ModelDimensionsMM => ToiletLayout.DimensionsMM;

        public static int MinSeatHeightMM => ToiletLayout.MinSeatHeightMM;

        public static int MaxSeatHeightMM => ToiletLayout.MaxSeatHeightMM;

        public bool HasFixedSize => true;

        public override bool CanFollowAnAttachParent => false;

        public int CisternHeightMM => ToiletLayout.CisternHeightMM(_seatHeightMM);

        protected override Vector3 EffectiveScale =>
            FurnitureLayout.PhysicalScale(ToiletLayout.DimensionsMM);

        public override Vector2Int DecorSurfaceMM =>
            FurnitureLayout.TopSurfaceMM(ToiletLayout.DimensionsMM);

        public override MeshRenderer? DecorRenderer => Ceramic.RendererOf(ToiletLayout.BowlName);

        [Undoable]
        public int SeatHeightMM
        {
            get => _seatHeightMM;
            set
            {
                value = ToiletLayout.ClampSeatHeightMM(value);
                if (_seatHeightMM == value) return;
                _seatHeightMM = value;
                ApplyDimensions();
            }
        }

        [NotUndoable(DecorSlots.PrimarySlotReason)]
        public string PrimaryMaterialId
        {
            get => _ceramicMaterialId;
            set { _ceramicMaterialId = DecorSlots.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(DecorSlots.SecondarySlotReason)]
        public string SecondaryMaterialId
        {
            get => _buttonMaterialId;
            set { _buttonMaterialId = DecorSlots.SlotIdOrDefault(value); ApplyMaterial(); }
        }

        [NotUndoable(DecorSlots.MaterialIdAliasReason)]
        public override string MaterialId
        {
            get => PrimaryMaterialId;
            set => PrimaryMaterialId = value;
        }

        public string PrimarySlotLabel => SanitaryDecor.CeramicLabel;

        public string SecondarySlotLabel => SanitaryDecor.ButtonLabel;

        private FurniturePartSet Ceramic => _ceramic ??= new FurniturePartSet(transform);

        private FurniturePartSet Chrome => _chrome ??= new FurniturePartSet(transform);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            _seatHeightMM = ToiletLayout.ClampSeatHeightMM(_seatHeightMM);
            transform.localScale = Vector3.one;
            Data.DimensionsMM = ToiletLayout.DimensionsMM;

            var dims = ToiletLayout.DimensionsMM;
            ApplianceCollider.FitBox(gameObject, new Vector3(dims.x, dims.y, dims.z),
                Vector3.zero);

            if (SuppressVisualRebuild) return;

            Ceramic.Place(ToiletLayout.CeramicParts(_seatHeightMM));
            Chrome.Place(ToiletLayout.ChromeParts());
            ApplyMaterial();
            MaterialManager.RefreshTiling(this);
        }

        public void SeatOnFloor(IReadOnlyList<KitchenElement> scene) =>
            FloorSeating.Seat(this, scene);

        public void SetPrimaryMaterial(Material material) => Ceramic.SetMaterial(
            SanitaryDecor.ChosenOrFactory(_ceramicMaterialId, material, SanitaryMaterials.Ceramic));

        public void SetSecondaryMaterial(Material material) => Chrome.SetMaterial(
            SanitaryDecor.ChosenOrFactory(_buttonMaterialId, material, SanitaryMaterials.Chrome));

        public void SetMaterial(Material material) => DecorSlots.SetBothSlots(this, material);

        private void ApplyMaterial()
            => DecorSlots.ApplyBothSlots(this, _ceramicMaterialId, _buttonMaterialId);

        public override void PrepareForDestruction() => DestroyChildren();

        protected override void OnElementDestroyed() => DestroyChildren();

        public void DestroyChildren()
        {
            _ceramic?.Destroy();
            _chrome?.Destroy();
        }
    }
}
