using System;

namespace KitchenDesigner.Core.Update
{
    public readonly struct UpdateAttemptFailure
    {
        public const long NoHttpStatus = 0;
        public const long RequestTimeoutStatus = 408;
        public const long TooManyRequestsStatus = 429;
        public const long FirstServerErrorStatus = 500;

        private UpdateAttemptFailure(bool cancelledByUser, long httpStatus)
        {
            CancelledByUser = cancelledByUser;
            HttpStatus = httpStatus;
        }

        public bool CancelledByUser { get; }
        public long HttpStatus { get; }

        public static UpdateAttemptFailure Cancelled() => new UpdateAttemptFailure(true, NoHttpStatus);

        public static UpdateAttemptFailure NoAnswer() => new UpdateAttemptFailure(false, NoHttpStatus);

        public static UpdateAttemptFailure FromResponse(long httpStatus) =>
            new UpdateAttemptFailure(false, httpStatus);
    }

    public sealed class UpdateRetryPolicy
    {
        private readonly float[] _pauseBeforeRetrySeconds;

        public UpdateRetryPolicy(params float[] pauseBeforeRetrySeconds)
        {
            _pauseBeforeRetrySeconds = pauseBeforeRetrySeconds;
            float previous = 0f;
            foreach (float pause in _pauseBeforeRetrySeconds)
            {
                if (pause <= previous)
                    throw new ArgumentException(
                        "паузы между попытками обязаны нарастать: " + pause + " после " + previous,
                        nameof(pauseBeforeRetrySeconds));
                previous = pause;
            }
        }

        public static UpdateRetryPolicy ForReleaseCheck() => new UpdateRetryPolicy(1f, 2f);

        public static UpdateRetryPolicy ForInstallerDownload() => new UpdateRetryPolicy(2f, 5f);

        public int MaxAttempts => _pauseBeforeRetrySeconds.Length + 1;

        public float PauseBeforeAttemptSeconds(int attemptNumber) =>
            attemptNumber <= 1 || attemptNumber > MaxAttempts
                ? 0f
                : _pauseBeforeRetrySeconds[attemptNumber - 2];

        public bool AttemptsExhausted(int attemptNumber) => attemptNumber >= MaxAttempts;

        public bool IsWorthRetrying(UpdateAttemptFailure failure)
        {
            if (failure.CancelledByUser) return false;
            if (failure.HttpStatus == UpdateAttemptFailure.NoHttpStatus) return true;
            if (failure.HttpStatus >= UpdateAttemptFailure.FirstServerErrorStatus) return true;
            return failure.HttpStatus == UpdateAttemptFailure.RequestTimeoutStatus
                || failure.HttpStatus == UpdateAttemptFailure.TooManyRequestsStatus;
        }

        public bool ShouldRetryAfter(int attemptNumber, UpdateAttemptFailure failure) =>
            !AttemptsExhausted(attemptNumber) && IsWorthRetrying(failure);
    }
}
