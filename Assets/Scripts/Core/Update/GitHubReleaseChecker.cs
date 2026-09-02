using System;
using UnityEngine;
using UnityEngine.Networking;

namespace KitchenDesigner.Core.Update
{
    public sealed class GitHubReleaseChecker : MonoBehaviour, IUpdateChecker
    {
        public const string ApiUrl =
            "https://api.github.com/repos/Evgeniy347/KitchenDesigner/releases/latest";

        [SerializeField] private int _timeoutSeconds = 10;

        private sealed class CheckOutcome : UpdateAttemptOutcome
        {
            public ReleaseManifest? Manifest;
        }

        public void Check(Action<ReleaseManifest> onSuccess, Action<string> onFailure)
        {
            StartCoroutine(CheckRoutine(onSuccess, onFailure));
        }

        private System.Collections.IEnumerator CheckRoutine(
            Action<ReleaseManifest> onSuccess, Action<string> onFailure)
        {
            var policy = UpdateRetryPolicy.ForReleaseCheck();
            var outcome = new CheckOutcome();

            for (int attempt = 1; attempt <= policy.MaxAttempts; attempt++)
            {
                float pause = policy.PauseBeforeAttemptSeconds(attempt);
                if (pause > 0f) yield return new WaitForSecondsRealtime(pause);

                yield return SendOnce(outcome);

                if (outcome.Succeeded)
                {
                    onSuccess?.Invoke(outcome.Manifest!);
                    yield break;
                }
                if (!policy.ShouldRetryAfter(attempt, outcome.Failure)) break;
            }

            onFailure?.Invoke(outcome.Reason);
        }

        private System.Collections.IEnumerator SendOnce(CheckOutcome outcome)
        {
            outcome.Manifest = null;

            var req = UnityWebRequest.Get(ApiUrl);
            req.SetRequestHeader("User-Agent", "KitchenDesigner-Updater");
            req.SetRequestHeader("Accept", "application/vnd.github+json");
            req.timeout = _timeoutSeconds;
            yield return req.SendWebRequest();

            var result = req.result;
            long code = req.responseCode;
            string error = req.error;
            string body = result == UnityWebRequest.Result.Success ? req.downloadHandler.text : string.Empty;
            req.Dispose();

            if (result != UnityWebRequest.Result.Success)
            {
                outcome.Fail($"HTTP {code}: {error}", UpdateAttemptFailure.FromResponse(code));
                yield break;
            }
            if (!ReleaseManifestParser.TryParse(body, out var manifest, out var parseError))
            {
                outcome.Fail(parseError, UpdateAttemptFailure.FromResponse(code));
                yield break;
            }
            outcome.Manifest = manifest;
            outcome.Succeed();
        }
    }
}
