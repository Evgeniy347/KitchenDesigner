using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum DoorSashType { Glass = 0, Blind = 1 }

    public class DoorElement : WallOpeningElement, IQuantifies
    {
        public override string DisplayTypeName => "Дверь";

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return PurchasedGoodsSpecItems.Piece(DisplayTypeName, DimensionsMM);
        }

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.AlignsCutout;

        [SerializeField] private DoorSashType _sashType = DoorSashType.Glass;

        private static Shader? _cachedShader;
        private static Material? _glassMat;

        private GameObject? _frameTop, _frameLeft, _frameRight;
        private GameObject? _glassPane;
        private GameObject? _sashLeft, _sashRight, _sashTop, _sashBottom;

        [Undoable]
        public DoorSashType SashType
        {
            get => _sashType;
            set { _sashType = value; ApplySashType(); }
        }

        protected override PerfMarker SnapToWallMarker => PerfMarkers.DoorSnapToWall;

        protected override void RegisterOnWall(Wall wall) => wall.RegisterDoor(this);
        protected override void UnregisterOnWall(Wall wall) => wall.UnregisterDoor(this);
        protected override bool IsRegisteredOn(Wall wall) => wall.HasDoor(this);

        protected override float AlignedY(int targetY, float toU, float wallHalfH, float wallCenterY) =>
            wallCenterY - wallHalfH + DoorOpeningLayout.CentreAboveWallBaseMM(targetY) * toU;

        protected override void EnsureChildren()
        {
            EnsureChildGroups();
            EnsureChildCount(8, GetChildName, IsSashChild);

            _frameLeft   = Children[0];
            _frameRight  = Children[1];
            _frameTop    = Children[2];
            _glassPane   = Children[3];
            _sashLeft    = Children[4];
            _sashRight   = Children[5];
            _sashTop     = Children[6];
            _sashBottom  = Children[7];
        }

        private static bool IsSashChild(int idx) => idx >= 3;

        private static string GetChildName(int idx) => idx switch
        {
            0 => "FrameLeft", 1 => "FrameRight", 2 => "FrameTop",
            3 => "Glass",
            4 => "SashLeft", 5 => "SashRight", 6 => "SashTop", 7 => "SashBottom",
            _ => "Child" + idx
        };

        protected override void RebuildGeometry()
        {
            if (StaticGroup == null || SashGroup == null) return;
            var dims = DimensionsMM;
            float toU = AppConstants.MM_TO_UNITS;
            float frameU = AppConstants.WINDOW_FRAME_MM * toU;

            float totalW = dims.x * toU;
            float totalD = dims.z * toU;
            float leafH = DoorOpeningLayout.LeafHeightMM(dims.y) * toU;
            float leafY = DoorOpeningLayout.LeafCentreOffsetMM(dims.y) * toU;
            float halfW = totalW * 0.5f;
            float halfLeafH = leafH * 0.5f;

            float innerW = totalW - 2f * frameU;
            float innerH = leafH - frameU;
            float innerY = leafY - frameU * 0.5f;

            int hidden = ComputeHiddenSides();

            if (_frameLeft != null)
            {
                _frameLeft.transform.localPosition = new Vector3(-halfW + frameU * 0.5f, leafY, 0f);
                _frameLeft.transform.localScale = new Vector3(frameU, leafH, totalD);
                _frameLeft.SetActive((hidden & 1) == 0);
            }
            if (_frameRight != null)
            {
                _frameRight.transform.localPosition = new Vector3(halfW - frameU * 0.5f, leafY, 0f);
                _frameRight.transform.localScale = new Vector3(frameU, leafH, totalD);
                _frameRight.SetActive((hidden & 2) == 0);
            }
            if (_frameTop != null)
            {
                _frameTop.transform.localPosition = new Vector3(0f, leafY + halfLeafH - frameU * 0.5f, 0f);
                _frameTop.transform.localScale = new Vector3(innerW, frameU, totalD);
                _frameTop.SetActive((hidden & 4) == 0);
            }

            float sashU = AppConstants.WINDOW_SASH_MM * toU;
            float sashD = Mathf.Min(AppConstants.WINDOW_SASH_DEPTH_MM * toU, totalD);
            SashClosedLocal = new Vector3(0f, innerY, 0f);
            SashHalfExtents = new Vector3(innerW * 0.5f, innerH * 0.5f, sashD * 0.5f);

            if (_sashLeft != null)
            {
                _sashLeft.transform.localPosition = new Vector3(-innerW * 0.5f + sashU * 0.5f, 0f, 0f);
                _sashLeft.transform.localScale = new Vector3(sashU, innerH, sashD);
                _sashLeft.SetActive(true);
            }
            if (_sashRight != null)
            {
                _sashRight.transform.localPosition = new Vector3(innerW * 0.5f - sashU * 0.5f, 0f, 0f);
                _sashRight.transform.localScale = new Vector3(sashU, innerH, sashD);
                _sashRight.SetActive(true);
            }
            if (_sashTop != null)
            {
                _sashTop.transform.localPosition = new Vector3(0f, innerH * 0.5f - sashU * 0.5f, 0f);
                _sashTop.transform.localScale = new Vector3(innerW - 2f * sashU, sashU, sashD);
                _sashTop.SetActive(true);
            }
            if (_sashBottom != null)
            {
                _sashBottom.transform.localPosition = new Vector3(0f, -innerH * 0.5f + sashU * 0.5f, 0f);
                _sashBottom.transform.localScale = new Vector3(innerW - 2f * sashU, sashU, sashD);
                _sashBottom.SetActive(true);
            }
            if (_glassPane != null)
            {
                _glassPane.transform.localPosition = Vector3.zero;
                _glassPane.SetActive(true);
            }

            ApplyDoorPose();
            ApplySashType();
            ApplyMaterialFrame();
        }

        private static Shader GetShader()
        {
            if (_cachedShader == null)
                _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
            return _cachedShader;
        }

        private void ApplySashType()
        {
            if (_glassPane == null) return;
            var mr = _glassPane.GetComponent<MeshRenderer>();
            if (mr == null) return;

            if (_sashType == DoorSashType.Blind)
            {
                var def = MaterialCatalog.Get(MaterialId);
                if (def != null)
                {
                    var mat = MaterialManager.GetSharedMaterial(def);
                    if (mat != null) mr.sharedMaterial = mat;
                }
            }
            else
            {
                if (_glassMat == null)
                    _glassMat = ElementHighlighter.MakeTransparent(GetShader(), new Color(0.6f, 0.75f, 0.85f, 0.35f));
                mr.sharedMaterial = _glassMat;
            }

            ResizePaneForSashType();
        }

        private void ResizePaneForSashType()
        {
            if (_glassPane == null) return;
            var dims = DimensionsMM;
            float toU = AppConstants.MM_TO_UNITS;
            float frameU = AppConstants.WINDOW_FRAME_MM * toU;
            float sashU = AppConstants.WINDOW_SASH_MM * toU;
            float sashD = Mathf.Min(AppConstants.WINDOW_SASH_DEPTH_MM * toU, dims.z * toU);
            float glassThick = AppConstants.GLASS_THICKNESS_MM * toU;
            float innerW = dims.x * toU - 2f * frameU;
            float innerH = DoorOpeningLayout.LeafHeightMM(dims.y) * toU - frameU;
            float paneThick = _sashType == DoorSashType.Blind ? sashD : glassThick;
            _glassPane.transform.localScale =
                new Vector3(innerW - 2f * sashU, innerH - 2f * sashU, paneThick);
        }

        protected override void PaintFrame(Material material)
        {
            foreach (var go in new[] { _frameLeft, _frameRight, _frameTop,
                                       _sashLeft, _sashRight, _sashTop, _sashBottom })
            {
                var mr = go != null ? go.GetComponent<MeshRenderer>() : null;
                if (mr != null) mr.sharedMaterial = material;
            }

            if (_sashType != DoorSashType.Blind || _glassPane == null) return;
            var pane = _glassPane.GetComponent<MeshRenderer>();
            if (pane != null) pane.sharedMaterial = material;
        }

        protected override void DestroyChildren()
        {
            foreach (var child in Children)
                if (child != null)
                {
                    if (Application.isPlaying) Object.Destroy(child);
                    else Object.DestroyImmediate(child);
                }
            Children.Clear();
            _frameLeft = _frameRight = _frameTop = null;
            _glassPane = null;
            _sashLeft = _sashRight = _sashTop = _sashBottom = null;
        }
    }
}
