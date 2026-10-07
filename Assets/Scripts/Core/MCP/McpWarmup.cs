using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public static class McpWarmup
    {
        private static bool _done;
        private static int _typesWarmed;

        public static bool Done => _done;

        public static int TypesWarmed => _typesWarmed;

        public static void RunOnce()
        {
            if (_done) return;
            _done = true;
            Run();
        }

        public static void Run()
        {
            int warmed = 0;
            foreach (var type in McpWireTypes.All(typeof(CreateItem)))
            {
                McpJson.Resolver.ResolveContract(type);
                warmed++;
            }
            _typesWarmed = warmed;

            ReadOneRequest();
            WriteOneResponse();
        }

        private static void ReadOneRequest()
        {
            var body = new JObject
            {
                ["items"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "board",
                        ["name"] = "warmup",
                        ["anchor_x_mm"] = 0,
                        ["anchor_y_mm"] = 0,
                        ["anchor_z_mm"] = 0
                    }
                }
            };
            body.ToObjectStrict<ParamsCreateElements>();
        }

        private static void WriteOneResponse()
        {
            var placement = new PlacementInfo();
            placement.touches = new List<PlacementContact> { new PlacementContact() };
            placement.gaps = new List<PlacementGap> { new PlacementGap() };
            var reply = new MutationReply();
            reply.placements = new List<PlacementInfo> { placement };
            reply.@ref = McpReference.DefaultName;
            McpJson.Serialize(reply);
            McpJson.Serialize(new List<ElementInfo> { new ElementInfo() });
        }
    }
}
