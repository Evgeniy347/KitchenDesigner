namespace KitchenDesigner.Core.Update
{
    public enum IntegrityMethod { None, Size, Sha256 }

    public enum IntegrityVerdict { Ok, Missing, Mismatch, CannotVerify }

    public readonly struct InstallerExpectation
    {
        public InstallerExpectation(string? sha256, long size)
        {
            Sha256 = Sha256Digest.Normalize(sha256);
            Size = size > 0 ? size : 0;
        }

        public string Sha256 { get; }
        public long Size { get; }

        public IntegrityMethod Method =>
            Sha256.Length > 0 ? IntegrityMethod.Sha256
            : Size > 0 ? IntegrityMethod.Size
            : IntegrityMethod.None;

        public bool NeedsHash => Method == IntegrityMethod.Sha256;

        public IntegrityVerdict Judge(FileFacts facts)
        {
            switch (Method)
            {
                case IntegrityMethod.None:
                    return IntegrityVerdict.CannotVerify;
                case IntegrityMethod.Sha256:
                    if (!facts.Exists) return IntegrityVerdict.Missing;
                    return Sha256Digest.Normalize(facts.Sha256) == Sha256
                        ? IntegrityVerdict.Ok
                        : IntegrityVerdict.Mismatch;
                default:
                    if (!facts.Exists) return IntegrityVerdict.Missing;
                    return facts.Size == Size ? IntegrityVerdict.Ok : IntegrityVerdict.Mismatch;
            }
        }
    }
}
