using TMPro;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsProjectTab
    {
        private readonly SettingsPage _page;
        private readonly System.Action _dependentStatesChanged;

        private TMP_InputField? _gridStepField;
        private TMP_InputField? _snapThresholdField;
        private TMP_InputField? _autoSaveIntervalField;

        public SettingsProjectTab(SettingsPage page, System.Action dependentStatesChanged)
        {
            _page = page;
            _dependentStatesChanged = dependentStatesChanged;
        }

        public void Build(KitchenSettings s)
        {
            _page.Section(Loc.T("settings.project.section.grid"));

            _page.AddSwitch(Loc.T("settings.project.grid"), s.GridEnabled,
                v => { s.GridEnabled = v; _dependentStatesChanged(); }, read: () => s.GridEnabled);
            Hint(Loc.T("settings.project.grid"), hint: "settings.project.grid");

            _gridStepField = _page.AddNumber(Loc.T("settings.project.gridStep"), s.GridStep.ToString(), false,
                f =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int parsed) ? parsed : s.GridStep);
                    s.GridStep = val;
                    f.text = s.GridStep.ToString();
                }, Loc.T("unit.mm"), () => s.GridStep.ToString(), indent: 1);
            Hint(Loc.T("settings.project.gridStep"), hint: "settings.project.gridStep");

            _page.AddSwitch(Loc.T("settings.project.snap"), s.SnapEnabled,
                v => { s.SnapEnabled = v; _dependentStatesChanged(); }, read: () => s.SnapEnabled);
            Hint(Loc.T("settings.project.snap"), hint: "settings.project.snap");

            _snapThresholdField = _page.AddNumber(Loc.T("settings.project.snapThreshold"),
                NumberFormat.Input(s.SnapThreshold, 0), true,
                f =>
                {
                    var val = ExpressionParser.EvaluateFloat(NumberFormat.Normalize(f.text))
                        ?? (NumberFormat.TryParse(f.text, out double parsed) ? (float)parsed : s.SnapThreshold);
                    s.SnapThreshold = val;
                    f.text = NumberFormat.Input(s.SnapThreshold, 0);
                }, Loc.T("unit.mm"), () => NumberFormat.Input(s.SnapThreshold, 0), indent: 1);
            Hint(Loc.T("settings.project.snapThreshold"), hint: "settings.project.snapThreshold");

            _page.AddSwitch(Loc.T("settings.project.spatialGrid"), s.SpatialGrid,
                v => { s.SpatialGrid = v; }, read: () => s.SpatialGrid);
            Hint(Loc.T("settings.project.spatialGrid"), hint: "settings.project.spatialGrid");

            _page.Section(Loc.T("settings.project.section.checks"));

            _page.AddSwitch(Loc.T("settings.project.blockOnViolation"), s.BlockOnViolation,
                v => { s.BlockOnViolation = v; }, read: () => s.BlockOnViolation);
            Hint(Loc.T("settings.project.blockOnViolation"), hint: "settings.project.blockOnViolation");

            _page.AddNumber(Loc.T("settings.project.edgePartialThreshold"),
                s.EdgePartialThresholdPct.ToString(), false,
                f =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int parsed) ? parsed : s.EdgePartialThresholdPct);
                    s.EdgePartialThresholdPct = val;
                    f.text = s.EdgePartialThresholdPct.ToString();
                }, "%", () => s.EdgePartialThresholdPct.ToString());
            Hint(Loc.T("settings.project.edgePartialThreshold"), hint: "settings.project.edgePartialThreshold");

            _page.Section(Loc.T("settings.project.section.save"));

            _page.AddSwitch(Loc.T("settings.project.autoSave"), s.AutoSave,
                v => { s.AutoSave = v; _dependentStatesChanged(); }, read: () => s.AutoSave);
            Hint(Loc.T("settings.project.autoSave"), hint: "settings.project.autoSave");

            _autoSaveIntervalField = _page.AddNumber(Loc.T("settings.project.autoSaveInterval"),
                s.AutoSaveInterval.ToString(), false,
                f =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int parsed) ? parsed : s.AutoSaveInterval);
                    s.AutoSaveInterval = val;
                    f.text = s.AutoSaveInterval.ToString();
                }, Loc.T("unit.s"), () => s.AutoSaveInterval.ToString(), indent: 1);
            Hint(Loc.T("settings.project.autoSaveInterval"), hint: "settings.project.autoSaveInterval");

            _page.Section(Loc.T("settings.project.section.camera"));

            _page.AddSwitch(Loc.T("settings.project.cameraPanFree"), s.CameraPanFree,
                v => { s.CameraPanFree = v; }, read: () => s.CameraPanFree);
            Hint(Loc.T("settings.project.cameraPanFree"), hint: "settings.project.cameraPanFree");
        }

        public void RefreshDependentStates(KitchenSettings s)
        {
            var form = _page.Form;
            form.SetFieldEnabled(_gridStepField, Loc.T("settings.project.gridStep"), s.GridEnabled);
            form.SetFieldEnabled(_snapThresholdField, Loc.T("settings.project.snapThreshold"), s.SnapEnabled);
            form.SetFieldEnabled(_autoSaveIntervalField, Loc.T("settings.project.autoSaveInterval"), s.AutoSave);
        }

        private void Hint(string rowKey, string hint) => _page.Hint(rowKey, hint);
    }
}
