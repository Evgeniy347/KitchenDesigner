using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Ventilation;

namespace KitchenDesigner.Core
{
    public class DuctElement : KitchenElement, IQuantifies
    {
        public override string DisplayTypeName => Loc.T("elementType.duct");

        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("воздуховод — тело без выделенной грани");

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public override bool ParticipatesInGapChecks => false;

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public const int DEFAULT_LENGTH_MM = DuctDefaults.DefaultLengthMm;

        [SerializeField] private DuctProfileKind _profileKind = DuctProfileKind.Round;
        [SerializeField] private int _diameterMm = DuctDefaults.DefaultRoundDiameterMm;
        [SerializeField] private int _rectWidthMm = DuctDefaults.DefaultRectWidthMm;
        [SerializeField] private int _rectHeightMm = DuctDefaults.DefaultRectHeightMm;
        [SerializeField] private int _airflowM3PerHour = DuctDefaults.DefaultAirflowM3PerHour;

        private readonly RebuildGuard _rebuild = new RebuildGuard();

        [Undoable]
        public DuctProfileKind ProfileKind
        {
            get => _profileKind;
            set
            {
                _profileKind = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public int DiameterMm
        {
            get => _diameterMm;
            set
            {
                _diameterMm = Mathf.Clamp(value, DuctProfile.MinRoundDiameterMm,
                    DuctProfile.MaxRoundDiameterMm);
                ApplyDimensions();
            }
        }

        [Undoable]
        public int RectWidthMm
        {
            get => _rectWidthMm;
            set
            {
                _rectWidthMm = Mathf.Max(1, value);
                ApplyDimensions();
            }
        }

        [Undoable]
        public int RectHeightMm
        {
            get => _rectHeightMm;
            set
            {
                _rectHeightMm = Mathf.Max(1, value);
                ApplyDimensions();
            }
        }

        [Undoable]
        public int AirflowM3PerHour
        {
            get => _airflowM3PerHour;
            set => _airflowM3PerHour = Mathf.Clamp(value, DuctDefaults.MinAirflowM3PerHour,
                DuctDefaults.MaxAirflowM3PerHour);
        }

        public int LengthMm => DimensionsMM.y;

        public Vector3 EndAUnits => transform.position - HalfRunUnits;

        public Vector3 EndBUnits => transform.position + HalfRunUnits;

        public Vector3 RunAxis => (transform.rotation * Vector3.up).normalized;

        private Vector3 HalfRunUnits =>
            (transform.rotation * Vector3.up) * (LengthMm * 0.5f * AppConstants.MM_TO_UNITS);

        public DuctProfile Profile => _profileKind == DuctProfileKind.Round
            ? DuctProfile.Round(_diameterMm)
            : DuctProfile.Rect(_rectWidthMm, _rectHeightMm);

        public override Vector2Int DecorSurfaceMM => _profileKind == DuctProfileKind.Round
            ? new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(_diameterMm * Mathf.PI)), LengthMm)
            : new Vector2Int(Mathf.Max(1, 2 * (_rectWidthMm + _rectHeightMm)), LengthMm);

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return DuctSpecItems.DuctLine(Profile, LengthMm);
        }

        public override void ApplyDimensions() => _rebuild.Run(Rebuild);

        private void SyncDimensions()
        {
            int sectionX = _profileKind == DuctProfileKind.Round ? _diameterMm : _rectWidthMm;
            int sectionZ = _profileKind == DuctProfileKind.Round ? _diameterMm : _rectHeightMm;
            Data.DimensionsMM = new Vector3Int(sectionX, Data.DimensionsMM.y, sectionZ);
        }

        private void Rebuild()
        {
            SyncDimensions();

            transform.localScale = Vector3.one;
            if (SuppressVisualRebuild) return;

            float toU = AppConstants.MM_TO_UNITS;
            int lengthMm = LengthMm;
            float halfLenU = lengthMm * 0.5f * toU;

            Mesh mesh;
            if (_profileKind == DuctProfileKind.Round)
            {
                mesh = PipeMesh.Build(_diameterMm * 0.5f * toU, lengthMm * toU);
            }
            else
            {
                var (vertices, triangles) = BoxRunMesh.Build(
                    new Vector3(0f, -halfLenU, 0f), new Vector3(0f, halfLenU, 0f),
                    Vector3.right, _rectWidthMm * toU, _rectHeightMm * toU);
                mesh = new Mesh { vertices = vertices, triangles = triangles };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }

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
