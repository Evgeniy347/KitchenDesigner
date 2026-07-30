using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Подложка некромкованного торца: то, что видно там, где кромки нет.
    ///
    /// Декор — это плёнка на ПЛАСТИ. Торец её не получает: на него клеят кромку,
    /// и только она переносит рисунок на 18 мм полосы. Нет кромки — на торце
    /// голая плита, а не дуб сонома.
    ///
    /// Какие торцы кромкуются, решает ТА ЖЕ функция, что и спецификацию:
    /// <see cref="EdgeBanding.Coverage"/> — открытый торец кромкуется, упёртый в
    /// соседа нет. Поэтому подложка ровно там, где в CSV пустая колонка кромки:
    /// расходимся с раскроем — расходимся с реальностью. Считать «нет кромки =
    /// не видно» нельзя: торец, упёртый в СТЕНУ, кромки не получает, а стену
    /// сносит и разрез, и режим обзора — и голая плита должна быть видна.
    ///
    /// Цвет по умолчанию — БЕЛЫЙ. Фотографическая текстура плиты выглядит
    /// правдоподобно, но по тону совпадает и с бежевым декором, и с
    /// валидационной заливкой: торец переставал читаться как торец. Белый
    /// решает главную задачу — «здесь кромки нет» видно с любого ракурса.
    ///
    /// Переопределяется каталогом: заведи в index.json декор с именем
    /// <see cref="DecorName"/> — и подложкой станет он (готовая картинка для
    /// этого уже есть, декор «МДФ шлифованная», достаточно переименовать).</summary>
    public static class EdgeSubstrate
    {
        /// <summary>Имя декора-подложки в каталоге. Именно ИМЯ, а не id: декор
        /// заводит человек записью в index.json, и опознаваемая точка стыка тут —
        /// то, что он видит в списке. Пока такого имени в каталоге нет, торцы
        /// белые — это и есть поведение по умолчанию.</summary>
        public const string DecorName = "Подложка торца";

        private static Material? _plain;

        // ── Какие грани детали рисуются подложкой ──────────────────────

        /// <summary>Маска граней (индексы <c>KitchenElement.GetFaces</c>), на
        /// которых кромки нет. Пласти в неё не попадают никогда — у них декор.
        ///
        /// others — вся сцена: перекрытие торца считается по соседям.</summary>
        public static int BareFaceMask(KitchenElement? element,
            IReadOnlyList<KitchenElement>? others)
            => BareFaceMask(element, others == null ? null : SceneFaces.Of(others));

        /// <summary>То же по готовому слепку сцены — так считает SyncScene,
        /// чтобы грани каждой детали строились один раз на весь проход.</summary>
        public static int BareFaceMask(KitchenElement? element, SceneFaces? scene)
        {
            if (element == null || !element.SupportsEdges) return 0;
            var layout = EdgeBanding.LayoutOf(element.DimensionsMM);
            if (!layout.IsValid) return 0;

            // Выключатель снят — кромки нет ни на одном торце, соседей можно не
            // спрашивать. Это ещё и самый частый случай на загрузке проекта.
            if (!element.EdgeBandingEnabled)
                return (1 << layout.FaceIndex(EdgeSide.L1))
                     | (1 << layout.FaceIndex(EdgeSide.L2))
                     | (1 << layout.FaceIndex(EdgeSide.W1))
                     | (1 << layout.FaceIndex(EdgeSide.W2));

            if (scene == null) return 0;

            var coverage = EdgeBanding.Coverage(element, scene);
            int mask = 0;
            foreach (EdgeSide side in AllSides)
                if (!coverage.HasEdge(side)) mask |= 1 << layout.FaceIndex(side);
            return mask;
        }

        private static readonly EdgeSide[] AllSides =
        {
            EdgeSide.L1, EdgeSide.L2, EdgeSide.W1, EdgeSide.W2,
        };

        /// <summary>Пересчитать подложку у ОДНОЙ детали (правка кромки, ресайз).</summary>
        public static void Sync(KitchenElement? element)
        {
            if (element == null) return;
            element.SetBareFaceMask(BareFaceMask(element, PartRegistry.GetAll()));
        }

        /// <summary>Пересчитать подложку у всей сцены. Зовётся оттуда же, откуда
        /// перекрашивается валидация (ElementHighlighter.RefreshHighlights):
        /// перекрытие торца зависит от СОСЕДЕЙ, а значит меняется от чужого
        /// сдвига, удаления и загрузки проекта — сама деталь об этом не узнаёт.
        ///
        /// Меш пересобирается только там, где маска реально изменилась, поэтому
        /// в устоявшейся сцене проход стоит одного расчёта перекрытий.</summary>
        public static void SyncScene(IReadOnlyList<KitchenElement>? all)
        {
            if (all == null) return;
            using var _ = PerfMarkers.EdgeSubstrateSync.Auto();

            // Слепок снимается ДО пересборки мешей и переживает её: пересборка
            // меняет сабмеши, а не габаритные грани, по которым считаются
            // перекрытия. Иначе грани каждой детали строились бы заново на
            // каждого соседа — это и был весь расход прохода.
            var scene = SceneFaces.Of(all);
            foreach (var element in all)
            {
                if (element == null || !element.SupportsEdges) continue;
                element.SetBareFaceMask(BareFaceMask(element, scene));
            }
        }

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

        /// <summary>Материал торца: ровный белый, а если в каталоге заведён
        /// декор <see cref="DecorName"/> — он. null только если в сборке нет
        /// шейдера URP (тогда звать нечего — сабмеш торцов остаётся на декоре,
        /// как было).</summary>
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

        /// <summary>Белая подложка — вариант по умолчанию. Один материал на всю
        /// сцену, как GrooveMesh.GrooveMaterial.
        ///
        /// Блеска у неё нет: голая плита матовая, а глянцевый торец на матовом
        /// щите читается как накладка.</summary>
        private static Material? Plain()
        {
            if (_plain != null) return _plain;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            _plain = new Material(shader);
            _plain.SetColor(Shader.PropertyToID("_BaseColor"), Color.white);
            _plain.SetFloat(Shader.PropertyToID("_Smoothness"), 0.05f);
            _plain.color = Color.white;
            return _plain;
        }
    }
}
