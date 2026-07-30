using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Мост между сценой и <see cref="ValidationCore"/>: превращает
    /// детали в снимки <see cref="ValidationElement"/>.
    ///
    /// Здесь и только здесь живёт ответ на вопрос «кто это»: пол, стена, проём,
    /// ящик, светильник, врезная техника, фасад с зазором. Ядро получает готовые
    /// флаги <see cref="ElementKind"/> и ни одного GetComponent.</summary>
    public static class ValidationSnapshot
    {
        /// <summary>Снимки набора деталей. Порядок сохраняется: индексы в
        /// результате валидации — это индексы в <paramref name="elements"/>.
        /// Список обязан быть без null.</summary>
        public static void Build(List<KitchenElement> elements, List<ValidationElement> into)
        {
            into.Clear();
            if (elements == null || elements.Count == 0) return;

            // Стена ищется по имени GameObject — так на неё ссылаются окна и
            // двери (AttachedWallName). Ядру имена не нужны: оно получает уже
            // разрешённый индекс.
            Dictionary<string, int>? wallIndexByName = null;
            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                if (e != null && e.GetComponent<Wall>() != null)
                    (wallIndexByName ??= new Dictionary<string, int>())[e.gameObject.name] = i;
            }

            foreach (var e in elements)
                into.Add(Build(e, wallIndexByName));
        }

        private static ValidationElement Build(KitchenElement e, Dictionary<string, int>? wallIndexByName)
        {
            var wall = e.GetComponent<Wall>();
            var kind = KindOf(e, wall);

            // Высота стены меряется от ЛОГИЧЕСКОЙ позы: визуально стена бывает
            // подрезана (WallCutaway), а проём обязан помещаться в настоящую.
            float centerY = wall != null ? wall.FullPosition.y : e.transform.position.y;
            var heightSpan = Span.FromCenter(centerY, e.DimensionsMM.y * AppConstants.MM_TO_UNITS);

            int wallIndex = -1;
            string? attachedWallName = e is WindowElement win ? win.AttachedWallName
                : e is DoorElement door ? door.AttachedWallName
                : null;
            if (!string.IsNullOrEmpty(attachedWallName) && wallIndexByName != null
                && wallIndexByName.TryGetValue(attachedWallName!, out int found))
                wallIndex = found;

            return new ValidationElement(
                e.ToGeometry(),
                e.GetVertices(),
                kind,
                e.GroupId,
                (e as DrawerElement)?.PairedDrawerName,
                heightSpan,
                wallIndex);
        }

        private static ElementKind KindOf(KitchenElement e, Wall? wall)
        {
            var kind = ElementKind.None;

            bool isFloor = e.GetComponent<BasePlate>() != null || e is FloorElement;
            bool isOpening = e is WindowElement || e is DoorElement;

            if (isFloor) kind |= ElementKind.FloorAnchor;
            if (isOpening) kind |= ElementKind.Opening;
            // Якорь графа связности — пол, стена и проём в ней: к ним заземляется
            // всё остальное, сами они опоры не требуют.
            if (isFloor || isOpening || wall != null) kind |= ElementKind.Anchor;

            if (e is DrawerElement) kind |= ElementKind.Drawer;
            if (e is LightSourceElement) kind |= ElementKind.Decor;
            if (e is SinkElement || e is CooktopElement) kind |= ElementKind.Recessed;
            if (e is FacadeElement fe && fe.GapMM > 0) kind |= ElementKind.FloatingFacade;

            return kind;
        }
    }
}
