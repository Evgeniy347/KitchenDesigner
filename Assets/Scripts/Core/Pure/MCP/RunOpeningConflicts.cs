using System;
using System.Collections.Generic;
using System.Globalization;

namespace KitchenDesigner.Core.MCP
{
    internal static class RunOpeningConflicts
    {
        public static List<string> Find(RunWall wall, IReadOnlyList<RunPlaced> cabinets, IReadOnlyList<NeighbourBox> openings)
        {
            var problems = new List<string>();
            foreach (var cabinet in cabinets)
                foreach (var opening in openings)
                    if (Blocks(wall, cabinet.Box, opening.Box))
                        problems.Add(Describe(wall, cabinet, opening));
            return problems;
        }

        private static bool Blocks(RunWall wall, BoxMm cabinet, BoxMm opening) =>
            Overlap(cabinet, opening, wall.RunAxis) > Tolerance.ContactMm && Overlap(cabinet, opening, 1) > Tolerance.ContactMm;

        private static float Overlap(BoxMm a, BoxMm b, int axis) =>
            Math.Min(a.Max[axis], b.Max[axis]) - Math.Max(a.Min[axis], b.Min[axis]);

        private static string Describe(RunWall wall, RunPlaced cabinet, NeighbourBox opening)
        {
            string axis = McpFace.AxisLetter(wall.RunAxis);
            string cabinetSpan = $"y {Range(cabinet.Box, 1)}, {axis} {Range(cabinet.Box, wall.RunAxis)} mm";
            string openingSpan = $"y {Range(opening.Box, 1)}, {axis} {Range(opening.Box, wall.RunAxis)} mm";
            string openingKind = opening.Box.Min.y > Tolerance.ContactMm + wall.Box.Min.y ? "window" : "door";
            string shift = $"move the run along the wall with start_mm so that '{cabinet.Name}' clears {axis} {Range(opening.Box, wall.RunAxis)}, "
                + $"or drop '{cabinet.Name}' from modules";
            if (cabinet.Kind == RunKind.Wall)
                return $"wall cabinet '{cabinet.Name}' ({cabinetSpan}) hangs in front of {openingKind} '{opening.Name}' ({openingSpan}): "
                    + shift + ", or make it kind base if it fits under the sill";
            return $"{RunKindDefaults.Word(cabinet.Kind)} cabinet '{cabinet.Name}' ({cabinetSpan}) stands in front of {openingKind} '{opening.Name}' ({openingSpan}): "
                + shift + (openingKind == "door" ? $", or start the run after the door with from:'{opening.Name}'" : string.Empty);
        }

        private static string Range(BoxMm box, int axis) =>
            Fmt(box.Min[axis]) + ".." + Fmt(box.Max[axis]);

        private static string Fmt(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
