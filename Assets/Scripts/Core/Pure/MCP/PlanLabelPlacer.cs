using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class PlanLabelPlacer
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int _padding;
        private readonly List<PlanRect> _taken = new List<PlanRect>();

        public PlanLabelPlacer(int width, int height, int padding)
        {
            _width = width;
            _height = height;
            _padding = padding;
        }

        public bool TryTake(PlanRect area)
        {
            if (area.X0 < 0 || area.Y0 < 0 || area.X1 > _width || area.Y1 > _height) return false;
            foreach (var other in _taken)
                if (Overlaps(area, other)) return false;
            _taken.Add(area);
            return true;
        }

        public void Reserve(PlanRect area) => _taken.Add(area);

        private bool Overlaps(PlanRect a, PlanRect b) =>
            a.X0 < b.X1 + _padding && b.X0 < a.X1 + _padding && a.Y0 < b.Y1 + _padding && b.Y0 < a.Y1 + _padding;
    }
}
