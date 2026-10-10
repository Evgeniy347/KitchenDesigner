using System;
using System.Collections.Generic;
using System.IO;

namespace KitchenDesigner.Core.Update
{
    public sealed class FileSystemUpdateFolder : IUpdateFolder
    {
        private readonly string _root;

        public FileSystemUpdateFolder(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("root", nameof(root));
            _root = Path.GetFullPath(root);
        }

        public string Root => _root;

        public string PathOf(string name) => Path.Combine(_root, name);

        public void EnsureExists() => Directory.CreateDirectory(_root);

        public IReadOnlyList<FolderEntry> List()
        {
            var entries = new List<FolderEntry>();
            if (!Directory.Exists(_root)) return entries;
            foreach (var file in new DirectoryInfo(_root).GetFiles())
                entries.Add(new FolderEntry(file.Name, file.Length));
            return entries;
        }

        public bool TryDelete(string name, out string error)
        {
            error = string.Empty;
            if (!InstallerFileName.IsOurs(name))
            {
                error = UpdateMessages.NotInUpdatesFolder(name);
                return false;
            }

            try
            {
                var path = PathOf(name);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        public bool TryPromote(string partName, string finalName, out string error)
        {
            error = string.Empty;
            if (!InstallerFileName.TryParsePart(partName, out var partVersion))
            {
                error = UpdateMessages.NotInUpdatesFolder(partName);
                return false;
            }
            if (!InstallerFileName.TryParseInstaller(finalName, out var finalVersion))
            {
                error = UpdateMessages.NotInUpdatesFolder(finalName);
                return false;
            }
            if (partVersion != finalVersion)
            {
                error = UpdateMessages.PromoteNamesDiffer(partName, finalName);
                return false;
            }

            var partPath = PathOf(partName);
            if (!File.Exists(partPath))
            {
                error = UpdateMessages.PartMissing(partName);
                return false;
            }

            try
            {
                var finalPath = PathOf(finalName);
                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(partPath, finalPath);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
