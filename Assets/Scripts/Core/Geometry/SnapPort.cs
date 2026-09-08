using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct SnapPort
    {
        public readonly Vector3 Position;

        public readonly Vector3 Outward;

        public SnapPort(Vector3 position, Vector3 outward)
        {
            Position = position;
            Outward = outward;
        }
    }
}
