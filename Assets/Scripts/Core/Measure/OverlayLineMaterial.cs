using System;
using UnityEngine;

namespace KitchenDesigner.Core.Measure
{
    internal static class OverlayLineMaterial
    {
        public const string ShaderName = "Hidden/OverlayLine";
        public const string ResourcesPath = "Shaders/OverlayLine";

        public static Shader? FindShader()
        {
            var shader = Shader.Find(ShaderName);
            return shader != null ? shader : Resources.Load<Shader>(ResourcesPath);
        }

        public static Material? BuildOrWarn(Shader? shader, Action<string> warn,
            string missingMessage)
        {
            if (shader == null)
            {
                warn(missingMessage);
                return null;
            }
            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
    }
}
