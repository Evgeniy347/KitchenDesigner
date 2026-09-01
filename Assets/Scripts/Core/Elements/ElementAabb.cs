using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct ElementAabb
    {
        public readonly float minX, maxX, minY, maxY, minZ, maxZ;

        public ElementAabb(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            this.minX = minX; this.maxX = maxX;
            this.minY = minY; this.maxY = maxY;
            this.minZ = minZ; this.maxZ = maxZ;
        }

        public static ElementAabb Of(KitchenElement el)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in el.GetVertices())
            {
                if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
                if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
            }
            return new ElementAabb(minX, maxX, minY, maxY, minZ, maxZ);
        }

        public bool CoversInXZ(Vector3 point, float margin) =>
            point.x >= minX - margin && point.x <= maxX + margin
            && point.z >= minZ - margin && point.z <= maxZ + margin;
    }
}
