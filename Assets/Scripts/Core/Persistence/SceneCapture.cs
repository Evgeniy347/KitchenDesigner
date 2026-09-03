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
            ElementData? basePlateData = null;
            foreach (var e in elements)
            {
                if (e == null) continue;
                if (e.GetComponent<BasePlate>() != null)
                {
                    basePlateData ??= ElementCapture.FromElement(e);
                    continue;
                }
                items.Add(ElementCapture.FromElement(e));
                ordered.Add(e);
            }
            basePlateData ??= BasePlateFromScene();

            var data = new ProjectData(items);
            data.groups = CaptureGroups();
            data.rooms = new List<RoomData>(ProjectRooms.Items).ToArray();
            data.floorplans = new List<FloorplanScopeData>(ProjectFloorplans.Items).ToArray();

            if (CameraController.Instance != null)
                data.camera = CameraController.Instance.GetState();

            data.handleMode = ResizeHandleManager.Mode.ToString();
            data.projectInstructions = ProjectInstructions.Text;

            if (basePlateData != null)
            {
                data.basePlate = basePlateData;
                data.basePlateValid = true;
            }

            data.settings = KitchenSettings.Instance.ToData();

            data.tintEnabled = ElementHighlighter.TintEnabled;
            data.lightsOn = LightSourceElement.GlobalOn;
            data.musicTrack = Audio.MusicState.Track;
            data.musicVolumePct = Audio.MusicState.VolumePct;
            data.windows = ProjectWindows.Capture();

            CaptureHistory(data, ordered);
            return data;
        }

        private static ElementData? BasePlateFromScene()
        {
            var floorGo = GameObject.FindWithTag("Floor");
            if (floorGo == null) return null;
            var bp = floorGo.GetComponent<BasePlate>();
            if (bp == null || bp.Element == null) return null;
            return ElementCapture.FromElement(bp.Element);
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
