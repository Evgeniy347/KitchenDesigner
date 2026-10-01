using System.Collections.Generic;
using KitchenDesigner.Core.Construction;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FenceElement : KitchenElement, IQuantifies
    {
        public override string DisplayTypeName => Loc.T("elementType.fence");

        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart(
                "профилированный лист симметричен по толщине: у забора нет одной характерной стороны");

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public override bool ParticipatesInGapChecks => false;

        public override Vector2Int DecorSurfaceMM => new Vector2Int(DimensionsMM.x, DimensionsMM.y);

        protected override Vector3 EffectiveScale => FurnitureLayout.PhysicalScale(DimensionsMM);

        public const int DEFAULT_LENGTH_MM = 6000;
        public const int DEFAULT_HEIGHT_MM = 2000;

        [SerializeField] private int _postSectionMm = FenceDefaults.PostSectionMm;
        [SerializeField] private int _postStepMm = FenceDefaults.PostStepMm;
        [SerializeField] private int _pitDepthMm = FenceDefaults.PitDepthMm;
        [SerializeField] private FenceSheetMark _sheetMark = FenceDefaults.SheetMark;

        private LegSet? _posts;

        [Undoable]
        public int PostSectionMm
        {
            get => _postSectionMm;
            set
            {
                _postSectionMm = Mathf.Clamp(value,
                    FenceDefaults.MinPostSectionMm, FenceDefaults.MaxPostSectionMm);
                ApplyDimensions();
            }
        }

        [Undoable]
        public int PostStepMm
        {
            get => _postStepMm;
            set
            {
                _postStepMm = Mathf.Clamp(value,
                    FenceDefaults.MinPostStepMm, FenceDefaults.MaxPostStepMm);
                ApplyDimensions();
            }
        }

        [Undoable]
        public int PitDepthMm
        {
            get => _pitDepthMm;
            set
            {
                _pitDepthMm = Mathf.Clamp(value,
                    FenceDefaults.MinPitDepthMm, FenceDefaults.MaxPitDepthMm);
                ApplyDimensions();
            }
        }

        [Undoable]
        public FenceSheetMark SheetMark
        {
            get => _sheetMark;
            set => _sheetMark = (FenceSheetMark)Mathf.Clamp((int)value,
                0, FenceSheetMarkTitles.All.Length - 1);
        }

        public int RailCount => FenceRailPlan.RailCountFor(DimensionsMM.y);

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements) =>
            FenceSpecItems.Of(DimensionsMM.x, DimensionsMM.y, _postStepMm, _postSectionMm,
                _pitDepthMm, FenceDefaults.SheetWorkingWidthMmOf(_sheetMark),
                FenceSheetMarkTitles.Of(_sheetMark));

        public override void ApplyDimensions()
        {
            transform.localScale = Vector3.one;
            if (SuppressVisualRebuild) return;

            float toU = AppConstants.MM_TO_UNITS;
            float lengthU = DimensionsMM.x * toU;
            float heightU = DimensionsMM.y * toU;
            float sheetU = FenceDefaults.SheetThicknessMm * toU;
            float halfLength = lengthU * 0.5f;
            float halfSheet = sheetU * 0.5f;

            float capU = FenceDefaults.PostCapAboveSheetMm * toU;
            float sheetHeightU = Mathf.Max(Tolerance.EpsilonUnits, heightU - capU);

            var profile = new[]
            {
                new Vector2(-halfLength, -halfSheet),
                new Vector2(halfLength, -halfSheet),
                new Vector2(halfLength, halfSheet),
                new Vector2(-halfLength, halfSheet),
            };

            var mesh = ProfileExtrusionMesh.Build(profile, lengthU, sheetU, sheetHeightU, sheetHeightU * 0.5f);
            AdoptOwnedMesh(mesh);

            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();

            ElementRoot.UseMeshCollider(gameObject, mesh);
            MaterialManager.RefreshTiling(this);

            RebuildPosts(lengthU, heightU, toU);
        }

        private void RebuildPosts(float lengthU, float heightU, float toU)
        {
            var posts = LegSet.For(ref _posts, transform);

            int postCount = FenceQuantities.PostsPerRun(DimensionsMM.x, _postStepMm);
            var footprint = new Vector2[postCount];
            if (postCount == 1)
            {
                footprint[0] = Vector2.zero;
            }
            else if (postCount > 1)
            {
                float step = lengthU / (postCount - 1);
                float halfLength = lengthU * 0.5f;
                for (int i = 0; i < postCount; i++)
                    footprint[i] = new Vector2(-halfLength + i * step, 0f);
            }

            float pitU = _pitDepthMm * toU;
            float postU = _postSectionMm * toU;
            float centreY = (heightU - pitU) * 0.5f;
            var legScale = new Vector3(postU, heightU + pitU, postU);
            posts.Place(footprint, centreY, legScale);
        }

        protected override void OnElementDestroyed()
        {
            _posts?.Destroy();
            _posts = null;
        }
    }
}
