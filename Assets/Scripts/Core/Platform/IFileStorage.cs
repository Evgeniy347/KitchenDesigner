namespace KitchenDesigner.Core
{
    public interface IFileStorage
    {
        string ReadAllText(string path);
        void WriteAllText(string path, string content);
        bool Exists(string path);
        void Delete(string path);
        string[] GetFiles(string directory, string pattern);
    }
}
