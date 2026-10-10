using System;
using UnityEngine;
using UnityEngine.Networking;

namespace KitchenDesigner.Core.Update
{
    public sealed class GitHubReleaseChecker : MonoBehaviour, IReleaseSource
    {
        public const string ApiUrl =
            "https://api.github.com/repos/Evgeniy347/KitchenDesigner/releases/latest";

        [SerializeField] private int _timeoutSeconds = 10;

        private sealed class CheckOutcome : UpdateAttemptOutcome
        {
            public ReleaseLookup? Lookup;
        }

        public void Fetch(Action<ReleaseLookup> done)
        {
            StartCoroutine(FetchRoutine(done));
        }

        private System.Collections.IEnumerator FetchRoutine(Action<ReleaseLookup> done)
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
                    done?.Invoke(outcome.Lookup!);
                    yield break;
                }
                if (!policy.ShouldRetryAfter(attempt, outcome.Failure)) break;
            }

            done?.Invoke(ReleaseLookup.Failed(outcome.Reason));
        }

        private System.Collections.IEnumerator SendOnce(CheckOutcome outcome)
        {
            outcome.Lookup = null;

            var req = UnityWebRequest.Get(ApiUrl);
            req.SetRequestHeader("User-Agent", HttpInstallerDownloader.UserAgent);
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

            outcome.Lookup = ReleaseManifestParser.Parse(body);
            outcome.Succeed();
        }
    }
}
