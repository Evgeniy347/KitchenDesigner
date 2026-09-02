using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct CornerRadii
    {
        public const int Count = 4;

        public readonly float MinusXMinusZ;
        public readonly float PlusXMinusZ;
        public readonly float PlusXPlusZ;
        public readonly float MinusXPlusZ;

        public CornerRadii(float minusXMinusZ, float plusXMinusZ,
            float plusXPlusZ, float minusXPlusZ)
        {
            MinusXMinusZ = minusXMinusZ;
            PlusXMinusZ = plusXMinusZ;
            PlusXPlusZ = plusXPlusZ;
            MinusXPlusZ = minusXPlusZ;
        }

        public static CornerRadii Uniform(float radius)
            => new CornerRadii(radius, radius, radius, radius);

        public CornerRadii Inset(float distance) => new CornerRadii(
            Mathf.Max(0f, MinusXMinusZ - distance),
            Mathf.Max(0f, PlusXMinusZ - distance),
            Mathf.Max(0f, PlusXPlusZ - distance),
            Mathf.Max(0f, MinusXPlusZ - distance));

        public float this[int corner]
        {
            get
            {
                switch (corner)
                {
                    case 0: return MinusXMinusZ;
                    case 1: return PlusXMinusZ;
                    case 2: return PlusXPlusZ;
                    case 3: return MinusXPlusZ;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }
    }
}
