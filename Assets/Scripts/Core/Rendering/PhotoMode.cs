using System;

namespace KitchenDesigner.Core
{
    /// <summary>Фоторежим: стены и пол не прячутся/не опускаются, все объекты
    /// становятся непрозрачными и показывают свои материалы, по стенам достраивается
    /// потолок, а качество картинки поднимается через URP (тени, сглаживание,
    /// пост-обработка). Обычный рабочий режим — лёгкий; всё тяжёлое включается только
    /// здесь и полностью откатывается на выходе.</summary>
    public static class PhotoMode
    {
        /// <summary>Производное от режима редактора: фоторежим — это
        /// <see cref="EditMode.Photo"/> и ничто иное. Отдельного флага нет
        /// намеренно — пока их было два, они расходились (Reset в обход SetMode
        /// оставлял фоторежим включённым при Mode = Normal).</summary>
        public static bool Active => EditModeManager.Mode == EditMode.Photo;

        /// <summary>Срабатывает при любой смене состояния (вход/выход).</summary>
        public static event Action? Changed;

        private static bool _prevTintEnabled;

        /// <summary>Прозрачность объекта подавляется в фоторежиме: в кадре всё
        /// должно быть видимым.</summary>
        public static bool ResolveTransparent(bool elementTransparent) =>
            ResolveTransparent(elementTransparent, Active);

        /// <summary>Чистая версия с явной активностью — для юнит-тестов.</summary>
        public static bool ResolveTransparent(bool elementTransparent, bool photoActive) =>
            elementTransparent && !photoActive;

        public static void Toggle() => SetActive(!Active);

        /// <summary>Фасад для F10 и тумблера в настройках: фоторежим — это режим
        /// редактора, поэтому включение идёт через <see cref="EditModeManager"/>,
        /// а тяжёлые эффекты применяет он же (<see cref="Enter"/>/<see cref="Exit"/>).</summary>
        public static void SetActive(bool on)
        {
            if (on == Active) return;
            EditModeManager.SetMode(on ? EditMode.Photo : EditMode.Normal);
        }

        /// <summary>Вход в фоторежим. Вызывает только EditModeManager.SetMode —
        /// он владеет состоянием режима.</summary>
        internal static void Enter()
        {
            // 1) материалы: снять прозрачность и валидационный тон,
            // 2) пересчитать рендереры (форс-непрозрачность применяется здесь),
            // 3) сцена: потолок, тени-кастеры, качество — уже по «финальным» материалам.
            _prevTintEnabled = ElementHighlighter.TintEnabled;
            ElementHighlighter.TintEnabled = false;
            RefreshHighlights();
            RebuildCeiling();
            PhotoShadowCasters.Enable();
            PhotoQualityController.Apply();
        }

        /// <summary>Выход из фоторежима: всё тяжёлое откатывается полностью.</summary>
        internal static void Exit()
        {
            PhotoQualityController.Restore();
            PhotoShadowCasters.Restore();
            CeilingBuilder.Clear();
            ElementHighlighter.TintEnabled = _prevTintEnabled;
            RefreshHighlights();
        }

        /// <summary>Сообщить подписчикам о смене состояния. Поднимает
        /// EditModeManager после того, как режим уже переключён.</summary>
        internal static void RaiseChanged() => Changed?.Invoke();

        private static void RefreshHighlights()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        /// <summary>Пере-применить качество и потолок, если режим активен
        /// (после смены пресета/эффектов в настройках).</summary>
        public static void RefreshIfActive()
        {
            if (!Active) return;
            RebuildCeiling();
            PhotoShadowCasters.Enable();
            PhotoQualityController.Apply();
        }

        private static void RebuildCeiling()
        {
            var s = KitchenSettings.Instance;
            if (s != null && s.PhotoCeiling) CeilingBuilder.Rebuild();
            else CeilingBuilder.Clear();
        }
    }
}
