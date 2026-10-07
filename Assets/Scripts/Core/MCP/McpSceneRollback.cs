using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal static class McpSceneRollback
    {
        public static void Discard(KitchenElement element)
        {
            if (element == null) return;
            SceneMembership.Leave(element.gameObject, element);
            DestroyNow.The(element.gameObject);
        }

        public static void Discard(IEnumerable<KitchenElement> elements)
        {
            foreach (var element in elements) Discard(element);
        }
    }
}
