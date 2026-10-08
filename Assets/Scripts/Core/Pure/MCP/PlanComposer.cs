using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    public static class PlanComposer
    {
        private const int OutlineWidth = 1;
        private const int IssueMarkerUnits = 4;
        private const int LastLabelPass = 2;
        private const int LabelGapUnits = 2;

        public static PlanDrawing Compose(DigestInput input, PlanView view, bool labels, int px)
        {
            var layout = PlanLayout.For(input, view, px);
            var drawing = new PlanDrawing(layout.Width, layout.Height, layout.MmPerPixel, view, input.Scope)
            {
                LabelsOn = labels,
                Items = input.Entries.Count,
            };
            var placer = new PlanLabelPlacer(layout.Width, layout.Height, LabelGapUnits * layout.FontScale);
            var drawn = DrawEntries(input, layout, drawing, placer);
            var labeler = new PlanLabeler(drawing, layout.FontScale, placer, Obstacles(drawn));
            var roomNames = view == PlanView.Top ? AddRooms(input, layout, drawing) : new List<(string id, PlanRect area)>();
            if (labels) LabelEntries(input, drawn, drawing, labeler);
            if (labels) LabelRooms(roomNames, labeler);
            drawing.Shapes.AddRange(labeler.Shapes);
            PlanFurniture.Add(drawing, layout);
            return drawing;
        }

        private static Dictionary<DigestEntry, PlanRect> DrawEntries(DigestInput input, PlanLayout layout,
            PlanDrawing drawing, PlanLabelPlacer placer)
        {
            var rects = new Dictionary<DigestEntry, PlanRect>();
            var markers = new List<PlanShape>();
            foreach (var entry in DrawOrder(input.Entries, layout.View))
            {
                var rect = layout.RectOf(entry.Box);
                rects[entry] = rect;
                var layer = PlanLayers.Of(entry);
                bool issue = entry.HasIssues;
                if (issue) drawing.WithIssues++;
                drawing.Shapes.Add(PlanShape.Rect(rect.X0, rect.Y0, rect.X1, rect.Y1, PlanPalette.FillOf(layer),
                    issue ? PlanPalette.Issue : PlanPalette.Ink, issue ? Math.Max(layout.FontScale, 2) : OutlineWidth, entry.Name));
                if (issue) markers.Add(IssueMarker(rect, layout.FontScale, placer));
            }
            drawing.Shapes.AddRange(markers);
            return rects;
        }

        private static PlanShape IssueMarker(PlanRect rect, int scale, PlanLabelPlacer placer)
        {
            int size = IssueMarkerUnits * scale;
            int left = rect.X0, bottom = rect.Y0;
            placer.Reserve(new PlanRect(left, bottom - size, left + size, bottom));
            return PlanShape.Triangle(left, bottom, left + size, bottom, left + size / 2, bottom - size, PlanPalette.Issue);
        }

        private static List<DigestEntry> DrawOrder(List<DigestEntry> entries, PlanView view)
        {
            var ordered = new List<DigestEntry>(entries);
            ordered.Sort((a, b) => CompareDrawing(a, b, view));
            return ordered;
        }

        private static int Rank(PlanLayer layer) => PlanLayers.IsSolid(layer) ? (int)PlanLayer.Module : (int)layer;

        private static int CompareDrawing(DigestEntry a, DigestEntry b, PlanView view)
        {
            int byIssue = a.HasIssues.CompareTo(b.HasIssues);
            if (byIssue != 0) return byIssue;
            int byLayer = Rank(PlanLayers.Of(a)).CompareTo(Rank(PlanLayers.Of(b)));
            if (byLayer != 0) return byLayer;
            int byDepth = view == PlanView.Top
                ? a.Box.Max.y.CompareTo(b.Box.Max.y)
                : b.Box.Center.z.CompareTo(a.Box.Center.z);
            if (byDepth != 0) return byDepth;
            int byName = McpNaturalName.Compare(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.Name, b.Name);
        }

        private static List<PlanRect> Obstacles(Dictionary<DigestEntry, PlanRect> rects)
        {
            var obstacles = new List<PlanRect>();
            foreach (var pair in rects)
                if (PlanLayers.IsSolid(PlanLayers.Of(pair.Key))) obstacles.Add(pair.Value);
            return obstacles;
        }

        private static void LabelEntries(DigestInput input, Dictionary<DigestEntry, PlanRect> rects,
            PlanDrawing drawing, PlanLabeler labeler)
        {
            var described = new List<DigestEntry>();
            described.AddRange(SceneDigestText.Ordered(input.Entries, module: true));
            described.AddRange(SceneDigestText.Ordered(input.Entries, module: false));
            var failed = new HashSet<DigestEntry>();
            for (int pass = 0; pass <= LastLabelPass; pass++)
                foreach (var entry in described)
                    if (LabelPass(PlanLayers.Of(entry)) == pass && !labeler.TryLabel(entry, PlanLayers.Of(entry), rects[entry]))
                        failed.Add(entry);
            foreach (var entry in described)
                if (failed.Contains(entry)) drawing.Unlabelled.Add(entry.Name);
        }

        private static int LabelPass(PlanLayer layer)
        {
            if (layer == PlanLayer.Floor) return LastLabelPass;
            return layer == PlanLayer.Wall ? 1 : 0;
        }

        private static List<(string id, PlanRect area)> AddRooms(DigestInput input, PlanLayout layout, PlanDrawing drawing)
        {
            var lines = new List<PlanShape>();
            var names = new List<(string id, PlanRect area)>();
            foreach (var room in input.Rooms)
            {
                var polygon = room.PolygonXz;
                if (polygon == null || polygon.Length < 6) continue;
                int count = polygon.Length / 2;
                int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
                for (int i = 0; i < count; i++)
                {
                    int x0 = layout.X(polygon[i * 2]), y0 = layout.Y(polygon[i * 2 + 1]);
                    int next = (i + 1) % count;
                    lines.Add(PlanShape.Line(x0, y0, layout.X(polygon[next * 2]), layout.Y(polygon[next * 2 + 1]), PlanPalette.RoomLine));
                    left = Math.Min(left, x0);
                    top = Math.Min(top, y0);
                    right = Math.Max(right, x0);
                    bottom = Math.Max(bottom, y0);
                }
                names.Add((room.Id, new PlanRect(left, top, right, bottom)));
            }
            drawing.Shapes.InsertRange(FirstAfterFloors(input), lines);
            return names;
        }

        private static void LabelRooms(List<(string id, PlanRect area)> names, PlanLabeler labeler)
        {
            foreach (var (id, area) in names)
                labeler.TryInCorner(id, area, PlanPalette.RoomLine);
        }

        private static int FirstAfterFloors(DigestInput input)
        {
            int floors = 0;
            foreach (var entry in input.Entries)
                if (PlanLayers.Of(entry) == PlanLayer.Floor && !entry.HasIssues) floors++;
            return floors;
        }
    }
}
