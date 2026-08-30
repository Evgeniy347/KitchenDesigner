using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal class ProjectFileStore
    {
        private const string LastPathKey = "KitchenLastSavePath";

        public string SavesDirectory =>
            Path.Combine(Application.persistentDataPath, "saves");

        public string LastPath
        {
            get => PlayerPrefs.GetString(LastPathKey, "");
            set
            {
                PlayerPrefs.SetString(LastPathKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        public bool HasLastPath => !string.IsNullOrEmpty(LastPath);

        public string LastDirectory
        {
            get
            {
                var lastPath = LastPath;
                if (!string.IsNullOrEmpty(lastPath))
                {
                    var dir = Path.GetDirectoryName(lastPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
                }
                if (!Directory.Exists(SavesDirectory)) Directory.CreateDirectory(SavesDirectory);
                return SavesDirectory;
            }
        }

        public string PathForName(string name) =>
            Path.Combine(SavesDirectory, name + ".json");

        public bool Write(string path, ProjectData data)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, ProjectJson.Serialize(data));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoad] Save failed: {ex.Message}");
                return false;
            }
        }

        public ProjectData? Read(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[SaveLoad] File not found: {path}");
                return null;
            }
            try
            {
                return ProjectJson.Deserialize(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoad] Load failed: {ex.Message}");
                return null;
            }
        }

        public string[] SaveNames()
        {
            if (!Directory.Exists(SavesDirectory)) return new string[0];
            var files = Directory.GetFiles(SavesDirectory, "*.json");
            var names = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
                names[i] = Path.GetFileNameWithoutExtension(files[i]);
            return names;
        }

        public void ArchiveExisting(string path)
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
    }
}
