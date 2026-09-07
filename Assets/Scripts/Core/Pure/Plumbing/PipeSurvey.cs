using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public sealed class PipeSurvey
    {
        private readonly IReadOnlyList<PipePort> _ports;
        private readonly string?[] _sizes;

        private PipeSurvey(IReadOnlyList<PipePort> ports, PipeNetwork network, string?[] sizes)
        {
            _ports = ports;
            Network = network;
            _sizes = sizes;
        }

        public IReadOnlyList<PipePort> Ports => _ports;

        public PipeNetwork Network { get; }

        public static PipeSurvey Of(IReadOnlyList<PipePort> ports)
        {
            var network = PipeNetwork.Build(ports);
            return new PipeSurvey(ports, network, PipeSizes.Resolve(ports, network));
        }

        public string? SizeOf(int portIndex) =>
            portIndex >= 0 && portIndex < _sizes.Length ? _sizes[portIndex] : null;

        public string DesignationOf(int portIndex) =>
            PipeSpec.DesignationOrDash(SizeOf(portIndex));

        public IReadOnlyList<string?> SizesOfElement(string elementId)
        {
            var found = new List<KeyValuePair<int, string?>>();
            for (int i = 0; i < _ports.Count; i++)
            {
                if (!string.Equals(_ports[i].ElementId, elementId, StringComparison.Ordinal)) continue;
                found.Add(new KeyValuePair<int, string?>(_ports[i].PortIndex, SizeOf(i)));
            }

            found.Sort((a, b) => a.Key.CompareTo(b.Key));

            var sizes = new string?[found.Count];
            for (int i = 0; i < found.Count; i++) sizes[i] = found[i].Value;
            return sizes;
        }
    }
}
