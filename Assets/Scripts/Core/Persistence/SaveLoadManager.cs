using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SaveLoadManager
    {
        internal static ISaveLoadManager Instance
        {
            get
            {
                if (GameContext.Services != null)
                    return GameContext.Services.SaveLoadManager;
                if (_fallback == null)
                    _fallback = new SaveLoadManagerInstance();
                return _fallback;
            }
            set => _fallback = value;
        }
        private static ISaveLoadManager? _fallback;

        public static string LastPath { get => Instance.LastPath; set => Instance.LastPath = value; }
        public static bool HasLastPath => Instance.HasLastPath;
        public static string LastDirectory => Instance.LastDirectory;
        public static string SavesDirectory =>
            ((SaveLoadManagerInstance)Instance).SavesDirectory;

        public static bool SaveToPath(string path) => Instance.SaveToPath(path);
        public static bool SaveToLastPath() => Instance.SaveToLastPath();
        public static bool LoadFromPath(string path) => Instance.LoadFromPath(path);
        public static bool LoadLastSession() => Instance.LoadLastSession();

        public static ProjectData CaptureScene(IEnumerable<KitchenElement> elements) =>
            Instance.CaptureScene(elements);
        public static string Serialize(ProjectData data) => Instance.Serialize(data);
        public static ProjectData? Deserialize(string json) => Instance.Deserialize(json);
        public static bool IsVersionCompatible(ProjectData data) =>
            Instance.IsVersionCompatible(data);
        public static List<GameObject> RestoreScene(ProjectData data) =>
            Instance.RestoreScene(data);

        public static bool SaveToFile(string path, ProjectData data) =>
            Instance.SaveToFile(path, data);
        public static ProjectData? LoadFromFile(string path) => Instance.LoadFromFile(path);

        public static string PathForName(string name) => Instance.PathForName(name);
        public static bool SaveProject(string name, bool backup = true) =>
            Instance.SaveProject(name, backup);
        public static string CaptureCurrentJson() => Instance.CaptureCurrentJson();
        public static bool LoadProject(string name) => Instance.LoadProject(name);
        public static string[] GetSaveFiles() => Instance.GetSaveFiles();
        public static void ClearBoards(System.Collections.Generic.IEnumerable<KitchenElement> elements) =>
            Instance.ClearBoards(elements);
    }
}
