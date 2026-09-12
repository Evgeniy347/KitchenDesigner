using System;
using System.Reflection;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ValidationGeometryContract
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly string[] MethodsWithoutArguments = { "GetVertices", "GetFaces" };

        private static readonly string[] MethodsTakingAPosition =
            { "GetVerticesAt", "GetFacesAt", "ValidationPositionAt" };

        private static readonly string[] PoseProperties =
        {
            "EffectiveScale", "ValidationPosition", "ValidationRotation",
            "AttachRestPosition", "AttachRestRotation", "PoseFollowsTransform",
        };

        public static bool BoxIsBuiltOnlyBy(Type type, Type[] proved)
        {
            foreach (var name in MethodsWithoutArguments)
                if (!IsProved(DeclarerOfMethod(type, name, Type.EmptyTypes), proved)) return false;

            var position = new[] { typeof(Vector3) };
            foreach (var name in MethodsTakingAPosition)
                if (!IsProved(DeclarerOfMethod(type, name, position), proved)) return false;

            foreach (var name in PoseProperties)
                if (!IsProved(DeclarerOfProperty(type, name), proved)) return false;

            return true;
        }

        private static Type? DeclarerOfMethod(Type type, string name, Type[] arguments)
        {
            for (Type? t = type; t != null; t = t.BaseType)
                if (t.GetMethod(name, Declared, null, arguments, null) != null) return t;
            return null;
        }

        private static Type? DeclarerOfProperty(Type type, string name)
        {
            for (Type? t = type; t != null; t = t.BaseType)
                if (t.GetProperty(name, Declared) != null) return t;
            return null;
        }

        private static bool IsProved(Type? declarer, Type[] proved)
        {
            if (declarer == null) return false;
            foreach (var type in proved)
                if (declarer == type) return true;
            return false;
        }
    }
}
