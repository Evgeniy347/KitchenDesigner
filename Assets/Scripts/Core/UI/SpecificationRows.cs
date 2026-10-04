using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class SpecificationRows
    {
        public const int NumberColumn = 0;
        public const int NameColumn = 1;
        public const int MaterialColumn = 2;
        public const int WidthColumn = 3;
        public const int HeightColumn = 4;
        public const int DepthColumn = 5;
        public const int PiecesColumn = 6;
        public const int QtyColumn = 7;
        public const int UnitColumn = 8;
        public const int ColumnCount = 9;

        private const float NumberColumnW = 40f;
        private const float MaterialColumnW = 150f;
        private const float DimensionColumnW = 64f;
        private const float PiecesColumnW = 56f;
        private const float QtyColumnW = 72f;
        private const float UnitColumnW = 60f;

        public static IReadOnlyList<DataColumn> Columns() => new[]
        {
            new DataColumn("n", Loc.T("spec.column.number"), NumberColumnW, CellAlign.Right),
            new DataColumn("name", Loc.T("spec.column.name"), 0f),
            new DataColumn("material", Loc.T("spec.column.material"), MaterialColumnW),
            new DataColumn("w", Loc.T("spec.column.width"), DimensionColumnW, CellAlign.Right),
            new DataColumn("h", Loc.T("spec.column.height"), DimensionColumnW, CellAlign.Right),
            new DataColumn("d", Loc.T("spec.column.depth"), DimensionColumnW, CellAlign.Right),
            new DataColumn("pieces", Loc.T("spec.column.pieces"), PiecesColumnW, CellAlign.Right),
            new DataColumn("qty", Loc.T("spec.column.qty"), QtyColumnW, CellAlign.Right),
            new DataColumn("unit", Loc.T("spec.column.unit"), UnitColumnW),
        };

        public static SpecTableModel Build(SpecResult result)
        {
            var rows = new List<DataRow>();
            int number = 0;
            int sections = 0;

            var bySection = result.lines
                .GroupBy(l => l.section)
                .OrderBy(g => g.Key, System.StringComparer.Ordinal);

            foreach (var section in bySection)
            {
                sections++;
                rows.Add(DataRow.Group(SectionLabel(section.Key)));

                var byMaterial = section
                    .GroupBy(l => l.material)
                    .OrderBy(g => g.Key, System.StringComparer.Ordinal);

                foreach (var materialGroup in byMaterial)
                {
                    var lines = materialGroup.ToList();
                    foreach (var line in lines)
                        rows.Add(ItemRow(++number, line));
                    AddMaterialSubtotals(rows, materialGroup.Key, lines);
                }
            }

            if (result.lines.Count > 0)
                AddTotals(rows, result);

            return new SpecTableModel(rows, number, sections);
        }

        public static string FormatQty(float qtyTotal, SpecUnit unit) =>
            unit == SpecUnit.Pieces
                ? NumberFormat.Integer(Mathf.RoundToInt(qtyTotal))
                : NumberFormat.Fixed(qtyTotal, 2);

        private static string SectionLabel(string section) =>
            string.IsNullOrEmpty(section) ? Loc.T("spec.noSection") : section;

        private static DataRow ItemRow(int number, SpecLine line)
        {
            var cells = new string[ColumnCount];
            cells[NumberColumn] = NumberFormat.Integer(number);
            cells[NameColumn] = line.name ?? "";
            cells[MaterialColumn] = string.IsNullOrEmpty(line.material) ? UIStyle.GlyphDash : line.material;
            cells[WidthColumn] = SpecCellFormat.DimCell(line.dimensionsMM.x, line.hasDims);
            cells[HeightColumn] = SpecCellFormat.DimCell(line.dimensionsMM.y, line.hasDims);
            cells[DepthColumn] = SpecCellFormat.DimCell(line.dimensionsMM.z, line.hasDims);
            cells[PiecesColumn] = SpecCellFormat.CountCell(line.count, line.hasDims);
            cells[QtyColumn] = FormatQty(line.qtyTotal, line.unit);
            cells[UnitColumn] = line.unit.Label();
            return DataRow.Item(cells);
        }

        private static void AddMaterialSubtotals(List<DataRow> rows, string material, IReadOnlyList<SpecLine> lines)
        {
            if (string.IsNullOrEmpty(material)) return;
            var totals = SpecTotals.ByUnit(lines.Select(l => (l.unit, l.qtyTotal)));
            foreach (var unit in totals.Keys.OrderBy(u => (int)u))
                rows.Add(SummaryRow(DataRowKind.Subtotal, Loc.F("spec.subtotal", material), totals[unit], unit));
        }

        private static void AddTotals(List<DataRow> rows, SpecResult result)
        {
            rows.Add(SummaryRow(DataRowKind.Total, Loc.T("spec.totalBoards"), result.totalCount, SpecUnit.Pieces));
            if (result.totalsBySection == null) return;
            foreach (var key in result.totalsBySection.Keys
                .OrderBy(k => (int)k.unit)
                .ThenBy(k => k.section, System.StringComparer.Ordinal))
                rows.Add(SummaryRow(DataRowKind.Total, Loc.F("spec.totalSection", SectionLabel(key.section)),
                    result.totalsBySection[key], key.unit));
        }

        private static DataRow SummaryRow(DataRowKind kind, string label, float qty, SpecUnit unit)
        {
            var cells = new string[ColumnCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = "";
            cells[NameColumn] = label;
            cells[QtyColumn] = FormatQty(qty, unit);
            cells[UnitColumn] = unit.Label();
            return new DataRow(kind, cells);
        }
    }
}
