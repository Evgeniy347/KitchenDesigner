#if !UNITY_WEBGL
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace KitchenDesigner.Core.Update
{
    public sealed class GitHubReleaseChecker : MonoBehaviour, IUpdateChecker
    {
        public const string ApiUrl =
            "https://api.github.com/repos/Evgeniy347/KitchenDesigner/releases/latest";

        [SerializeField] private int _timeoutSeconds = 12;

        public void Check(Action<ReleaseManifest> onSuccess, Action<string> onFailure)
        {
            StartCoroutine(CheckRoutine(onSuccess, onFailure));
        }

        private System.Collections.IEnumerator CheckRoutine(
            Action<ReleaseManifest> onSuccess, Action<string> onFailure)
        {
            var req = UnityWebRequest.Get(ApiUrl);
            req.SetRequestHeader("User-Agent", "KitchenDesigner-Updater");
            req.SetRequestHeader("Accept", "application/vnd.github+json");
            req.timeout = _timeoutSeconds;
            yield return req.SendWebRequest();

            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onFailure?.Invoke($"HTTP {req.responseCode}: {req.error}");
                    yield break;
                }
                if (!ReleaseManifestParser.TryParse(req.downloadHandler.text,
                        out var manifest, out var error))
                {
                    onFailure?.Invoke(error);
                    yield break;
                }
                onSuccess?.Invoke(manifest!);
            }
            finally
            {
                req.Dispose();
            }
        }
    }
}
#endif
