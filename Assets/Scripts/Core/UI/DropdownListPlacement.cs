using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class DropdownListPlacement
    {
        public static void KeepInside(RectTransform list, RectTransform canvas)
        {
            var corners = new Vector3[4];
            list.GetWorldCorners(corners);
            var low = canvas.InverseTransformPoint(corners[0]);
            var high = canvas.InverseTransformPoint(corners[2]);
            var screen = canvas.rect;

            var (x, y) = DropdownListMath.ShiftInto(low.x, low.y, high.x, high.y,
                screen.xMin, screen.yMin, screen.xMax, screen.yMax);
            if (x == 0f && y == 0f) return;

            list.position += canvas.TransformVector(new Vector2(x, y));
        }

        public static void PlaceBelowOrAbove(RectTransform popup, RectTransform field, RectTransform canvas)
        {
            var fieldCorners = new Vector3[4];
            field.GetWorldCorners(fieldCorners);
            popup.pivot = new Vector2(0f, 1f);
            popup.position = fieldCorners[0];

            var popupCorners = new Vector3[4];
            popup.GetWorldCorners(popupCorners);
            float bottom = canvas.InverseTransformPoint(popupCorners[0]).y;
            float roomAbove = canvas.rect.yMax - canvas.InverseTransformPoint(fieldCorners[1]).y;
            if (DropdownListMath.OpensAbove(bottom, canvas.rect.yMin, roomAbove, popup.rect.height))
            {
                popup.pivot = new Vector2(0f, 0f);
                popup.position = fieldCorners[1];
            }
            KeepInside(popup, canvas);
        }

        public static void ScrollToItem(ScrollRect scroll, RectTransform item)
        {
            var content = scroll.content;
            float centreFromTop = -content.InverseTransformPoint(item.TransformPoint(item.rect.center)).y
                + content.rect.height * (1f - content.pivot.y);
            float offset = DropdownListMath.ScrollOffset(centreFromTop, scroll.viewport.rect.height, content.rect.height);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, offset);
        }
    }
}
