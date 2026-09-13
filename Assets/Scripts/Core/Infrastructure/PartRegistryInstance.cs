using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class PartRegistryInstance : IPartRegistry
    {
        private readonly List<KitchenElement> _all = new List<KitchenElement>();
        private readonly List<Wall> _walls = new List<Wall>();

        private static int _getAllCalls;

        public static int TakeGetAllCalls()
        {
            int n = _getAllCalls;
            _getAllCalls = 0;
            return n;
        }

        public IReadOnlyList<KitchenElement> All
        {
            get
            {
                NoteTheListWasSharedWithoutAName();
                PurgeDead();
                return _all;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("KD_PERF")]
        private static void NoteTheListWasSharedWithoutAName()
        {
            SceneScanLog.NoteShare();
            SceneScanCounter.NoteShare();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("KD_PERF")]
        private static void NoteTheScanAndWhoAskedForIt(string? scannedBy, string? scannedIn)
        {
            string where = SceneScanLog.Where(scannedBy, scannedIn);
            SceneScanLog.NoteWhere(where);
            SceneScanCounter.Note(where);
        }

        public void Register(KitchenElement element)
        {
            PurgeDead();
            if (element == null || _all.Contains(element)) return;
            _all.Add(element);
            RegisterWall(element.GetComponent<Wall>());
            SceneChangeTracker.NoteMembershipChanged();
            GroupMembershipRevision.Bump();
            SceneRevision.Bump();
            SceneVisibilityManager.Invalidate();
        }

        public void Unregister(KitchenElement element)
        {
            if (!_all.Remove(element)) return;
            if (element != null) UnregisterWall(element.GetComponent<Wall>());
            SceneChangeTracker.NoteMembershipChanged();
            GroupMembershipRevision.Bump();
            SceneRevision.Bump();
            SceneVisibilityManager.Invalidate();
        }

        public List<KitchenElement> GetAll(
            [System.Runtime.CompilerServices.CallerMemberName] string? scannedBy = null,
            [System.Runtime.CompilerServices.CallerFilePath] string? scannedIn = null)
        {
            using var _ = PerfMarkers.PartRegistryGetAll.Auto();
            _getAllCalls++;
            NoteTheScanAndWhoAskedForIt(scannedBy, scannedIn);
            PurgeDead();
            return new List<KitchenElement>(_all);
        }

        public void Clear()
        {
            _walls.Clear();
            if (_all.Count == 0) return;
            _all.Clear();
            SceneChangeTracker.NoteMembershipChanged();
            GroupMembershipRevision.Bump();
            SceneRevision.Bump();
        }

        public IReadOnlyList<Wall> Walls
        {
            get
            {
                _walls.RemoveAll(w => w == null);
                return _walls;
            }
        }

        public void RegisterWall(Wall wall)
        {
            if (wall == null || _walls.Contains(wall)) return;
            _walls.Add(wall);
        }

        public void UnregisterWall(Wall wall)
        {
            if (wall == null) return;
            _walls.Remove(wall);
        }

        private void PurgeDead()
        {
            int removed = _all.RemoveAll(e => e == null);
            if (removed == 0) return;
            SceneChangeTracker.NoteMembershipChanged();
            GroupMembershipRevision.Bump();
            SceneRevision.Bump();
            SceneVisibilityManager.Invalidate();
        }
    }
}
