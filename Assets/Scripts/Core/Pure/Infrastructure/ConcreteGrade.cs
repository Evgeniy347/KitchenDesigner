namespace KitchenDesigner.Core
{
    public enum ConcreteGrade
    {
        B15 = 0,
        B20 = 1,
        B25 = 2,
    }

    public static class ConcreteGradeTitles
    {
        public static readonly string[] All = { "B15", "B20", "B25" };

        public static string Of(ConcreteGrade grade)
        {
            int index = (int)grade;
            return index >= 0 && index < All.Length ? All[index] : All[(int)ConcreteGrade.B20];
        }
    }
}
