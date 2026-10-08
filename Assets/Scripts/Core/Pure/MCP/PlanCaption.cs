using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Core.MCP
{
    internal static class PlanCaption
    {
        public const int MaxNamesListed = 8;
        public const string WholeScene = "whole scene";

        public static string Of(PlanDrawing drawing)
        {
            var caption = new StringBuilder("render_plan ").Append(PlanViewWord.Name(drawing.View)).Append(' ')
                .Append(drawing.Width).Append('x').Append(drawing.Height).Append(" px; scope ")
                .Append(drawing.Scope ?? WholeScene).Append("; 1 px = ").Append(PlacementWording.Mm((float)drawing.MmPerPixel))
                .Append(" mm, bar ").Append(drawing.ScaleBarMm).Append(" mm; ").Append(PlanViewWord.Axes(drawing.View))
                .Append("; ").Append(drawing.Items).Append(" items, ").Append(Issues(drawing));
            if (!drawing.LabelsOn) return caption.Append("; labels off").ToString();
            AppendUnlabelled(caption, drawing.Unlabelled);
            AppendShortened(caption, drawing.Shortened);
            return caption.ToString();
        }

        private static string Issues(PlanDrawing drawing) =>
            drawing.WithIssues == 0
                ? "no issues"
                : drawing.WithIssues + " with issues (red outline + triangle, ! before the name)";

        private static void AppendUnlabelled(StringBuilder caption, List<string> names)
        {
            if (names.Count == 0) return;
            caption.Append("; no room for the name of: ").Append(Listed(names));
        }

        private static void AppendShortened(StringBuilder caption, List<KeyValuePair<string, string>> pairs)
        {
            if (pairs.Count == 0) return;
            var words = new List<string>();
            foreach (var pair in pairs)
            {
                if (words.Count == MaxNamesListed) break;
                words.Add(pair.Key + "=" + pair.Value);
            }
            caption.Append("; shortened: ").Append(string.Join(", ", words));
            if (pairs.Count > words.Count) caption.Append(", +").Append(pairs.Count - words.Count).Append(" more");
        }

        private static string Listed(List<string> names)
        {
            var shown = names.Count > MaxNamesListed ? names.GetRange(0, MaxNamesListed) : names;
            var text = string.Join(", ", shown);
            return names.Count > shown.Count ? text + ", +" + (names.Count - shown.Count) + " more" : text;
        }
    }
}
