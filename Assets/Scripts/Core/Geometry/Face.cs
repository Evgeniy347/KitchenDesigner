using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct Face
    {
        public const int BoxFaceCount = 6;

        public Vector3 center;
        public Vector3 normal;
        public Vector2 size;
        public Vector3 rightAxis;
        public Vector3 upAxis;

        public Face(Vector3 center, Vector3 normal, Vector2 size, Vector3 right, Vector3 up)
        {
            this.center = center;
            this.normal = normal;
            this.size = size;
            this.rightAxis = right;
            this.upAxis = up;
        }
    }
}
