using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct DragFrameRepeat
    {
        public const int NoRevision = -1;

        private bool _known;
        private Vector3 _pose;
        private int _revision;
        private bool _snapping;

        public void Forget()
        {
            _known = false;
            _pose = Vector3.zero;
            _revision = NoRevision;
            _snapping = false;
        }

        public bool Repeats(Vector3 pose, int sceneRevision, bool snapping) =>
            _known && _revision == sceneRevision && _snapping == snapping && _pose == pose;

        public void Remember(Vector3 pose, int sceneRevision, bool snapping)
        {
            _known = true;
            _pose = pose;
            _revision = sceneRevision;
            _snapping = snapping;
        }
    }
}
