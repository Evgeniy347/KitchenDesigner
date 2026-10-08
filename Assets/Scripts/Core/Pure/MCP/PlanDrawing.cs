using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    public sealed class PlanDrawing
    {
        public readonly int Width;
        public readonly int Height;
        public readonly double MmPerPixel;
        public readonly PlanView View;
        public readonly string? Scope;
        public readonly List<PlanShape> Shapes = new List<PlanShape>();
        public readonly List<string> Unlabelled = new List<string>();
        public readonly List<KeyValuePair<string, string>> Shortened = new List<KeyValuePair<string, string>>();
        public int Items;
        public int WithIssues;
        public int ScaleBarMm;
        public bool LabelsOn;

        public PlanDrawing(int width, int height, double mmPerPixel, PlanView view, string? scope)
        {
            Width = width;
            Height = height;
            MmPerPixel = mmPerPixel;
            View = view;
            Scope = scope;
        }
    }
}
