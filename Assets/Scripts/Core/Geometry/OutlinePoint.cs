using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct OutlinePoint
    {
        public readonly Vector2 Position;
        public readonly Vector2 Outward;

        public OutlinePoint(Vector2 position, Vector2 outward)
        {
            Position = position;
            Outward = outward;
        }
    }
}
