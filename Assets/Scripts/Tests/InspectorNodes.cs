using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Tests
{
    public static class InspectorNodes
    {
        public const string Panel = "ContextMenu";

        public static Transform FindNode(this Transform root, string path)
        {
            int slash = path.IndexOf('/');
            string first = slash < 0 ? path : path.Substring(0, slash);
            var found = Breadth(root, first);
            if (found == null || slash < 0) return found!;
            return found.Find(path.Substring(slash + 1));
        }

        public static Transform Node(this Transform root, string path) =>
            root.FindNode(path) ?? throw new KeyNotFoundException("no node '" + path + "' under " + root.name);

        public static bool IsShown(this Transform? node) => node != null && node.gameObject.activeInHierarchy;

        private static Transform? Breadth(Transform root, string name)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (Transform child in current)
                {
                    if (child.name == name) return child;
                    queue.Enqueue(child);
                }
            }
            return null;
        }
    }
}
