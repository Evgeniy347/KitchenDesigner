using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ValidationGeometryContract
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly string[] ShapeMethodsWithoutArguments = { "GetVertices", "GetFaces" };

        private static readonly string[] ShapeMethodsTakingAPosition =
            { "GetVerticesAt", "GetFacesAt" };

        private static readonly string[] PoseMethodsTakingAPosition = { "ValidationPositionAt" };

        private static readonly string[] PoseProperties =
        {
            "EffectiveScale", "ValidationPosition", "ValidationRotation",
            "AttachRestPosition", "AttachRestRotation", "PoseFollowsTransform",
        };

        public static IReadOnlyList<string> MembersTheBoxIsBuiltFrom => TheBoxIsBuiltFrom;

        private static readonly string[] TheBoxIsBuiltFrom = Gather();

        private static string[] Gather()
        {
            var all = new List<string>();
            all.AddRange(ShapeMethodsWithoutArguments);
            all.AddRange(ShapeMethodsTakingAPosition);
            all.AddRange(PoseMethodsTakingAPosition);
            all.AddRange(PoseProperties);
            return all.ToArray();
        }

        public static IReadOnlyList<string> VirtualMembersOf(Type type)
        {
            var names = new List<string>();

            foreach (var property in type.GetProperties(Declared))
            {
                var getter = property.GetGetMethod(nonPublic: true);
                if (getter != null && getter.IsVirtual && !getter.IsFinal) names.Add(property.Name);
            }

            foreach (var method in type.GetMethods(Declared))
            {
                if (method.IsSpecialName || !method.IsVirtual || method.IsFinal) continue;
                if (!names.Contains(method.Name)) names.Add(method.Name);
            }

            return names;
        }

        public static bool ShapeIsBuiltOnlyBy(Type type, Type[] proved)
        {
            foreach (var name in ShapeMethodsWithoutArguments)
                if (!IsProved(DeclarerOfMethod(type, name, Type.EmptyTypes), proved)) return false;

            var position = new[] { typeof(Vector3) };
            foreach (var name in ShapeMethodsTakingAPosition)
                if (!IsProved(DeclarerOfMethod(type, name, position), proved)) return false;

            return true;
        }

        public static bool PoseIsBuiltOnlyBy(Type type, Type[] proved)
        {
            var position = new[] { typeof(Vector3) };
            foreach (var name in PoseMethodsTakingAPosition)
                if (!IsProved(DeclarerOfMethod(type, name, position), proved)) return false;

            foreach (var name in PoseProperties)
                if (!IsProved(DeclarerOfProperty(type, name), proved)) return false;

            return true;
        }

        public static bool BoxIsBuiltOnlyBy(Type type, Type[] provedShape, Type[] provedPose) =>
            ShapeIsBuiltOnlyBy(type, provedShape) && PoseIsBuiltOnlyBy(type, provedPose);

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
