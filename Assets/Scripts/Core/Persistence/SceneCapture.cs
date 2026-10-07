using System.Collections.Generic;
using KitchenDesigner.Core.UI;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class SceneCapture
    {
        public static ProjectData Capture(IEnumerable<KitchenElement> elements)
        {
            var items = new List<ElementData>();
            var ordered = new List<KitchenElement>();
            foreach (var e in elements)
            {
                if (e == null || e.GetComponent<BasePlate>() != null) continue;
                items.Add(ElementCapture.FromElement(e));
                ordered.Add(e);
            }

            var data = new ProjectData(items);
            data.appVersion = BuildInfo.Version;
            data.createdAtUtc = ProjectCreationDate.Value;
            data.groups = CaptureGroups();
            data.rooms = new List<RoomData>(ProjectRooms.Items).ToArray();
            data.floorplans = new List<FloorplanScopeData>(ProjectFloorplans.Items).ToArray();
            data.levels = new List<Level>(LevelRegistry.Items).ToArray();

            if (CameraController.Instance != null)
                data.camera = CameraController.Instance.GetState();

            data.handleMode = ResizeHandleManager.Mode.ToString();
            data.projectInstructions = ProjectInstructions.Text;

            data.settings = KitchenSettings.Instance.ToData();

            data.lightsOn = LightSourceElement.GlobalOn;
            data.musicTrack = Audio.MusicState.Track;
            data.musicVolumePct = Audio.MusicState.VolumePct;
            data.windows = ProjectWindows.Capture();

            CaptureHistory(data, ordered);
            return data;
        }

        private static GroupData[] CaptureGroups()
        {
            var groups = new List<GroupData>();
            foreach (var g in GroupManager.AllGroups())
                groups.Add(new GroupData
                {
                    id = g.id, name = g.name, movable = g.movable, widthAxis = g.widthAxis
                });
            return groups.ToArray();
        }

        private static void CaptureHistory(ProjectData data, List<KitchenElement> ordered)
        {
            var indexOf = new Dictionary<KitchenElement, int>();
            for (int i = 0; i < ordered.Count; i++) indexOf[ordered[i]] = i;
            int IndexOf(KitchenElement el) =>
                el != null && indexOf.TryGetValue(el, out var i) ? i : -1;
            data.undoHistory = CommandStack.Instance.ExportUndo(IndexOf).ToArray();
            data.redoHistory = CommandStack.Instance.ExportRedo(IndexOf).ToArray();
        }
    }
}
