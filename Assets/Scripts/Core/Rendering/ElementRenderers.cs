using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ElementRenderers
    {
        public static List<MeshRenderer> BodyOf(KitchenElement? element)
        {
            var result = new List<MeshRenderer>();
            if (element == null) return result;
            Collect(element, element.transform, result);
            return result;
        }

        public static string PathOf(KitchenElement element, Component renderer)
        {
            if (element == null || renderer == null) return "?";

            var node = renderer.transform;
            var name = node.name;
            while (node != null && node != element.transform)
            {
                node = node.parent;
                if (node != null && node != element.transform) name = node.name + "/" + name;
            }
            return name;
        }

        private static void Collect(KitchenElement owner, Transform node, List<MeshRenderer> into)
        {
            if (node == null) return;
            if (node != owner.transform && node.GetComponent<KitchenElement>() != null) return;

            var renderer = node.GetComponent<MeshRenderer>();
            if (renderer != null) into.Add(renderer);

            for (int i = 0; i < node.childCount; i++)
                Collect(owner, node.GetChild(i), into);
        }
    }
}
