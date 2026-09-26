using System.Collections.Generic;
using KitchenDesigner.Core.Ports;

namespace KitchenDesigner.Core.Ventilation
{
    public sealed class DuctSurvey
    {
        private DuctSurvey(IReadOnlyList<Port> ports, PortNetwork network,
            IReadOnlyList<DuctRun> ducts, IReadOnlyList<GrilleRun> grilles,
            IReadOnlyList<RoomFootprint> rooms)
        {
            Ports = ports;
            Network = network;
            Ducts = ducts;
            Grilles = grilles;
            Rooms = rooms;
        }

        public IReadOnlyList<Port> Ports { get; }

        public PortNetwork Network { get; }

        public IReadOnlyList<DuctRun> Ducts { get; }

        public IReadOnlyList<GrilleRun> Grilles { get; }

        public IReadOnlyList<RoomFootprint> Rooms { get; }

        public static DuctSurvey Of(IReadOnlyList<Port> ports, IReadOnlyList<DuctRun> ducts,
            IReadOnlyList<GrilleRun> grilles, IReadOnlyList<RoomFootprint> rooms)
        {
            var network = PortNetwork.Build(ports, (a, b) => PortJoint.Connects(a, b));
            return new DuctSurvey(ports, network, ducts, grilles, rooms);
        }
    }
}
