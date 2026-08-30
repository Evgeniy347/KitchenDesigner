using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct SnapCandidate
    {
        public float dist;
        public SnapResult result;
        public Vector3 normal;
        public float planeShift;
        public Vector3 u, v;
        public float du, dv;
        public bool hasLineContact;
        public string? log;

        public string TargetFaceKey => result.targetName + "#" + result.faceIndex;
    }
}
