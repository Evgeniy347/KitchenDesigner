using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ThumbnailRenderer
    {
        public const int DefaultSize = 128;
        private const int IsolationLayer = 31;
        private const float Fov = 45f;
        private const float DistanceScale = 2.5f;

        public static RenderTexture Render(Func<GameObject> spawn, int size = DefaultSize)
        {
            if (spawn == null) throw new ArgumentNullException(nameof(spawn));

            var rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32);

            using (ElementFactorySandbox.Enter())
            {
                GameObject? go = null;
                GameObject? camGo = null;
                try
                {
                    go = spawn();
                    if (go == null) return rt;

                    SetLayerRecursively(go, IsolationLayer);
                    var bounds = RendererBoundsOf(go);

                    camGo = new GameObject("ThumbnailCam");
                    var cam = camGo.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                    cam.orthographic = false;
                    cam.fieldOfView = Fov;
                    cam.aspect = 1f;
                    cam.nearClipPlane = 0.01f;
                    cam.farClipPlane = 100f;
                    cam.cullingMask = 1 << IsolationLayer;

                    camGo.transform.position = IsoCameraRig.Position(bounds.center, bounds.size, DistanceScale);
                    camGo.transform.LookAt(bounds.center);

                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;

                    return rt;
                }
                finally
                {
                    if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                    if (go != null)
                    {
                        go.GetComponent<KitchenElement>()?.PrepareForDestruction();
                        UnityEngine.Object.DestroyImmediate(go);
                    }
                }
            }
        }

        private static Bounds RendererBoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
