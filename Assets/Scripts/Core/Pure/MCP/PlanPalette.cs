using System.Globalization;

namespace KitchenDesigner.Core.MCP
{
    public static class PlanPalette
    {
        public const int Background = 0;
        public const int Floor = 1;
        public const int RoomLine = 2;
        public const int Wall = 3;
        public const int Ink = 4;
        public const int Door = 5;
        public const int Window = 6;
        public const int Module = 7;
        public const int Part = 8;
        public const int Appliance = 9;
        public const int Issue = 10;
        public const int WallFacing = 11;
        public const int WallLine = 12;
        public const int DoorLine = 13;
        public const int WindowLine = 14;

        private static readonly int[] Rgb =
        {
            0xFFFFFF, 0xEFEBE2, 0x5B84B8, 0xA3A3A3, 0x1A1A1A, 0xD9B77E, 0x9ED3F0, 0xF6E9A6, 0xDDB88C, 0x9FB5CC, 0xD21F1F, 0xE6E9EE, 0x6E6E6E, 0x8A5A1E, 0x2F6DA3,
        };

        public static int Count => Rgb.Length;

        public static int FillOf(PlanLayer layer)
        {
            switch (layer)
            {
                case PlanLayer.Floor: return Floor;
                case PlanLayer.Wall: return Wall;
                case PlanLayer.Door: return Door;
                case PlanLayer.Window: return Window;
                case PlanLayer.Module: return Module;
                case PlanLayer.Appliance: return Appliance;
                default: return Part;
            }
        }

        public static byte[] Bytes(int index)
        {
            int value = Rgb[index];
            return new[] { (byte)(value >> 16), (byte)(value >> 8), (byte)value };
        }

        public static string Hex(int index) => "#" + Rgb[index].ToString("X6", CultureInfo.InvariantCulture);
    }
}
