using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    [System.Serializable]
    public class ProjectData
    {
        public int version = AppConstants.SAVE_FORMAT_VERSION;
        public ElementData[] elements = new ElementData[0];
        public GroupData[] groups = new GroupData[0];
        public RoomData[] rooms = new RoomData[0];
        public FloorplanScopeData[] floorplans = new FloorplanScopeData[0];
        public CameraState camera = new CameraState();

        public string handleMode = "Resize";

        public string projectInstructions = "";

        public bool basePlateValid = false;

        public ElementData? basePlate = ElementData.OfCurrentFormat();

        public CommandRecord[] undoHistory = new CommandRecord[0];
        public CommandRecord[] redoHistory = new CommandRecord[0];

        public KitchenSettingsData? settings = null;

        public bool lightsOn = true;

        public int musicTrack = 0;
        public int musicVolumePct = Audio.MusicState.DEFAULT_VOLUME_PCT;

        public WindowStateData[] windows = new WindowStateData[0];

        public ProjectData() { }

        public ProjectData(IEnumerable<ElementData> items)
        {
            elements = new List<ElementData>(items).ToArray();
        }
    }

    [System.Serializable]
    public class WindowStateData
    {
        public string id = "";
        public bool visible;
        public float x;
        public float y;
        public float height;
    }

    [System.Serializable]
    public class GroupData
    {
        public int id;
        public string name = "Группа";
        public bool movable = true;
        public string widthAxis = "x";
    }

    [System.Serializable]
    public class RoomData
    {
        public string floorplanId = "";
        public string id = "";
        public string floor = "";
        public string[] walls = System.Array.Empty<string>();
        public string[] openings = System.Array.Empty<string>();
        public int[] polygonXZ = System.Array.Empty<int>();
    }

    [System.Serializable]
    public class FloorplanScopeData
    {
        public string id = "";
        public string[] elements = System.Array.Empty<string>();
    }

    [System.Serializable]
    public struct CameraState
    {
        public bool valid;
        public float targetX, targetY, targetZ;
        public float angleX, angleY, distance;
        public float photoDistance;
        public float photoTargetX, photoTargetY, photoTargetZ;
        public float photoAngleX, photoAngleY;
    }
}
