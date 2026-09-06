using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Габаритная коробка списка ориентированных коробок раскрытия.
/// Проверка коллизий давно считает по OBB, но многим тестам нужен именно
/// осевой габарит («угол внутри AABB») — здесь один перевод на всех, чтобы
/// он не расползся копиями по файлам тестов.</summary>
internal static class OpenBoxAabb
{
    public static (Vector3 min, Vector3 max) Of(OpenBoxes boxes, float progress)
    {
        var list = new List<OrientedBox>();
        boxes(progress, list);
        return Of(list);
    }

    public static (Vector3 min, Vector3 max) Of(List<OrientedBox> boxes)
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var box in boxes)
            for (int i = 0; i < 8; i++)
            {
                var corner = box.Center + box.Rotation * new Vector3(
                    (i & 1) == 0 ? -box.Half.x : box.Half.x,
                    (i & 2) == 0 ? -box.Half.y : box.Half.y,
                    (i & 4) == 0 ? -box.Half.z : box.Half.z);
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }

        return (min, max);
    }
}
