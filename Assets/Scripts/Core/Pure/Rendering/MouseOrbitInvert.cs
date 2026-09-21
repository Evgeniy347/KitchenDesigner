using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class MouseOrbitInvert
    {
        public static Vector2 Apply(Vector2 mouseDelta, bool invertX, bool invertY) =>
            new Vector2(invertX ? -mouseDelta.x : mouseDelta.x, invertY ? -mouseDelta.y : mouseDelta.y);
    }
}
