namespace KitchenDesigner.Core
{
    public readonly struct PortedPart
    {
        public readonly int Id;

        public readonly string Name;

        public readonly SnapPort[] Ports;

        public PortedPart(int id, string name, SnapPort[]? ports)
        {
            Id = id;
            Name = name ?? string.Empty;
            Ports = ports ?? System.Array.Empty<SnapPort>();
        }

        public bool HasPorts => Ports != null && Ports.Length > 0;

        public static PortedPart Of(in ElementGeometry geometry) =>
            new PortedPart(geometry.Id, geometry.Name, geometry.Ports);
    }
}
