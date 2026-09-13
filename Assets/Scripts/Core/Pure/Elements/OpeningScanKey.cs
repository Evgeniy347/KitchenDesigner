using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct OpeningScanKey
    {
        public readonly int Revision;
        public readonly Vector3 RestPosition;
        public readonly Quaternion RestRotation;
        public readonly Vector3 RestSpan;

        public OpeningScanKey(int revision, Vector3 restPosition, Quaternion restRotation,
            Vector3 restSpan)
        {
            Revision = revision;
            RestPosition = restPosition;
            RestRotation = restRotation;
            RestSpan = restSpan;
        }

        public bool Matches(in OpeningScanKey other) =>
            Revision == other.Revision
            && SamePoint(RestPosition, other.RestPosition)
            && SameTurn(RestRotation, other.RestRotation)
            && SamePoint(RestSpan, other.RestSpan);

        private static bool SamePoint(Vector3 a, Vector3 b) =>
            a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z);

        private static bool SameTurn(Quaternion a, Quaternion b) =>
            a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z) && a.w.Equals(b.w);
    }
}
