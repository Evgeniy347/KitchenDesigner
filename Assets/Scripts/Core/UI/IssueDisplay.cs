using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;
using UnityEngine;
using KitchenDesigner.Core.Update;

namespace KitchenDesigner.Core.UI
{
    public static class IssueDisplay
    {
        public static string LevelName(IssueLevel level) => level switch
        {
            IssueLevel.Error => "Ошибка",
            IssueLevel.Warning => "Предупреждение",
            IssueLevel.Info => "Инфо",
            _ => level.ToString(),
        };

        public static Color LevelColor(IssueLevel level) => level switch
        {
            IssueLevel.Error => UIStyle.HighlightError,
            IssueLevel.Warning => UIStyle.HighlightWarning,
            IssueLevel.Info => UIStyle.TextSecondary,
            _ => UIStyle.Text,
        };

        public static StatusLevel StatusLevelOf(IssueLevel level) => level switch
        {
            IssueLevel.Error => StatusLevel.Error,
            IssueLevel.Warning => StatusLevel.Warning,
            _ => StatusLevel.Info,
        };

        public static string FormatRowForCopy(AnalysisIssue iss) => string.Concat(
            LevelName(iss.Level), "\t",
            iss.Code ?? "", "\t",
            iss.Detail ?? "", "\t",
            iss.Message ?? "");

        public static void RevealIssue(AnalysisIssue iss)
        {
            SelectInScene(iss);
            FocusCameraOn(iss);
        }

        public static void SelectInScene(AnalysisIssue iss)
        {
            var sel = SelectionManager.Instance;
            if (sel == null || iss.Target == null) return;

            if (iss.Secondary != null)
                sel.SelectOnly(new List<KitchenElement> { iss.Target, iss.Secondary });
            else
                sel.Select(iss.Target);
        }

        private static void FocusCameraOn(AnalysisIssue iss)
        {
            if (iss.Target == null) return;
            CameraController.Instance?.FocusOn(iss.Target.transform.position);
        }
    }
}
