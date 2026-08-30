using System;
using System.Reflection;

namespace KitchenDesigner.Core.MCP.Contract
{
    public static class McpContractEnums
    {
        public static string[] Of(Type paramsType, string fieldName)
        {
            var field = paramsType.GetField(fieldName);
            if (field == null)
                throw new ArgumentException(
                    "no such field: " + paramsType.Name + "." + fieldName, nameof(fieldName));
            var param = Attribute.GetCustomAttribute(field, typeof(McpParamAttribute)) as McpParamAttribute;
            if (param == null)
                throw new ArgumentException(
                    "field is not an MCP parameter: " + paramsType.Name + "." + fieldName, nameof(fieldName));
            return param.Enum;
        }
    }
}
