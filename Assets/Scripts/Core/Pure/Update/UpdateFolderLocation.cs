using System.IO;

namespace KitchenDesigner.Core.Update
{
    public static class UpdateFolderLocation
    {
        public const string AppFolderName = "KitchenDesigner";
        public const string UpdatesFolderName = "Updates";

        public static string RootUnder(string tempPath) =>
            Path.Combine(tempPath, AppFolderName, UpdatesFolderName);
    }
}
