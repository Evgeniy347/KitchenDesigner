using System.Collections.Generic;
using KitchenDesigner.Core.Handles;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class HoverTint
    {
        public const string ShaderMissingMessage =
            "[HoverTint] Шейдер " + HandleMaterials.ShaderName + " не найден — подсветка "
            + "детали, названной пунктом списка, не будет видна. Подмены обычным шейдером "
            + "здесь нет намеренно: с честной проверкой глубины прокрас тонет в соседних "
            + "деталях, то есть отвечает не на тот вопрос, который задали.";

        private sealed class Paint
        {
            public Paint(MeshRenderer renderer, Material source)
            {
                this.renderer = renderer;
                this.source = source;
            }

            public readonly MeshRenderer renderer;
            public readonly Material source;
        }

        private static readonly List<Paint> Applied = new List<Paint>();
        private static Material? _painted;
        private static KitchenElement? _shownFor;

        public static KitchenElement? ShownFor => _shownFor;

        public static int PaintedRendererCount => Applied.Count;

        public static void Show(KitchenElement? element, Color color)
        {
            if (element != null && ReferenceEquals(element, _shownFor) && Applied.Count > 0) return;

            Hide();
            if (element == null) return;

            var painted = Overlay(color);
            if (painted == null) return;

            foreach (var renderer in ElementRenderers.BodyOf(element))
            {
                if (renderer == null) continue;
                var source = renderer.sharedMaterial;
                if (source == null) continue;

                renderer.material = painted;
                Applied.Add(new Paint(renderer, source));
            }

            if (Applied.Count == 0)
            {
                DestroyNow.The(painted);
                return;
            }

            _painted = painted;
            _shownFor = element;
        }

        public static void Hide()
        {
            foreach (var paint in Applied)
            {
                if (paint.renderer == null || paint.source == null) continue;
                if (!ElementTint.Wears(paint.renderer, _painted, ElementTint.HoverName)) continue;
                paint.renderer.material = paint.source;
            }

            Applied.Clear();

            if (_painted != null) DestroyNow.The(_painted);
            _painted = null;
            _shownFor = null;
        }

        public static void Sync()
        {
            if (Applied.Count == 0) return;
            if (HoverAnchor.IsGone(_shownFor)) Hide();
        }

        private static Material? Overlay(Color color)
        {
            var shader = HandleMaterials.FindShader();
            if (shader == null)
            {
                Debug.LogWarning(ShaderMissingMessage);
                return null;
            }

            var painted = new Material(shader)
            {
                name = ElementTint.HoverName,
                hideFlags = HideFlags.DontSave,
            };
            painted.color = color;
            if (painted.HasProperty("_BaseColor")) painted.SetColor("_BaseColor", color);
            return painted;
        }
    }
}
