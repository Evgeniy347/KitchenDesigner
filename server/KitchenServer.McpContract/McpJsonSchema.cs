using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenServer.McpContract
{
    /// <summary>
    /// SERVER-SIDE (not compiled by Unity). Turns a tool's params POCO from the
    /// shared contract into a JSON Schema for MCP tools/list, and exposes the
    /// agent->wire rename map used when forwarding a call to the Unity client.
    /// Mirrors the Zod emitted by McpContractGen — same [McpParam]/[McpIgnore] rules.
    /// </summary>
    public static class McpJsonSchema
    {
        private static readonly JsonElement EmptyObjectSchema =
            JsonSerializer.SerializeToElement(new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject(),
            });

        /// <summary>JSON Schema for a tool's arguments. Empty object schema when no params.</summary>
        public static JsonElement BuildInputSchema(Type? paramsType)
        {
            if (paramsType == null) return EmptyObjectSchema;

            var properties = new JsonObject();
            var required = new JsonArray();

            foreach (var f in paramsType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.GetCustomAttribute<McpIgnoreAttribute>() != null) continue;
                var p = f.GetCustomAttribute<McpParamAttribute>();
                if (p == null) continue;

                var agentName = string.IsNullOrEmpty(p.Name) ? f.Name : p.Name;
                properties[agentName] = FieldSchema(f, p);
                if (p.Required) required.Add(agentName);
            }

            var schema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties,
            };
            if (required.Count > 0) schema["required"] = required;
            return JsonSerializer.SerializeToElement(schema);
        }

        /// <summary>agent-facing param name -> Unity wire field name (only where they differ). Null if none.</summary>
        public static IReadOnlyDictionary<string, string>? BuildRenameMap(Type? paramsType)
        {
            if (paramsType == null) return null;
            Dictionary<string, string>? map = null;
            foreach (var f in paramsType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.GetCustomAttribute<McpIgnoreAttribute>() != null) continue;
                var p = f.GetCustomAttribute<McpParamAttribute>();
                if (p == null) continue;
                var agentName = string.IsNullOrEmpty(p.Name) ? f.Name : p.Name;
                if (agentName != f.Name)
                {
                    map ??= new Dictionary<string, string>();
                    map[agentName] = f.Name;
                }
            }
            return map;
        }

        private static JsonObject FieldSchema(FieldInfo field, McpParamAttribute p)
        {
            var t = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
            var node = new JsonObject();

            if (p.Enum is { Length: > 0 })
            {
                node["type"] = "string";
                var e = new JsonArray();
                foreach (var v in p.Enum) e.Add(v);
                node["enum"] = e;
            }
            else if (t == typeof(string))
            {
                node["type"] = "string";
            }
            else if (t == typeof(int) || t == typeof(long))
            {
                node["type"] = "integer";
                if (p.HasMin) node["minimum"] = (long)p.Min;
                if (p.HasMax) node["maximum"] = (long)p.Max;
            }
            else if (t == typeof(float) || t == typeof(double))
            {
                node["type"] = "number";
                if (p.HasMin) node["minimum"] = p.Min;
                if (p.HasMax) node["maximum"] = p.Max;
            }
            else if (t == typeof(bool))
            {
                node["type"] = "boolean";
            }
            else if (field.FieldType == typeof(string[]))
            {
                node["type"] = "array";
                node["items"] = new JsonObject { ["type"] = "string" };
                if (p.HasMin) node["minItems"] = (long)p.Min;
            }
            else if (field.FieldType.IsArray
                && field.FieldType.GetElementType() is { IsClass: true } et && et != typeof(string))
            {
                // Массив объектов (BatchOp[] и т.п.) — зеркалит z.array(z.object({...}))
                // кодогенератора. Вложенные rename не поддерживаются (как и в Zod-пути).
                var itemProps = new JsonObject();
                var itemRequired = new JsonArray();
                foreach (var f2 in et.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (f2.GetCustomAttribute<McpIgnoreAttribute>() != null) continue;
                    var p2 = f2.GetCustomAttribute<McpParamAttribute>();
                    if (p2 == null) continue;
                    if (!string.IsNullOrEmpty(p2.Name) && p2.Name != f2.Name)
                        throw new InvalidOperationException(
                            $"Nested param rename is not supported: {et.Name}.{f2.Name} -> {p2.Name}");
                    itemProps[f2.Name] = FieldSchema(f2, p2);
                    if (p2.Required) itemRequired.Add(f2.Name);
                }
                var item = new JsonObject { ["type"] = "object", ["properties"] = itemProps };
                if (itemRequired.Count > 0) item["required"] = itemRequired;
                node["type"] = "array";
                node["items"] = item;
                if (p.HasMin) node["minItems"] = (long)p.Min;
                if (p.HasMax) node["maxItems"] = (long)p.Max;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported param type {field.FieldType.Name} on {field.DeclaringType?.Name}.{field.Name}");
            }

            node["description"] = p.Description;
            return node;
        }
    }
}
