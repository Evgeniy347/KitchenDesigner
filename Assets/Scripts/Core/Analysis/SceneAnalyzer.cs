using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace KitchenDesigner.Core.Analysis
{
    public static class SceneAnalyzer
    {
        public const float NearContactMinGapMm = 2f;

        public const float NearContactMaxGapMm = 4f;

        public const float DishwasherBackGapMinMm = 5f;

        public const int FacadeMinGapMm = 1;

        private static readonly string[] StageNames =
        {
            "collisions", "edgeCover", "nearContacts", "dishwasherFacadeBackGaps",
            "dishwasherSupport", "panelSeating", "facadeGaps", "drawerFacadeLinks",
            "attachLinks", "dishwasherFacadeLinks", "screwLegMounting", "screwLegFooting",
            "pipeRuns", "unknownTypes", "millimetreGrid", "foundation", "floorSlab",
            "wallLayerHosts", "ventilation",
        };

        public static int MostStagesRemembered => StageNames.Length;

        private const float SlowAnalyzeMs = 20f;

        [ThreadStatic] private static NamedTally? _stageBreakdown;

        public static List<AnalysisIssue> Analyze()
        {
            using var _ = PerfMarkers.SceneAnalyzerAnalyze.Auto();
            long began = Stopwatch.GetTimestamp();
            _stageBreakdown?.Clear();

            var issues = new List<AnalysisIssue>();
            var all = PartRegistry.GetAll();
            if (all == null || all.Count == 0) return issues;
            var byName = AttachLinks.PartsByName.Of(all);

            int stage = 0;
            long t = Stopwatch.GetTimestamp();
            using (PerfMarkers.AnalyzeCollisions.Auto()) CollectCollisions(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeEdgeCover.Auto()) CollectEdgeCover(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeNearContacts.Auto()) CollectNearContacts(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeDishwasherFacadeBackGaps.Auto()) CollectDishwasherFacadeBackGaps(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeDishwasherSupport.Auto()) CollectDishwasherSupport(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzePanelSeating.Auto()) CollectPanelSeating(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeFacadeGaps.Auto()) CollectFacadeGaps(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeDrawerFacadeLinks.Auto()) CollectDrawerFacadeLinks(all, byName, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeAttachLinks.Auto()) CollectAttachLinks(all, byName, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeDishwasherFacadeLinks.Auto()) CollectDishwasherFacadeLinks(all, byName, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeScrewLegMounting.Auto()) CollectScrewLegMounting(all, byName, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeScrewLegFooting.Auto()) CollectScrewLegFooting(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzePipeRuns.Auto()) CollectPipeRuns(all, byName, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeUnknownTypes.Auto()) CollectUnknownTypes(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeMillimetreGrid.Auto()) CollectMillimetreGrid(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeFoundation.Auto()) CollectFoundation(all, byName, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeFloorSlab.Auto()) CollectFloorSlab(all, byName, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeWallLayerHosts.Auto()) CollectWallLayerHosts(all, issues);
            t = NoteStage(StageNames[stage++], t);
            using (PerfMarkers.AnalyzeVentilation.Auto()) CollectVentilation(all, byName, issues);
            NoteStage(StageNames[stage++], t);

            LogBreakdownIfSlow(began);
            return issues;
        }

        private static long NoteStage(string name, long since)
        {
            long now = Stopwatch.GetTimestamp();
            (_stageBreakdown ??= new NamedTally(MostStagesRemembered)).Add(name, now - since);
            return now;
        }

        public static string TakeStageBreakdown()
        {
            string text = PeekStageBreakdown();
            _stageBreakdown?.Clear();
            return text;
        }

        private static string PeekStageBreakdown()
        {
            var stages = _stageBreakdown;
            if (stages == null || stages.IsEmpty) return string.Empty;

            return stages.Format((name, ticks, times) =>
                times > 1
                    ? name + " " + MsOf(ticks).ToString("F2") + "мс×" + times
                    : name + " " + MsOf(ticks).ToString("F2") + "мс");
        }

        private static void LogBreakdownIfSlow(long began)
        {
            double totalMs = MsOf(Stopwatch.GetTimestamp() - began);
            if (totalMs < SlowAnalyzeMs) return;

            LogBreakdownNow(totalMs);
        }

        private static void LogBreakdownNow(double totalMs)
        {
            string breakdown = PeekStageBreakdown();
            if (!string.IsNullOrEmpty(breakdown))
                UnityEngine.Debug.Log(
                    $"[Perf] SceneAnalyzer.Analyze {totalMs:F1}мс — {breakdown}");
        }

        internal static void ForceSlowPathLogForTests() => LogBreakdownNow(0);

        private static double MsOf(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

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
            var scene = SceneFaces.Of(all);

            for (int k = 0; k < scene.Count; k++)
            {
                var e = scene.ElementAt(k);
                if (e == null || !e.EdgeBandingEnabled) continue;

                var coverage = EdgeBanding.Coverage(e, scene, k);
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
            foreach (var d in DishwasherFitting.FindFacadeBackGaps(all))
                issues.Add(IssueCatalog.DishwasherFacadeBackGap(d.dishwasher, d.facade, d.gapMm));
        }

        private static void CollectDishwasherSupport(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var s in DishwasherFitting.FindSupportIssues(all))
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
                if (f.GapLeft < FacadeMinGapMm) tooSmallGaps.Add(Loc.F("issue.fac01.sideLeft", f.GapLeft));
                if (f.GapRight < FacadeMinGapMm) tooSmallGaps.Add(Loc.F("issue.fac01.sideRight", f.GapRight));
                if (f.GapTop < FacadeMinGapMm) tooSmallGaps.Add(Loc.F("issue.fac01.sideTop", f.GapTop));
                if (f.GapBottom < FacadeMinGapMm) tooSmallGaps.Add(Loc.F("issue.fac01.sideBottom", f.GapBottom));
                if (tooSmallGaps.Count > 0)
                    issues.Add(IssueCatalog.FacadeGap(f, string.Join(", ", tooSmallGaps)));
            }
        }

        private static bool FacadeBelongsToTheLowerHalfOfTheDoublePair(DrawerElement d) =>
            d.IsUpperDrawer && d.IsDouble;

        private static void CollectDrawerFacadeLinks(List<KitchenElement> all, AttachLinks.PartsByName byName,
            List<AnalysisIssue> issues)
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

                var facade = FindFacade(byName, d.AttachedFacadeName);
                if (facade != null && !DrawerLinks.IsFacadeInContact(d, facade))
                    issues.Add(IssueCatalog.DrawerFacadeOrphaned(d, facade));
            }
        }

        private static void CollectDishwasherFacadeLinks(List<KitchenElement> all, AttachLinks.PartsByName byName,
            List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is DishwasherElement dw)) continue;
                if (string.IsNullOrEmpty(dw.AttachedFacadeName))
                {
                    issues.Add(IssueCatalog.DishwasherNoFacade(dw));
                    continue;
                }

                var facade = FindFacade(byName, dw.AttachedFacadeName);
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

        private static void CollectAttachLinks(List<KitchenElement> all, AttachLinks.PartsByName byName,
            List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (e == null || string.IsNullOrEmpty(e.AttachedToName)) continue;
                var parent = AttachLinks.Parent(e, byName);
                if (parent == null) continue;
                if (!AttachLinks.InContact(e, parent))
                    issues.Add(IssueCatalog.AttachDetached(e, parent));
            }
        }

        private static void CollectWallLayerHosts(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is WallLayerElement layer) || string.IsNullOrEmpty(layer.HostWallName)) continue;
                if (layer.ResolveHostWall() == null)
                    issues.Add(IssueCatalog.WallLayerHostMissing(layer, layer.HostWallName));
            }
        }

        private static void CollectScrewLegMounting(List<KitchenElement> all, AttachLinks.PartsByName byName,
            List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is ScrewLegElement leg)) continue;
                var host = AttachLinks.Parent(leg, byName);
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

        private static void CollectPipeRuns(List<KitchenElement> all, AttachLinks.PartsByName byName,
            List<AnalysisIssue> issues)
        {
            foreach (var finding in Plumbing.PipeRules.Collect(new ScenePipeSnapshot(all)))
                issues.Add(IssueCatalog.FromPipeFinding(finding,
                    FindByName(byName, finding.ElementId), FindByName(byName, finding.OtherElementId)));
        }

        private static void CollectUnknownTypes(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (e == null) continue;
                var marker = UnknownTypeMarker.On(e);
                if (marker == null || string.IsNullOrEmpty(marker.TypeId)) continue;
                issues.Add(IssueCatalog.UnknownType(e, marker.TypeId));
            }
        }

        private static void CollectMillimetreGrid(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (e == null) continue;
                if (!MmGrid.TryMinCornerMm(e, out var minCornerMm)) continue;
                var message = MmGridIssueCatalog.OffMillimetreGrid(minCornerMm,
                    MmGridMath.OffGridFindingToleranceMm);
                if (message == null) continue;
                issues.Add(IssueCatalog.OffMillimetreGrid(e, message));
            }
        }

        private static void CollectFoundation(List<KitchenElement> all, AttachLinks.PartsByName byName,
            List<AnalysisIssue> issues)
        {
            var foundations = SceneFoundationSnapshot.Foundations(all);
            var walls = SceneFoundationSnapshot.LoadBearingWalls(all);
            foreach (var finding in Construction.FoundationRules.Collect(foundations, walls))
                issues.Add(IssueCatalog.FromConstructionFinding(finding, FindByName(byName, finding.ElementId)));
        }

        private static void CollectFloorSlab(List<KitchenElement> all, AttachLinks.PartsByName byName,
            List<AnalysisIssue> issues)
        {
            var slabs = SceneSlabSnapshot.Slabs(all);
            foreach (var finding in Construction.FloorSlabRules.Collect(slabs))
                issues.Add(IssueCatalog.FromConstructionFinding(finding, FindByName(byName, finding.ElementId)));
        }

        private static void CollectVentilation(List<KitchenElement> all, AttachLinks.PartsByName byName,
            List<AnalysisIssue> issues)
        {
            var survey = SceneDuctSnapshot.Survey(all);
            foreach (var finding in Ventilation.DuctRules.Collect(survey))
                issues.Add(IssueCatalog.FromConstructionFinding(finding, FindByName(byName, finding.ElementId),
                    FindByName(byName, finding.OtherElementId)));
        }

        private static KitchenElement? FindByName(AttachLinks.PartsByName byName, string? name) =>
            byName.First(name);

        private static FacadeElement? FindFacade(AttachLinks.PartsByName byName, string name) =>
            byName.First(name) as FacadeElement;
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
        public const string CodeWallThicknessOffFormat = "WAL-01";
        public const string CodeUnknownElementType = "TYP-01";
        public const string CodeOffMillimetreGrid = MmGridIssueCatalog.CodeOffMillimetreGrid;
        public const string CodeFoundationDepthBelowFrost = Construction.FoundationFindings.CodeDepthBelowFrost;
        public const string CodeFoundationSoleTooNarrow = Construction.FoundationFindings.CodeSoleTooNarrow;
        public const string CodeFoundationCushionTooThin = Construction.FoundationFindings.CodeCushionTooThin;
        public const string CodeFoundationRebarProtection = Construction.FoundationFindings.CodeRebarProtection;
        public const string CodeFoundationWallNotCovered = Construction.FoundationFindings.CodeWallNotCovered;
        public const string CodeFloorSlabGapToSupportingWall = Construction.FloorSlabRules.CodeGapToSupportingWall;
        public const string CodeDuctVelocity = Ventilation.DuctIssueCatalog.CodeVelocity;
        public const string CodeDuctProfileMismatch = Ventilation.DuctIssueCatalog.CodeProfileMismatch;
        public const string CodeDuctAirExchange = Ventilation.DuctIssueCatalog.CodeAirExchange;

        public static AnalysisIssue OffMillimetreGrid(KitchenElement element, string message) =>
            new AnalysisIssue(IssueLevel.Warning, CodeOffMillimetreGrid,
                Name(element), message, element);

        public static AnalysisIssue UnknownType(KitchenElement element, string typeId) =>
            new AnalysisIssue(IssueLevel.Warning, CodeUnknownElementType,
                Name(element),
                Loc.F("issue.typ01.message", typeId),
                element);

        public static AnalysisIssue FromViolation(ContactViolation v)
        {
            switch (v.kind)
            {
                case ViolationKind.Overlap:
                    return new AnalysisIssue(IssueLevel.Error, CodeOverlap,
                        PairDetail(v.element, v.other), Loc.T("issue.col01.message"),
                        v.element, v.other);

                case ViolationKind.Unsupported:
                    return new AnalysisIssue(IssueLevel.Error, CodeUnsupported,
                        Name(v.element), Loc.T("issue.col02.message"),
                        v.element);

                case ViolationKind.OutOfWallBounds:
                    return new AnalysisIssue(IssueLevel.Error, CodeOutOfWallBounds,
                        Name(v.element), Loc.T("issue.col03.message"),
                        v.element);

                default:
                    return new AnalysisIssue(IssueLevel.Error, CodeUnknownViolation,
                        Name(v.element), Loc.T("issue.col00.message"), v.element);
            }
        }

        public static AnalysisIssue EdgePartialCover(KitchenElement element, string sides,
            KitchenElement? dominantCoverer = null) =>
            dominantCoverer != null
                ? new AnalysisIssue(IssueLevel.Error, CodeEdgePartialCover,
                    PairDetail(element, dominantCoverer),
                    Loc.F("issue.edg01.message", sides), element, dominantCoverer)
                : new AnalysisIssue(IssueLevel.Error, CodeEdgePartialCover,
                    Name(element), Loc.F("issue.edg01.message", sides), element);

        public static AnalysisIssue NearContact(KitchenElement a, KitchenElement b, float gapMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodeNearContact,
                PairDetail(a, b),
                Loc.F("issue.gap01.message", gapMm, SceneAnalyzer.NearContactMinGapMm),
                a, b);

        public static AnalysisIssue NearContactFar(KitchenElement a, KitchenElement b, float gapMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodeNearContactFar,
                PairDetail(a, b),
                Loc.F("issue.gap02.message", gapMm, SceneAnalyzer.NearContactMaxGapMm),
                a, b);

        public static AnalysisIssue DishwasherFacadeBackGap(KitchenElement dishwasher, KitchenElement facade,
            float gapMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherBackGap,
                PairDetail(dishwasher, facade),
                Loc.F("issue.dwh04.message", gapMm, SceneAnalyzer.DishwasherBackGapMinMm),
                dishwasher, facade);

        public static AnalysisIssue PanelNotSeated(KitchenElement panel, KitchenElement board,
            float insertionMm, float depthMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodePanelNotSeated,
                $"{Name(panel)} ↔ {Name(board)}",
                Loc.F("issue.seat01.message", insertionMm, depthMm),
                panel, board);

        public static AnalysisIssue FacadeGap(KitchenElement facade, string sides) =>
            new AnalysisIssue(IssueLevel.Warning, CodeFacadeGap,
                Name(facade), Loc.F("issue.fac01.message", SceneAnalyzer.FacadeMinGapMm, sides),
                facade);

        public static AnalysisIssue DrawerNoFacade(KitchenElement drawer) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDrawerNoFacade,
                Name(drawer), Loc.T("issue.drw01.message"), drawer);

        public static AnalysisIssue DrawerFacadeOrphaned(KitchenElement drawer, KitchenElement? facade) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDrawerFacadeOrphaned,
                PairDetail(drawer, facade),
                Loc.F("issue.drw02.message", Name(drawer), Name(facade)),
                drawer, facade);

        public static AnalysisIssue DishwasherNoFacade(KitchenElement dishwasher) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherNoFacade,
                Name(dishwasher), Loc.T("issue.dwh01.message"),
                dishwasher);

        public static AnalysisIssue DishwasherFacadeMissing(KitchenElement dishwasher, string facadeName) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeOrphaned,
                Name(dishwasher), Loc.F("issue.dwh02.facadeDeleted", facadeName),
                dishwasher);

        public static AnalysisIssue DishwasherFacadeOrphaned(KitchenElement dishwasher, KitchenElement? facade) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeOrphaned,
                PairDetail(dishwasher, facade),
                Loc.F("issue.dwh02.message", Name(dishwasher), Name(facade)),
                dishwasher, facade);

        public static AnalysisIssue DishwasherNoSupport(KitchenElement dishwasher) =>
            new AnalysisIssue(IssueLevel.Error, CodeDishwasherNoSupport,
                Name(dishwasher),
                Loc.T("issue.dwh05.noSupport"),
                dishwasher);

        public static AnalysisIssue DishwasherSunk(KitchenElement dishwasher, KitchenElement? blocker,
            float sinkMm) =>
            new AnalysisIssue(IssueLevel.Error, CodeDishwasherNoSupport,
                PairDetail(dishwasher, blocker),
                Loc.F("issue.dwh05.sunk", Name(blocker), sinkMm),
                dishwasher, blocker);

        public static AnalysisIssue DishwasherFacadeHeight(KitchenElement dishwasher,
            KitchenElement? facade, int facadeHeightMM) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeHeight,
                PairDetail(dishwasher, facade),
                Loc.F("issue.dwh03.message", facadeHeightMM, DishwasherElement.FACADE_MIN_HEIGHT_MM, DishwasherElement.FACADE_MAX_HEIGHT_MM, DishwasherElement.PlinthForFacade(facadeHeightMM), DishwasherElement.PLINTH_MIN_MM, DishwasherElement.PLINTH_MAX_MM),
                dishwasher, facade);

        public static AnalysisIssue AttachDetached(KitchenElement child, KitchenElement parent) =>
            new AnalysisIssue(IssueLevel.Error, CodeAttachDetached,
                PairDetail(child, parent),
                Loc.F("issue.att01.message", Name(child), Name(parent)),
                child, parent);

        public static AnalysisIssue WallLayerHostMissing(KitchenElement layer, string hostWallName) =>
            new AnalysisIssue(IssueLevel.Error, CodeAttachDetached,
                $"{Name(layer)} ↔ {hostWallName}",
                Loc.F("issue.att01.wallLayerHostMissing", Name(layer), hostWallName),
                layer);

        public static AnalysisIssue ScrewLegOffCentre(KitchenElement leg, KitchenElement host,
            ScrewLegOffCentre offCentre) =>
            new AnalysisIssue(IssueLevel.Warning, CodeScrewLegOffCentre,
                PairDetail(leg, host),
                Loc.F("issue.leg01.message", offCentre.Axis, Name(host), offCentre.SpanMM, ScrewLegSpec.CENTRING_REQUIRED_SPAN_MM, offCentre.OffsetMM, offCentre.WallMM, ScrewLegSpec.MIN_INSERT_WALL_MM),
                leg, host);

        public static AnalysisIssue ScrewLegShallow(KitchenElement leg, KitchenElement host,
            int insertionMM) =>
            new AnalysisIssue(IssueLevel.Error, CodeScrewLegShallow,
                PairDetail(leg, host),
                Loc.F("issue.leg02.message", Name(host), insertionMM, ScrewLegSpec.MIN_INSERTION_INTO_HOST_MM),
                leg, host);

        public static AnalysisIssue ScrewLegNoFooting(KitchenElement leg, ScrewLegSupport below) =>
            below.Nearest != null
                ? new AnalysisIssue(IssueLevel.Warning, CodeScrewLegNoFooting,
                    PairDetail(leg, below.Nearest),
                    Loc.F("issue.leg03.gap", Name(below.Nearest), below.GapMM, Tolerance.ContactMm),
                    leg, below.Nearest)
                : new AnalysisIssue(IssueLevel.Warning, CodeScrewLegNoFooting,
                    Name(leg),
                    Loc.T("issue.leg03.nothingBelow"),
                    leg);

        public static AnalysisIssue FromPipeFinding(Plumbing.PipeFinding finding,
            KitchenElement? element, KitchenElement? other) =>
            new AnalysisIssue(
                finding.Level == Plumbing.PipeFindingLevel.Error ? IssueLevel.Error : IssueLevel.Warning,
                finding.Code, PairDetail(element, other), finding.Message, element, other);

        public static AnalysisIssue FromConstructionFinding(Construction.ConstructionFinding finding,
            KitchenElement? element, KitchenElement? other = null) =>
            new AnalysisIssue(
                finding.Level == Construction.ConstructionFindingLevel.Error ? IssueLevel.Error : IssueLevel.Warning,
                finding.Code, other != null ? PairDetail(element, other) : Name(element), finding.Message,
                element, other);

        private static string Name(KitchenElement? e) =>
            e != null && !string.IsNullOrEmpty(e.PartName) ? e.PartName : "—";

        private static string PairDetail(KitchenElement? a, KitchenElement? b) =>
            b != null ? $"{Name(a)} ↔ {Name(b)}" : Name(a);
    }
}
