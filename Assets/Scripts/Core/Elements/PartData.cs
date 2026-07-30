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
        // Кромкование по умолчанию включено: наличие кромки на каждом торце
        // считается автоматически по геометрии, и «выключено» здесь означает
        // не «ещё не посчитано», а сознательный отказ от кромки на этой детали.
        [SerializeField] private bool _edgeBanding = true;
        [SerializeField] private float _edgeThicknessMM = AppConstants.EDGE_THICKNESS_DEFAULT_MM;
        [SerializeField] private int _edgeManualMask;

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

        /// <summary>Зазоры одним значением — то, что от детали нужно
        /// <see cref="GappedBox"/>. Ядро геометрии не видит PartData: он тянет
        /// каталог декоров, а ядро обязано исполняться без Unity.</summary>
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

        /// <summary>Клеить ли кромку на открытые торцы детали.</summary>
        public bool EdgeBanding
        {
            get => _edgeBanding;
            set => _edgeBanding = value;
        }

        /// <summary>Толщина кромочной ленты, мм.</summary>
        public float EdgeThicknessMM
        {
            get => _edgeThicknessMM <= 0f ? AppConstants.EDGE_THICKNESS_DEFAULT_MM : _edgeThicknessMM;
            set => _edgeThicknessMM = Mathf.Clamp(value,
                AppConstants.EDGE_THICKNESS_MIN_MM, AppConstants.EDGE_THICKNESS_MAX_MM);
        }

        /// <summary>Стороны, кромку которых пользователь проставил ВРУЧНУЮ
        /// (битовая маска по <see cref="EdgeSide"/>). Обычно кромка выводится из
        /// геометрии — открытый торец кромкуется, закрытый нет. Но геометрия не
        /// знает всего: деталь может стоять вплотную к чему-то, чего в проекте
        /// нет. Ручная сторона перестаёт проверяться на «перекрыт частично».</summary>
        public int EdgeManualMask
        {
            get => _edgeManualMask;
            set => _edgeManualMask = value & EdgeManual.AllMask;
        }

        public bool IsEdgeManual(EdgeSide side) => EdgeManual.Has(_edgeManualMask, side);

        public void SetEdgeManual(EdgeSide side, bool manual) =>
            _edgeManualMask = EdgeManual.With(_edgeManualMask, side, manual);

        /// <summary>Пазы детали. Список живой — правится через KitchenElement,
        /// который пересобирает меш.</summary>
        public List<GrooveSpec> Grooves => _grooves ??= new List<GrooveSpec>();

        /// <summary>Накладки текстур (стена, пол). Список живой — правится через
        /// KitchenElement, который дёргает пересборку накладок в сцене.</summary>
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
