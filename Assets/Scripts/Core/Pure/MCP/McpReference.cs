using System;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    public enum ReferenceSide
    {
        Min,
        Center,
        Max
    }

    public readonly struct McpReference : IEquatable<McpReference>
    {
        public const string DefaultName = "left-bottom-back";

        public const string Syntax =
            "Words joined with '-': left|right = X min|max, bottom|top = Y min|max, "
            + "back|front = Z min|max, center = the middle of every axis not named. "
            + "Examples: left-bottom-back (the default, the minimum corner), "
            + "center-bottom (middle of the footprint at floor level), center, right-top-front. "
            + "min and max are shortcuts for the two opposite corners. "
            + "Always WORLD axes of the box after rotation";

        private static readonly string[] XNames = { "left", "center", "right" };
        private static readonly string[] YNames = { "bottom", "center", "top" };
        private static readonly string[] ZNames = { "back", "center", "front" };

        public readonly ReferenceSide X;
        public readonly ReferenceSide Y;
        public readonly ReferenceSide Z;

        public McpReference(ReferenceSide x, ReferenceSide y, ReferenceSide z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static McpReference MinCorner =>
            new McpReference(ReferenceSide.Min, ReferenceSide.Min, ReferenceSide.Min);

        public bool IsMinCorner =>
            X == ReferenceSide.Min && Y == ReferenceSide.Min && Z == ReferenceSide.Min;

        public string Canonical => XNames[(int)X] + "-" + YNames[(int)Y] + "-" + ZNames[(int)Z];

        public static bool TryParse(string? text, out McpReference reference, out string error)
        {
            reference = MinCorner;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return true;

            var words = text!.Trim().ToLowerInvariant()
                .Split(new[] { '-', '_', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1 && words[0] == "min") return true;
            if (words.Length == 1 && words[0] == "max")
            {
                reference = new McpReference(ReferenceSide.Max, ReferenceSide.Max, ReferenceSide.Max);
                return true;
            }

            var slots = new ReferenceSide?[3];
            bool center = false;
            foreach (var word in words)
            {
                if (word == "center" || word == "centre") { center = true; continue; }
                if (!TryName(word, out int axis, out var side)
                    || !TryAssign(slots, axis, side))
                {
                    error = $"ref '{text}' is not understood. {Syntax}";
                    return false;
                }
            }

            var rest = center ? ReferenceSide.Center : ReferenceSide.Min;
            reference = new McpReference(slots[0] ?? rest, slots[1] ?? rest, slots[2] ?? rest);
            return true;
        }

        public Vector3 PointOf(Vector3 boxMin, Vector3 boxMax) => new Vector3(
            Pick(X, boxMin.x, boxMax.x), Pick(Y, boxMin.y, boxMax.y), Pick(Z, boxMin.z, boxMax.z));

        public Vector3 OffsetFromMin(Vector3 boxMin, Vector3 boxMax) => PointOf(boxMin, boxMax) - boxMin;

        public bool Equals(McpReference other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object? obj) => obj is McpReference other && Equals(other);

        public override int GetHashCode() => ((int)X * 3 + (int)Y) * 3 + (int)Z;

        private static float Pick(ReferenceSide side, float min, float max) =>
            side == ReferenceSide.Min ? min : side == ReferenceSide.Max ? max : (min + max) * 0.5f;

        private static bool TryName(string word, out int axis, out ReferenceSide side)
        {
            axis = 0;
            side = ReferenceSide.Min;
            switch (word)
            {
                case "left": return true;
                case "right": side = ReferenceSide.Max; return true;
                case "bottom": axis = 1; return true;
                case "top": axis = 1; side = ReferenceSide.Max; return true;
                case "back": axis = 2; return true;
                case "front": axis = 2; side = ReferenceSide.Max; return true;
                default: return false;
            }
        }

        private static bool TryAssign(ReferenceSide?[] slots, int axis, ReferenceSide side)
        {
            var current = slots[axis];
            if (current.HasValue && current.Value != side) return false;
            slots[axis] = side;
            return true;
        }
    }
}
