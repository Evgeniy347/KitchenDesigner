using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct SnapResult
    {
        public bool snapped;
        public Vector3 position;
        public string targetName;
        public int faceIndex;
        public Vector3 snapPoint;
        public Vector3 targetPoint;
    }
}
