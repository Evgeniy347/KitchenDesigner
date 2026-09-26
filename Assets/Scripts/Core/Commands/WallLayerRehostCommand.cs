using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class WallLayerRehostCommand : IUndoCommand
    {
        private readonly WallLayerElement _layer;
        private readonly string _hostBefore;
        private readonly string _hostAfter;
        private readonly Vector3 _posBefore;
        private readonly Vector3 _posAfter;
        private readonly Quaternion _rotBefore;
        private readonly Quaternion _rotAfter;
        private readonly Vector3Int _dimsBefore;
        private readonly Vector3Int _dimsAfter;

        public string Description => $"Перепривязка {_layer.PartName}";

        public WallLayerRehostCommand(WallLayerElement layer,
            string hostBefore, string hostAfter,
            Vector3 posBefore, Vector3 posAfter,
            Quaternion rotBefore, Quaternion rotAfter,
            Vector3Int dimsBefore, Vector3Int dimsAfter)
        {
            _layer = layer;
            _hostBefore = hostBefore;
            _hostAfter = hostAfter;
            _posBefore = posBefore;
            _posAfter = posAfter;
            _rotBefore = rotBefore;
            _rotAfter = rotAfter;
            _dimsBefore = dimsBefore;
            _dimsAfter = dimsAfter;
        }

        public void Execute()
        {
            _layer.HostWallName = _hostAfter;
            _layer.transform.SetPositionAndRotation(_posAfter, _rotAfter);
            _layer.DimensionsMM = _dimsAfter;
        }

        public void Undo()
        {
            _layer.HostWallName = _hostBefore;
            _layer.transform.SetPositionAndRotation(_posBefore, _rotBefore);
            _layer.DimensionsMM = _dimsBefore;
        }
    }
}
