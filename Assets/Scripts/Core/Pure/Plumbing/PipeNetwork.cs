using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public sealed class PipeNetwork
    {
        public const int NoPartner = Ports.PortNetwork.NoPartner;

        private readonly Ports.PortNetwork _inner;
        private readonly List<PipeLink> _links;

        private PipeNetwork(Ports.PortNetwork inner)
        {
            _inner = inner;
            _links = new List<PipeLink>(inner.Links.Count);
            foreach (var link in inner.Links)
                _links.Add(new PipeLink(link.APortIndex, link.BPortIndex));
        }

        public IReadOnlyList<PipeLink> Links => _links;

        public IReadOnlyList<int> FreePorts => _inner.FreePorts;

        public int PartnerOf(int portIndex) => _inner.PartnerOf(portIndex);

        public bool IsFree(int portIndex) => _inner.IsFree(portIndex);

        public static PipeNetwork Build(IReadOnlyList<PipePort> ports) =>
            new PipeNetwork(Ports.PortNetwork.Build(ports, (a, b) => PipeJoint.Connects(a, b)));
    }
}
