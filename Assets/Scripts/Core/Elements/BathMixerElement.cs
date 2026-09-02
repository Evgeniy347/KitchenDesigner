using UnityEngine;

namespace KitchenDesigner.Core
{
    public class BathMixerElement : KitchenElement, IWallMounted, IFixedSizeElement
    {
        public override string DisplayTypeName => "Смеситель для ванны";

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public bool HasFixedSize => true;

        [SerializeField] private int _centresMM = BathMixerSpec.DefaultCentresMM;
        [SerializeField] private int _bodyLengthMM = BathMixerSpec.DefaultBodyLengthMM;
        [SerializeField] private int _bodyDiameterMM = BathMixerSpec.DefaultBodyDiameterMM;
        [SerializeField] private int _escutcheonReachMM = BathMixerSpec.DefaultEscutcheonReachMM;
        [SerializeField] private int _spoutLengthMM = BathMixerSpec.DefaultSpoutLengthMM;
        [SerializeField] private int _outletDiameterMM = BathMixerSpec.DefaultOutletDiameterMM;

        private readonly RebuildGuard _rebuild = new RebuildGuard();

        public BathMixerSpec Spec => BathMixerSpec.Clamped(_centresMM, _bodyLengthMM,
            _bodyDiameterMM, _escutcheonReachMM, _spoutLengthMM, _outletDiameterMM);

        [Undoable]
        public int CentresMM
        {
            get => _centresMM;
            set => Set(ref _centresMM,
                BathMixerSpec.ClampCentresMM(value, _bodyLengthMM, _bodyDiameterMM));
        }

        [Undoable]
        public int BodyLengthMM
        {
            get => _bodyLengthMM;
            set => Set(ref _bodyLengthMM,
                BathMixerSpec.ClampBodyLengthMM(value, _bodyDiameterMM));
        }

        [Undoable]
        public int BodyDiameterMM
        {
            get => _bodyDiameterMM;
            set => Set(ref _bodyDiameterMM, BathMixerSpec.ClampBodyDiameterMM(value));
        }

        [Undoable]
        public int EscutcheonReachMM
        {
            get => _escutcheonReachMM;
            set => Set(ref _escutcheonReachMM, BathMixerSpec.ClampEscutcheonReachMM(value));
        }

        [Undoable]
        public int SpoutLengthMM
        {
            get => _spoutLengthMM;
            set => Set(ref _spoutLengthMM, BathMixerSpec.ClampSpoutLengthMM(value));
        }

        [Undoable]
        public int OutletDiameterMM
        {
            get => _outletDiameterMM;
            set => Set(ref _outletDiameterMM, BathMixerSpec.ClampOutletDiameterMM(value));
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
            _centresMM = spec.CentresMM;
            _bodyLengthMM = spec.BodyLengthMM;
            _bodyDiameterMM = spec.BodyDiameterMM;
            _escutcheonReachMM = spec.EscutcheonReachMM;
            _spoutLengthMM = spec.SpoutLengthMM;
            _outletDiameterMM = spec.OutletDiameterMM;

            Data.DimensionsMM = BathMixerLayout.DimensionsMM(spec);
            transform.localScale = Vector3.one;
            if (SuppressVisualRebuild) return;

            var builder = new PlumbingMesh(BathMixerLayout.BoundsMM(spec).center);
            builder.AddSegments(BathMixerLayout.Parts(spec));
            var mesh = builder.Build();
            AdoptOwnedMesh(mesh);

            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PlumbingMaterials.Chrome;

            ElementRoot.UseMeshCollider(gameObject, mesh);
        }
    }
}
