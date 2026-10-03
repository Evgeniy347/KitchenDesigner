using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KitchenDesigner.Core
{
    internal sealed class FolderPublisherStore : IPublisherStore
    {
        private readonly string _oldCompanyDir;
        private readonly string _oldDir;
        private readonly string _newCompanyDir;
        private readonly string _newDir;

        public FolderPublisherStore(string root, string oldCompany, string newCompany, string product)
        {
            _oldCompanyDir = Path.Combine(root, oldCompany);
            _oldDir = Path.Combine(_oldCompanyDir, product);
            _newCompanyDir = Path.Combine(root, newCompany);
            _newDir = Path.Combine(_newCompanyDir, product);
        }

        public string Describe => "folder " + _oldDir + " → " + _newDir;

        public bool OldHoldsData() => HoldsData(_oldDir);

        public bool NewHoldsData() => HoldsData(_newDir);

        public void Move()
        {
            if (Directory.Exists(_newDir)) MoveEntriesBesideTheEngineFiles();
            else
            {
                Directory.CreateDirectory(_newCompanyDir);
                Directory.Move(_oldDir, _newDir);
            }
            DeleteIfEmpty(_oldDir);
            DeleteIfEmpty(_oldCompanyDir);
        }

        private void MoveEntriesBesideTheEngineFiles()
        {
            var moved = new List<(string from, string to)>();
            try
            {
                foreach (var from in OwnEntries(_oldDir))
                {
                    var to = Path.Combine(_newDir, Path.GetFileName(from));
                    Relocate(from, to);
                    moved.Add((from, to));
                }
            }
            catch (Exception moveFailure)
            {
                try
                {
                    for (int i = moved.Count - 1; i >= 0; i--) Relocate(moved[i].to, moved[i].from);
                }
                catch (Exception rollBackFailure)
                {
                    throw new IOException(moveFailure.Message + "; moving back failed too: " + rollBackFailure.Message, moveFailure);
                }
                throw;
            }
        }

        private static void Relocate(string from, string to)
        {
            if (Directory.Exists(from)) Directory.Move(from, to);
            else File.Move(from, to);
        }

        private static bool HoldsData(string dir) => Directory.Exists(dir) && OwnEntries(dir).Any();

        private static IEnumerable<string> OwnEntries(string dir) =>
            Directory.EnumerateFileSystemEntries(dir)
                .Where(e => !EngineOwnedEntries.IsEngineFile(Path.GetFileName(e)))
                .ToList();

        private static void DeleteIfEmpty(string dir)
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
        }
    }
}
