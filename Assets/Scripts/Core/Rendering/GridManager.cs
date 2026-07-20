using UnityEngine;
using KitchenDesigner.Core;

public static class GridManager
{
    public static Vector3 SnapToGrid(Vector3 position)
    {
        if (!KitchenSettings.Instance.GridEnabled)
            return position;

        int step = KitchenSettings.Instance.GridStep;
        float stepM = step * 0.001f;
        return new Vector3(
            Mathf.Round(position.x / stepM) * stepM,
            Mathf.Round(position.y / stepM) * stepM,
            Mathf.Round(position.z / stepM) * stepM
        );
    }

    /// <summary>Округление к сетке только по горизонтали (X/Z), Y не трогается.
    /// Для горизонтального перетаскивания: высоту детали держат снэп и стартовая
    /// позиция, а повторное округление Y каждый кадр (при шаге 18 мм центр по
    /// высоте обычно не кратен шагу) ломало контакт с полом — снэпу приходилось
    /// чинить вертикаль вместо прилипания к соседу, и деталь краснела.</summary>
    public static Vector3 SnapToGridXZ(Vector3 position)
    {
        Vector3 snapped = SnapToGrid(position);
        snapped.y = position.y;
        return snapped;
    }
}
