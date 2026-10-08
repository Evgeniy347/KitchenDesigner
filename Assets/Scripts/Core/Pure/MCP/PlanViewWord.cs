namespace KitchenDesigner.Core.MCP
{
    public static class PlanViewWord
    {
        public const string Words = "top|front";

        public static bool TryParse(string? text, out PlanView view)
        {
            view = PlanView.Top;
            var word = (text ?? string.Empty).Trim().ToLowerInvariant();
            if (word == "top") return true;
            if (word != "front") return false;
            view = PlanView.Front;
            return true;
        }

        public static string Name(PlanView view) => view == PlanView.Top ? "top" : "front";

        public static string Axes(PlanView view) => view == PlanView.Top ? "x right, z up" : "x right, y up";
    }
}
