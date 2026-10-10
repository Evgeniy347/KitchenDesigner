using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class McpRunScene
    {
        private const float RightAngleToleranceDeg = 0.5f;
        private const float QuarterTurnDeg = 90f;

        public readonly KitchenElement WallElement;
        public readonly BoxMm WallBox;
        public readonly int RunAxis;
        public readonly List<BoxMm> Floors = new List<BoxMm>();
        public readonly List<NeighbourBox> Openings = new List<NeighbourBox>();

        private McpRunScene(KitchenElement wallElement, BoxMm wallBox, int runAxis)
        {
            WallElement = wallElement;
            WallBox = wallBox;
            RunAxis = runAxis;
        }

        public static bool TryRead(KitchenElement? element, string wallName, out McpRunScene scene, out string problem)
        {
            scene = null!;
            problem = string.Empty;
            if (element == null)
            {
                problem = McpNameHints.NotFound(wallName, AllNames());
                return false;
            }
            var wall = element.GetComponent<Wall>();
            if (wall == null)
            {
                problem = $"'{wallName}' is not a wall. Name the wall the run stands against (get_scene_tree lists walls)";
                return false;
            }
            float yaw = element.transform.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Round(yaw / QuarterTurnDeg) * QuarterTurnDeg)) > RightAngleToleranceDeg)
            {
                problem = $"wall '{wallName}' is turned {yaw:0.#} degrees: apply_run needs a wall along x or along z. "
                    + "Put its cabinets one by one with place instead";
                return false;
            }
            var box = BoxOf(element);
            scene = new McpRunScene(element, box, RunWall.RunAxisOf(box));
            scene.ReadFloors(element.LevelId);
            scene.ReadOpenings(wall);
            return true;
        }

        public static BoxMm BoxOf(KitchenElement element) =>
            McpAnchor.ToMmBoxStruct(McpAabb.Of(element.GetVertices()));

        private void ReadFloors(string levelId)
        {
            foreach (var element in PartRegistry.GetAll())
                if (element is FloorElement && element.LevelId == levelId)
                    Floors.Add(BoxOf(element));
        }

        private void ReadOpenings(Wall wall)
        {
            foreach (var window in wall.AttachedWindows)
                if (window != null) Openings.Add(new NeighbourBox(window.PartName, BoxOf(window)));
            foreach (var door in wall.AttachedDoors)
                if (door != null) Openings.Add(new NeighbourBox(door.PartName, BoxOf(door)));
        }

        private static List<string> AllNames()
        {
            var names = new List<string>();
            foreach (var element in PartRegistry.GetAll())
                if (element != null) names.Add(element.PartName);
            return names;
        }
    }
}
