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
}
