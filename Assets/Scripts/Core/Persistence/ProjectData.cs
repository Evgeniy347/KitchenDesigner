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

        public ElementData? basePlate = null;

        public CommandRecord[] undoHistory = new CommandRecord[0];
        public CommandRecord[] redoHistory = new CommandRecord[0];

        public KitchenSettingsData? settings = null;

        public bool tintEnabled = true;
        public bool lightsOn = true;

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

    [System.Serializable]
    public class KitchenSettingsData
    {
        public int gridStep = 18;
        public bool gridEnabled = true;
        public bool snapEnabled = true;
        public float snapThreshold = 50f;
        public bool blockOnViolation = true;
        public bool autoSave = true;
        public int autoSaveInterval = 60;
        public bool spatialGrid = false;
        public bool windowedMode = true;
        public bool cameraPanFree = false;

        public const int CURRENT_VIEW_SCHEMA = 1;
        public int viewSchema = 0;

        public ViewPreset? viewNormal;
        public ViewPreset? viewRoom;

        public bool edgeOutline = true;
        public bool wallsEnabled = true;
        public bool lowerNearWalls = true;
        public bool wallOutline = true;
        public bool hideOpeningsOnLoweredWalls = false;
        public bool objectsVisible = true;
        public bool hideLightSources = false;
        public int edgePartialThresholdPct = KitchenSettings.EDGE_PARTIAL_THRESHOLD_DEFAULT_PCT;
        public float mouseSensitivity = 1f;
        public float wasdSpeed = 1f;
        public float arrowSpeed = 1f;

        public int photoQuality = (int)PhotoQualityPreset.High;
        public bool photoShadows = true;
        public bool photoSoftShadows = true;
        public bool photoAntiAliasing = true;
        public bool photoSupersampling = true;
        public bool photoAmbientOcclusion = true;
        public bool photoBloom = true;
        public bool photoVignette = true;
        public bool photoCeiling = true;
        public bool photoSSGI = true;

        public int photoAmbientPct = KitchenSettings.PHOTO_AMBIENT_DEFAULT_PCT;
        public int photoFloorBouncePct = KitchenSettings.PHOTO_FLOOR_BOUNCE_DEFAULT_PCT;
        public int photoExposurePct = KitchenSettings.PHOTO_EXPOSURE_DEFAULT_PCT;
        public int photoContrastPct = KitchenSettings.PHOTO_CONTRAST_DEFAULT_PCT;
        public int photoSaturationPct = KitchenSettings.PHOTO_SATURATION_DEFAULT_PCT;
        public int photoBloomPct = KitchenSettings.PHOTO_BLOOM_DEFAULT_PCT;
        public int photoBloomThresholdPct = KitchenSettings.PHOTO_BLOOM_THRESHOLD_DEFAULT_PCT;
        public int photoVignettePct = KitchenSettings.PHOTO_VIGNETTE_DEFAULT_PCT;
        public int photoSunShadowStrengthPct = KitchenSettings.PHOTO_SUN_SHADOW_DEFAULT_PCT;
        public int photoShadowDistanceM = KitchenSettings.PHOTO_SHADOW_DISTANCE_DEFAULT_M;
        public bool photoLampShadows = true;
    }
}
