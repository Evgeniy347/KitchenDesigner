using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    internal static class SceneTreeByLevel
    {
        public static List<SceneTree.Node> Build(ISet<int>? collapsedGroups, ISet<string>? collapsedLevels)
        {
            var levels = new List<Level>(LevelRegistry.Items);
            if (levels.Count <= 1)
            {
                var single = SceneTree.Build(PartRegistry.GetAll(), GroupManager.AllGroups(), collapsedGroups);
                FoldLevelWhenCollapsed(single, collapsedLevels);
                return single;
            }

            levels.Sort((a, b) => b.floorElevationMm.CompareTo(a.floorElevationMm));

            var nodes = new List<SceneTree.Node>();
            foreach (var level in levels)
            {
                var elementsOnLevel = ElementsOnLevel(level);
                var groupsOnLevel = GroupsOnLevel(level, elementsOnLevel);
                var levelNodes = SceneTree.Build(elementsOnLevel, groupsOnLevel, collapsedGroups);
                levelNodes[0].rootLabel = level.name;
                levelNodes[0].rootKey = level.id;
                FoldLevelWhenCollapsed(levelNodes, collapsedLevels);
                nodes.AddRange(levelNodes);
            }
            return nodes;
        }

        private static void FoldLevelWhenCollapsed(List<SceneTree.Node> levelNodes, ISet<string>? collapsedLevels)
        {
            if (collapsedLevels == null || !collapsedLevels.Contains(levelNodes[0].rootKey)) return;
            levelNodes[0].collapsed = true;
            levelNodes.RemoveRange(1, levelNodes.Count - 1);
        }

        private static List<KitchenElement> ElementsOnLevel(Level level)
        {
            var result = new List<KitchenElement>();
            foreach (var e in PartRegistry.GetAll())
                if (e != null && LevelRegistry.LevelOf(e).id == level.id) result.Add(e);
            return result;
        }

        private static List<LinkGroup> GroupsOnLevel(Level level, List<KitchenElement> elementsOnLevel)
        {
            var result = new List<LinkGroup>();
            foreach (var g in GroupManager.AllGroups())
            {
                bool hasMemberHere = false;
                foreach (var e in elementsOnLevel)
                    if (e.GroupId == g.id) { hasMemberHere = true; break; }
                if (hasMemberHere) result.Add(g);
            }
            return result;
        }
    }
}
