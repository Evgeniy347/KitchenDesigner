using System.Collections.Generic;

namespace KitchenDesigner.Core.Analysis
{
    public static class SceneAnalyzer
    {
        public const float NearContactMinGapMm = 2f;

        public const float NearContactMaxGapMm = 4f;

        public const float DishwasherBackGapMinMm = 5f;

        public const int FacadeMinGapMm = 1;

        public static List<AnalysisIssue> Analyze()
        {
            var issues = new List<AnalysisIssue>();
            var all = PartRegistry.GetAll();
            if (all == null || all.Count == 0) return issues;

            CollectCollisions(all, issues);
            CollectEdgeCover(all, issues);
            CollectNearContacts(all, issues);
            CollectDishwasherFacadeBackGaps(all, issues);
            CollectDishwasherSupport(all, issues);
            CollectPanelSeating(all, issues);
            CollectFacadeGaps(all, issues);
            CollectDrawerFacadeLinks(all, issues);
            CollectAttachLinks(all, issues);
            CollectDishwasherFacadeLinks(all, issues);
            CollectScrewLegMounting(all, issues);
            CollectScrewLegFooting(all, issues);
            CollectPipeRuns(all, issues);
            return issues;
        }

        private static void CollectCollisions(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            var result = ConstraintValidator.Validate(all);
            if (result.diagnostics == null) return;
            foreach (var diag in result.diagnostics)
                issues.Add(IssueCatalog.FromViolation(diag));
        }

        private static float MinReportedCoverageRatio()
        {
            var settings = KitchenSettings.Instance;
            return (settings != null ? settings.EdgePartialThresholdPct : 0) / 100f;
        }

        private static void CollectEdgeCover(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            float minReportedCoverageRatio = MinReportedCoverageRatio();

            foreach (var e in all)
            {
                if (e == null || !e.EdgeBandingEnabled) continue;

                var coverage = EdgeBanding.Coverage(e, all);
                var partiallyCoveredSides = new List<string>();

                KitchenElement? dominantCoverer = null;
                float dominantCoveredArea = 0f;

                foreach (EdgeSide side in System.Enum.GetValues(typeof(EdgeSide)))
                {
                    if (EdgeStates.IsExplicit(e.EdgeStateOf(side))) continue;
                    if (!coverage.IsPartial(side)) continue;
                    if (coverage.Ratio(side) < minReportedCoverageRatio) continue;
                    partiallyCoveredSides.Add($"{side} {coverage.Ratio(side) * 100f:F0}%");

                    var coverer = EdgeBanding.DominantCoverer(e, all, side, out float area);
                    if (coverer == null || area <= dominantCoveredArea) continue;
                    dominantCoverer = coverer;
                    dominantCoveredArea = area;
                }

                if (partiallyCoveredSides.Count > 0)
                    issues.Add(IssueCatalog.EdgePartialCover(e,
                        string.Join(", ", partiallyCoveredSides), dominantCoverer));
            }
        }

        private static void CollectNearContacts(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var nc in ConstraintValidator.FindNearContacts(all, NearContactMinGapMm, NearContactMaxGapMm))
            {
                if (nc.kind == ConstraintValidator.NearContactKind.TooSmall)
                    issues.Add(IssueCatalog.NearContact(nc.a, nc.b, nc.gapMm));
                else
                    issues.Add(IssueCatalog.NearContactFar(nc.a, nc.b, nc.gapMm));
            }
        }

        private static void CollectDishwasherFacadeBackGaps(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var d in ConstraintValidator.FindDishwasherFacadeBackGaps(all))
                issues.Add(IssueCatalog.DishwasherFacadeBackGap(d.dishwasher, d.facade, d.gapMm));
        }

        private static void CollectDishwasherSupport(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var s in ConstraintValidator.FindDishwasherSupportIssues(all))
                issues.Add(s.blocker != null
                    ? IssueCatalog.DishwasherSunk(s.dishwasher, s.blocker, s.sinkMm)
                    : IssueCatalog.DishwasherNoSupport(s.dishwasher));
        }

        private static void CollectPanelSeating(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var u in ConstraintValidator.FindUnseatedPanels(all))
                issues.Add(IssueCatalog.PanelNotSeated(u.panel, u.board, u.insertionMm, u.depthMm));
        }

        private static void CollectFacadeGaps(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is FacadeElement f)) continue;
                var tooSmallGaps = new List<string>();
                if (f.GapLeft < FacadeMinGapMm) tooSmallGaps.Add($"слева {f.GapLeft}");
                if (f.GapRight < FacadeMinGapMm) tooSmallGaps.Add($"справа {f.GapRight}");
                if (f.GapTop < FacadeMinGapMm) tooSmallGaps.Add($"сверху {f.GapTop}");
                if (f.GapBottom < FacadeMinGapMm) tooSmallGaps.Add($"снизу {f.GapBottom}");
                if (tooSmallGaps.Count > 0)
                    issues.Add(IssueCatalog.FacadeGap(f, string.Join(", ", tooSmallGaps)));
            }
        }

        private static bool FacadeBelongsToTheLowerHalfOfTheDoublePair(DrawerElement d) =>
            d.IsUpperDrawer && d.IsDouble;

        private static void CollectDrawerFacadeLinks(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is DrawerElement d)) continue;
                if (string.IsNullOrEmpty(d.AttachedFacadeName))
                {
                    if (FacadeBelongsToTheLowerHalfOfTheDoublePair(d)) continue;
                    issues.Add(IssueCatalog.DrawerNoFacade(d));
                    continue;
                }

                var facade = FindFacade(all, d.AttachedFacadeName);
                if (facade != null && !DrawerLinks.IsFacadeInContact(d, facade))
                    issues.Add(IssueCatalog.DrawerFacadeOrphaned(d, facade));
            }
        }

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

        private static void CollectAttachLinks(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (e == null || string.IsNullOrEmpty(e.AttachedToName)) continue;
                var parent = AttachLinks.Parent(e);
                if (parent == null) continue;
                if (!AttachLinks.InContact(e, parent))
                    issues.Add(IssueCatalog.AttachDetached(e, parent));
            }
        }

        private static void CollectScrewLegMounting(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is ScrewLegElement leg)) continue;
                var host = AttachLinks.Parent(leg);
                if (host == null) continue;
                if (ScrewLegCentring.TryFindOffCentre(leg, host, out var offCentre))
                    issues.Add(IssueCatalog.ScrewLegOffCentre(leg, host, offCentre));

                int insertionMM = leg.InsertionIntoMM(host);
                if (!ScrewLegSpec.InsertionHolds(insertionMM))
                    issues.Add(IssueCatalog.ScrewLegShallow(leg, host, insertionMM));
            }
        }

        private static void CollectScrewLegFooting(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is ScrewLegElement leg)) continue;
                if (!ScrewLegFooting.TryFindUnsupported(leg, all, out var below)) continue;
                issues.Add(IssueCatalog.ScrewLegNoFooting(leg, below));
            }
        }

        private static void CollectPipeRuns(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var finding in Plumbing.PipeRules.Collect(new ScenePipeSnapshot(all)))
                issues.Add(IssueCatalog.FromPipeFinding(finding,
                    FindByName(all, finding.ElementId), FindByName(all, finding.OtherElementId)));
        }

        private static KitchenElement? FindByName(List<KitchenElement> all, string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var e in all)
                if (e != null && e.PartName == name) return e;
            return null;
        }

        private static FacadeElement? FindFacade(List<KitchenElement> all, string name)
        {
            foreach (var e in all)
                if (e is FacadeElement f && f.PartName == name)
                    return f;
            return null;
        }
    }

    public static class IssueCatalog
    {
        public const string CodeOverlap = "COL-01";
        public const string CodeUnsupported = "COL-02";
        public const string CodeOutOfWallBounds = "COL-03";
        public const string CodeUnknownViolation = "COL-00";
        public const string CodeEdgePartialCover = "EDG-01";
        public const string CodeNearContact = "GAP-01";
        public const string CodeNearContactFar = "GAP-02";
        public const string CodePanelNotSeated = "SEAT-01";
        public const string CodeFacadeGap = "FAC-01";
        public const string CodeDrawerNoFacade = "DRW-01";
        public const string CodeDrawerFacadeOrphaned = "DRW-02";
        public const string CodeDishwasherNoFacade = "DWH-01";
        public const string CodeDishwasherFacadeOrphaned = "DWH-02";
        public const string CodeDishwasherFacadeHeight = "DWH-03";
        public const string CodeDishwasherBackGap = "DWH-04";
        public const string CodeDishwasherNoSupport = "DWH-05";
        public const string CodeAttachDetached = "ATT-01";
        public const string CodeScrewLegOffCentre = "LEG-01";
        public const string CodeScrewLegShallow = "LEG-02";
        public const string CodeScrewLegNoFooting = "LEG-03";
        public const string CodePipeOpenEnd = "PIP-01";
        public const string CodePipeSizeMismatch = "PIP-02";
        public const string CodePipeObstacleCrossed = "PIP-03";
        public const string CodePipeSameRoleJoin = "PIP-04";

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
                    return new AnalysisIssue(IssueLevel.Error, CodeUnknownViolation,
                        Name(v.element), "Нарушение геометрии", v.element);
            }
        }

        public static AnalysisIssue EdgePartialCover(KitchenElement element, string sides,
            KitchenElement? dominantCoverer = null) =>
            dominantCoverer != null
                ? new AnalysisIssue(IssueLevel.Error, CodeEdgePartialCover,
                    PairDetail(element, dominantCoverer),
                    $"Торец под кромку перекрыт частично: {sides}", element, dominantCoverer)
                : new AnalysisIssue(IssueLevel.Error, CodeEdgePartialCover,
                    Name(element), $"Торец под кромку перекрыт частично: {sides}", element);

        public static AnalysisIssue NearContact(KitchenElement a, KitchenElement b, float gapMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodeNearContact,
                PairDetail(a, b),
                $"Зазор по оси {gapMm:F1} мм — требуется ≥{SceneAnalyzer.NearContactMinGapMm:F0} мм (нет прямого контакта)",
                a, b);

        public static AnalysisIssue NearContactFar(KitchenElement a, KitchenElement b, float gapMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodeNearContactFar,
                PairDetail(a, b),
                $"Зазор по оси {gapMm:F1} мм — слишком большой (допустимо ≤{SceneAnalyzer.NearContactMaxGapMm:F0} мм)",
                a, b);

        public static AnalysisIssue DishwasherFacadeBackGap(KitchenElement dishwasher, KitchenElement facade,
            float gapMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherBackGap,
                PairDetail(dishwasher, facade),
                $"Задний зазор фасада {gapMm:F1} мм — требуется ≥{SceneAnalyzer.DishwasherBackGapMinMm:F0} мм",
                dishwasher, facade);

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

        public static AnalysisIssue DishwasherFacadeMissing(KitchenElement dishwasher, string facadeName) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeOrphaned,
                Name(dishwasher), $"Фасад «{facadeName}» удалён — посудомойка осталась без лица",
                dishwasher);

        public static AnalysisIssue DishwasherFacadeOrphaned(KitchenElement dishwasher, KitchenElement? facade) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeOrphaned,
                PairDetail(dishwasher, facade),
                $"Фасад посудомойки не на месте — {Name(dishwasher)} и {Name(facade)} не в контакте",
                dishwasher, facade);

        public static AnalysisIssue DishwasherNoSupport(KitchenElement dishwasher) =>
            new AnalysisIssue(IssueLevel.Error, CodeDishwasherNoSupport,
                Name(dishwasher),
                "Посудомойке не на чем стоять — под низом нужен пол, цоколь или опорная деталь",
                dishwasher);

        public static AnalysisIssue DishwasherSunk(KitchenElement dishwasher, KitchenElement? blocker,
            float sinkMm) =>
            new AnalysisIssue(IssueLevel.Error, CodeDishwasherNoSupport,
                PairDetail(dishwasher, blocker),
                $"Посудомойка утоплена в {Name(blocker)} на {sinkMm:F0} мм — низ прибора должен стоять на опоре",
                dishwasher, blocker);

        public static AnalysisIssue DishwasherFacadeHeight(KitchenElement dishwasher,
            KitchenElement? facade, int facadeHeightMM) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeHeight,
                PairDetail(dishwasher, facade),
                $"Высота фасада {facadeHeightMM} мм вне диапазона "
                + $"{DishwasherElement.FACADE_MIN_HEIGHT_MM}–{DishwasherElement.FACADE_MAX_HEIGHT_MM} мм: "
                + $"цоколь получится {DishwasherElement.PlinthForFacade(facadeHeightMM)} мм "
                + $"(допустимо {DishwasherElement.PLINTH_MIN_MM}–{DishwasherElement.PLINTH_MAX_MM})",
                dishwasher, facade);

        public static AnalysisIssue AttachDetached(KitchenElement child, KitchenElement parent) =>
            new AnalysisIssue(IssueLevel.Error, CodeAttachDetached,
                PairDetail(child, parent),
                $"Прикреплённая деталь отошла от родителя — {Name(child)} и {Name(parent)} не в контакте",
                child, parent);

        public static AnalysisIssue ScrewLegOffCentre(KitchenElement leg, KitchenElement host,
            ScrewLegOffCentre offCentre) =>
            new AnalysisIssue(IssueLevel.Warning, CodeScrewLegOffCentre,
                PairDetail(leg, host),
                $"Футорке не за что держаться: сторона {offCentre.Axis} у {Name(host)} — "
                + $"{offCentre.SpanMM:F1} мм (тоньше {ScrewLegSpec.CENTRING_REQUIRED_SPAN_MM} мм), "
                + $"смещение {offCentre.OffsetMM:F1} мм оставило стенку {offCentre.WallMM:F1} мм "
                + $"вместо {ScrewLegSpec.MIN_INSERT_WALL_MM:F0} мм",
                leg, host);

        public static AnalysisIssue ScrewLegShallow(KitchenElement leg, KitchenElement host,
            int insertionMM) =>
            new AnalysisIssue(IssueLevel.Error, CodeScrewLegShallow,
                PairDetail(leg, host),
                $"Резьба вошла в {Name(host)} на {insertionMM} мм — "
                + $"требуется {ScrewLegSpec.MIN_INSERTION_INTO_HOST_MM} мм",
                leg, host);

        public static AnalysisIssue ScrewLegNoFooting(KitchenElement leg, ScrewLegSupport below) =>
            below.Nearest != null
                ? new AnalysisIssue(IssueLevel.Warning, CodeScrewLegNoFooting,
                    PairDetail(leg, below.Nearest),
                    $"Опоре не на чем стоять: от низа пятака до {Name(below.Nearest)} "
                    + $"{below.GapMM:F1} мм — низ опоры обязан касаться пола или детали "
                    + $"(допуск {Tolerance.ContactMm:F1} мм)",
                    leg, below.Nearest)
                : new AnalysisIssue(IssueLevel.Warning, CodeScrewLegNoFooting,
                    Name(leg),
                    "Опоре не на чем стоять: под пятаком нет ни пола, ни детали",
                    leg);

        public static AnalysisIssue FromPipeFinding(Plumbing.PipeFinding finding,
            KitchenElement? element, KitchenElement? other) =>
            new AnalysisIssue(
                finding.Level == Plumbing.PipeFindingLevel.Error ? IssueLevel.Error : IssueLevel.Warning,
                finding.Code, PairDetail(element, other), finding.Message, element, other);

        private static string Name(KitchenElement? e) =>
            e != null && !string.IsNullOrEmpty(e.PartName) ? e.PartName : "—";

        private static string PairDetail(KitchenElement? a, KitchenElement? b) =>
            b != null ? $"{Name(a)} ↔ {Name(b)}" : Name(a);
    }
}
