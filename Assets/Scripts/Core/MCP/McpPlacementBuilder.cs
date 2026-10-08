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
            var relations = PlacementRelations.Of(box, NeighboursExcluding(new[] { el }));
            var info = Describe(el.PartName, box, relations);
            info.level = LevelRegistry.LevelOf(el).id;
            info.issues = McpPlacementIssues.Of(el, _all, _validation, relations);
            return info;
        }

        public PlacementInfo BuildGroup(string name, IReadOnlyList<KitchenElement> members)
        {
            int violating = 0;
            foreach (var member in members)
                if (_validation != null && _validation.violations.Contains(member)) violating++;
            var union = UnionBoxOf(members);
            var info = Describe(name, union, PlacementRelations.Of(union, NeighboursExcluding(members)));
            info.level = members.Count > 0 ? LevelRegistry.LevelOf(members[0]).id : null;
            if (violating > 0) info.issues.Add(violating + " of " + members.Count + " parts have issues");
            return info;
        }

        public static string? RoomAt(Vector3 centreMm)
        {
            foreach (var room in ProjectRooms.Items)
                if (McpRoomPolygon.Contains(room.polygonXZ, centreMm.x, centreMm.z))
                    return room.id;
            return null;
        }

        private PlacementInfo Describe(string name, BoxMm box, PlacementRelationSet relations)
        {
            var info = new PlacementInfo();
            info.name = name;
            info.posMm = McpAnchor.MmTriple(_reference.PointOf(box.Min, box.Max));
            info.footprintMm = McpAnchor.MmTriple(box.Size);
            info.on = relations.On;
            info.touches = relations.Touches.Count > 0 ? relations.Touches : null;
            info.gaps = relations.Gaps.Count > 0 ? relations.Gaps : null;
            info.room = RoomAt(box.Center);
            return info;
        }

        public static BoxMm BoxOf(KitchenElement el) =>
            McpAnchor.ToMmBoxStruct(McpAabb.Of(el.GetVertices()));

        public static BoxMm UnionBoxOf(IReadOnlyList<KitchenElement> members)
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var member in members)
            {
                var box = BoxOf(member);
                min = Vector3.Min(min, box.Min);
                max = Vector3.Max(max, box.Max);
            }
            return new BoxMm(min, max);
        }

        private List<NeighbourBox> NeighboursExcluding(IEnumerable<KitchenElement> subjects)
        {
            _scene ??= SceneBoxes();
            var excluded = new HashSet<KitchenElement>(subjects);
            var others = new List<NeighbourBox>(_scene.Count);
            foreach (var (element, box) in _scene)
                if (!excluded.Contains(element)) others.Add(box);
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
