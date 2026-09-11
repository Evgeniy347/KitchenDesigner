using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class DestroyNow
    {
        public static void The(UnityEngine.Object? target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
