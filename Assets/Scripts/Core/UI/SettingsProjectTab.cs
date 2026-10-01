using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsProjectTab
    {
        private readonly SettingsRowFactory _rows;
        private readonly Action _dependentStatesChanged;

        private TMP_InputField? _gridStepField;
        private TMP_InputField? _snapThresholdField;
        private TMP_InputField? _autoSaveIntervalField;

        public SettingsProjectTab(SettingsRowFactory rows, Action dependentStatesChanged)
        {
            _rows = rows;
            _dependentStatesChanged = dependentStatesChanged;
        }

        public void Build(Transform page, KitchenSettings s, float topY)
        {
            float y = topY;

            BuildLanguageRow(page, ref y);
            y -= SettingsRowFactory.GapPx;

            _rows.AddToggle(page, ref y, Loc.T("settings.project.grid"), s.GridEnabled,
                v => { s.GridEnabled = v; _dependentStatesChanged(); }, read: () => s.GridEnabled);
            Hint(Loc.T("settings.project.grid"), hint: "settings.project.grid");

            _gridStepField = _rows.AddInput(page, ref y, Loc.T("settings.project.gridStep"), s.GridStep.ToString(),
                TMP_InputField.ContentType.IntegerNumber,
                f =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int parsed) ? parsed : s.GridStep);
                    s.GridStep = val;
                    f.text = s.GridStep.ToString();
                }, s.GridStep.ToString(), unit: Loc.T("unit.mm"), indent: true, read: () => s.GridStep.ToString());
            Hint(Loc.T("settings.project.gridStep"), hint: "settings.project.gridStep");

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, Loc.T("settings.project.snap"), s.SnapEnabled,
                v => { s.SnapEnabled = v; _dependentStatesChanged(); }, read: () => s.SnapEnabled);
            Hint(Loc.T("settings.project.snap"), hint: "settings.project.snap");

            _snapThresholdField = _rows.AddInput(page, ref y, Loc.T("settings.project.snapThreshold"),
                s.SnapThreshold.ToString("F0"),
                TMP_InputField.ContentType.DecimalNumber,
                f =>
                {
                    var val = ExpressionParser.EvaluateFloat(f.text)
                        ?? (float.TryParse(f.text, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float parsed)
                            ? parsed
                            : s.SnapThreshold);
                    s.SnapThreshold = val;
                    f.text = s.SnapThreshold.ToString("F0");
                }, s.SnapThreshold.ToString("F0"), unit: Loc.T("unit.mm"), indent: true,
                read: () => s.SnapThreshold.ToString("F0"));
            Hint(Loc.T("settings.project.snapThreshold"), hint: "settings.project.snapThreshold");

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, Loc.T("settings.project.blockOnViolation"), s.BlockOnViolation,
                v => { s.BlockOnViolation = v; }, read: () => s.BlockOnViolation);
            Hint(Loc.T("settings.project.blockOnViolation"), hint: "settings.project.blockOnViolation");

            _rows.AddToggle(page, ref y, Loc.T("settings.project.autoSave"), s.AutoSave,
                v => { s.AutoSave = v; _dependentStatesChanged(); }, read: () => s.AutoSave);
            Hint(Loc.T("settings.project.autoSave"), hint: "settings.project.autoSave");

            _autoSaveIntervalField = _rows.AddInput(page, ref y, Loc.T("settings.project.autoSaveInterval"),
                s.AutoSaveInterval.ToString(),
                TMP_InputField.ContentType.IntegerNumber,
                f =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int parsed) ? parsed : s.AutoSaveInterval);
                    s.AutoSaveInterval = val;
                    f.text = s.AutoSaveInterval.ToString();
                }, s.AutoSaveInterval.ToString(), unit: Loc.T("unit.s"), indent: true,
                read: () => s.AutoSaveInterval.ToString());
            Hint(Loc.T("settings.project.autoSaveInterval"), hint: "settings.project.autoSaveInterval");

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, Loc.T("settings.project.spatialGrid"), s.SpatialGrid,
                v => { s.SpatialGrid = v; }, read: () => s.SpatialGrid);
            Hint(Loc.T("settings.project.spatialGrid"), hint: "settings.project.spatialGrid");

            _rows.AddInput(page, ref y, Loc.T("settings.project.edgePartialThreshold"),
                s.EdgePartialThresholdPct.ToString(),
                TMP_InputField.ContentType.IntegerNumber,
                f =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int parsed) ? parsed : s.EdgePartialThresholdPct);
                    s.EdgePartialThresholdPct = val;
                    f.text = s.EdgePartialThresholdPct.ToString();
                }, s.EdgePartialThresholdPct.ToString(), unit: "%",
                read: () => s.EdgePartialThresholdPct.ToString());
            Hint(Loc.T("settings.project.edgePartialThreshold"), hint: "settings.project.edgePartialThreshold");

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, Loc.T("settings.project.cameraPanFree"), s.CameraPanFree,
                v => { s.CameraPanFree = v; }, read: () => s.CameraPanFree);
            Hint(Loc.T("settings.project.cameraPanFree"), hint: "settings.project.cameraPanFree");
        }

        private void BuildLanguageRow(Transform page, ref float y)
        {
            var languages = Loc.Languages;
            _rows.AddDropdown(page, ref y, Loc.T("settings.project.language"),
                languages.Select(l => l.NativeName).ToList(),
                IndexOfCurrent(languages),
                index =>
                {
                    if (index >= 0 && index < languages.Count) LanguageStartup.Choose(languages[index].Code);
                },
                id: LanguageRowId,
                read: () => IndexOfCurrent(Loc.Languages));
        }

        internal const string LanguageRowId = "Language";

        private static int IndexOfCurrent(IReadOnlyList<LanguageInfo> languages)
        {
            for (int i = 0; i < languages.Count; i++)
                if (languages[i].Code == Loc.Language) return i;
            return 0;
        }

        public void RefreshDependentStates(KitchenSettings s)
        {
            _rows.SetFieldEnabled(_gridStepField, Loc.T("settings.project.gridStep"), s.GridEnabled);
            _rows.SetFieldEnabled(_snapThresholdField, Loc.T("settings.project.snapThreshold"), s.SnapEnabled);
            _rows.SetFieldEnabled(_autoSaveIntervalField, Loc.T("settings.project.autoSaveInterval"), s.AutoSave);
        }

        private void Hint(string rowKey, string hint) =>
            HintBadge.AttachAfterLabel(_rows.RowLabel(rowKey), hint);
    }
}
