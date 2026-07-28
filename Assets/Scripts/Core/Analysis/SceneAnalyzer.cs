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
    /// DRW-xx (ящик).</summary>
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

        private static string Name(KitchenElement? e) =>
            e != null && !string.IsNullOrEmpty(e.PartName) ? e.PartName : "—";

        private static string PairDetail(KitchenElement? a, KitchenElement? b) =>
            b != null ? $"{Name(a)} ↔ {Name(b)}" : Name(a);
    }
}
