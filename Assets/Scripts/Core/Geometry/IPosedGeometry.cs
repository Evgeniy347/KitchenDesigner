using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface IPosedGeometry
    {
        ElementGeometry At(Vector3 position);
    }
}
