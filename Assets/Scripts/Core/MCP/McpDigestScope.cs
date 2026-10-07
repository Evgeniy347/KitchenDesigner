using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Bulk;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class McpDigestScope
    {
        public const string RoomPrefix = "room:";

        public const string ModulePrefix = "module:";

        public string? Label { get; private set; }

        public List<KitchenElement>? Detail { get; private set; }

        public string? RoomId { get; private set; }

        public static McpDigestScope WholeScene => new McpDigestScope();

        public static bool TryResolve(string? text, List<KitchenElement> live, out McpDigestScope scope, out string error)
        {
            scope = WholeScene;
            error = string.Empty;
            var wanted = (text ?? string.Empty).Trim();
            if (wanted.Length == 0) return true;

            if (wanted.StartsWith(RoomPrefix, StringComparison.OrdinalIgnoreCase))
                return TryRoom(wanted.Substring(RoomPrefix.Length).Trim(), wanted, ref scope, out error);

            var moduleName = wanted.StartsWith(ModulePrefix, StringComparison.OrdinalIgnoreCase)
                ? wanted.Substring(ModulePrefix.Length).Trim()
                : wanted;
            var group = FindGroup(moduleName);
            if (group != null)
            {
                scope = new McpDigestScope { Label = "module " + group.name, Detail = LiveMembers(group) };
                return true;
            }

            var room = FindRoom(wanted);
            if (room != null)
            {
                scope = new McpDigestScope { Label = "room " + room, RoomId = room };
                return true;
            }

            var matched = ElementSelector.Match(wanted, live);
            if (matched.Count > 0)
            {
                scope = new McpDigestScope { Label = "selector \"" + wanted + "\"", Detail = matched };
                return true;
            }

            error = McpNameHints.UnknownScope(wanted, ModuleNames(), RoomIds());
            return false;
        }

        private static bool TryRoom(string id, string wanted, ref McpDigestScope scope, out string error)
        {
            error = string.Empty;
            var room = FindRoom(id);
            if (room == null)
            {
                error = McpNameHints.UnknownScope(wanted, ModuleNames(), RoomIds());
                return false;
            }
            scope = new McpDigestScope { Label = "room " + room, RoomId = room };
            return true;
        }

        public static List<KitchenElement> LiveMembers(LinkGroup group)
        {
            var live = new List<KitchenElement>();
            foreach (var member in GroupManager.MembersOf(group))
                if (IsLive(member)) live.Add(member);
            return live;
        }

        public static bool IsLive(KitchenElement? element) => element != null && element.gameObject.activeInHierarchy;

        private static LinkGroup? FindGroup(string name)
        {
            foreach (var group in GroupManager.AllGroups())
                if (string.Equals(group.name, name, StringComparison.OrdinalIgnoreCase)) return group;
            return null;
        }

        private static string? FindRoom(string id)
        {
            foreach (var room in ProjectRooms.Items)
                if (string.Equals(room.id, id, StringComparison.OrdinalIgnoreCase)) return room.id;
            return null;
        }

        private static List<string> ModuleNames()
        {
            var names = new List<string>();
            foreach (var group in GroupManager.AllGroups()) names.Add(group.name);
            names.Sort(McpNaturalName.Compare);
            return names;
        }

        private static List<string> RoomIds()
        {
            var ids = new List<string>();
            foreach (var room in ProjectRooms.Items) ids.Add(room.id);
            return ids;
        }
    }
}
