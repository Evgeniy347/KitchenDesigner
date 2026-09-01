using System.Collections.Generic;
using System.IO;
using KitchenDesigner.Core.UI;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SaveLoadManagerInstance : ISaveLoadManager
    {
        private readonly ProjectFileStore _files = new ProjectFileStore();

        public string SavesDirectory => _files.SavesDirectory;

        public string LastPath
        {
            get => _files.LastPath;
            set => _files.LastPath = value;
        }

        public bool HasLastPath => _files.HasLastPath;

        public string LastDirectory => _files.LastDirectory;

        public string PathForName(string name) => _files.PathForName(name);

        public string[] GetSaveFiles() => _files.SaveNames();

        public bool SaveToFile(string path, ProjectData data) => _files.Write(path, data);

        public ProjectData? LoadFromFile(string path) => _files.Read(path);

        public string Serialize(ProjectData data) => ProjectJson.Serialize(data);

        public ProjectData? Deserialize(string json) => ProjectJson.Deserialize(json);

        public bool IsVersionCompatible(ProjectData data) => ProjectJson.IsVersionCompatible(data);

        public ProjectData CaptureScene(IEnumerable<KitchenElement> elements) =>
            SceneCapture.Capture(elements);

        public List<GameObject> RestoreScene(ProjectData data) => SceneRestorer.Restore(data);

        public void ClearBoards(IEnumerable<KitchenElement> elements) =>
            SceneElements.ClearKeepingBasePlate(elements);

        public string CaptureCurrentJson() => Serialize(CaptureCurrentScene());

        public bool SaveToPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            bool ok = _files.Write(path, CaptureCurrentScene());
            if (ok) LastPath = path;
            return ok;
        }

        public bool SaveToLastPath() => HasLastPath && SaveToPath(LastPath);

        public bool SaveProject(string name, bool backup = true)
        {
            var data = CaptureCurrentScene();
            string path = PathForName(name);
            if (backup && File.Exists(path))
                _files.ArchiveExisting(path);
            return _files.Write(path, data);
        }

        public bool LoadFromPath(string path)
        {
            if (!ReplaceSceneWithFile(path)) return false;
            LastPath = path;
            return true;
        }

        public bool LoadProject(string name) => ReplaceSceneWithFile(PathForName(name));

        public bool LoadLastSession()
        {
            if (HasLastPath && File.Exists(LastPath))
                return LoadFromPath(LastPath);

            if (File.Exists(PathForName(AutoSaveManager.AutoSaveName)))
                return LoadProject(AutoSaveManager.AutoSaveName);

            return false;
        }

        private ProjectData CaptureCurrentScene() => SceneCapture.Capture(SceneElements.All());

        private bool ReplaceSceneWithFile(string path)
        {
            var data = _files.Read(path);
            if (data == null) return false;

            using var batch = HighlightBatch.Open();

            if (!IsVersionCompatible(data))
                Debug.LogWarning($"[SaveLoad] Version mismatch: file={data.version}, app={AppConstants.SAVE_FORMAT_VERSION}");

            SceneElements.ClearKeepingBasePlate(SceneElements.All());
            SceneRestorer.Restore(data);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
            return true;
        }
    }
}
