using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [System.Serializable]
    public class PartData
    {
        [SerializeField] private List<GrooveSpec> _grooves = new List<GrooveSpec>();
        [SerializeField] private List<TextureOverlaySpec> _textureOverlays = new List<TextureOverlaySpec>();
        [SerializeField] private string _partName = "Board";
        [SerializeField] private Vector3Int _dimensionsMM = new Vector3Int(800, 400, 18);
        [SerializeField] private bool _movable = true;
        [SerializeField] private int _groupId = 0;
        [SerializeField] private string _materialId = MaterialCatalog.DefaultId;
        [SerializeField] private int _gapLeft;
        [SerializeField] private int _gapRight;
        [SerializeField] private int _gapTop;
        [SerializeField] private int _gapBottom;
        [SerializeField] private int _gapFront;
        [SerializeField] private int _gapBack;
        [SerializeField] private bool _transparent;
        [SerializeField] private string _attachedToName = "";
        [SerializeField] private bool _edgeBanding = true;
        [SerializeField] private float _edgeThicknessMM = AppConstants.EDGE_THICKNESS_DEFAULT_MM;
        [SerializeField] private int _edgeManualMask;
        [SerializeField] private int _edgeSuppressedMask;

        public string PartName
        {
            get => _partName;
            set => _partName = value ?? "Board";
        }

        public Vector3Int DimensionsMM
        {
            get => _dimensionsMM;
            set => _dimensionsMM = ClampDimensions(value);
        }

        public bool Movable
        {
            get => _movable;
            set => _movable = value;
        }

        public int GroupId
        {
            get => _groupId;
            set => _groupId = value;
        }

        public string MaterialId
        {
            get => string.IsNullOrEmpty(_materialId) ? MaterialCatalog.DefaultId : _materialId;
            set => _materialId = string.IsNullOrEmpty(value) ? MaterialCatalog.DefaultId : value;
        }

        public int GapLeft
        {
            get => _gapLeft;
            set => _gapLeft = Mathf.Max(0, value);
        }

        public int GapRight
        {
            get => _gapRight;
            set => _gapRight = Mathf.Max(0, value);
        }

        public int GapTop
        {
            get => _gapTop;
            set => _gapTop = Mathf.Max(0, value);
        }

        public int GapBottom
        {
            get => _gapBottom;
            set => _gapBottom = Mathf.Max(0, value);
        }

        public int GapFront
        {
            get => _gapFront;
            set => _gapFront = Mathf.Max(0, value);
        }

        public int GapBack
        {
            get => _gapBack;
            set => _gapBack = Mathf.Max(0, value);
        }

        public BoxGaps Gaps =>
            new BoxGaps(_gapLeft, _gapRight, _gapTop, _gapBottom, _gapFront, _gapBack);

        public int GapOf(GapSide side) => Gaps.Of(side);

        public void SetGap(GapSide side, int valueMM)
        {
            int v = Mathf.Max(0, valueMM);
            switch (side)
            {
                case GapSide.Left: _gapLeft = v; break;
                case GapSide.Right: _gapRight = v; break;
                case GapSide.Top: _gapTop = v; break;
                case GapSide.Bottom: _gapBottom = v; break;
                case GapSide.Front: _gapFront = v; break;
                default: _gapBack = v; break;
            }
        }

        public bool Transparent
        {
            get => _transparent;
            set => _transparent = value;
        }

        public string AttachedToName
        {
            get => _attachedToName;
            set => _attachedToName = value ?? "";
        }

        public bool EdgeBanding
        {
            get => _edgeBanding;
            set => _edgeBanding = value;
        }

        public float EdgeThicknessMM
        {
            get => _edgeThicknessMM <= 0f ? AppConstants.EDGE_THICKNESS_DEFAULT_MM : _edgeThicknessMM;
            set => _edgeThicknessMM = Mathf.Clamp(value,
                AppConstants.EDGE_THICKNESS_MIN_MM, AppConstants.EDGE_THICKNESS_MAX_MM);
        }

        public int EdgeForcedMask
        {
            get => _edgeManualMask;
            set
            {
                _edgeManualMask = value & EdgeManual.AllMask;
                _edgeSuppressedMask &= ~_edgeManualMask;
            }
        }

        public int EdgeSuppressedMask
        {
            get => _edgeSuppressedMask;
            set
            {
                _edgeSuppressedMask = value & EdgeManual.AllMask;
                _edgeManualMask &= ~_edgeSuppressedMask;
            }
        }

        public EdgeSideState EdgeStateOf(EdgeSide side) =>
            EdgeStates.Of(_edgeManualMask, _edgeSuppressedMask, side);

        public void SetEdgeState(EdgeSide side, EdgeSideState state)
        {
            _edgeManualMask = EdgeStates.ForcedMaskWith(_edgeManualMask, side, state);
            _edgeSuppressedMask = EdgeStates.SuppressedMaskWith(_edgeSuppressedMask, side, state);
        }

        public List<GrooveSpec> Grooves => _grooves ??= new List<GrooveSpec>();

        public List<TextureOverlaySpec> TextureOverlays =>
            _textureOverlays ??= new List<TextureOverlaySpec>();

        public int GapMM =>
            _gapLeft + _gapRight + _gapTop + _gapBottom + _gapFront + _gapBack;

        public static Vector3Int ClampDimensions(Vector3Int dims) => new Vector3Int(
            Mathf.Max(1, dims.x),
            Mathf.Max(1, dims.y),
            Mathf.Max(1, dims.z)
        );

        public override string ToString()
        {
            return $"{_partName} ({_dimensionsMM.x}x{_dimensionsMM.y}x{_dimensionsMM.z}мм)";
        }
    }
}
