using UnityEngine;

namespace KitchenDesigner.Core
{
    public class BathtubElement : KitchenElement
    {
        public override string DisplayTypeName => "Ванна";

        public const int DefaultWidthMM = BathtubLayout.DefaultWidthMM;
        public const int DefaultHeightMM = BathtubLayout.DefaultHeightMM;
        public const int DefaultDepthMM = BathtubLayout.DefaultDepthMM;
        public const int DefaultRimWidthMM = BathtubLayout.DefaultRimWidthMM;
        public const int DefaultBowlRadiusMM = BathtubLayout.DefaultBowlRadiusMM;
        public const int DefaultBowlDepthMM = BathtubLayout.DefaultBowlDepthMM;
        public const int DefaultBowlFilletMM = BathtubLayout.DefaultBowlFilletMM;

        private OwnedMeshBody? _body;
        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [SerializeField] private int _rimWidthMM = DefaultRimWidthMM;
        [SerializeField] private int _bowlRadiusMM = DefaultBowlRadiusMM;
        [SerializeField] private int _bowlDepthMM = DefaultBowlDepthMM;
        [SerializeField] private int _bowlFilletMM = DefaultBowlFilletMM;

        public static int MaxRimWidthMM(Vector3Int dimensionsMM)
            => BathtubLayout.MaxRimWidthMM(dimensionsMM);

        public static int MaxBowlRadiusMM(Vector3Int dimensionsMM, int rimWidthMM)
            => BathtubLayout.MaxBowlRadiusMM(dimensionsMM, rimWidthMM);

        public static int MaxBowlDepthMM(Vector3Int dimensionsMM)
            => BathtubLayout.MaxBowlDepthMM(dimensionsMM);

        public static int MaxBowlFilletMM(Vector3Int dimensionsMM, int rimWidthMM,
            int bowlDepthMM)
            => BathtubLayout.MaxBowlFilletMM(dimensionsMM, rimWidthMM, bowlDepthMM);

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public override Vector2Int DecorSurfaceMM => FurnitureLayout.TopSurfaceMM(DimensionsMM);

        public override MeshRenderer? DecorRenderer => GetComponent<MeshRenderer>();

        [Undoable]
        public int RimWidthMM
        {
            get => _rimWidthMM;
            set
            {
                value = BathtubLayout.ClampRimWidthMM(DimensionsMM, value);
                if (_rimWidthMM == value) return;
                _rimWidthMM = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public int BowlRadiusMM
        {
            get => _bowlRadiusMM;
            set
            {
                value = BathtubLayout.ClampBowlRadiusMM(DimensionsMM, _rimWidthMM, value);
                if (_bowlRadiusMM == value) return;
                _bowlRadiusMM = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public int BowlDepthMM
        {
            get => _bowlDepthMM;
            set
            {
                value = BathtubLayout.ClampBowlDepthMM(DimensionsMM, value);
                if (_bowlDepthMM == value) return;
                _bowlDepthMM = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public int BowlFilletMM
        {
            get => _bowlFilletMM;
            set
            {
                value = BathtubLayout.ClampBowlFilletMM(
                    DimensionsMM, _rimWidthMM, _bowlDepthMM, value);
                if (_bowlFilletMM == value) return;
                _bowlFilletMM = value;
                ApplyDimensions();
            }
        }

        private OwnedMeshBody Body => _body ??= new OwnedMeshBody(gameObject, AdoptOwnedMesh);

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void Rebuild()
        {
            var dims = DimensionsMM;
            _rimWidthMM = BathtubLayout.ClampRimWidthMM(dims, _rimWidthMM);
            _bowlRadiusMM = BathtubLayout.ClampBowlRadiusMM(dims, _rimWidthMM, _bowlRadiusMM);
            _bowlDepthMM = BathtubLayout.ClampBowlDepthMM(dims, _bowlDepthMM);
            _bowlFilletMM = BathtubLayout.ClampBowlFilletMM(
                dims, _rimWidthMM, _bowlDepthMM, _bowlFilletMM);

            transform.localScale = Vector3.one;
            Body.SetMaterial(Skin());
            Body.Rebuild(BasinMesh.Build(Shell(dims)));
            MaterialManager.RefreshTiling(this);
        }

        private Material Skin()
        {
            if (SanitaryDecor.IsFactoryLook(MaterialId)) return SanitaryMaterials.WhiteAcrylic;
            var decor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(MaterialId));
            return decor != null ? decor! : SanitaryMaterials.WhiteAcrylic;
        }

        private BasinSurface Shell(Vector3Int dims)
        {
            float toU = AppConstants.MM_TO_UNITS;
            int shellRadiusMM =
                BathtubLayout.ShellCornerRadiusMM(dims, _rimWidthMM, _bowlRadiusMM);

            return new BasinSurface(dims.x * toU, dims.z * toU, dims.y * toU,
                CornerRadii.Uniform(shellRadiusMM * toU),
                _rimWidthMM * toU, _bowlDepthMM * toU, _bowlFilletMM * toU);
        }
    }
}
