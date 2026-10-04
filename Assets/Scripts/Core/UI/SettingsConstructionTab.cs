using System.Collections.Generic;
using TMPro;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsConstructionTab
    {
        public static string RegionId => Loc.T("settings.construction.region");
        public static string FrostDepthId => Loc.T("settings.construction.frostDepth");
        public static string MasonryId => Loc.T("settings.construction.masonry");
        public static string SoilId => Loc.T("settings.construction.soil");
        public static string ConcreteId => Loc.T("settings.construction.concrete");
        public static string NeighbourLevelsId => Loc.T("settings.construction.neighbourLevels");
        public const string FrostDepthUnknown = FrostDepth.UnknownValue;

        private readonly SettingsPage _page;
        private TextMeshProUGUI? _frostDepthValue;
        private HintBadge? _frostDepthHint;

        public SettingsConstructionTab(SettingsPage page) => _page = page;

        public static string FrostDepthText(KitchenSettings s) =>
            FrostDepth.Read(s.ConstructionRegion, s.ConstructionSoil).Value;

        public static List<string> MasonryTitles()
        {
            var titles = new List<string>(MasonryUnit.Table.Count);
            foreach (var unit in MasonryUnit.Table) titles.Add(unit.Title);
            return titles;
        }

        public void Build(KitchenSettings s)
        {
            _page.Section(Loc.T("settings.construction.section.site"));

            _page.AddDropdown(RegionId,
                new List<string>(ConstructionRegionTitles.All), (int)s.ConstructionRegion,
                v => { s.ConstructionRegion = (ConstructionRegion)v; ShowFrostDepth(s); },
                read: () => (int)s.ConstructionRegion);
            Hint(RegionId, hint: "settings.construction.region");

            _frostDepthValue = _page.AddReadOnly(FrostDepthId, FrostDepthText(s),
                read: () => FrostDepthText(s));
            _frostDepthHint = Hint(FrostDepthId, hint: "settings.construction.frostDepth");
            ShowFrostDepth(s);

            _page.AddDropdown(SoilId,
                new List<string>(SoilKindTitles.All), (int)s.ConstructionSoil,
                v => { s.ConstructionSoil = (SoilKind)v; ShowFrostDepth(s); },
                read: () => (int)s.ConstructionSoil);
            Hint(SoilId, hint: "settings.construction.soil");

            _page.Section(Loc.T("settings.construction.section.house"));

            AddMillimetres(Loc.T("settings.construction.floorHeight"),
                () => s.ConstructionFloorHeightMm, v => s.ConstructionFloorHeightMm = v);
            Hint(Loc.T("settings.construction.floorHeight"), hint: "settings.construction.floorHeight");

            _page.AddDropdown(NeighbourLevelsId,
                new List<string>(NeighbourLevelsModeTitles.All), (int)s.NeighbourLevels,
                v =>
                {
                    s.NeighbourLevels = (NeighbourLevelsMode)v;
                    SceneVisibilityManager.Invalidate();
                    ElementHighlighter.Current?.RefreshHighlights();
                },
                read: () => (int)s.NeighbourLevels);
            Hint(NeighbourLevelsId, hint: "settings.construction.neighbourLevels");

            _page.Section(Loc.T("settings.construction.section.masonry"));

            _page.AddDropdown(MasonryId, MasonryTitles(),
                (int)s.ConstructionMasonry,
                v => { s.ConstructionMasonry = (MasonryTechnology)v; },
                read: () => (int)s.ConstructionMasonry);
            Hint(MasonryId, hint: "settings.construction.masonry");

            AddMillimetres(Loc.T("settings.construction.joint"),
                () => s.ConstructionJointMm, v => s.ConstructionJointMm = v);
            Hint(Loc.T("settings.construction.joint"), hint: "settings.construction.joint");

            AddWhole(Loc.T("settings.construction.waste"), "%",
                () => s.ConstructionWastePct, v => s.ConstructionWastePct = v);
            Hint(Loc.T("settings.construction.waste"), hint: "settings.construction.waste");

            _page.Section(Loc.T("elementType.foundation"));

            _page.AddDropdown(ConcreteId,
                new List<string>(ConcreteGradeTitles.All), (int)s.ConstructionConcrete,
                v => { s.ConstructionConcrete = (ConcreteGrade)v; },
                read: () => (int)s.ConstructionConcrete);
            Hint(ConcreteId, hint: "settings.construction.concrete");

            AddMillimetres(Loc.T("settings.construction.sand"),
                () => s.ConstructionSandMm, v => s.ConstructionSandMm = v);
            Hint(Loc.T("settings.construction.sand"), hint: "settings.construction.sand");

            AddMillimetres(Loc.T("settings.construction.gravel"),
                () => s.ConstructionGravelMm, v => s.ConstructionGravelMm = v);
            Hint(Loc.T("settings.construction.gravel"), hint: "settings.construction.gravel");

            _page.AddSwitch(Loc.T("settings.construction.compacted"), s.ConstructionCompacted,
                v => { s.ConstructionCompacted = v; }, read: () => s.ConstructionCompacted);
            Hint(Loc.T("settings.construction.compacted"), hint: "settings.construction.compacted");

            _page.Section(Loc.T("elementType.floorSlab"));

            AddMillimetres(Loc.T("settings.construction.slabThickness"),
                () => s.ConstructionSlabThicknessMm, v => s.ConstructionSlabThicknessMm = v);
            Hint(Loc.T("settings.construction.slabThickness"), hint: "settings.construction.slabThickness");
        }

        private void AddMillimetres(string label, System.Func<int> read, System.Action<int> write) =>
            AddWhole(label, Loc.T("unit.mm"), read, write);

        private void AddWhole(string label, string unit, System.Func<int> read, System.Action<int> write)
        {
            _page.AddNumber(label, read().ToString(), false,
                f =>
                {
                    int parsed = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int direct) ? direct : read());
                    write(parsed);
                    f.text = read().ToString();
                }, unit, () => read().ToString());
        }

        private void ShowFrostDepth(KitchenSettings s)
        {
            if (_frostDepthValue != null) _frostDepthValue.text = FrostDepthText(s);
            if (_frostDepthHint != null)
                _frostDepthHint.Retext(
                    FrostDepthHint.For(s.ConstructionRegion, s.ConstructionSoil));
        }

        private HintBadge? Hint(string rowKey, string hint) => _page.Hint(rowKey, hint);
    }
}
