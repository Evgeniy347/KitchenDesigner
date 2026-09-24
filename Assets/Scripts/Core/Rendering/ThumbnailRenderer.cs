using System;
using System.Diagnostics;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct ThumbnailFraming
    {
        public readonly Bounds Bounds;
        public readonly float MaxDimension;
        public readonly float CameraDistance;
        public readonly Vector3 ViewDirection;

        public ThumbnailFraming(Bounds bounds, float maxDimension, float cameraDistance,
            Vector3 viewDirection)
        {
            Bounds = bounds;
            MaxDimension = maxDimension;
            CameraDistance = cameraDistance;
            ViewDirection = viewDirection;
        }
    }

    public static class ThumbnailRenderer
    {
        public const int DefaultSize = 128;
        private const int IsolationLayer = 31;
        private const float Fov = 45f;
        private const float DistanceScale = 2.5f;

        private const float ContextFreeMinDistanceUnits = 0.01f;

        private static int _warmupCount;
        private static long _warmupTicks;
        private static bool _warmupLogged;

        public static RenderTexture Render(Func<GameObject> spawn, int size = DefaultSize) =>
            Render(spawn, size, size, out _);

        public static RenderTexture Render(Func<GameObject> spawn, int size,
            out ThumbnailFraming framing) =>
            Render(spawn, size, size, out framing);

        public static RenderTexture Render(Func<GameObject> spawn, int widthPx, int heightPx,
            out ThumbnailFraming framing)
        {
            long began = Stopwatch.GetTimestamp();
            var rt = RenderCore(spawn, widthPx, heightPx, out framing);
            _warmupCount++;
            _warmupTicks += Stopwatch.GetTimestamp() - began;
            return rt;
        }

        public static void LogWarmupBatchOnceIdle()
        {
            if (_warmupLogged || _warmupCount == 0) return;
            _warmupLogged = true;
            double ms = _warmupTicks * 1000.0 / Stopwatch.Frequency;
            UnityEngine.Debug.Log(
                $"[Perf] ThumbnailRenderer: {_warmupCount} плиток за {ms:F0} мс");
        }

        internal static void ForgetWarmupForTests()
        {
            _warmupCount = 0;
            _warmupTicks = 0;
            _warmupLogged = false;
        }

        private static RenderTexture RenderCore(Func<GameObject> spawn, int widthPx, int heightPx,
            out ThumbnailFraming framing)
        {
            if (spawn == null) throw new ArgumentNullException(nameof(spawn));

            var rt = new RenderTexture(Mathf.Max(ThumbnailFrame.MinPixels, widthPx),
                Mathf.Max(ThumbnailFrame.MinPixels, heightPx), 16, RenderTextureFormat.ARGB32);
            framing = default;

            using (ElementFactorySandbox.Enter())
            using (ThumbnailOverlays.Suppress())
            {
                GameObject? go = null;
                GameObject? camGo = null;
                try
                {
                    go = spawn();
                    if (go == null) return rt;

                    SetLayerRecursively(go, IsolationLayer);
                    var bounds = RendererBoundsOf(go);
                    float distance = IsoCameraRig.Distance(
                        bounds.size, DistanceScale, ContextFreeMinDistanceUnits);
                    Vector3 viewDirection = IsoCameraRig.ViewDir;
                    framing = new ThumbnailFraming(bounds,
                        Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)),
                        distance, viewDirection);

                    camGo = new GameObject("ThumbnailCam");
                    var cam = camGo.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                    cam.orthographic = false;
                    cam.fieldOfView = Fov;
                    cam.nearClipPlane = 0.01f;
                    cam.farClipPlane = 100f;
                    cam.cullingMask = 1 << IsolationLayer;

                    camGo.transform.position = bounds.center + viewDirection * distance;
                    camGo.transform.LookAt(bounds.center);

                    cam.targetTexture = rt;
                    cam.aspect = ThumbnailFrame.Aspect(rt.width, rt.height);
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
