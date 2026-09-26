namespace KitchenDesigner.Core
{
    public static class RecentProjects
    {
        public static void Remember(string path) =>
            RecentProjectsMemory.Remember(System.Environment.GetCommandLineArgs(), path);

        public static string[] Paths()
        {
            var stored = RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values;
            if (stored.Length == 0 && SaveLoadManager.HasLastPath)
                return new[] { SaveLoadManager.LastPath };
            return stored;
        }
    }
}
