namespace KitchenDesigner.Core.UI
{
    public sealed class DataColumn
    {
        public DataColumn(string key, string header, float width, CellAlign align = CellAlign.Left,
            bool sortable = false)
        {
            Key = key;
            Header = header;
            Width = width;
            Align = align;
            Sortable = sortable;
        }

        public string Key { get; }

        public string Header { get; }

        public float Width { get; }

        public CellAlign Align { get; }

        public bool Sortable { get; }

        public bool IsFlexible => Width <= 0f;
    }
}
