namespace KitchenDesigner.Core.Update
{
    public class UpdateAttemptOutcome
    {
        public bool Succeeded { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public UpdateAttemptFailure Failure { get; private set; }

        public void Succeed()
        {
            Succeeded = true;
            Reason = string.Empty;
            Failure = default;
        }

        public void Fail(string reason, UpdateAttemptFailure failure)
        {
            Succeeded = false;
            Reason = reason;
            Failure = failure;
        }
    }
}
