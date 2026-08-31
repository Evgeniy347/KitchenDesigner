namespace KitchenDesigner.Core
{
    public struct Vector3Serializer
    {
        public float x, y, z;
        public Vector3Serializer(UnityEngine.Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(x, y, z);
    }
}
