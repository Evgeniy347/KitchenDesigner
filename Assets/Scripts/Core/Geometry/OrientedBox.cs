using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct OrientedBox
    {
        public readonly Vector3 Center;
        public readonly Quaternion Rotation;
        public readonly Vector3 Half;

        public OrientedBox(Vector3 center, Quaternion rotation, Vector3 half)
        {
            Center = center;
            Rotation = rotation;
            Half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
        }

        public Vector3 Axis(int index) =>
            Rotation * (index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward);

        public float RadiusAlong(Vector3 axis) =>
            Half.x * Mathf.Abs(Vector3.Dot(axis, Axis(0)))
            + Half.y * Mathf.Abs(Vector3.Dot(axis, Axis(1)))
            + Half.z * Mathf.Abs(Vector3.Dot(axis, Axis(2)));
    }
}
