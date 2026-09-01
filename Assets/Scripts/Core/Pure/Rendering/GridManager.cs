using UnityEngine;
using KitchenDesigner.Core;

public static class GridManager
{
    public static Vector3 SnapToGrid(Vector3 position)
    {
        if (!KitchenSettings.Instance.GridEnabled)
            return WorldBounds.Clamp(position);

        int step = KitchenSettings.Instance.GridStep;
        float stepM = step * 0.001f;
        return WorldBounds.Clamp(new Vector3(
            Mathf.Round(position.x / stepM) * stepM,
            Mathf.Round(position.y / stepM) * stepM,
            Mathf.Round(position.z / stepM) * stepM
        ));
    }

    public static Vector3 SnapToGridXZ(Vector3 position)
    {
        Vector3 snapped = SnapToGrid(position);
        snapped.y = Mathf.Clamp(position.y, -WorldBounds.LimitMeters, WorldBounds.LimitMeters);
        return snapped;
    }
}
