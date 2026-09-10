using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SetWallGeometryCommand : IUndoCommand
    {
        private readonly KitchenElement _element;
        private readonly Vector3Int _beforeDims, _afterDims;
        private readonly Vector3 _beforePos, _afterPos;
        private readonly Quaternion _beforeRot, _afterRot;
        private readonly string _beforeKind, _afterKind, _beforeMaterial, _afterMaterial;
        private readonly WallMeshBuilder.EndShape _beforeShape, _afterShape;
        public string Description => $"Set wall geometry {_element.PartName}";

        public SetWallGeometryCommand(KitchenElement element, Vector3Int dims, Vector3 pos,
            Quaternion rot, string kind, string material, WallMeshBuilder.EndShape shape)
        {
            _element = element;
            _beforeDims = element.DimensionsMM; _afterDims = dims;
            _beforePos = element.transform.position; _afterPos = pos;
            _beforeRot = element.transform.rotation; _afterRot = rot;
            var wall = element.GetComponent<Wall>();
            _beforeKind = wall != null ? wall.Kind : ""; _afterKind = kind;
            _beforeShape = wall != null ? wall.EndShape : WallMeshBuilder.EndShape.Square;
            _afterShape = shape;
            _beforeMaterial = element.MaterialId; _afterMaterial = material;
        }

        public void Execute() => Apply(_afterDims, _afterPos, _afterRot, _afterKind, _afterMaterial, _afterShape);
        public void Undo() => Apply(_beforeDims, _beforePos, _beforeRot, _beforeKind, _beforeMaterial, _beforeShape);

        private void Apply(Vector3Int dims, Vector3 pos, Quaternion rot, string kind, string material,
            WallMeshBuilder.EndShape shape)
        {
            _element.DimensionsMM = dims;
            _element.transform.position = pos;
            _element.transform.rotation = rot;
            var wall = _element.GetComponent<Wall>();
            if (wall != null) { wall.LoadBearing = kind == "bearing"; wall.SetEndShape(shape); }
            MaterialManager.ApplyById(_element, material);
        }
    }
}
