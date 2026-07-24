using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KitchenDesigner.Core
{
    /// <summary>В фоторежиме включает отбрасывание теней у всех непрозрачных мешей
    /// (стены, мебель, рамы, глухие панели дверей, потолок) и оставляет прозрачные
    /// (стекло) пропускающими свет. Именно это даёт реализм: закрытая глухая дверь
    /// перекрывает солнце, открытая — пропускает, а сквозь стекло/окно свет проходит.
    /// В рабочем режиме тени у створок выключены ради производительности, поэтому
    /// прежние значения запоминаются и возвращаются на выходе.</summary>
    public static class PhotoShadowCasters
    {
        private static readonly List<(MeshRenderer renderer, ShadowCastingMode prev)> _changed = new();

        public static void Enable()
        {
            Restore();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                var target = IsTransparent(r.sharedMaterial)
                    ? ShadowCastingMode.Off   // стекло — свет проходит
                    : ShadowCastingMode.On;   // непрозрачное — перекрывает свет
                if (r.shadowCastingMode == target) continue;
                _changed.Add((r, r.shadowCastingMode));
                r.shadowCastingMode = target;
            }
        }

        public static void Restore()
        {
            foreach (var (r, prev) in _changed)
                if (r != null) r.shadowCastingMode = prev;
            _changed.Clear();
        }

        /// <summary>Материал считается прозрачным, если он в очереди Transparent
        /// (стекло дверей/окон собирается через ElementHighlighter.MakeTransparent).</summary>
        private static bool IsTransparent(Material? m) =>
            m != null && m.renderQueue >= (int)RenderQueue.Transparent;
    }
}
