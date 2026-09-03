using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace KitchenDesigner.Core
{
    public class ScreenSpaceGIFeature : ScriptableRendererFeature
    {
        public const int GatherPassIndex = 0;
        public const int DenoisePassIndex = 1;

        internal const RenderPassEvent InjectBeforePostProcessing =
            RenderPassEvent.BeforeRenderingPostProcessing;

        private const int MinPassSize = 16;

        [SerializeField] private Material? _material;
        [SerializeField, Range(0f, 3f)] private float _strength = 0.7f;
        [SerializeField, Range(0.1f, 3f)] private float _radius = 0.8f;
        [SerializeField, Range(4, 64)] private int _samples = 32;
        [SerializeField, Range(0.25f, 1f)] private float _resolution = 0.5f;
        [SerializeField, Range(0, 6)] private int _blurRadius = 3;

        private SsgiPass? _pass;

        internal ScriptableRenderPass? Pass => _pass;

        public void Configure(float strength, float radius, int samples, float resolution, int blurRadius)
        {
            _strength = strength;
            _radius = radius;
            _samples = samples;
            _resolution = resolution;
            _blurRadius = blurRadius;
        }

        public override void Create()
        {
            _pass = new SsgiPass
            {
                renderPassEvent = InjectBeforePostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _pass == null) return;
            _pass.Setup(_material, _strength, _radius, _samples, _resolution, _blurRadius);
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_pass);
        }

        internal static int ScaledSize(int full, float resolution) =>
            Mathf.Max(MinPassSize, Mathf.RoundToInt(full * Mathf.Clamp(resolution, 0.25f, 1f)));

        private sealed class SsgiPass : ScriptableRenderPass
        {
            private static readonly int StrengthId = Shader.PropertyToID("_SsgiStrength");
            private static readonly int RadiusId = Shader.PropertyToID("_SsgiRadius");
            private static readonly int SamplesId = Shader.PropertyToID("_SsgiSamples");
            private static readonly int BlurRadiusId = Shader.PropertyToID("_SsgiBlurRadius");

            private Material? _mat;
            private float _strength, _radius, _resolution;
            private int _samples, _blurRadius;

            public void Setup(Material mat, float strength, float radius, int samples,
                float resolution, int blurRadius)
            {
                _mat = mat;
                _strength = strength;
                _radius = radius;
                _samples = samples;
                _resolution = resolution;
                _blurRadius = blurRadius;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mat == null) return;

                var cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer) return;

                _mat.SetFloat(StrengthId, _strength);
                _mat.SetFloat(RadiusId, _radius);
                _mat.SetInt(SamplesId, _samples);
                _mat.SetInt(BlurRadiusId, _blurRadius);

                TextureHandle source = resourceData.activeColorTexture;
                var full = renderGraph.GetTextureDesc(source);

                var small = full;
                small.name = "SSGI_Gather";
                small.clearBuffer = false;
                small.depthBufferBits = 0;
                small.width = ScaledSize(full.width, _resolution);
                small.height = ScaledSize(full.height, _resolution);
                TextureHandle gathered = renderGraph.CreateTexture(small);

                var output = full;
                output.name = "SSGI_Composited";
                output.clearBuffer = false;
                output.depthBufferBits = 0;
                TextureHandle composited = renderGraph.CreateTexture(output);

                var gather = new RenderGraphUtils.BlitMaterialParameters(
                    source, gathered, _mat, GatherPassIndex);
                renderGraph.AddBlitPass(gather, "ScreenSpaceGI Gather");

                var denoise = new RenderGraphUtils.BlitMaterialParameters(
                    gathered, composited, _mat, DenoisePassIndex);
                renderGraph.AddBlitPass(denoise, "ScreenSpaceGI Composite");

                resourceData.cameraColor = composited;
            }
        }
    }
}
