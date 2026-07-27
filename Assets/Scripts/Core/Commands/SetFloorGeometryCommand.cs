using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SetFloorGeometryCommand : IUndoCommand
    {
        private readonly FloorElement _floor;
        private readonly Vector3Int _beforeDims, _afterDims;
        private readonly Vector3 _beforePos, _afterPos;
        private readonly List<Vector2Int> _beforePoly, _afterPoly;

        public string Description => $"Set floor geometry {_floor.PartName}";

        public SetFloorGeometryCommand(FloorElement floor, Vector3Int afterDims,
            Vector3 afterPos, IReadOnlyList<Vector2Int> afterPoly)
        {
            _floor = floor;
            _beforeDims = floor.DimensionsMM;
            _beforePos = floor.transform.position;
            _beforePoly = new List<Vector2Int>(floor.PolygonLocalMm);
            _afterDims = afterDims;
            _afterPos = afterPos;
            _afterPoly = new List<Vector2Int>(afterPoly);
        }

        public void Execute() => Apply(_afterDims, _afterPos, _afterPoly);
        public void Undo() => Apply(_beforeDims, _beforePos, _beforePoly);

        private void Apply(Vector3Int dims, Vector3 pos, IReadOnlyList<Vector2Int> poly)
        {
            _floor.DimensionsMM = dims;
            _floor.transform.position = pos;
            _floor.SetPolygonLocalMm(poly);
        }
    }
}
