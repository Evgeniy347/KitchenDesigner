using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct Cuboid
    {
        public readonly Vector3 CentreMM;
        public readonly Vector3 SizeMM;

        public Cuboid(Vector3 centreMM, Vector3 sizeMM)
        {
            CentreMM = centreMM;
            SizeMM = sizeMM;
        }

        public static Cuboid Between(Vector3 minMM, Vector3 maxMM)
            => new Cuboid((minMM + maxMM) * 0.5f, maxMM - minMM);
    }
}
