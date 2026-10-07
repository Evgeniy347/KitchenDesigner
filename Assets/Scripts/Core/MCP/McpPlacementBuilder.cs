using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class McpPlacementBuilder
    {
        private readonly McpReference _reference;
        private readonly List<KitchenElement> _all;
        private readonly ValidationResult? _validation;
        private List<(KitchenElement element, NeighbourBox box)>? _scene;

        public McpPlacementBuilder(McpReference reference, List<KitchenElement> all, ValidationResult? validation)
        {
            _reference = reference;
            _all = all;
            _validation = validation;
        }

        public PlacementInfo Build(KitchenElement el)
        {
            var box = BoxOf(el);
            var relations = PlacementRelations.Of(box, NeighboursOf(el));
            var info = new PlacementInfo();
            info.name = el.PartName;
            info.posMm = McpAnchor.MmTriple(_reference.PointOf(box.Min, box.Max));
            info.footprintMm = McpAnchor.MmTriple(box.Size);
            info.on = relations.On;
            info.touches = relations.Touches.Count > 0 ? relations.Touches : null;
            info.gaps = relations.Gaps.Count > 0 ? relations.Gaps : null;
            info.room = RoomAt(box.Center);
            info.level = LevelRegistry.LevelOf(el).id;
            info.issues = McpPlacementIssues.Of(el, _all, _validation, relations);
            return info;
        }

        private static BoxMm BoxOf(KitchenElement el) =>
            McpAnchor.ToMmBoxStruct(McpAabb.Of(el.GetVertices()));

        private static string? RoomAt(Vector3 centreMm)
        {
            foreach (var room in ProjectRooms.Items)
                if (McpRoomPolygon.Contains(room.polygonXZ, centreMm.x, centreMm.z))
                    return room.id;
            return null;
        }

        private List<NeighbourBox> NeighboursOf(KitchenElement subject)
        {
            _scene ??= SceneBoxes();
            var others = new List<NeighbourBox>(_scene.Count);
            foreach (var (element, box) in _scene)
                if (element != subject) others.Add(box);
            return others;
        }

        private List<(KitchenElement element, NeighbourBox box)> SceneBoxes()
        {
            var scene = new List<(KitchenElement, NeighbourBox)>(_all.Count);
            foreach (var el in _all)
                if (el != null) scene.Add((el, new NeighbourBox(el.PartName, BoxOf(el))));
            return scene;
        }
    }
}
