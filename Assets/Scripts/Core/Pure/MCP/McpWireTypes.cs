using System;
using System.Collections.Generic;
using System.Reflection;

namespace KitchenDesigner.Core.MCP
{
    public static class McpWireTypes
    {
        public const string ParamsPrefix = "Params";

        private const BindingFlags MemberScope =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        public static IReadOnlyList<Type> All(Type anchor)
        {
            var found = new List<Type>();
            var seen = new HashSet<Type>();
            foreach (var declared in anchor.Assembly.GetTypes())
            {
                if (!IsWireContract(declared, anchor)) continue;
                Collect(declared, found, seen);
            }
            return found;
        }

        public static bool IsWireContract(Type type, Type anchor) =>
            type.IsClass
            && type.IsPublic
            && !type.IsAbstract
            && !type.IsGenericTypeDefinition
            && type.Namespace == anchor.Namespace
            && type.GetConstructor(Type.EmptyTypes) != null;

        private static void Collect(Type type, List<Type> found, HashSet<Type> seen)
        {
            var bare = Bare(type);
            if (bare == null || !seen.Add(bare)) return;
            if (bare.IsPrimitive || bare.IsEnum || bare == typeof(string) || bare == typeof(decimal))
                return;
            if (bare.Namespace == null
                || !bare.Namespace.StartsWith("KitchenDesigner", StringComparison.Ordinal))
                return;

            found.Add(bare);
            foreach (var field in bare.GetFields(MemberScope)) Collect(field.FieldType, found, seen);
            foreach (var property in bare.GetProperties(MemberScope)) Collect(property.PropertyType, found, seen);
        }

        private static Type? Bare(Type type)
        {
            if (type.IsByRef || type.IsPointer) return null;
            if (type.IsArray) return Bare(type.GetElementType()!);
            var nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) return Bare(nullable);
            if (type.IsGenericType && type.GetGenericArguments().Length == 1)
                return Bare(type.GetGenericArguments()[0]);
            return type;
        }
    }
}
