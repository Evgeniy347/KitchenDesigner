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
        public static bool Active { get; private set; }

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

        public static void SetActive(bool on)
        {
            if (on == Active) return;
            Active = on;

            if (on) Enter();
            else Exit();

            // Пересчитать материалы: снять/вернуть прозрачность и валидационную
            // тонировку у всех деталей.
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            Changed?.Invoke();
        }

        /// <summary>Пере-применить качество и потолок, если режим активен
        /// (после смены пресета/эффектов в настройках).</summary>
        public static void RefreshIfActive()
        {
            if (!Active) return;
            RebuildCeiling();
            PhotoQualityController.Apply();
        }

        private static void Enter()
        {
            // Показать реальные материалы вместо валидационного тона.
            _prevTintEnabled = ElementHighlighter.TintEnabled;
            ElementHighlighter.TintEnabled = false;

            RebuildCeiling();
            PhotoQualityController.Apply();
        }

        private static void Exit()
        {
            ElementHighlighter.TintEnabled = _prevTintEnabled;
            CeilingBuilder.Clear();
            PhotoQualityController.Restore();
        }

        private static void RebuildCeiling()
        {
            var s = KitchenSettings.Instance;
            if (s != null && s.PhotoCeiling) CeilingBuilder.Rebuild();
            else CeilingBuilder.Clear();
        }
    }
}
