using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface ISnapPorts
    {
        int SnapPortCount { get; }

        SnapPort SnapPortAt(int index, Vector3 transformPosition);
    }
}
