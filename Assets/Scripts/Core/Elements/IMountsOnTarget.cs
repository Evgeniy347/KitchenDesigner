using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface IMountsOnTarget
    {
        Vector3 MountNormal { get; }

        float MountEdgeDetentUnits { get; }
    }
}
