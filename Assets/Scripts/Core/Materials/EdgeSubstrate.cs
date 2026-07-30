using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Подложка открытого торца: то, что видно на некромкованной детали.
    ///
    /// Декор — это плёнка на ПЛАСТИ. Торец её не получает: на него клеят кромку,
    /// и только она переносит рисунок на 18 мм полосы. Снял кромкование — на
    /// торце остаётся голая плита, а не дуб сонома. Отсюда и правило: у детали с
    /// выключенной кромкой торцы рисуются этой подложкой, а не декором.
    ///
    /// Чем именно рисовать, решает каталог: если в нём есть декор с именем
    /// <see cref="DecorName"/> — берём его, иначе белый. Каталог собирается из
    /// index.json, который можно перечитать в рантайме, поэтому декор ищется на
    /// каждый вызов, а кэшируется (в MaterialManager) уже сам материал.</summary>
    public static class EdgeSubstrate
    {
        /// <summary>Имя декора-подложки в каталоге. Именно ИМЯ, а не id: декор
        /// заводит человек записью в index.json, и опознаваемая точка стыка тут —
        /// то, что он видит в списке.</summary>
        public const string DecorName = "МДФ шлифованная";

        private static Material? _plain;

        /// <summary>Декор подложки из каталога или null, если его там нет.
        /// Сравнение без учёта регистра и краевых пробелов: имя набирают руками.</summary>
        public static MaterialDef? Decor()
        {
            foreach (var def in MaterialCatalog.All)
            {
                if (def == null || string.IsNullOrEmpty(def.displayName)) continue;
                if (string.Equals(def.displayName.Trim(), DecorName,
                        System.StringComparison.OrdinalIgnoreCase))
                    return def;
            }
            return null;
        }

        /// <summary>Материал торца: декор подложки, а без него — ровный белый.
        /// null только если в сборке нет шейдера URP (тогда звать нечего —
        /// сабмеш торцов остаётся на декоре, как было).</summary>
        public static Material? Material()
        {
            var def = Decor();
            if (def != null)
            {
                var shared = MaterialManager.GetSharedMaterial(def);
                if (shared != null) return shared;
            }
            return Plain();
        }

        /// <summary>Белая подложка — запасной вариант, когда декора в каталоге
        /// нет. Один материал на всю сцену, как GrooveMesh.GrooveMaterial.</summary>
        private static Material? Plain()
        {
            if (_plain != null) return _plain;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            _plain = new Material(shader);
            _plain.SetColor(Shader.PropertyToID("_BaseColor"), Color.white);
            _plain.color = Color.white;
            return _plain;
        }
    }
}
