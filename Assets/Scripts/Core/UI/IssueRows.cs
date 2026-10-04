using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class IssueRows
    {
        public const int GlyphColumn = 0;
        public const int CodeColumn = 1;
        public const int PartColumn = 2;
        public const int MessageColumn = 3;

        private const float GlyphColumnW = 40f;
        private const float CodeColumnW = 90f;
        private const float PartColumnW = 220f;

        public static DataRow For(AnalysisIssue issue) => new DataRow(DataRowKind.Item,
            IssueDisplay.LevelGlyph(issue.Level), issue.Code ?? "", issue.Detail ?? "", issue.Message ?? "")
        {
            Tag = issue,
            CellColors = new Color?[] { IssueDisplay.LevelColor(issue.Level) },
        };

        public static IReadOnlyList<DataColumn> Columns() => new[]
        {
            new DataColumn("level", "", GlyphColumnW, CellAlign.Center),
            new DataColumn("code", Loc.T("errors.column.code"), CodeColumnW),
            new DataColumn("part", Loc.T("errors.column.part"), PartColumnW),
            new DataColumn("message", Loc.T("errors.column.message"), 0f),
        };
    }
}
