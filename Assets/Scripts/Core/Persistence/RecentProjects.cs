namespace KitchenDesigner.Core
{
    public static class RecentProjects
    {
        public static void Remember(string path) =>
            RecentProjectsMemory.Remember(System.Environment.GetCommandLineArgs(), path);

        public static string[] Paths() =>
            RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values;
    }
}
