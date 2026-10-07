using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct ImpliedGround : System.IEquatable<ImpliedGround>
    {
        public static readonly ImpliedGround None = default;

        public bool Present { get; }

        public float Y { get; }

        private ImpliedGround(float y)
        {
            Present = true;
            Y = y;
        }

        public static ImpliedGround At(float y) => new ImpliedGround(y);

        public bool Holds(in ElementGeometry body) =>
            Present && Mathf.Abs(body.Min.y - Y) <= Tolerance.ContactUnits;

        public bool Equals(ImpliedGround other) =>
            Present == other.Present && (!Present || Y.Equals(other.Y));

        public override bool Equals(object? obj) => obj is ImpliedGround other && Equals(other);

        public override int GetHashCode() => Present ? Y.GetHashCode() : 0;

        public static bool operator ==(ImpliedGround a, ImpliedGround b) => a.Equals(b);

        public static bool operator !=(ImpliedGround a, ImpliedGround b) => !a.Equals(b);
    }
}
