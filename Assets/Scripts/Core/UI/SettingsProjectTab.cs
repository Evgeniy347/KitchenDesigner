using System;
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

            _rows.AddToggle(page, ref y, "Сетка", s.GridEnabled,
                v => { s.GridEnabled = v; _dependentStatesChanged(); }, read: () => s.GridEnabled);

            _gridStepField = _rows.AddInput(page, ref y, "Шаг сетки", s.GridStep.ToString(),
                TMP_InputField.ContentType.IntegerNumber,
                f =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int parsed) ? parsed : s.GridStep);
                    s.GridStep = val;
                    f.text = s.GridStep.ToString();
                }, s.GridStep.ToString(), unit: "мм", indent: true, read: () => s.GridStep.ToString());

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, "Привязка к деталям", s.SnapEnabled,
                v => { s.SnapEnabled = v; _dependentStatesChanged(); }, read: () => s.SnapEnabled);

            _snapThresholdField = _rows.AddInput(page, ref y, "Порог привязки",
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
                }, s.SnapThreshold.ToString("F0"), unit: "мм", indent: true,
                read: () => s.SnapThreshold.ToString("F0"));

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, "Блокировать недопустимые изменения", s.BlockOnViolation,
                v => { s.BlockOnViolation = v; }, read: () => s.BlockOnViolation);

            _rows.AddToggle(page, ref y, "Автосохранение", s.AutoSave,
                v => { s.AutoSave = v; _dependentStatesChanged(); }, read: () => s.AutoSave);

            _autoSaveIntervalField = _rows.AddInput(page, ref y, "Интервал автосохранения",
                s.AutoSaveInterval.ToString(),
                TMP_InputField.ContentType.IntegerNumber,
                f =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int parsed) ? parsed : s.AutoSaveInterval);
                    s.AutoSaveInterval = val;
                    f.text = s.AutoSaveInterval.ToString();
                }, s.AutoSaveInterval.ToString(), unit: "с", indent: true,
                read: () => s.AutoSaveInterval.ToString());

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, "Пространственная сетка", s.SpatialGrid,
                v => { s.SpatialGrid = v; }, read: () => s.SpatialGrid);
            Hint("Пространственная сетка", hint: "settings.project.spatialGrid");

            _rows.AddInput(page, ref y, "Нижний порог кромки",
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
            Hint("Нижний порог кромки", hint: "settings.project.edgePartialThreshold");

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, "Свободное панорамирование", s.CameraPanFree,
                v => { s.CameraPanFree = v; }, read: () => s.CameraPanFree);
        }

        public void RefreshDependentStates(KitchenSettings s)
        {
            _rows.SetFieldEnabled(_gridStepField, "Шаг сетки", s.GridEnabled);
            _rows.SetFieldEnabled(_snapThresholdField, "Порог привязки", s.SnapEnabled);
            _rows.SetFieldEnabled(_autoSaveIntervalField, "Интервал автосохранения", s.AutoSave);
        }

        private void Hint(string rowKey, string hint) =>
            HintBadge.AttachAfterLabel(_rows.RowLabel(rowKey), hint);
    }
}
