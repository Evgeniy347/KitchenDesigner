namespace KitchenDesigner.Core.Update
{
    public readonly struct FolderEntry
    {
        public FolderEntry(string name, long size)
        {
            Name = name;
            Size = size;
        }

        public string Name { get; }
        public long Size { get; }
    }

    public readonly struct FileFacts
    {
        public FileFacts(bool exists, long size, string? sha256)
        {
            Exists = exists;
            Size = size;
            Sha256 = sha256 ?? string.Empty;
        }

        public bool Exists { get; }
        public long Size { get; }
        public string Sha256 { get; }

        public static FileFacts Absent => new FileFacts(false, 0, null);
    }
}
