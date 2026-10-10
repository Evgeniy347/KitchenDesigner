using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal static class McpRunScopes
    {
        public const string Prefix = "run:";

        public static string Key(string id) => Prefix + id;

        public static HashSet<string> Owned(string id)
        {
            var scope = ProjectFloorplans.Find(Key(id));
            return new HashSet<string>(scope?.elements ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public static string? OwnerOf(string elementName, string exceptId)
        {
            foreach (var scope in ProjectFloorplans.Items)
            {
                if (!scope.id.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                if (string.Equals(scope.id, Key(exceptId), StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var name in scope.elements)
                    if (string.Equals(name, elementName, StringComparison.OrdinalIgnoreCase))
                        return scope.id.Substring(Prefix.Length);
            }
            return null;
        }

        public static IUndoCommand Record(string id, string[] names) =>
            new SetFloorplanMetadataCommand(Key(id), new List<RoomData>(), names);
    }
}
