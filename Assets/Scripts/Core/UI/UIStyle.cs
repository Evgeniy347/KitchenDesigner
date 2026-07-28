using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    /// <summary>
    /// Дизайн-токены UI: цвета, размеры, отступы, шрифты и глифы.
    /// Единственный источник стиля — локальные цветовые константы в панелях
    /// запрещены (см. docs/UI-GUIDELINES.md, п. 10).
    /// </summary>
    public static class UIStyle
    {
        // ── Цвета ──────────────────────────────────────────────────────
        public static readonly Color Panel = new Color(0.12f, 0.12f, 0.14f, 0.92f);
        public static readonly Color Surface = new Color(0.22f, 0.24f, 0.30f, 1f);
        public static readonly Color Field = new Color(0.08f, 0.08f, 0.10f, 1f);
        public static readonly Color Accent = new Color(0.30f, 0.50f, 0.75f, 1f);
        public static readonly Color Danger = new Color(0.62f, 0.20f, 0.20f, 1f);
        public static readonly Color Text = new Color(0.92f, 0.92f, 0.92f, 1f);
        public static readonly Color TextSecondary = new Color(0.60f, 0.62f, 0.66f, 1f);
        public static readonly Color TextDisabled = new Color(0.55f, 0.55f, 0.55f, 1f);
        /// <summary>Жёлтая рамка «значение изменено, но не применено».</summary>
        public static readonly Color HighlightChanged = new Color(1f, 0.84f, 0.0f, 1f);
        /// <summary>Красная рамка «введённое значение не принято».</summary>
        public static readonly Color HighlightError = new Color(0.90f, 0.25f, 0.25f, 1f);
        /// <summary>Фон нажатого тоггла тулбара / активной вкладки.</summary>
        public static readonly Color SurfaceActive = new Color(0.28f, 0.33f, 0.42f, 1f);
        public static readonly Color Separator = new Color(0.35f, 0.37f, 0.42f, 1f);

        // ── Схема кромок детали ────────────────────────────────────────
        /// <summary>Торец с кромкой (открытая поверхность).</summary>
        public static readonly Color EdgePresent = new Color(0.30f, 0.75f, 0.35f, 1f);
        /// <summary>Торец без кромки (упирается в соседа).</summary>
        public static readonly Color EdgeAbsent = new Color(0.72f, 0.74f, 0.78f, 1f);
        /// <summary>Заливка пласти на схеме кромок.</summary>
        public static readonly Color EdgeBoard = new Color(0.18f, 0.19f, 0.22f, 1f);
        /// <summary>Сторона в полуручном режиме: кромку назначил человек,
        /// автоматическая проверка на неё не смотрит.</summary>
        public static readonly Color EdgeManualSide = new Color(0.95f, 0.80f, 0.25f, 1f);
        /// <summary>Подсветка стороны на самой детали в 3D (светло-жёлтая,
        /// полупрозрачная — под ней должна читаться текстура детали).</summary>
        public static readonly Color EdgeHighlight3D = new Color(1f, 0.95f, 0.55f, 0.55f);

        // ── Рулетка (замеры поверх сцены) ──────────────────────────────
        /// <summary>Розовая точка-подсказка: вершина под курсором.</summary>
        public static readonly Color MeasureHint = new Color(1f, 0.35f, 0.75f, 1f);
        /// <summary>Красная точка зафиксированного конца замера и сам отрезок.</summary>
        public static readonly Color MeasureLine = new Color(0.95f, 0.15f, 0.15f, 1f);
        /// <summary>Светло-жёлтая подсветка отрезка под курсором.</summary>
        public static readonly Color MeasureHover = new Color(1f, 0.95f, 0.55f, 1f);
        /// <summary>Прозрачный жёлтый цилиндр вокруг выбранного отрезка.</summary>
        public static readonly Color MeasureSelected = new Color(1f, 0.85f, 0.1f, 0.25f);

        // ── Размеры и ритм (правило 8) ────────────────────────────────
        /// <summary>Минимальный хит-таргет кликабельных элементов.</summary>
        public const float HitTarget = 32f;
        /// <summary>Отступ внутри смысловой группы.</summary>
        public const float GapInner = 8f;
        /// <summary>Отступ между смысловыми группами.</summary>
        public const float GapSection = 16f;
        /// <summary>Паддинг контента окна со всех сторон.</summary>
        public const float WindowPad = 12f;
        /// <summary>Кнопка закрытия окна: размер и отступ от углов (правило 7).</summary>
        public const float CloseBtnSize = 32f;
        public const float CloseBtnInset = 8f;

        // ── Шрифты ─────────────────────────────────────────────────────
        public const int FontTitle = 20;
        public const int FontSection = 15;
        public const int FontBody = 16;
        public const int FontSmall = 14;

        // ── Глифы (правило 4: один глиф — одно значение) ───────────────
        // Только символы из WGL4 — рантайм-атлас TMP собирается из
        // LiberationSans, и глифы вне WGL4 (✓, ✕, ▸) в нём отсутствуют.
        /// <summary>Закрыть окно/панель. Только для этого.</summary>
        public const string GlyphClose = "×";
        /// <summary>Свёрнуто (раскрывашка).</summary>
        public const string GlyphCollapsed = "►";
        /// <summary>Развёрнуто (раскрывашка).</summary>
        public const string GlyphExpanded = "▼";
        /// <summary>Стрелка выпадающего списка.</summary>
        public const string GlyphDropdown = "▼";
        /// <summary>Замер не по оси (диагональ). Именно «∟» (U+221F): знак угла
        /// «∠» (U+2220) вне WGL4 и в рантайм-атласе TMP отсутствует.</summary>
        public const string GlyphAngle = "∟";
    }
}
