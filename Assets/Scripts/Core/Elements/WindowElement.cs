using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum GlassTint { Clear = 0, Tinted = 1 }

    public class WindowElement : WallOpeningElement, IQuantifies
    {
        public override string DisplayTypeName => "Окно";

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return PurchasedGoodsSpecItems.Piece(DisplayTypeName, DimensionsMM);
        }

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        [SerializeField] private GlassTint _tint = GlassTint.Clear;
        [SerializeField] private int _sillProtrusionMM = AppConstants.WINDOW_SILL_DEFAULT_MM;

        private static Shader? _cachedShader;
        private static Material? _tintedGlassMat;
        private static Material? _clearGlassMat;
        private static Material? _slopeMat;

        private GameObject? _frameTop, _frameBottom, _frameLeft, _frameRight;
        private GameObject? _glassPane;
        private GameObject? _sillObj;
        private GameObject? _dripObj;
        private GameObject? _slopeTop, _slopeBottom, _slopeLeft, _slopeRight;
        private GameObject? _sashLeft, _sashRight, _sashTop, _sashBottom;

        [Undoable]
        public GlassTint Tint
        {
            get => _tint;
            set { _tint = value; ApplyTint(); }
        }

        [Undoable]
        public int SillProtrusionMM
        {
            get => _sillProtrusionMM;
            set { _sillProtrusionMM = Mathf.Clamp(value, 0, 200); ApplyDimensions(); }
        }

        protected override ProfilerMarker SnapToWallMarker => PerfMarkers.WindowSnapToWall;

        protected override void RegisterOnWall(Wall wall) => wall.RegisterWindow(this);
        protected override void UnregisterOnWall(Wall wall) => wall.UnregisterWindow(this);
        protected override bool IsRegisteredOn(Wall wall) => wall.HasWindow(this);

        protected override float AlignedY(int targetY, float toU, float wallHalfH, float wallCenterY)
        {
            float targetHalfH = targetY * toU * 0.5f;
            return Mathf.Clamp(transform.position.y,
                wallCenterY - wallHalfH + targetHalfH,
                wallCenterY + wallHalfH - targetHalfH);
        }

        protected override void EnsureChildren()
        {
            EnsureChildGroups();
            EnsureChildCount(15, GetChildName, IsSashChild);

            _frameLeft   = Children[0];
            _frameRight  = Children[1];
            _frameTop    = Children[2];
            _frameBottom = Children[3];
            _glassPane   = Children[4];
            _sillObj     = Children[5];
            _dripObj     = Children[6];
            _slopeTop    = Children[7];
            _slopeBottom = Children[8];
            _slopeLeft   = Children[9];
            _slopeRight  = Children[10];
            _sashLeft    = Children[11];
            _sashRight   = Children[12];
            _sashTop     = Children[13];
            _sashBottom  = Children[14];
        }

        private static bool IsSashChild(int idx) => idx == 4 || idx >= 11;

        private static string GetChildName(int idx) => idx switch
        {
            0 => "FrameLeft", 1 => "FrameRight", 2 => "FrameTop", 3 => "FrameBottom",
            4 => "Glass", 5 => "Sill", 6 => "DripCap",
            7 => "SlopeTop", 8 => "SlopeBottom", 9 => "SlopeLeft", 10 => "SlopeRight",
            11 => "SashLeft", 12 => "SashRight", 13 => "SashTop", 14 => "SashBottom",
            _ => "Child" + idx
        };

        protected override void RebuildGeometry()
        {
            if (StaticGroup == null || SashGroup == null) return;
            var dims = DimensionsMM;
            float toU = AppConstants.MM_TO_UNITS;
            float frameU = AppConstants.WINDOW_FRAME_MM * toU;
            float glassThick = AppConstants.GLASS_THICKNESS_MM * toU;

            float totalW = dims.x * toU;
            float totalH = dims.y * toU;
            float totalD = dims.z * toU;
            float halfW = totalW * 0.5f;
            float halfH = totalH * 0.5f;
            float halfD = totalD * 0.5f;

            float innerW = totalW - 2f * frameU;
            float innerH = totalH - 2f * frameU;

            int hidden = ComputeHiddenSides();

            float slopeU = AppConstants.WINDOW_SLOPE_MM * toU;
            float frameD = totalD - slopeU;
            float frameCZ = halfD - frameD * 0.5f;

            if (_frameLeft != null)
            {
                _frameLeft.transform.localPosition = new Vector3(-halfW + frameU * 0.5f, 0f, frameCZ);
                _frameLeft.transform.localScale = new Vector3(frameU, totalH, frameD);
                _frameLeft.SetActive((hidden & 1) == 0);
            }
            if (_frameRight != null)
            {
                _frameRight.transform.localPosition = new Vector3(halfW - frameU * 0.5f, 0f, frameCZ);
                _frameRight.transform.localScale = new Vector3(frameU, totalH, frameD);
                _frameRight.SetActive((hidden & 2) == 0);
            }
            if (_frameTop != null)
            {
                _frameTop.transform.localPosition = new Vector3(0f, halfH - frameU * 0.5f, frameCZ);
                _frameTop.transform.localScale = new Vector3(innerW, frameU, frameD);
                _frameTop.SetActive((hidden & 4) == 0);
            }
            if (_frameBottom != null)
            {
                _frameBottom.transform.localPosition = new Vector3(0f, -halfH + frameU * 0.5f, frameCZ);
                _frameBottom.transform.localScale = new Vector3(innerW, frameU, frameD);
                _frameBottom.SetActive((hidden & 8) == 0);
            }

            float sashU = AppConstants.WINDOW_SASH_MM * toU;
            float sashD = Mathf.Min(AppConstants.WINDOW_SASH_DEPTH_MM * toU, totalD);
            SashClosedLocal = new Vector3(0f, 0f, 0f);
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
                _glassPane.transform.localScale = new Vector3(innerW - 2f * sashU, innerH - 2f * sashU, glassThick);
                _glassPane.SetActive(true);
            }

            if (_sillObj != null)
            {
                float sillProt = _sillProtrusionMM * toU;
                float sillThick = AppConstants.WINDOW_SILL_THICKNESS_MM * toU;
                _sillObj.transform.localPosition = new Vector3(
                    0f, -halfH + frameU - sillThick * 0.5f, halfD + sillProt * 0.5f);
                _sillObj.transform.localScale = new Vector3(totalW, sillThick, sillProt);
                _sillObj.SetActive(_sillProtrusionMM > 0);
            }
            if (_dripObj != null)
            {
                float dripH = AppConstants.WINDOW_DRIP_DEFAULT_MM * toU;
                float dripProtr = 30f * toU;
                _dripObj.transform.localPosition = new Vector3(0f, -halfH + frameU * 0.5f, -halfD - dripProtr * 0.5f);
                _dripObj.transform.localScale = new Vector3(totalW, dripH, dripProtr);
                _dripObj.SetActive(true);
            }
            if (_slopeTop != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeTop.transform.localPosition = new Vector3(0f, halfH - frameU * 0.5f, -halfD + slopeT * 0.5f);
                _slopeTop.transform.localScale = new Vector3(innerW, slopeT, slopeT);
                _slopeTop.SetActive((hidden & 4) == 0);
            }
            if (_slopeBottom != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeBottom.transform.localPosition = new Vector3(0f, -halfH + frameU * 0.5f, -halfD + slopeT * 0.5f);
                _slopeBottom.transform.localScale = new Vector3(innerW, slopeT, slopeT);
                _slopeBottom.SetActive((hidden & 8) == 0);
            }
            if (_slopeLeft != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeLeft.transform.localPosition = new Vector3(-halfW + frameU * 0.5f, 0f, -halfD + slopeT * 0.5f);
                _slopeLeft.transform.localScale = new Vector3(slopeT, innerH, slopeT);
                _slopeLeft.SetActive((hidden & 1) == 0);
            }
            if (_slopeRight != null)
            {
                float slopeT = AppConstants.WINDOW_SLOPE_MM * toU;
                _slopeRight.transform.localPosition = new Vector3(halfW - frameU * 0.5f, 0f, -halfD + slopeT * 0.5f);
                _slopeRight.transform.localScale = new Vector3(slopeT, innerH, slopeT);
                _slopeRight.SetActive((hidden & 2) == 0);
            }

            ApplyDoorPose();
            ApplyTint();
            ApplyMaterialFrame();
        }

        private static Shader GetShader()
        {
            if (_cachedShader == null)
                _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
            return _cachedShader;
        }

        private void ApplyTint()
        {
            if (_glassPane == null) return;
            var mr = _glassPane.GetComponent<MeshRenderer>();
            if (mr == null) return;

            if (_tint == GlassTint.Tinted)
            {
                if (_tintedGlassMat == null)
                    _tintedGlassMat = ElementHighlighter.MakeTransparent(GetShader(), new Color(0.15f, 0.18f, 0.22f, 0.70f));
                mr.sharedMaterial = _tintedGlassMat;
            }
            else
            {
                if (_clearGlassMat == null)
                    _clearGlassMat = ElementHighlighter.MakeTransparent(GetShader(), new Color(0.6f, 0.75f, 0.85f, 0.35f));
                mr.sharedMaterial = _clearGlassMat;
            }
        }

        private static Material SlopeMaterial()
        {
            if (_slopeMat == null)
            {
                _slopeMat = new Material(GetShader());
                _slopeMat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.82f, 1f));
                _slopeMat.color = new Color(0.85f, 0.85f, 0.82f, 1f);
            }
            return _slopeMat;
        }

        protected override void PaintFrame(Material material)
        {
            foreach (var go in new[] { _frameLeft, _frameRight, _frameTop, _frameBottom,
                                       _sashLeft, _sashRight, _sashTop, _sashBottom,
                                       _slopeTop, _slopeBottom, _slopeLeft, _slopeRight })
            {
                var mr = go != null ? go.GetComponent<MeshRenderer>() : null;
                if (mr != null) mr.sharedMaterial = material;
            }
            var slopeMat = SlopeMaterial();
            foreach (var go in new[] { _sillObj, _dripObj })
            {
                var mr = go != null ? go.GetComponent<MeshRenderer>() : null;
                if (mr != null) mr.sharedMaterial = slopeMat;
            }
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
            _frameLeft = _frameRight = _frameTop = _frameBottom = null;
            _glassPane = _sillObj = _dripObj = null;
            _slopeTop = _slopeBottom = _slopeLeft = _slopeRight = null;
            _sashLeft = _sashRight = _sashTop = _sashBottom = null;
        }
    }
}
