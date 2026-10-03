namespace KitchenDesigner.Core
{
    internal sealed class RegistryValue
    {
        public RegistryValue(string name, int kind, byte[] data)
        {
            Name = name;
            Kind = kind;
            Data = data;
        }

        public string Name { get; }
        public int Kind { get; }
        public byte[] Data { get; }

        public bool SameAs(RegistryValue other)
        {
            if (Name != other.Name || Kind != other.Kind || Data.Length != other.Data.Length) return false;
            for (int i = 0; i < Data.Length; i++)
                if (Data[i] != other.Data[i]) return false;
            return true;
        }
    }
}
