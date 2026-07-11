using System.IO;

namespace KitchenDesigner.Core
{
    public sealed class DesktopFileStorage : IFileStorage
    {
        public string ReadAllText(string path) => File.ReadAllText(path);
        public void WriteAllText(string path, string content) => File.WriteAllText(path, content);
        public bool Exists(string path) => File.Exists(path);
        public void Delete(string path) => File.Delete(path);
        public string[] GetFiles(string directory, string pattern) => Directory.GetFiles(directory, pattern);
    }
}
