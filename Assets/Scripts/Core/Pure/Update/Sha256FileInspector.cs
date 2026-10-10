using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace KitchenDesigner.Core.Update
{
    public sealed class Sha256FileInspector : IFileInspector
    {
        private readonly IUpdateFolder _folder;
        private readonly IMainThread _mainThread;

        public Sha256FileInspector(IUpdateFolder folder, IMainThread mainThread)
        {
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
            _mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
        }

        public void Inspect(string fileName, bool computeSha256, Action<FileFacts> done)
        {
            var path = _folder.PathOf(fileName);
            Task.Run(() => Examine(path, computeSha256))
                .ContinueWith(t => _mainThread.Post(() => done(t.IsFaulted ? Unreadable() : t.Result)),
                    TaskScheduler.Default);
        }

        public static FileFacts Examine(string path, bool computeSha256)
        {
            FileInfo info;
            try
            {
                info = new FileInfo(path);
                if (!info.Exists) return FileFacts.Absent;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                return Unreadable();
            }

            if (!computeSha256) return new FileFacts(true, info.Length, null);

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sha = SHA256.Create();
                return new FileFacts(true, info.Length, Sha256Digest.ToHex(sha.ComputeHash(stream)));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return new FileFacts(true, info.Length, null);
            }
        }

        private static FileFacts Unreadable() => new FileFacts(true, 0, null);
    }
}
