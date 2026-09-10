namespace KitchenDesigner.Core
{
    public static class SpecCellFormat
    {
        public static string DimCell(int valueMM, bool hasDims) => hasDims ? valueMM.ToString() : "";

        public static string CountCell(int count, bool hasDims) => hasDims ? count.ToString() : "";
    }
}
