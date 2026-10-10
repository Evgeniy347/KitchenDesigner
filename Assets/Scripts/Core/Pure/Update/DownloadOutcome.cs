namespace KitchenDesigner.Core.Update
{
    public readonly struct DownloadOutcome
    {
        private DownloadOutcome(bool succeeded, string reason)
        {
            Succeeded = succeeded;
            Reason = reason;
        }

        public bool Succeeded { get; }
        public string Reason { get; }

        public static DownloadOutcome Success() => new DownloadOutcome(true, string.Empty);

        public static DownloadOutcome Failure(string reason) => new DownloadOutcome(false, reason);
    }
}
