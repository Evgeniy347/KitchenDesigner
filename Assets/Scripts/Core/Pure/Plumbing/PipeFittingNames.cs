using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeFittingNames
    {
        public static readonly PipeNodeKind[] Kinds =
        {
            PipeNodeKind.Elbow,
            PipeNodeKind.Coupling,
            PipeNodeKind.Tee,
            PipeNodeKind.Cap,
            PipeNodeKind.Supply,
            PipeNodeKind.Return,
        };

        public static string TypeId(PipeNodeKind kind) => kind switch
        {
            PipeNodeKind.Elbow => "pipe_elbow",
            PipeNodeKind.Coupling => "pipe_coupling",
            PipeNodeKind.Tee => "pipe_tee",
            PipeNodeKind.Cap => "pipe_cap",
            PipeNodeKind.Supply => "pipe_supply",
            PipeNodeKind.Return => "pipe_return",
            _ => "pipe",
        };

        public static string Title(PipeNodeKind kind) => kind switch
        {
            PipeNodeKind.Elbow => Loc.T("plumbing.fitting.elbow90"),
            PipeNodeKind.Coupling => Loc.T("plumbing.fitting.coupling"),
            PipeNodeKind.Tee => Loc.T("plumbing.fitting.tee"),
            PipeNodeKind.Cap => Loc.T("plumbing.fitting.cap"),
            PipeNodeKind.Supply => Loc.T("plumbing.fitting.supply"),
            PipeNodeKind.Return => Loc.T("plumbing.fitting.return"),
            _ => Loc.T("elementType.pipe"),
        };

        public static bool TryParseTypeId(string? typeId, out PipeNodeKind kind)
        {
            foreach (var candidate in Kinds)
            {
                if (!string.Equals(TypeId(candidate), typeId, StringComparison.OrdinalIgnoreCase))
                    continue;
                kind = candidate;
                return true;
            }

            kind = PipeNodeKind.Elbow;
            return false;
        }

        public static IReadOnlyList<string> TypeIds()
        {
            var ids = new string[Kinds.Length];
            for (int i = 0; i < Kinds.Length; i++) ids[i] = TypeId(Kinds[i]);
            return ids;
        }
    }
}
