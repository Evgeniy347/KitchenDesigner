using System;

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
