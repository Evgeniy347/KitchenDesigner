using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public sealed class PipeNetwork
    {
        public const int NoPartner = -1;

        private readonly int[] _partner;
        private readonly List<PipeLink> _links;
        private readonly List<int> _freePorts;

        private PipeNetwork(int[] partner, List<PipeLink> links, List<int> freePorts)
        {
            _partner = partner;
            _links = links;
            _freePorts = freePorts;
        }

        public IReadOnlyList<PipeLink> Links => _links;

        public IReadOnlyList<int> FreePorts => _freePorts;

        public int PartnerOf(int portIndex) =>
            portIndex >= 0 && portIndex < _partner.Length ? _partner[portIndex] : NoPartner;

        public bool IsFree(int portIndex) => PartnerOf(portIndex) == NoPartner;

        public static PipeNetwork Build(IReadOnlyList<PipePort> ports)
        {
            var partner = new int[ports.Count];
            for (int i = 0; i < partner.Length; i++) partner[i] = NoPartner;

            var links = new List<PipeLink>();
            for (int i = 0; i < ports.Count; i++)
            {
                if (partner[i] != NoPartner) continue;
                for (int j = i + 1; j < ports.Count; j++)
                {
                    if (partner[j] != NoPartner) continue;
                    if (!PipeJoint.Connects(ports[i], ports[j])) continue;
                    partner[i] = j;
                    partner[j] = i;
                    links.Add(new PipeLink(i, j));
                    break;
                }
            }

            var freePorts = new List<int>();
            for (int i = 0; i < partner.Length; i++)
                if (partner[i] == NoPartner) freePorts.Add(i);

            return new PipeNetwork(partner, links, freePorts);
        }
    }
}
