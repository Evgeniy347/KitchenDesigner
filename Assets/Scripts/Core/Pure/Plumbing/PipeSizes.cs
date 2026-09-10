using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeSizes
    {
        public static string?[] Resolve(IReadOnlyList<PipePort> ports, PipeNetwork network)
        {
            var sizes = new string?[ports.Count];
            for (int i = 0; i < ports.Count; i++)
                sizes[i] = ports[i].DeclaredSizeId;

            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < ports.Count; i++)
                {
                    if (sizes[i] != null) continue;

                    int partner = network.PartnerOf(i);
                    if (partner == PipeNetwork.NoPartner) continue;

                    if (sizes[partner] != null)
                    {
                        sizes[i] = sizes[partner];
                        changed = true;
                        continue;
                    }

                    if (!PipeNodePorts.RequiresOneSize(ports[i].OwnerKind)) continue;
                    sizes[i] = SiblingSizeOf(ports, sizes, i);
                    if (sizes[i] != null) changed = true;
                }
            }

            return sizes;
        }

        private static string? SiblingSizeOf(IReadOnlyList<PipePort> ports, string?[] sizes, int i)
        {
            for (int j = 0; j < ports.Count; j++)
            {
                if (j == i || sizes[j] == null) continue;
                if (!string.Equals(ports[j].ElementId, ports[i].ElementId, StringComparison.Ordinal))
                    continue;
                return sizes[j];
            }
            return null;
        }

        public static IReadOnlyList<string?> PadToPortCount(IReadOnlyList<string?> boreSizeIds, int portCount)
        {
            if (boreSizeIds.Count == portCount) return boreSizeIds;

            var padded = new string?[portCount];
            for (int i = 0; i < portCount; i++)
                padded[i] = i < boreSizeIds.Count ? boreSizeIds[i] : null;
            return padded;
        }

        public static string Widest(IReadOnlyList<string?>? sizes)
        {
            if (sizes == null) return PipeSpec.DEFAULT_SIZE;

            string? widest = null;
            float widestMm = 0f;
            foreach (var id in sizes)
            {
                if (id == null) continue;
                float outer = PipeSpec.Get(id).OuterDiameterMm;
                if (widest != null && outer <= widestMm) continue;
                widest = PipeSpec.NormalizeSize(id);
                widestMm = outer;
            }

            return widest ?? PipeSpec.DEFAULT_SIZE;
        }
    }
}
