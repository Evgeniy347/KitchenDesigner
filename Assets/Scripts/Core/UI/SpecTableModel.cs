using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public sealed class SpecTableModel
    {
        public SpecTableModel(IReadOnlyList<DataRow> rows, int positions, int sections)
        {
            Rows = rows;
            Positions = positions;
            Sections = sections;
        }

        public IReadOnlyList<DataRow> Rows { get; }

        public int Positions { get; }

        public int Sections { get; }
    }
}
