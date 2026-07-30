using System.Collections.Generic;

namespace KitchenDesigner.Core.Analysis
{
    /// <summary>
    /// Сборщик проблем сцены для окна анализа ошибок и MCP. Errors — коллизии
    /// (<see cref="ConstraintValidator"/>). Warnings — потенциальные дефекты
    /// сборки: почти-касания (недожатый снэп), зазоры фасада, ящик без фасада.
    /// Новый источник = новый Collect-метод + коды в <see cref="IssueCatalog"/>.
    /// Предупреждения НЕ подсвечиваются на сцене — только этот список.
    /// </summary>
    public static class SceneAnalyzer
    {
        /// <summary>Порог «почти касания», мм: зазор ≤ этого визуально трудно
        /// заметить и вероятно означает недожатый снэп.</summary>
        public const float NearContactMaxGapMm = 8f;

        /// <summary>Минимальный технологический зазор фасада с каждой стороны, мм.</summary>
        public const int FacadeMinGapMm = 1;

        public static List<AnalysisIssue> Analyze()
        {
            var issues = new List<AnalysisIssue>();
            var all = PartRegistry.GetAll();
            if (all == null || all.Count == 0) return issues;

            CollectCollisions(all, issues);
            CollectEdgeCover(all, issues);
            CollectNearContacts(all, issues);
            CollectPanelSeating(all, issues);
            CollectFacadeGaps(all, issues);
            CollectDrawerFacadeLinks(all, issues);
            CollectDishwasherFacadeLinks(all, issues);
            return issues;
        }

        // ── Errors: коллизии ─────────────────────────────────────────────
        private static void CollectCollisions(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            var result = ConstraintValidator.Validate(all);
            if (result.diagnostics == null) return;
            foreach (var diag in result.diagnostics)
                issues.Add(IssueCatalog.FromViolation(diag));
        }

        // ── Error: торец под кромку перекрыт частично ────────────────────
        // Кромку клеят на весь торец: если сосед закрывает его наполовину,
        // деталь либо не встанет на место (кромка мешает), либо кромка
        // оборвётся посередине. Полностью закрытый торец — норма (кромки нет),
        // полностью открытый — норма (кромка есть); ошибка ровно посередине.
        private static void CollectEdgeCover(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            // Планка или царга задевает торец на пару процентов — это нормальная
            // конструкция, а не наезд на кромку. Порог настраивается («Нижний
            // порог кромки»), 0 — сообщать о любом перекрытии.
            var settings = KitchenSettings.Instance;
            float minRatio = (settings != null ? settings.EdgePartialThresholdPct : 0) / 100f;

            foreach (var e in all)
            {
                if (e == null || !e.EdgeBandingEnabled) continue;

                var coverage = EdgeBanding.Coverage(e, all);
                var sides = new List<string>();

                // Виновник — сосед, накрывший торец больше всех. Торцов может быть
                // испорчено несколько, а вторая деталь в отчёте одна: берём того,
                // у кого площадь перекрытия максимальна по всем сторонам.
                KitchenElement? culprit = null;
                float culpritArea = 0f;

                foreach (EdgeSide side in System.Enum.GetValues(typeof(EdgeSide)))
                {
                    // Ручную сторону пользователь взял на себя: геометрия про неё
                    // больше не спорит.
                    if (e.IsEdgeManual(side)) continue;
                    if (!coverage.IsPartial(side)) continue;
                    if (coverage.Ratio(side) < minRatio) continue;
                    sides.Add($"{side} {coverage.Ratio(side) * 100f:F0}%");

                    var coverer = EdgeBanding.DominantCoverer(e, all, side, out float area);
                    if (coverer == null || area <= culpritArea) continue;
                    culprit = coverer;
                    culpritArea = area;
                }

                if (sides.Count > 0)
                    issues.Add(IssueCatalog.EdgePartialCover(e, string.Join(", ", sides), culprit));
            }
        }

        // ── Warning: почти касание (зазор ≤ порога) ──────────────────────
        private static void CollectNearContacts(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var nc in ConstraintValidator.FindNearContacts(all, NearContactMaxGapMm))
                issues.Add(IssueCatalog.NearContact(nc.a, nc.b, nc.gapMm));
        }

        // ── Warning: вкладная панель (ДВП) не дошла до дна паза ──────────
        private static void CollectPanelSeating(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var u in ConstraintValidator.FindUnseatedPanels(all))
                issues.Add(IssueCatalog.PanelNotSeated(u.panel, u.board, u.insertionMm, u.depthMm));
        }

        // ── Warning: зазоры фасада < минимума ────────────────────────────
        private static void CollectFacadeGaps(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is FacadeElement f)) continue;
                var bad = new List<string>();
                if (f.GapLeft < FacadeMinGapMm) bad.Add($"слева {f.GapLeft}");
                if (f.GapRight < FacadeMinGapMm) bad.Add($"справа {f.GapRight}");
                if (f.GapTop < FacadeMinGapMm) bad.Add($"сверху {f.GapTop}");
                if (f.GapBottom < FacadeMinGapMm) bad.Add($"снизу {f.GapBottom}");
                if (bad.Count > 0)
                    issues.Add(IssueCatalog.FacadeGap(f, string.Join(", ", bad)));
            }
        }

        // ── Warning: ящик без ссылки на фасад или фасад оторвался ─────────
        private static void CollectDrawerFacadeLinks(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is DrawerElement d)) continue;
                if (string.IsNullOrEmpty(d.AttachedFacadeName))
                {
                    // Верхний ящик двойной пары штатно без своего фасада — фасад у нижнего.
                    if (d.IsUpperDrawer && d.IsDouble) continue;
                    issues.Add(IssueCatalog.DrawerNoFacade(d));
                    continue;
                }
                // Фасад указан, но физического контакта нет — ящик или фасад сдвинули.
                var facade = FindFacade(all, d.AttachedFacadeName);
                if (facade != null && !DrawerLinks.IsFacadeInContact(d, facade))
                    issues.Add(IssueCatalog.DrawerFacadeOrphaned(d, facade));
            }
        }

        // ── Warning: фасад посудомоечной машины ──────────────────────────
        // Машина полновстраиваемая: без пристёгнутого фасада на кухне зияет
        // дыра, а фасад не той высоты не сходится с цоколем. Всё это
        // ПРЕДУПРЕЖДЕНИЯ, а не запреты: пользователь вправе собирать кухню в
        // любом порядке и доводить размеры потом — отказ на полпути сборки
        // только мешал бы (так же ведут себя зазоры фасада, FAC-01).
        private static void CollectDishwasherFacadeLinks(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is DishwasherElement dw)) continue;
                if (string.IsNullOrEmpty(dw.AttachedFacadeName))
                {
                    issues.Add(IssueCatalog.DishwasherNoFacade(dw));
                    continue;
                }

                var facade = FindFacade(all, dw.AttachedFacadeName);
                if (facade == null)
                {
                    // Фасад удалили, а имя осталось. У ящика этот случай
                    // молчал (DRW-02 требует найденный фасад) — здесь он
                    // сообщается: у машины фасад один и он обязателен, так что
                    // «ссылка есть, фасада нет» это уже дефект сборки.
                    issues.Add(IssueCatalog.DishwasherFacadeMissing(dw, dw.AttachedFacadeName));
                    continue;
                }

                if (!DrawerLinks.IsFacadeInContact(dw, facade))
                    issues.Add(IssueCatalog.DishwasherFacadeOrphaned(dw, facade));

                int facadeHeight = facade.DimensionsMM.y;
                if (!DishwasherElement.IsFacadeHeightValid(facadeHeight))
                    issues.Add(IssueCatalog.DishwasherFacadeHeight(dw, facade, facadeHeight));
            }
        }

        private static FacadeElement? FindFacade(List<KitchenElement> all, string name)
        {
            foreach (var e in all)
                if (e is FacadeElement f && f.PartName == name)
                    return f;
            return null;
        }
    }

    /// <summary>Каталог кодов: единственный источник «причина → код + уровень +
    /// текст». Коды стабильны (на них завязан фильтр по кодам) — только добавляются.
    /// Errors: COL-xx (коллизии). Warnings: GAP-xx (зазор), FAC-xx (фасад),
    /// DRW-xx (ящик), DWH-xx (посудомоечная машина).</summary>
    public static class IssueCatalog
    {
        // Коллизии геометрии (ConstraintValidator).
        public const string CodeOverlap = "COL-01";
        public const string CodeUnsupported = "COL-02";
        public const string CodeOutOfWallBounds = "COL-03";
        public const string CodeEdgePartialCover = "EDG-01";
        // Предупреждения.
        public const string CodeNearContact = "GAP-01";
        public const string CodePanelNotSeated = "SEAT-01";
        public const string CodeFacadeGap = "FAC-01";
        public const string CodeDrawerNoFacade = "DRW-01";
        public const string CodeDrawerFacadeOrphaned = "DRW-02";
        public const string CodeDishwasherNoFacade = "DWH-01";
        public const string CodeDishwasherFacadeOrphaned = "DWH-02";
        public const string CodeDishwasherFacadeHeight = "DWH-03";

        public static AnalysisIssue FromViolation(ContactViolation v)
        {
            switch (v.kind)
            {
                case ViolationKind.Overlap:
                    return new AnalysisIssue(IssueLevel.Error, CodeOverlap,
                        PairDetail(v.element, v.other), "Детали пересекаются в объёме",
                        v.element, v.other);

                case ViolationKind.Unsupported:
                    return new AnalysisIssue(IssueLevel.Error, CodeUnsupported,
                        Name(v.element), "Деталь не имеет опоры — висит в воздухе",
                        v.element);

                case ViolationKind.OutOfWallBounds:
                    return new AnalysisIssue(IssueLevel.Error, CodeOutOfWallBounds,
                        Name(v.element), "Элемент выходит за габарит стены",
                        v.element);

                default:
                    return new AnalysisIssue(IssueLevel.Error, "COL-00",
                        Name(v.element), "Нарушение геометрии", v.element);
            }
        }

        /// <summary>culprit — сосед, накрывший торец больше всех; может быть null,
        /// если перекрытие дают только якоря вроде пола или стены.</summary>
        public static AnalysisIssue EdgePartialCover(KitchenElement element, string sides,
            KitchenElement? culprit = null) =>
            culprit != null
                // Пара в колонке «Деталь» — как у GAP-01/COL-01: по клику
                // ErrorPanelUI выделяет обе детали, и виновник виден в сцене.
                ? new AnalysisIssue(IssueLevel.Error, CodeEdgePartialCover,
                    PairDetail(element, culprit),
                    $"Торец под кромку перекрыт частично: {sides}", element, culprit)
                : new AnalysisIssue(IssueLevel.Error, CodeEdgePartialCover,
                    Name(element), $"Торец под кромку перекрыт частично: {sides}", element);

        public static AnalysisIssue NearContact(KitchenElement a, KitchenElement b, float gapMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodeNearContact,
                PairDetail(a, b), $"Почти касается, зазор {gapMm:F1} мм (нет прямого контакта)",
                a, b);

        public static AnalysisIssue PanelNotSeated(KitchenElement panel, KitchenElement board,
            float insertionMm, float depthMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodePanelNotSeated,
                $"{Name(panel)} ↔ {Name(board)}",
                $"Панель вошла в паз не до дна: {insertionMm:F1} из {depthMm:F1} мм",
                panel, board);

        public static AnalysisIssue FacadeGap(KitchenElement facade, string sides) =>
            new AnalysisIssue(IssueLevel.Warning, CodeFacadeGap,
                Name(facade), $"Зазор фасада меньше {SceneAnalyzer.FacadeMinGapMm} мм: {sides}",
                facade);

        public static AnalysisIssue DrawerNoFacade(KitchenElement drawer) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDrawerNoFacade,
                Name(drawer), "Ящик без ссылки на фасад", drawer);

        public static AnalysisIssue DrawerFacadeOrphaned(KitchenElement drawer, KitchenElement? facade) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDrawerFacadeOrphaned,
                PairDetail(drawer, facade),
                $"Фасад ящика не на месте — {Name(drawer)} и {Name(facade)} не в контакте",
                drawer, facade);

        public static AnalysisIssue DishwasherNoFacade(KitchenElement dishwasher) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherNoFacade,
                Name(dishwasher), "Посудомойка без фасада — прибор полновстраиваемый, лица у него нет",
                dishwasher);

        /// <summary>Имя фасада есть, а самого фасада в сцене нет — его удалили.
        /// Пара для отчёта здесь одна: показывать нечего, кроме машины.</summary>
        public static AnalysisIssue DishwasherFacadeMissing(KitchenElement dishwasher, string facadeName) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeOrphaned,
                Name(dishwasher), $"Фасад «{facadeName}» удалён — посудомойка осталась без лица",
                dishwasher);

        public static AnalysisIssue DishwasherFacadeOrphaned(KitchenElement dishwasher, KitchenElement? facade) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeOrphaned,
                PairDetail(dishwasher, facade),
                $"Фасад посудомойки не на месте — {Name(dishwasher)} и {Name(facade)} не в контакте",
                dishwasher, facade);

        public static AnalysisIssue DishwasherFacadeHeight(KitchenElement dishwasher,
            KitchenElement? facade, int facadeHeightMM) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeHeight,
                PairDetail(dishwasher, facade),
                $"Высота фасада {facadeHeightMM} мм вне диапазона "
                + $"{DishwasherElement.FACADE_MIN_HEIGHT_MM}–{DishwasherElement.FACADE_MAX_HEIGHT_MM} мм: "
                + $"цоколь получится {DishwasherElement.PlinthForFacade(facadeHeightMM)} мм "
                + $"(допустимо {DishwasherElement.PLINTH_MIN_MM}–{DishwasherElement.PLINTH_MAX_MM})",
                dishwasher, facade);

        private static string Name(KitchenElement? e) =>
            e != null && !string.IsNullOrEmpty(e.PartName) ? e.PartName : "—";

        private static string PairDetail(KitchenElement? a, KitchenElement? b) =>
            b != null ? $"{Name(a)} ↔ {Name(b)}" : Name(a);
    }
}
