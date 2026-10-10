using System;

namespace KitchenDesigner.Core.MCP
{
    internal static class RunKindDefaults
    {
        public const string Words = "base|wall|tall";

        public const int BaseHeightMm = 720;
        public const int BaseDepthMm = 560;

        public const int WallHeightMm = 720;
        public const int WallDepthMm = 320;
        public const int WallHangsAboveFloorMm = 1400;

        public const int TallHeightMm = 2100;
        public const int TallDepthMm = 560;

        public static bool TryParse(string? text, out RunKind kind)
        {
            kind = RunKind.Base;
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "base": kind = RunKind.Base; return true;
                case "wall": kind = RunKind.Wall; return true;
                case "tall": kind = RunKind.Tall; return true;
                default: return false;
            }
        }

        public static string Word(RunKind kind) => kind.ToString().ToLowerInvariant();

        public static int HeightMm(RunKind kind) => kind switch
        {
            RunKind.Wall => WallHeightMm,
            RunKind.Tall => TallHeightMm,
            RunKind.Base => BaseHeightMm,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static int DepthMm(RunKind kind) => kind switch
        {
            RunKind.Wall => WallDepthMm,
            RunKind.Tall => TallDepthMm,
            RunKind.Base => BaseDepthMm,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static int HangsAboveFloorMm(RunKind kind) => kind == RunKind.Wall ? WallHangsAboveFloorMm : 0;
    }
}
