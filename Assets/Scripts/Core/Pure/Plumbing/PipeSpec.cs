using System;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeSpec
    {
        public const string Dn15 = "dn15";
        public const string Dn20 = "dn20";
        public const string Dn25 = "dn25";
        public const string Dn32 = "dn32";
        public const string Dn40 = "dn40";
        public const string Dn50 = "dn50";

        public const string DEFAULT_SIZE = Dn20;

        public const string NoValue = "—";

        public static readonly PipeSize[] Table =
        {
            new PipeSize(Dn15, "1/2\"", 15, 21.3f, 2.8f),
            new PipeSize(Dn20, "3/4\"", 20, 26.8f, 2.8f),
            new PipeSize(Dn25, "1\"", 25, 33.5f, 3.2f),
            new PipeSize(Dn32, "1 1/4\"", 32, 42.3f, 3.2f),
            new PipeSize(Dn40, "1 1/2\"", 40, 48.0f, 3.5f),
            new PipeSize(Dn50, "2\"", 50, 60.0f, 3.5f),
        };

        public static readonly string[] Sizes = CollectIds();

        public static bool TryFind(string? id, out PipeSize size)
        {
            foreach (var candidate in Table)
            {
                if (!string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase)) continue;
                size = candidate;
                return true;
            }

            size = Table[0];
            return false;
        }

        public static PipeSize Get(string? id)
        {
            if (TryFind(id, out var size)) return size;
            TryFind(DEFAULT_SIZE, out var fallback);
            return fallback;
        }

        public static string NormalizeSize(string? id) =>
            TryFind(id, out var size) ? size.Id : DEFAULT_SIZE;

        public static string DesignationOrDash(string? id) =>
            TryFind(id, out var size) ? size.Designation : NoValue;

        public static string NominalOrDash(string? id) =>
            TryFind(id, out var size) ? size.NominalBoreMm.ToString() : NoValue;

        private static string[] CollectIds()
        {
            var ids = new string[Table.Length];
            for (int i = 0; i < Table.Length; i++) ids[i] = Table[i].Id;
            return ids;
        }
    }
}
