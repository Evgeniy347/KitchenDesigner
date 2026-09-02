using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct SoftSlabRing
    {
        public readonly float Inset;
        public readonly float Y;
        public readonly float Radial;
        public readonly float Up;

        public SoftSlabRing(float inset, float y, float radial, float up)
        {
            Inset = inset;
            Y = y;
            Radial = radial;
            Up = up;
        }

        public Vector3 Normal(Vector2 planarOutward)
        {
            var normal = new Vector3(
                planarOutward.x * Radial, Up, planarOutward.y * Radial);
            return normal.sqrMagnitude > Tolerance.EpsilonSqr
                ? normal.normalized
                : new Vector3(0f, Mathf.Sign(Up), 0f);
        }
    }
}
