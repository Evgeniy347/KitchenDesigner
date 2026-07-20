using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [System.Serializable]
    public class PartData
    {
        [SerializeField] private List<GrooveSpec> _grooves = new List<GrooveSpec>();
        [SerializeField] private string _partName = "Board";
        [SerializeField] private Vector3Int _dimensionsMM = new Vector3Int(800, 400, 18);
        [SerializeField] private bool _movable = true;
        [SerializeField] private int _groupId = 0;
        [SerializeField] private string _materialId = MaterialCatalog.DefaultId;
        [SerializeField] private int _gapLeft;
        [SerializeField] private int _gapRight;
        [SerializeField] private int _gapTop;
        [SerializeField] private int _gapBottom;
        [SerializeField] private bool _transparent;

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

        public bool Transparent
        {
            get => _transparent;
            set => _transparent = value;
        }

        /// <summary>Пазы детали. Список живой — правится через KitchenElement,
        /// который пересобирает меш.</summary>
        public List<GrooveSpec> Grooves => _grooves ??= new List<GrooveSpec>();

        public int GapMM => _gapLeft + _gapRight + _gapTop + _gapBottom;

        public bool IsFacade => GapMM > 0 || _gapLeft > 0 || _gapRight > 0 || _gapTop > 0 || _gapBottom > 0;

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
