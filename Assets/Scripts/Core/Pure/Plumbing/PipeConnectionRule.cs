using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeConnectionRule
    {
        public const int NoChoice = 0;

        public static bool CanConnect(PipeNodeKind a, PipeNodeKind b) =>
            a != PipeNodeKind.Pipe || b != PipeNodeKind.Pipe;

        public static IReadOnlyList<PipeNodeKind> ChoicesFor(PipeNodeKind owner)
        {
            var choices = new List<PipeNodeKind>();
            foreach (var candidate in PipeNodePorts.Kinds)
                if (CanConnect(owner, candidate)) choices.Add(candidate);
            return choices;
        }

        public static IReadOnlyList<PipeNodeKind> ChoicesForAnyFitting() =>
            ChoicesFor(PipeNodeKind.Elbow);

        public static int OptionOf(IReadOnlyList<PipeNodeKind> choices, PipeNodeKind? neighbour)
        {
            if (choices == null || !neighbour.HasValue) return NoChoice;
            for (int i = 0; i < choices.Count; i++)
                if (choices[i] == neighbour.Value) return i + 1;
            return NoChoice;
        }

        public static PipeNodeKind? KindAt(IReadOnlyList<PipeNodeKind> choices, int option)
        {
            if (choices == null || option <= NoChoice || option > choices.Count) return null;
            return choices[option - 1];
        }
    }
}
