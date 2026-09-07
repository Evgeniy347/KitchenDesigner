using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeSizes
    {
        public static string?[] Resolve(IReadOnlyList<PipePort> ports, PipeNetwork network)
        {
            var sizes = new string?[ports.Count];
            for (int i = 0; i < ports.Count; i++)
            {
                var own = ports[i].DeclaredSizeId;
                if (own != null)
                {
                    sizes[i] = own;
                    continue;
                }

                int partner = network.PartnerOf(i);
                sizes[i] = partner == PipeNetwork.NoPartner ? null : ports[partner].DeclaredSizeId;
            }

            return sizes;
        }
    }
}
