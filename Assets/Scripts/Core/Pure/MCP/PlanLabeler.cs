using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    public sealed class PlanLabeler
    {
        public const int MinReadableScale = 1;

        private const int Padding = 2;

        private readonly PlanDrawing _drawing;
        private readonly PlanLayout _layout;
        private readonly PlanLabelPlacer _placer;
        private readonly List<PlanRect> _obstacles;
        private readonly PlanLabelNames _names = new PlanLabelNames();

        public readonly List<PlanShape> Shapes = new List<PlanShape>();

        public PlanLabeler(PlanDrawing drawing, PlanLayout layout, PlanLabelPlacer placer, List<PlanRect> obstacles)
        {
            _drawing = drawing;
            _layout = layout;
            _placer = placer;
            _obstacles = obstacles;
        }

        public bool TryLabel(DigestEntry entry, PlanLayer layer, PlanRect rect)
        {
            var prefix = entry.HasIssues ? "!" : string.Empty;
            var text = prefix + entry.Name;
            if (PlanStyles.LabelsInStrip(layer, _layout.View)) return TryInStrip(text, rect);
            if (layer == PlanLayer.Floor) return TryInCorner(text, rect, PlanPalette.Ink);
            if (_layout.View == PlanView.Front && (layer == PlanLayer.Door || layer == PlanLayer.Window))
                return TryInOpening(entry.Name, prefix, rect);
            foreach (int scale in Scales())
                if (TryInside(entry.Name, prefix, rect, scale, shorten: false)) return true;
            if (_layout.View == PlanView.Top && PlanLayers.IsThin(layer) && TryBeside(text, rect)) return true;
            return TryInside(entry.Name, prefix, rect, MinReadableScale, shorten: true);
        }

        public bool TryInCorner(string text, PlanRect rect, int color)
        {
            foreach (int scale in Scales())
            {
                int width = TextWidth(text, scale), height = PlanFont.Rows * scale;
                if (rect.Width < width + 2 * Padding || rect.Height < height + 2 * Padding) continue;
                foreach (var corner in Corners(rect, width, height))
                {
                    if (Blocked(corner)) continue;
                    if (!_placer.TryTake(corner)) continue;
                    Shapes.Add(PlanShape.Label(corner.X0, corner.Y0, text, PlanTextAnchor.Start, scale, color));
                    return true;
                }
            }
            return false;
        }

        private bool TryInOpening(string name, string prefix, PlanRect rect)
        {
            foreach (bool shorten in new[] { false, true })
                foreach (int scale in shorten ? new[] { MinReadableScale } : new List<int>(Scales()).ToArray())
                {
                    int chars = (rect.Width - Padding + scale) / (PlanFont.Advance * scale);
                    var shown = Fit(name, prefix, chars, shorten);
                    if (shown == null) continue;
                    if (TryAtOpeningSpots(prefix + shown, rect, scale) && CommitShortened(shown, name)) return true;
                }
            return false;
        }

        private bool TryAtOpeningSpots(string text, PlanRect rect, int scale)
        {
            int width = TextWidth(text, scale), height = PlanFont.Rows * scale;
            if (rect.Width < width + 2 * Padding || rect.Height < height + 2 * Padding) return false;
            int[] rows = { rect.CenterY, rect.Y0 + Padding + height / 2, rect.Y1 - Padding - height / 2 };
            int[] columns = { rect.CenterX, rect.X0 + Padding + width / 2, rect.X1 - Padding - width / 2 };
            foreach (int cy in rows)
                foreach (int cx in columns)
                {
                    var area = new PlanRect(cx - width / 2, cy - height / 2, cx - width / 2 + width, cy - height / 2 + height);
                    if (Blocked(area) || !_placer.TryTake(area)) continue;
                    Shapes.Add(PlanShape.Label(cx, area.Y0, text, PlanTextAnchor.Middle, scale, PlanPalette.Ink));
                    return true;
                }
            return false;
        }

        private bool CommitShortened(string shown, string name)
        {
            if (shown == name) return true;
            _names.Commit(shown, name);
            _drawing.Shortened.Add(new KeyValuePair<string, string>(shown, name));
            return true;
        }

        private IEnumerable<int> Scales()
        {
            for (int scale = _layout.FontScale; scale >= MinReadableScale; scale--) yield return scale;
        }

        private bool TryInside(string name, string prefix, PlanRect rect, int scale, bool shorten)
        {
            if (rect.Height < PlanFont.Rows * scale + Padding) return false;
            int chars = (rect.Width - Padding + scale) / (PlanFont.Advance * scale);
            var shown = Fit(name, prefix, chars, shorten);
            if (shown == null) return false;
            var text = prefix + shown;
            int width = TextWidth(text, scale), height = PlanFont.Rows * scale;
            int top = rect.CenterY - height / 2, left = rect.CenterX - width / 2;
            if (!_placer.TryTake(new PlanRect(left, top, left + width, top + height))) return false;
            Shapes.Add(PlanShape.Label(rect.CenterX, top, text, PlanTextAnchor.Middle, scale, PlanPalette.Ink));
            if (shown != name)
            {
                _names.Commit(shown, name);
                _drawing.Shortened.Add(new KeyValuePair<string, string>(shown, name));
            }
            return true;
        }

        private string? Fit(string name, string prefix, int chars, bool shorten)
        {
            if (!shorten) return name.Length + prefix.Length <= chars ? name : null;
            int room = chars - prefix.Length;
            if (name.Length <= room || room < McpNameShortening.MinChars) return null;
            return _names.Unique(name, room);
        }

        private bool TryInStrip(string text, PlanRect rect)
        {
            foreach (int scale in Scales())
            {
                int width = TextWidth(text, scale), height = PlanFont.Rows * scale;
                for (int row = 0; row < _layout.StripRows; row++)
                {
                    int top = _layout.StripTop + row * _layout.StripRowPitch;
                    foreach (int centre in Centres(rect))
                    {
                        int left = Math.Max(0, Math.Min(centre - width / 2, _layout.Width - width));
                        if (!_placer.TryTake(new PlanRect(left, top, left + width, top + height))) continue;
                        Shapes.Add(PlanShape.Label(left, top, text, PlanTextAnchor.Start, scale, PlanPalette.Ink));
                        return true;
                    }
                }
            }
            return false;
        }

        private static IEnumerable<int> Centres(PlanRect rect)
        {
            yield return rect.CenterX;
            yield return rect.X0 + rect.Width / 4;
            yield return rect.X0 + rect.Width * 3 / 4;
        }

        private static IEnumerable<PlanRect> Corners(PlanRect rect, int width, int height)
        {
            int left = rect.X0 + Padding, right = rect.X1 - Padding - width;
            int top = rect.Y0 + Padding, bottom = rect.Y1 - Padding - height;
            yield return new PlanRect(left, top, left + width, top + height);
            yield return new PlanRect(left, bottom, left + width, bottom + height);
            yield return new PlanRect(right, top, right + width, top + height);
            yield return new PlanRect(right, bottom, right + width, bottom + height);
        }

        private bool Blocked(PlanRect area)
        {
            foreach (var other in _obstacles)
                if (area.X0 < other.X1 && other.X0 < area.X1 && area.Y0 < other.Y1 && other.Y0 < area.Y1) return true;
            return false;
        }

        private bool TryBeside(string text, PlanRect rect)
        {
            bool landscape = rect.Width >= rect.Height;
            foreach (int scale in Scales())
            {
                int width = TextWidth(text, scale), height = PlanFont.Rows * scale;
                if (landscape && width > rect.Width) continue;
                int top = rect.CenterY - height / 2;
                foreach (int left in Sides(rect, width, landscape))
                {
                    var area = new PlanRect(left, top, left + width, top + height);
                    if (Blocked(area) || !_placer.TryTake(area)) continue;
                    Shapes.Add(PlanShape.Label(left, top, text, PlanTextAnchor.Start, scale, PlanPalette.Ink));
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<int> Sides(PlanRect rect, int width, bool landscape)
        {
            if (landscape)
            {
                yield return rect.CenterX - width / 2;
                yield return rect.X0 + rect.Width / 4 - width / 2;
                yield return rect.X0 + rect.Width * 3 / 4 - width / 2;
                yield break;
            }
            yield return rect.X1 + Padding;
            yield return rect.X0 - Padding - width;
        }

        private static int TextWidth(string text, int scale) => text.Length * PlanFont.Advance * scale - scale;
    }
}
