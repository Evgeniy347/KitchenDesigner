using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct ResizeFrameRepeat
    {
        public const int NoRevision = -1;

        private bool _known;
        private float _pointer;
        private bool _snapping;
        private int _revision;
        private Vector3Int _dims;
        private Vector3 _center;

        public void Forget()
        {
            _known = false;
            _pointer = 0f;
            _snapping = false;
            _revision = NoRevision;
            _dims = Vector3Int.zero;
            _center = Vector3.zero;
        }

        public bool Repeats(float pointer, bool snapping, int sceneRevision,
            Vector3Int dims, Vector3 center) =>
            _known && _pointer == pointer && _snapping == snapping
            && _revision == sceneRevision && _dims == dims && _center == center;

        public void Remember(float pointer, bool snapping, int sceneRevision,
            Vector3Int dims, Vector3 center)
        {
            _known = true;
            _pointer = pointer;
            _snapping = snapping;
            _revision = sceneRevision;
            _dims = dims;
            _center = center;
        }
    }
}
