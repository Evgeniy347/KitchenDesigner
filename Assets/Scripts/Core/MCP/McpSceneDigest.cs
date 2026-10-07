using System.Collections.Generic;
using KitchenDesigner.Core.Bulk;

namespace KitchenDesigner.Core.MCP
{
    internal static class McpSceneDigest
    {
        public static bool TryCollect(string? scopeText, McpReference reference, out DigestInput input, out string error)
        {
            var all = PartRegistry.GetAll() ?? new List<KitchenElement>();
            var live = LiveParts(all);
            input = new DigestInput { Ref = reference.Canonical };
            if (!McpDigestScope.TryResolve(scopeText, live, out var scope, out error)) return false;

            input.Scope = scope.Label;
            var placements = new McpPlacementBuilder(reference, all, McpValidationCache.Get(all));
            if (scope.Detail != null) AddElements(input.Entries, scope.Detail, placements);
            else AddSceneView(input, live, placements, scope.RoomId);
            if (scope.Label == null) AddLevelsAndRooms(input);
            return true;
        }

        private static List<KitchenElement> LiveParts(List<KitchenElement> all)
        {
            var live = new List<KitchenElement>(all.Count);
            foreach (var element in all)
                if (McpDigestScope.IsLive(element)) live.Add(element);
            return live;
        }

        private static void AddElements(List<DigestEntry> entries, List<KitchenElement> elements,
            McpPlacementBuilder placements)
        {
            foreach (var element in elements) entries.Add(ElementEntry(element, placements));
        }

        private static void AddSceneView(DigestInput input, List<KitchenElement> live,
            McpPlacementBuilder placements, string? roomId)
        {
            var inModule = new HashSet<KitchenElement>();
            var entries = new List<DigestEntry>();
            foreach (var group in GroupManager.AllGroups())
            {
                var members = McpDigestScope.LiveMembers(group);
                if (members.Count == 0) continue;
                inModule.UnionWith(members);
                entries.Add(new DigestEntry
                {
                    Name = group.name,
                    Kind = "module",
                    Parts = members.Count,
                    Placement = placements.BuildGroup(group.name, members),
                });
            }
            foreach (var element in live)
                if (!inModule.Contains(element)) entries.Add(ElementEntry(element, placements));

            foreach (var entry in entries)
                if (roomId == null || entry.Placement.room == roomId) input.Entries.Add(entry);
        }

        private static DigestEntry ElementEntry(KitchenElement element, McpPlacementBuilder placements) => new DigestEntry
        {
            Name = element.PartName,
            Kind = ElementSelector.TypeOf(element),
            Placement = placements.Build(element),
        };

        private static void AddLevelsAndRooms(DigestInput input)
        {
            foreach (var level in LevelRegistry.Items)
                input.Levels.Add(new DigestGroup { Id = level.id, Detail = LevelDetail(level) });
            foreach (var room in ProjectRooms.Items)
                input.Rooms.Add(new DigestGroup { Id = room.id });
        }

        private static string LevelDetail(Level level)
        {
            var span = level.floorElevationMm + ".." + (level.floorElevationMm + level.heightMm);
            return string.IsNullOrEmpty(level.name) ? span : "\"" + level.name + "\" " + span;
        }
    }
}
