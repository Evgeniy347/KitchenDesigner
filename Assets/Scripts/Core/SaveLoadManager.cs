using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

        // --- Последний выбранный пользователем файл (запоминается между сессиями) ---

        private const string LastPathKey = "KitchenLastSavePath";

        /// <summary>Полный путь к последнему сохранённому/загруженному файлу.</summary>
        public static string LastPath
        {
            get => PlayerPrefs.GetString(LastPathKey, "");
            set
            {
                PlayerPrefs.SetString(LastPathKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        public static bool HasLastPath => !string.IsNullOrEmpty(LastPath);

        /// <summary>Каталог последнего файла — для стартовой папки диалога.</summary>
        public static string LastDirectory
        {
            get
            {
                var p = LastPath;
                if (!string.IsNullOrEmpty(p))
                {
                    var dir = Path.GetDirectoryName(p);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
                }
                if (!Directory.Exists(SavesDirectory)) Directory.CreateDirectory(SavesDirectory);
                return SavesDirectory;
            }
        }

        /// <summary>Сохранить текущую сцену в произвольный файл и запомнить путь.</summary>
        public static bool SaveToPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var data = CaptureScene(FindAllElements());
            bool ok = SaveToFile(path, data);
            if (ok)
                LastPath = path;
            return ok;
        }

        /// <summary>Записать в последний выбранный файл. false, если файл ещё не выбран.</summary>
        public static bool SaveToLastPath()
        {
            return HasLastPath && SaveToPath(LastPath);
        }

        /// <summary>Загрузить сцену из произвольного файла и запомнить путь.</summary>
        public static bool LoadFromPath(string path)
        {
            var data = LoadFromFile(path);
            if (data == null) return false;

            if (!IsVersionCompatible(data))
                Debug.LogWarning($"[SaveLoad] Version mismatch: file={data.version}, app={AppConstants.SAVE_FORMAT_VERSION}");

            ClearBoardsImmediate(FindAllElements());
            RestoreScene(data);
            LastPath = path;

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
            return true;
        }

        /// <summary>Загрузка последней сессии при старте приложения: последний
        /// выбранный пользователем файл (если существует), иначе проект
        /// автосохранения. Возвращает true, если что-то загружено.</summary>
        public static bool LoadLastSession()
        {
            if (HasLastPath && File.Exists(LastPath))
                return LoadFromPath(LastPath);

            if (File.Exists(PathForName(AutoSaveManager.AutoSaveName)))
                return LoadProject(AutoSaveManager.AutoSaveName);

            return false;
        }

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
                var go = ed.isWall
                    ? ElementFactory.CreateWall(ed.Dimensions, ed.name, ed.Position)
                    : ElementFactory.CreateBoard(ed.Dimensions, ed.name, ed.Position);
                go.transform.rotation = ed.Rotation;
                var el = go.GetComponent<KitchenElement>();
                if (el != null) el.Movable = ed.movable;
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

        public static bool SaveProject(string name, bool backup = true)
        {
            var data = CaptureScene(FindAllElements());
            string path = PathForName(name);

            if (backup && File.Exists(path))
                BackupExisting(path);

            return SaveToFile(path, data);
        }

        /// <summary>JSON текущей сцены — для проверки «есть ли изменения» (автосохранение).</summary>
        public static string CaptureCurrentJson()
        {
            return Serialize(CaptureScene(FindAllElements()));
        }

        /// <summary>
        /// Существующий файл архивируется в zip (имя записи с датой), затем
        /// перезаписывается новым сохранением. Компрессия — Optimal.
        /// </summary>
        private static void BackupExisting(string path)
        {
            try
            {
                string dir = Path.Combine(SavesDirectory, "backups");
                Directory.CreateDirectory(dir);

                string baseName = Path.GetFileNameWithoutExtension(path);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string zipPath = Path.Combine(dir, $"{baseName}_{stamp}.zip");

                using (var fs = new FileStream(zipPath, FileMode.Create))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry($"{baseName}_{stamp}.json", System.IO.Compression.CompressionLevel.Optimal);
                    using (var es = entry.Open())
                    using (var src = File.OpenRead(path))
                        src.CopyTo(es);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoad] Backup failed: {ex.Message}");
            }
        }

        public static bool LoadProject(string name)
        {
            var data = LoadFromFile(PathForName(name));
            if (data == null) return false;

            if (!IsVersionCompatible(data))
                Debug.LogWarning($"[SaveLoad] Version mismatch: file={data.version}, app={AppConstants.SAVE_FORMAT_VERSION}");

            ClearBoardsImmediate(FindAllElements());
            RestoreScene(data);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
            return true;
        }

        private static void ClearBoardsImmediate(IEnumerable<KitchenElement> elements)
        {
            foreach (var e in elements)
            {
                if (e == null) continue;
                if (e.GetComponent<BasePlate>() != null) continue;
                UnityEngine.Object.DestroyImmediate(e.gameObject);
            }
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
            return BoardRegistry.GetAll();
        }

        private static void DestroyElement(GameObject go)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
