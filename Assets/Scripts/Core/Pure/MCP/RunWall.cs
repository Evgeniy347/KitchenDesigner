namespace KitchenDesigner.Core.MCP
{
    internal readonly struct RunWall
    {
        public readonly string Name;
        public readonly BoxMm Box;
        public readonly int RunAxis;
        public readonly bool RoomOnMaxSide;

        public RunWall(string name, BoxMm box, int runAxis, bool roomOnMaxSide)
        {
            Name = name;
            Box = box;
            RunAxis = runAxis;
            RoomOnMaxSide = roomOnMaxSide;
        }

        public int PerpAxis => PerpAxisOf(RunAxis);

        public float LowEnd => Box.Min[RunAxis];

        public float HighEnd => Box.Max[RunAxis];

        public float LengthMm => HighEnd - LowEnd;

        public string RoomFace => McpFace.NameOf(PerpAxis, RoomOnMaxSide);

        public float RotYDeg
        {
            get
            {
                if (RunAxis == 0) return RoomOnMaxSide ? 0f : 180f;
                return RoomOnMaxSide ? 90f : 270f;
            }
        }

        public static int RunAxisOf(BoxMm wall) => wall.Size.x >= wall.Size.z ? 0 : 2;

        public static int PerpAxisOf(int runAxis) => runAxis == 0 ? 2 : 0;
    }
}
