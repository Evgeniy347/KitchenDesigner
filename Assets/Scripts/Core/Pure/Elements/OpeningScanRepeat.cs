using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct OpeningScanRepeat
    {
        public const int NoRevision = -1;

        private bool _known;
        private int _revision;
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _extents;
        private float _safeProgress;

        public void Forget()
        {
            _known = false;
            _revision = NoRevision;
            _restPosition = Vector3.zero;
            _restRotation = Quaternion.identity;
            _extents = Vector3.zero;
            _safeProgress = 1f;
        }

        public bool Repeats(int sceneRevision, Vector3 restPosition, Quaternion restRotation,
            Vector3 extents) =>
            _known && _revision == sceneRevision && _restPosition == restPosition
            && _restRotation == restRotation && _extents == extents;

        public float SafeProgress => _known ? _safeProgress : 1f;

        public void Remember(int sceneRevision, Vector3 restPosition, Quaternion restRotation,
            Vector3 extents, float safeProgress)
        {
            _known = true;
            _revision = sceneRevision;
            _restPosition = restPosition;
            _restRotation = restRotation;
            _extents = extents;
            _safeProgress = safeProgress;
        }
    }
}
