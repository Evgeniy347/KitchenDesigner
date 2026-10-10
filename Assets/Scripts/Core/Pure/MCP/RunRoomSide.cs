using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal static class RunRoomSide
    {
        public static string FaceWords(int runAxis) => runAxis == 0 ? "front|back" : "left|right";

        public static bool TryResolve(string wallName, BoxMm wall, int runAxis, string? word,
            IReadOnlyList<BoxMm> floors, out bool onMaxSide, out string problem)
        {
            onMaxSide = true;
            problem = string.Empty;
            int perp = RunWall.PerpAxisOf(runAxis);
            if (!string.IsNullOrWhiteSpace(word))
                return TryFromWord(wallName, runAxis, perp, word!, out onMaxSide, out problem);
            return TryFromFloors(wallName, wall, runAxis, perp, floors, out onMaxSide, out problem);
        }

        private static bool TryFromWord(string wallName, int runAxis, int perp, string word,
            out bool onMaxSide, out string problem)
        {
            onMaxSide = true;
            problem = string.Empty;
            if (McpFace.TryParse(word, out int axis, out bool maxSide) && axis == perp)
            {
                onMaxSide = maxSide;
                return true;
            }
            problem = $"room_side '{word}' does not fit wall '{wallName}': it runs along {McpFace.AxisLetter(runAxis)}, "
                + $"so the face that looks into the room is {FaceWords(runAxis)}";
            return false;
        }

        private static bool TryFromFloors(string wallName, BoxMm wall, int runAxis, int perp,
            IReadOnlyList<BoxMm> floors, out bool onMaxSide, out string problem)
        {
            onMaxSide = true;
            problem = string.Empty;
            float towardMax = 0f, towardMin = 0f;
            foreach (var floor in floors)
            {
                if (floor.Max[runAxis] <= wall.Min[runAxis] || floor.Min[runAxis] >= wall.Max[runAxis]) continue;
                towardMax = System.Math.Max(towardMax, floor.Max[perp] - wall.Max[perp]);
                towardMin = System.Math.Max(towardMin, wall.Min[perp] - floor.Min[perp]);
            }
            if (towardMax > towardMin + Tolerance.ContactMm) { onMaxSide = true; return true; }
            if (towardMin > towardMax + Tolerance.ContactMm) { onMaxSide = false; return true; }
            problem = $"cannot tell which side of wall '{wallName}' is the room: the floor does not extend to one side only. "
                + $"Add room_side:'{FaceWords(runAxis).Replace("|", "' or '")}' (the face of the wall that looks into the room)";
            return false;
        }
    }
}
