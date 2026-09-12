using System;
using System.Threading;
using Newtonsoft.Json.Serialization;

namespace KitchenDesigner.Core.MCP
{
    public sealed class McpContractResolver : DefaultContractResolver
    {
        private int _contractsBuilt;

        public int ContractsBuilt => Volatile.Read(ref _contractsBuilt);

        protected override JsonContract CreateContract(Type objectType)
        {
            Interlocked.Increment(ref _contractsBuilt);
            return base.CreateContract(objectType);
        }
    }
}
