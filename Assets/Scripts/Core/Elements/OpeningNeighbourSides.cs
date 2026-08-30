using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class OpeningNeighbourSides
    {
        public const int Left = 1;
        public const int Right = 2;
        public const int Top = 4;
        public const int Bottom = 8;

        public static int HiddenSidesOf(KitchenElement opening, Wall? wall)
        {
            if (opening == null || wall == null) return 0;
            if (wall.GetComponent<KitchenElement>() == null) return 0;

            var wallT = wall.transform;
            var self = LocalRect(wallT, opening);

            int hidden = 0;
            foreach (var other in wall.AttachedWindows)
                hidden |= HiddenBy(self, LocalRectOfNeighbour(wallT, other, opening));
            foreach (var other in wall.AttachedDoors)
                hidden |= HiddenBy(self, LocalRectOfNeighbour(wallT, other, opening));
            return hidden;
        }

        public static int HiddenBy(Rect self, Rect other)
        {
            if (other.width <= 0f || other.height <= 0f) return 0;

            bool yOverlap = self.yMax > other.yMin && self.yMin < other.yMax;
            bool xOverlap = self.xMax > other.xMin && self.xMin < other.xMax;

            int hidden = 0;
            if (yOverlap && !xOverlap)
            {
                if (other.xMax >= self.xMin && other.xMax <= self.xMax) hidden |= Left;
                if (other.xMin >= self.xMin && other.xMin <= self.xMax) hidden |= Right;
            }
            if (xOverlap && !yOverlap)
            {
                if (other.yMax >= self.yMin && other.yMax <= self.yMax) hidden |= Bottom;
                if (other.yMin >= self.yMin && other.yMin <= self.yMax) hidden |= Top;
            }
            return hidden;
        }

        public static Rect LocalRect(Transform wallTransform, KitchenElement opening)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var dims = opening.DimensionsMM;
            float halfW = dims.x * 0.5f * toU;
            float halfH = dims.y * 0.5f * toU;
            Vector3 local = wallTransform.InverseTransformPoint(opening.transform.position);
            return Rect.MinMaxRect(local.x - halfW, local.y - halfH, local.x + halfW, local.y + halfH);
        }

        private static Rect LocalRectOfNeighbour(Transform wallTransform,
            KitchenElement? other, KitchenElement self)
        {
            if (other == null || other == self) return Rect.zero;
            return LocalRect(wallTransform, other);
        }
    }
}
