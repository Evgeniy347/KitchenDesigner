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

        [SerializeField] private Material? _material;
        [SerializeField, Range(0f, 3f)] private float _strength = 0.7f;
        [SerializeField, Range(0.1f, 3f)] private float _radius = 0.8f;
        [SerializeField, Range(4, 64)] private int _samples = 32;

        private SsgiPass? _pass;

        internal ScriptableRenderPass? Pass => _pass;

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
            _pass.Setup(_material, _strength, _radius, _samples);
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_pass);
        }

        private sealed class SsgiPass : ScriptableRenderPass
        {
            private static readonly int StrengthId = Shader.PropertyToID("_SsgiStrength");
            private static readonly int RadiusId = Shader.PropertyToID("_SsgiRadius");
            private static readonly int SamplesId = Shader.PropertyToID("_SsgiSamples");

            private Material? _mat;
            private float _strength, _radius;
            private int _samples;

            public void Setup(Material mat, float strength, float radius, int samples)
            {
                _mat = mat;
                _strength = strength;
                _radius = radius;
                _samples = samples;
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

                TextureHandle source = resourceData.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "SSGI_Gather";
                desc.clearBuffer = false;
                desc.depthBufferBits = 0;
                TextureHandle gathered = renderGraph.CreateTexture(desc);
                desc.name = "SSGI_Blurred";
                TextureHandle denoised = renderGraph.CreateTexture(desc);

                var gather = new RenderGraphUtils.BlitMaterialParameters(
                    source, gathered, _mat, GatherPassIndex);
                renderGraph.AddBlitPass(gather, "ScreenSpaceGI Gather");

                var denoise = new RenderGraphUtils.BlitMaterialParameters(
                    gathered, denoised, _mat, DenoisePassIndex);
                renderGraph.AddBlitPass(denoise, "ScreenSpaceGI Blur");

                resourceData.cameraColor = denoised;
            }
        }
    }
}
