using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    public static class HandleView
    {
        public static PinholeView Of(Camera camera)
        {
            var t = camera.transform;
            return new PinholeView(t.position, t.forward, t.right, t.up,
                camera.fieldOfView, camera.pixelWidth, camera.pixelHeight,
                camera.orthographic, camera.orthographicSize);
        }
    }
}
