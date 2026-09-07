namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeNodePorts
    {
        public static readonly PipeNodeKind[] Kinds =
        {
            PipeNodeKind.Pipe,
            PipeNodeKind.Elbow,
            PipeNodeKind.Coupling,
            PipeNodeKind.Tee,
            PipeNodeKind.Cap,
            PipeNodeKind.Supply,
            PipeNodeKind.Return,
        };

        public static int CountOf(PipeNodeKind kind) => kind switch
        {
            PipeNodeKind.Pipe => 2,
            PipeNodeKind.Elbow => 2,
            PipeNodeKind.Coupling => 2,
            PipeNodeKind.Tee => 3,
            PipeNodeKind.Cap => 1,
            PipeNodeKind.Supply => 1,
            PipeNodeKind.Return => 1,
            _ => 2,
        };

        public static bool DeclaresOwnSize(PipeNodeKind kind) =>
            kind == PipeNodeKind.Pipe;

        public static bool RequiresOneSize(PipeNodeKind kind) =>
            kind == PipeNodeKind.Elbow || kind == PipeNodeKind.Tee;

        public static bool ClosesAnEnd(PipeNodeKind kind) =>
            kind != PipeNodeKind.Pipe;
    }
}
