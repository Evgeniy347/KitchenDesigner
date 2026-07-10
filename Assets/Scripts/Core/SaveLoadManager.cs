using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Сохранение/загрузка проекта в JSON. Логика разделена на чистые части
    /// (Capture/Serialize/Deserialize/Restore — тестируются без файлов) и файловый IO.
    /// </summary>
    public static class SaveLoadManager
    {
        public static string SavesDirectory =>
            Path.Combine(Application.persistentDataPath, "saves");

        // --- Чистая логика (тестируемая без файлов) ---

        /// <summary>Снимок сцены в ProjectData. BasePlate исключается (это пол, не доска).</summary>
        public static ProjectData CaptureScene(IEnumerable<KitchenElement> elements)
        {
            var items = new List<ElementData>();
            foreach (var e in elements)
            {
                if (e == null) continue;
                if (e.GetComponent<BasePlate>() != null) continue;
                items.Add(ElementData.FromElement(e));
            }
            return new ProjectData(items);
        }

        public static string Serialize(ProjectData data) => JsonUtility.ToJson(data, true);

        public static ProjectData Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<ProjectData>(json);
        }

        /// <summary>Совпадает ли версия формата с текущей.</summary>
        public static bool IsVersionCompatible(ProjectData data) =>
            data != null && data.version == AppConstants.SAVE_FORMAT_VERSION;

        /// <summary>Удаляет все доски сцены, кроме BasePlate.</summary>
        public static void ClearBoards(IEnumerable<KitchenElement> elements)
        {
            foreach (var e in elements)
            {
                if (e == null) continue;
                if (e.GetComponent<BasePlate>() != null) continue;
                DestroyElement(e.gameObject);
            }
        }

        /// <summary>Создаёт доски из ProjectData через ElementFactory.</summary>
        public static List<GameObject> RestoreScene(ProjectData data)
        {
            var created = new List<GameObject>();
            if (data == null || data.elements == null) return created;

            foreach (var ed in data.elements)
            {
                if (ed == null) continue;
                var go = ElementFactory.CreateBoard(ed.Dimensions, ed.name, ed.Position);
                go.transform.rotation = ed.Rotation;
                created.Add(go);
            }
            return created;
        }

        // --- Файловый IO ---

        public static bool SaveToFile(string path, ProjectData data)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, Serialize(data));
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveLoad] Save failed: {ex.Message}");
                return false;
            }
        }

        public static ProjectData LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[SaveLoad] File not found: {path}");
                return null;
            }
            try
            {
                return Deserialize(File.ReadAllText(path));
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveLoad] Load failed: {ex.Message}");
                return null;
            }
        }

        // --- Высокоуровневое API (имя проекта → файл в SavesDirectory) ---

        public static string PathForName(string name) =>
            Path.Combine(SavesDirectory, name + ".json");

        public static bool SaveProject(string name)
        {
            var data = CaptureScene(FindAllElements());
            return SaveToFile(PathForName(name), data);
        }

        public static bool LoadProject(string name)
        {
            var data = LoadFromFile(PathForName(name));
            if (data == null) return false;

            if (!IsVersionCompatible(data))
                Debug.LogWarning($"[SaveLoad] Version mismatch: file={data.version}, app={AppConstants.SAVE_FORMAT_VERSION}");

            ClearBoards(FindAllElements());
            RestoreScene(data);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
            return true;
        }

        public static string[] GetSaveFiles()
        {
            if (!Directory.Exists(SavesDirectory)) return new string[0];
            var files = Directory.GetFiles(SavesDirectory, "*.json");
            var names = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
                names[i] = Path.GetFileNameWithoutExtension(files[i]);
            return names;
        }

        private static IEnumerable<KitchenElement> FindAllElements()
        {
            return Object.FindObjectsByType<KitchenElement>();
        }

        private static void DestroyElement(GameObject go)
        {
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
    }
}
