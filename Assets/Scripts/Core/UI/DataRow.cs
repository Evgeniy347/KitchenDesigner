using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public sealed class DataRow
    {
        public DataRow(DataRowKind kind, params string[] cells)
        {
            Kind = kind;
            Cells = cells;
        }

        public static DataRow Item(params string[] cells) => new DataRow(DataRowKind.Item, cells);

        public static DataRow Group(string title) => new DataRow(DataRowKind.Group, title);

        public DataRowKind Kind { get; }

        public IReadOnlyList<string> Cells { get; }

        public object? Tag { get; set; }

        public IReadOnlyList<string>? SortKeys { get; set; }

        public string Cell(int column) => column >= 0 && column < Cells.Count ? Cells[column] : "";

        public string SortKey(int column) =>
            SortKeys != null && column < SortKeys.Count ? SortKeys[column] : Cell(column);
    }
}
