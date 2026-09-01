#if UNITY_WEBGL
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace KitchenDesigner.Core.Networking
{
    [Serializable]
    public class ServerProjectInfo
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public string createdAt = string.Empty;
        public string updatedAt = string.Empty;
        public int version;
    }

    [Serializable]
    public class ServerProjectDetail
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public string jsonData = string.Empty;
    }

    [Serializable]
    internal class ProjectListWrapper { public List<ServerProjectInfo> items = new(); }

    [Serializable]
    internal class ServerConfigResponse { public bool serverSaveEnabled; }

    [Serializable]
    internal class ServerErrorResponse { public string error = string.Empty; }

    public class ProjectApiClient : MonoBehaviour
    {
        public static ProjectApiClient? Instance { get; private set; }

        public static bool Enabled { get; private set; }

        private const string BasePath = "/api/projects";
        private const string ProjectIdKey = "KitchenCurrentProjectId";
        private const string ProjectNameKey = "KitchenCurrentProjectName";

        public string CurrentProjectId
        {
            get => PlayerPrefs.GetString(ProjectIdKey, "");
            internal set
            {
                PlayerPrefs.SetString(ProjectIdKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        public string CurrentProjectName
        {
            get => PlayerPrefs.GetString(ProjectNameKey, "");
            private set
            {
                PlayerPrefs.SetString(ProjectNameKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        public bool HasCurrentProject => !string.IsNullOrEmpty(CurrentProjectId);

        public string LockGuid
        {
            get => PlayerPrefs.GetString("KitchenLockGuid", "");
            internal set
            {
                PlayerPrefs.SetString("KitchenLockGuid", value ?? "");
                PlayerPrefs.Save();
            }
        }

        public bool HasLock => !string.IsNullOrEmpty(LockGuid);

        private void Awake()
        {
            Instance = this;
        }

        public void FetchConfig(Action<bool> onComplete)
        {
            StartCoroutine(FetchConfigRoutine(onComplete));
        }

        private IEnumerator FetchConfigRoutine(Action<bool> onComplete)
        {
            using (var req = UnityWebRequest.Get("/api/config"))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Enabled = ServerSaveEnabledFrom(req.downloadHandler.text);
                }
                else
                {
                    Enabled = false;
                }
                onComplete?.Invoke(Enabled);
            }
        }

        internal static bool ServerSaveEnabledFrom(string configJson)
        {
            var cfg = JsonUtility.FromJson<ServerConfigResponse>(configJson);
            return cfg != null && cfg.serverSaveEnabled;
        }

        public void FetchList(Action<List<ServerProjectInfo>> onSuccess, Action<string> onError)
        {
            StartCoroutine(FetchListRoutine(onSuccess, onError));
        }

        private IEnumerator FetchListRoutine(Action<List<ServerProjectInfo>> onSuccess, Action<string> onError)
        {
            using (var req = UnityWebRequest.Get(BasePath + "/"))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(req.responseCode == 401 ? "Требуется вход" : req.error);
                    yield break;
                }

                var json = "{\"items\":" + req.downloadHandler.text + "}";
                var wrapper = JsonUtility.FromJson<ProjectListWrapper>(json);
                onSuccess?.Invoke(wrapper?.items ?? new List<ServerProjectInfo>());
            }
        }

        public void LoadProject(string projectId, Action<string, string> onSuccess, Action<string> onError)
        {
            StartCoroutine(LoadProjectRoutine(projectId, onSuccess, onError));
        }

        private IEnumerator LoadProjectRoutine(string projectId, Action<string, string> onSuccess, Action<string> onError)
        {
            using (var req = UnityWebRequest.Get(BasePath + "/" + projectId))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(req.responseCode == 401 ? "Требуется вход" : req.error);
                    yield break;
                }

                var detail = JsonUtility.FromJson<ServerProjectDetail>(req.downloadHandler.text);
                if (detail == null || string.IsNullOrEmpty(detail.jsonData))
                {
                    onError?.Invoke("Пустой проект");
                    yield break;
                }

                CurrentProjectId = detail.id;
                CurrentProjectName = detail.name ?? "Без имени";
                onSuccess?.Invoke(detail.name ?? "", detail.jsonData ?? "");
            }
        }

        public void SaveCurrent(string jsonData, Action onSuccess, Action<string> onError)
        {
            if (!HasCurrentProject)
            {
                onError?.Invoke("Нет текущего проекта");
                return;
            }
            StartCoroutine(SaveCurrentRoutine(jsonData, onSuccess, onError));
        }

        private IEnumerator SaveCurrentRoutine(string jsonData, Action onSuccess, Action<string> onError)
        {
            var body = new UpdateRequestBody { jsonData = jsonData, lockGuid = LockGuid };
            var bodyJson = JsonUtility.ToJson(body);
            using (var req = UnityWebRequest.Put(BasePath + "/" + CurrentProjectId, bodyJson))
            {
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    var errorText = ParseServerError(req.downloadHandler.text);
                    onError?.Invoke(!string.IsNullOrEmpty(errorText) ? errorText
                        : req.responseCode == 401 ? "Требуется вход" : req.error);
                    yield break;
                }
                onSuccess?.Invoke();
            }
        }

        public void CreateAndSave(string name, string jsonData, Action<string> onSuccess, Action<string> onError)
        {
            StartCoroutine(CreateAndSaveRoutine(name, jsonData, onSuccess, onError));
        }

        private IEnumerator CreateAndSaveRoutine(string name, string jsonData, Action<string> onSuccess, Action<string> onError)
        {
            var createBody = new CreateRequestBody { name = name };
            var createJson = JsonUtility.ToJson(createBody);

            using (var req = new UnityWebRequest(BasePath + "/", "POST"))
            {
                var uploadBytes = System.Text.Encoding.UTF8.GetBytes(createJson);
                req.uploadHandler = new UploadHandlerRaw(uploadBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    var errorText = ParseServerError(req.downloadHandler.text);
                    onError?.Invoke(!string.IsNullOrEmpty(errorText) ? errorText
                        : req.responseCode == 401 ? "Требуется вход" : req.error);
                    yield break;
                }

                var created = JsonUtility.FromJson<ServerProjectInfo>(req.downloadHandler.text);
                if (created == null || string.IsNullOrEmpty(created.id))
                {
                    onError?.Invoke("Неожиданный ответ сервера при создании проекта");
                    yield break;
                }
                CurrentProjectId = created.id;
                CurrentProjectName = created.name;
            }

            var updateBody = new UpdateRequestBody { jsonData = jsonData };
            var updateJson = JsonUtility.ToJson(updateBody);
            using (var req = UnityWebRequest.Put(BasePath + "/" + CurrentProjectId, updateJson))
            {
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    var errorText = ParseServerError(req.downloadHandler.text);
                    onError?.Invoke(!string.IsNullOrEmpty(errorText) ? errorText
                        : req.responseCode == 401 ? "Требуется вход" : req.error);
                    yield break;
                }
            }

            onSuccess?.Invoke(CurrentProjectId);
        }

        public void LoadLastProject(Action<string, string> onSuccess, Action<string> onError)
        {
            if (!HasCurrentProject)
            {
                onError?.Invoke("Нет сохранённого проекта");
                return;
            }
            LoadProject(CurrentProjectId, onSuccess, onError);
        }

        [Serializable]
        private class CreateRequestBody { public string name = string.Empty; }

        [Serializable]
        private class UpdateRequestBody { public string jsonData = string.Empty; public string lockGuid = string.Empty; }

        private static string ParseServerError(string body)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            try
            {
                var errorObj = JsonUtility.FromJson<ServerErrorResponse>(body);
                if (errorObj != null && !string.IsNullOrEmpty(errorObj.error))
                    return errorObj.error;
            }
            catch { }
            return body;
        }
    }
}
#endif
