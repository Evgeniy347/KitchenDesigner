using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    /// <summary>Материалы ручек: ZTest Always поверх всего (обоснование — в HandleOverlay.shader).</summary>
    public static class HandleMaterials
    {
        public const string ShaderName = "Hidden/KD/HandleOverlay";

        public const string ShaderResourcePath = "Shaders/HandleOverlay";

        public const string ShaderMissingMessage =
            "[Handles] Шейдер " + ShaderName + " не найден — ручки трансформации "
            + "не будут видны. Подмены обычным шейдером здесь нет намеренно: "
            + "с честной проверкой глубины ручка тонет в соседней детали, и "
            + "инструмент молча перестаёт работать.";

        public static readonly Color AxisX = new Color(0.90f, 0.25f, 0.25f);
        public static readonly Color AxisY = new Color(0.35f, 0.85f, 0.35f);
        public static readonly Color AxisZ = new Color(0.35f, 0.55f, 0.95f);

        public static System.Func<Color, Material?>? MaterialFactory;

        private static readonly Dictionary<Color, Material> _cache = new Dictionary<Color, Material>();
        private static bool _shaderMissing;

        public static Color ForAxis(int axis) => axis == 0 ? AxisX : (axis == 1 ? AxisY : AxisZ);

        public static Material? For(Color color)
        {
            if (MaterialFactory != null) return MaterialFactory(color);
            if (_cache.TryGetValue(color, out var cached) && cached != null) return cached;
            if (_shaderMissing) return null;

            var shader = FindShader();
            if (shader == null)
            {
                _shaderMissing = true;
                Debug.LogWarning(ShaderMissingMessage);
                return null;
            }

            var material = new Material(shader) { hideFlags = HideFlags.DontSave };
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            _cache[color] = material;
            return material;
        }

        public static Shader? FindShader()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) shader = Resources.Load<Shader>(ShaderResourcePath);
            return shader;
        }
    }
}
