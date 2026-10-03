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

            var shift = Vector2.zero;
            if (high.x > screen.xMax) shift.x = screen.xMax - high.x;
            if (low.x + shift.x < screen.xMin) shift.x = screen.xMin - low.x;
            if (high.y > screen.yMax) shift.y = screen.yMax - high.y;
            if (low.y + shift.y < screen.yMin) shift.y = screen.yMin - low.y;
            if (shift == Vector2.zero) return;

            list.position += canvas.TransformVector(shift);
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
            if (bottom < canvas.rect.yMin && roomAbove >= popup.rect.height)
            {
                popup.pivot = new Vector2(0f, 0f);
                popup.position = fieldCorners[1];
            }
            KeepInside(popup, canvas);
        }

        public static void ScrollToItem(ScrollRect scroll, RectTransform item)
        {
            var content = scroll.content;
            float contentH = content.rect.height;
            float viewH = scroll.viewport.rect.height;
            float room = contentH - viewH;
            if (room <= 0f) return;

            float centreFromTop = -content.InverseTransformPoint(item.TransformPoint(item.rect.center)).y
                + content.rect.height * (1f - content.pivot.y);
            float offset = Mathf.Clamp(centreFromTop - viewH * 0.5f, 0f, room);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, offset);
        }
    }
}
