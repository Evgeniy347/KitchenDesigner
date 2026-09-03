using System;
using System.Collections.Generic;
using System.Reflection;

namespace KitchenDesigner.Core.MCP.Contract
{
    public static class McpJsonSchema
    {
        private const BindingFlags ParamFields = BindingFlags.Public | BindingFlags.Instance;

        public static Dictionary<string, object> ForTool(McpToolDef tool)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<object>();

            if (tool.ParamsType != null)
            {
                foreach (var field in tool.ParamsType.GetFields(ParamFields))
                {
                    var param = ParamOf(field);
                    if (param == null)
                        continue;

                    var agentName = AgentNameOf(field, param);
                    properties[agentName] = PropertySchema(field, param);
                    if (param.Required)
                        required.Add(agentName);
                }
            }

            var schema = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties }
            };
            if (required.Count > 0)
                schema["required"] = required;
            return schema;
        }

        public static Dictionary<string, string> RenameTable(McpToolDef tool)
        {
            var table = new Dictionary<string, string>();
            if (tool.ParamsType == null)
                return table;

            foreach (var field in tool.ParamsType.GetFields(ParamFields))
            {
                var param = ParamOf(field);
                if (param == null)
                    continue;

                var agentName = AgentNameOf(field, param);
                if (agentName != field.Name)
                    table[agentName] = field.Name;
            }
            return table;
        }

        public static Dictionary<string, object> Annotations(McpToolDef tool)
        {
            if (tool.Kind == McpToolKind.Read)
                return new Dictionary<string, object>
                {
                    { "readOnlyHint", true },
                    { "openWorldHint", false }
                };

            if (tool.Kind == McpToolKind.Destructive)
                return new Dictionary<string, object>
                {
                    { "readOnlyHint", false },
                    { "destructiveHint", true },
                    { "openWorldHint", false }
                };

            if (tool.OpenWorld)
                return new Dictionary<string, object>
                {
                    { "readOnlyHint", false },
                    { "openWorldHint", true }
                };

            return new Dictionary<string, object>
            {
                { "readOnlyHint", false },
                { "destructiveHint", false },
                { "openWorldHint", false }
            };
        }

        private static McpParamAttribute? ParamOf(FieldInfo field)
        {
            if (field.GetCustomAttribute<McpIgnoreAttribute>() != null)
                return null;
            return field.GetCustomAttribute<McpParamAttribute>();
        }

        private static string AgentNameOf(FieldInfo field, McpParamAttribute param)
        {
            return string.IsNullOrEmpty(param.AgentName) ? field.Name : param.AgentName;
        }

        private static Dictionary<string, object> PropertySchema(FieldInfo field, McpParamAttribute param)
        {
            var declared = field.FieldType;
            var underlying = Nullable.GetUnderlyingType(declared) ?? declared;
            Dictionary<string, object> schema;

            if (param.Enum.Length > 0)
            {
                var values = new List<object>();
                foreach (var value in param.Enum)
                    values.Add(value);
                schema = new Dictionary<string, object>
                {
                    { "type", "string" },
                    { "enum", values }
                };
            }
            else if (underlying == typeof(string))
            {
                schema = new Dictionary<string, object> { { "type", "string" } };
                if (param.Required)
                    schema["minLength"] = 1L;
            }
            else if (underlying == typeof(int) || underlying == typeof(long))
            {
                schema = new Dictionary<string, object> { { "type", "integer" } };
                if (param.HasMin)
                    schema["minimum"] = (long)param.Min;
                if (param.HasMax)
                    schema["maximum"] = (long)param.Max;
            }
            else if (underlying == typeof(float) || underlying == typeof(double))
            {
                schema = new Dictionary<string, object> { { "type", "number" } };
                if (param.HasMin)
                    schema["minimum"] = param.Min;
                if (param.HasMax)
                    schema["maximum"] = param.Max;
            }
            else if (underlying == typeof(bool))
            {
                schema = new Dictionary<string, object> { { "type", "boolean" } };
            }
            else if (declared == typeof(string[]))
            {
                schema = new Dictionary<string, object>
                {
                    { "type", "array" },
                    {
                        "items", new Dictionary<string, object>
                        {
                            { "type", "string" },
                            { "minLength", 1L }
                        }
                    }
                };
                if (param.HasMin)
                    schema["minItems"] = (long)param.Min;
            }
            else if (declared.IsArray && IsNestedOpType(declared.GetElementType()))
            {
                schema = new Dictionary<string, object>
                {
                    { "type", "array" },
                    { "items", NestedObjectSchema(declared.GetElementType()!) }
                };
                if (param.HasMin)
                    schema["minItems"] = (long)param.Min;
                if (param.HasMax)
                    schema["maxItems"] = (long)param.Max;
            }
            else
            {
                throw new InvalidOperationException(
                    "Unsupported param type " + declared.Name + " on " +
                    field.DeclaringType?.Name + "." + field.Name);
            }

            schema["description"] = param.Description;
            return schema;
        }

        private static bool IsNestedOpType(Type? elementType)
        {
            return elementType != null && elementType.IsClass && elementType != typeof(string);
        }

        private static Dictionary<string, object> NestedObjectSchema(Type opType)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<object>();

            foreach (var field in opType.GetFields(ParamFields))
            {
                var param = ParamOf(field);
                if (param == null)
                    continue;

                if (!string.IsNullOrEmpty(param.AgentName) && param.AgentName != field.Name)
                    throw new InvalidOperationException(
                        "Nested param rename is not supported (the router applies rename only to " +
                        "top-level fields): " + opType.Name + "." + field.Name + " -> " + param.AgentName);

                properties[field.Name] = PropertySchema(field, param);
                if (param.Required)
                    required.Add(field.Name);
            }

            var schema = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties }
            };
            if (required.Count > 0)
                schema["required"] = required;
            return schema;
        }
    }
}
