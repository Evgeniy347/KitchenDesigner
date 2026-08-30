using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    internal static class ElementNameNormalizer
    {
        public static void NormalizeAndRemapLinks(ElementData[] elements)
        {
            if (elements == null) return;
            var renames = BuildRenameMap(elements);
            foreach (var ed in elements)
            {
                if (ed == null) continue;
                ed.drawerPairedName = Renamed(renames, ed.drawerPairedName);
                ed.drawerAttachedFacadeName = Renamed(renames, ed.drawerAttachedFacadeName);
                ed.dishwasherAttachedFacadeName = Renamed(renames, ed.dishwasherAttachedFacadeName);
                ed.attachedToName = Renamed(renames, ed.attachedToName);
                ed.windowAttachedWallName = Renamed(renames, ed.windowAttachedWallName);
                ed.doorAttachedWallName = Renamed(renames, ed.doorAttachedWallName);
            }
        }

        private static Dictionary<string, string> BuildRenameMap(ElementData[] elements)
        {
            var takenInSceneAndFile = ElementNaming.ReservedFromScene();
            var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ed in elements)
            {
                if (ed == null) continue;
                var oldName = ed.name ?? string.Empty;

                var newName = ElementNaming.Normalize(oldName, null, takenInSceneAndFile);
                takenInSceneAndFile.Add(newName);
                ed.name = newName;

                if (!string.IsNullOrEmpty(oldName) && !renames.ContainsKey(oldName))
                    renames[oldName] = newName;
            }

            return renames;
        }

        private static string Renamed(Dictionary<string, string> renames, string? link)
        {
            if (string.IsNullOrEmpty(link)) return "";
            return renames.TryGetValue(link!, out var newName) ? newName : link!;
        }
    }
}
