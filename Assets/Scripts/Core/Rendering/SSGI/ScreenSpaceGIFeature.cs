using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace KitchenDesigner.Core
{
    /// <summary>URP-фича экранного непрямого освещения (SSGI). Один fullscreen-проход
    /// перед пост-обработкой: собирает свет с соседних поверхностей и добавляет к
    /// цвету. Подключается к рендереру, включается только в фоторежиме
    /// (PhotoQualityController дергает SetActive). RenderGraph-совместима.</summary>
    public class ScreenSpaceGIFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material? _material;
        [SerializeField, Range(0f, 3f)] private float _strength = 0.7f;
        [SerializeField, Range(0.1f, 3f)] private float _radius = 0.8f;
        [SerializeField, Range(4, 64)] private int _samples = 32;

        private SsgiPass? _pass;

        public override void Create()
        {
            _pass = new SsgiPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
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
                TextureHandle dest = renderGraph.CreateTexture(desc);

                // Проход 0: сбор непрямого света + композит (шумный результат).
                var gather = new RenderGraphUtils.BlitMaterialParameters(source, gathered, _mat, 0);
                renderGraph.AddBlitPass(gather, "ScreenSpaceGI Gather");

                // Проход 1: билатеральный денойз по глубине.
                var blur = new RenderGraphUtils.BlitMaterialParameters(gathered, dest, _mat, 1);
                renderGraph.AddBlitPass(blur, "ScreenSpaceGI Blur");

                // Дальнейшие проходы (пост-обработка) работают уже по результату SSGI.
                resourceData.cameraColor = dest;
            }
        }
    }
}
