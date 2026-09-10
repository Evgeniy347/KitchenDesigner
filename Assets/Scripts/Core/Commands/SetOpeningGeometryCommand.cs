using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SetOpeningGeometryCommand : IUndoCommand
    {
        private readonly KitchenElement _opening;
        private readonly Vector3Int _beforeDims, _afterDims;
        private readonly Vector3 _beforePos, _afterPos;
        private readonly Quaternion _beforeRot;
        private readonly string _beforeWall;
        private readonly Wall _afterWall;
        public string Description => $"Set opening geometry {_opening.PartName}";

        public SetOpeningGeometryCommand(KitchenElement opening, Vector3Int dims,
            Vector3 pos, Wall wall)
        {
            _opening = opening; _beforeDims = opening.DimensionsMM; _afterDims = dims;
            _beforePos = opening.transform.position; _afterPos = pos;
            _beforeRot = opening.transform.rotation; _afterWall = wall;
            _beforeWall = Opening?.AttachedWallName ?? "";
        }

        private WallOpeningElement? Opening => _opening as WallOpeningElement;

        public void Execute()
        {
            _opening.DimensionsMM = _afterDims;
            _opening.transform.position = _afterPos;
            Attach(_afterWall);
        }

        public void Undo()
        {
            _opening.DimensionsMM = _beforeDims;
            _opening.transform.SetPositionAndRotation(_beforePos, _beforeRot);
            Wall? oldWall = null;
            foreach (var e in PartRegistry.GetAll())
                if (e != null && e.PartName == _beforeWall) { oldWall = e.GetComponent<Wall>(); break; }
            if (oldWall != null) Attach(oldWall);
            else Opening?.ReleaseHostCutout();
        }

        private void Attach(Wall wall) => Opening?.AttachToWall(wall);
    }
}
