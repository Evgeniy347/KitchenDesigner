using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class LoadProjectRows
    {
        public const int NameColumn = 0;
        public const int ModifiedColumn = 1;
        public const int CreatedColumn = 2;
        public const int VersionColumn = 3;
        public const int ActionColumn = 4;
        public const int ColumnCount = 5;

        private const float DateColumnW = 150f;
        private const float VersionColumnW = 90f;
        private const float ActionColumnW = 40f;

        public static IReadOnlyList<DataColumn> Columns() => new[]
        {
            new DataColumn("name", Loc.T("window.load.column.name"), 0f, CellAlign.Left, sortable: true),
            new DataColumn("modified", Loc.T("window.load.column.modified"), DateColumnW, CellAlign.Left, sortable: true),
            new DataColumn("created", Loc.T("window.load.column.created"), DateColumnW, CellAlign.Left, sortable: true),
            new DataColumn("version", Loc.T("window.load.column.version"), VersionColumnW, CellAlign.Left, sortable: true),
            new DataColumn("action", "", ActionColumnW),
        };

        public static DataRow For(RecentProjectRow project)
        {
            string name = System.IO.Path.GetFileName(project.Path);
            bool flagged = project.FileExists && project.VersionMismatch;
            string version = flagged ? UIStyle.GlyphWarning + " " + project.VersionLabel : project.VersionLabel;

            var row = new DataRow(DataRowKind.Item, name, project.ModifiedLabel, project.CreatedLabel, version, "")
            {
                Tag = project,
                Enabled = project.FileExists,
                SortKeys = new[]
                {
                    name.ToLowerInvariant(), SortableDate(project.ModifiedLabel), SortableDate(project.CreatedLabel),
                    project.FileExists ? project.VersionLabel : "", "",
                },
            };
            if (project.FileExists)
                row.CellColors = new Color?[]
                {
                    null, UIStyle.TextSecondary, UIStyle.TextSecondary,
                    flagged ? UIStyle.TextError : UIStyle.TextSecondary,
                };
            return row;
        }

        public static bool MatchesSearch(RecentProjectRow project, string search) =>
            search.Length == 0
            || System.IO.Path.GetFileName(project.Path).IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static string SortableDate(string label) =>
            label == RecentProjectRow.MissingVersionLabel ? "" : label;
    }
}
