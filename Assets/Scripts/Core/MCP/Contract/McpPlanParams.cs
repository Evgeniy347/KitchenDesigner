using System;

namespace KitchenDesigner.Core.MCP.Contract
{
    [Serializable]
    public class ParamsRenderPlan
    {
        [McpParam("Projection: top = plan seen from above (x RIGHT, z UP the picture, north up like preview_floorplan), front = elevation (x RIGHT, y UP). Default top.",
            Enum = new[] { "top", "front" })]
        public string view = "top";

        [McpParam("Narrow the picture to one place and zoom to it: a module name (module:B4 also works), room:ID or a room id, "
            + "or a selector (name mask, type:board, module:B4*, thickness==18 - see guide topic bulk); the PARTS are then drawn one by one. "
            + "Same words as describe_scene scope. Omit for the whole scene.")]
        public string? scope;

        [McpParam("Print the names on the parts (default true). false gives a bare silhouette.")]
        public bool labels = true;

        [McpParam("COUNT of pixels of the LONGER side of the picture, 256..2048, default 512 (about 3 KB of PNG). "
            + "Larger = names fit on smaller parts.",
            Min = PlanRender.MinPx, Max = PlanRender.MaxPx)]
        public int px = PlanRender.DefaultPx;
    }
}
