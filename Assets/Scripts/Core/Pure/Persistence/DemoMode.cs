using System;

namespace KitchenDesigner.Core
{
    public sealed class DemoMode
    {
        public static DemoMode Current { get; private set; } = new DemoMode();

        public static void ResetCurrent() => Current = new DemoMode();

        public bool IsActive { get; private set; }

        public string DemoPath { get; private set; } = string.Empty;

        public bool MutationsAllowed => !IsActive;

        public bool AutoSaveAllowed => !IsActive;

        public static bool ShouldOpenDemo(bool firstRunRecorded, bool hasOwnProject, bool demoFileExists) =>
            !firstRunRecorded && !hasOwnProject && demoFileExists;

        public void Enter(string demoPath)
        {
            if (string.IsNullOrEmpty(demoPath))
                throw new ArgumentException(
                    "Демо-режим без пути к демо-проекту не снимается сохранением", nameof(demoPath));
            IsActive = true;
            DemoPath = demoPath;
        }

        public bool IsDemoFile(string path) => IsActive && SamePath(path, DemoPath);

        public void ProjectSavedTo(string path) => LeaveWhenThatIsAnotherFile(path);

        public void ProjectLoadedFrom(string path) => LeaveWhenThatIsAnotherFile(path);

        private void LeaveWhenThatIsAnotherFile(string path)
        {
            if (!IsActive || string.IsNullOrEmpty(path)) return;
            if (SamePath(path, DemoPath)) return;
            IsActive = false;
            DemoPath = string.Empty;
        }

        public static bool SamePath(string a, string b) =>
            string.Equals(Normalized(a), Normalized(b), StringComparison.OrdinalIgnoreCase);

        private static string Normalized(string path) =>
            (path ?? string.Empty).Replace('/', '\\').Trim().TrimEnd('\\');
    }
}
