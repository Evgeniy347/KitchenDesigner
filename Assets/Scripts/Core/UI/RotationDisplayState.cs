using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class RotationDisplayState
    {
        private Vector3? _shown;

        public void Forget() => _shown = null;

        public void Remember(Vector3 euler) => _shown = RotationSteps.Normalize(euler);

        public Vector3 For(Quaternion rotation)
        {
            if (_shown is Vector3 shown
                && Quaternion.Angle(Quaternion.Euler(shown), rotation) <= RotationSteps.MatchToleranceDeg)
                return shown;
            var fresh = RotationSteps.Normalize(rotation.eulerAngles);
            _shown = fresh;
            return fresh;
        }
    }
}
