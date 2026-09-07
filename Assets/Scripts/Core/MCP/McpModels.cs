using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace KitchenDesigner.Core.MCP
{
    [Serializable]
    public class McpRequest
    {
        public string id = string.Empty;
        public string method = string.Empty;

        [Newtonsoft.Json.JsonProperty("params")]
        public JObject? Params { get; set; }

        [Newtonsoft.Json.JsonProperty("headers")]
        public Dictionary<string, string>? Headers { get; set; }
    }

    [Serializable]
    public class McpResponse
    {
        public string id = string.Empty;
        public string type = string.Empty;
        public object? data;

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string? etag;

        public static McpResponse Result(string id, object data) =>
            new McpResponse { id = id, type = "result", data = data };

        public static McpResponse Error(string id, int code, string message) =>
            new McpResponse { id = id, type = "error", data = new { code, message } };

        public static McpResponse NotModified(string id, string etag) =>
            new McpResponse { id = id, type = "not_modified", etag = etag };
    }

    [Serializable]
    public class ElementInfo
    {
        public string name = string.Empty;
        public string type = string.Empty;
        public int dimXMm;
        public int dimYMm;
        public int dimZMm;
        public float posXMm;
        public float posYMm;
        public float posZMm;
        public float rotXDeg;
        public float rotYDeg;
        public float rotZDeg;
        public bool active;
        public bool locked;
        public bool transparent;
        public int moduleId;
        public string? moduleName;
        public string materialId = string.Empty;
        public bool hasViolations;
        public float aabbMinXMm, aabbMinYMm, aabbMinZMm;
        public float aabbMaxXMm, aabbMaxYMm, aabbMaxZMm;
        public int worldDimXMm, worldDimYMm, worldDimZMm;
        public int effectiveDimXMm, effectiveDimYMm, effectiveDimZMm;
        public List<AxisGapInfo>? faceGaps;
        public int cornerRadiusMm;
        public string? grooves;
        public string? textureOverlays;
        public bool? edgeBanding;
        public float? edgeThicknessMM;
        public string? edgeSides;
        public string? edges;
        public string? attachedToName;
        public bool? attachDetached;

        public string? facadeMode;
        public float? faceNormalX, faceNormalY, faceNormalZ;
        public bool? faceInward;
        public List<FaceObstructionInfo>? faceObstructions;
        public List<OpeningViolationInfo>? openingViolations;
        public DrawerInfo? drawer;
        public TableInfo? table;
		public RadiusTableInfo? radiusTable;
		public StoolInfo? stool;
		public ChairInfo? chair;
		public SofaInfo? sofa;
		public PouffeInfo? pouffe;
		public ToiletInfo? toilet;
		public WallHungToiletInfo? wallHungToilet;
		public BathtubInfo? bathtub;
		public BathMixerInfo? bathMixer;
		public ShowerColumnInfo? showerColumn;
		public WallDeviceInfo? wallDevice;
		public LightSwitchInfo? lightSwitch;
		public BedInfo? bed;
		public PillarInfo? pillar;
		public ScrewLegInfo? screwLeg;
		public PipeInfo? pipe;
		public CooktopInfo? cooktop;
		public OvenInfo? oven;
		public DishwasherInfo? dishwasher;
		public WindowInfo? window;
		public DoorInfo? door;
    }

    [Serializable]
    public class FaceObstructionInfo
    {
        public string neighbor = string.Empty;
        public float distanceFromFaceMm;
        public float overlapWidthMm;
        public float overlapHeightMm;
    }

    [Serializable]
    public class OpeningViolationInfo
    {
        public string neighbor = string.Empty;
        public string openingMode = string.Empty;
        public float collisionAtProgress;
        public float collisionOverlapMm;
    }

    [Serializable]
    public class ModuleInfo
    {
        public int id;
        public string name = string.Empty;
        public bool movable;
        public string widthAxis = "x";
        public bool editing;
        public int elementCount;
        public float[]? boundsCenterMm;
        public int[]? boundsSizeMM;
        public List<ElementInfo> elements = new();
    }

    [Serializable]
    public class SpecInfo
    {
        public List<SpecLineInfo> lines = new();
        public int totalCount;
        public float totalAreaM2;
    }

    [Serializable]
    public class SpecLineInfo
    {
        public string name = string.Empty;
        public int dimXMm;
        public int dimYMm;
        public int dimZMm;
        public int count;
        public float areaPerBoardM2;
        public float totalAreaM2;
    }

    [Serializable]
    public class ConsoleLogEntry
    {
        public string type = string.Empty;
        public string message = string.Empty;
        public string stackTrace = string.Empty;
    }

    [Serializable]
    public class ObjectInfo
    {
        public string name = string.Empty;
        public string path = string.Empty;
        public float posXMm;
        public float posYMm;
        public float posZMm;
        public float rotXDeg;
        public float rotYDeg;
        public float rotZDeg;
        public float scaleX;
        public float scaleY;
        public float scaleZ;
        public bool active;
        public List<string> components = new();
        public List<string> children = new();
    }

    [Serializable]
    public class HierarchyNode
    {
        public string name = string.Empty;
        public string path = string.Empty;
        public bool active;
        public List<HierarchyNode> children = new();
    }

    [Serializable]
    public class StatusInfo
    {
        public string sceneName = string.Empty;
        public int objectCount;
        public bool isPlaying;
        public string platform = string.Empty;
        public string projectInstructions = string.Empty;
    }

    [Serializable]
    public class AabbMmInfo
    {
        public float minXMm, minYMm, minZMm;
        public float maxXMm, maxYMm, maxZMm;
    }

    [Serializable]
    public class AabbInfo
    {
        public float minX, minY, minZ;
        public float maxX, maxY, maxZ;
    }

    [Serializable]
    public class FaceInfo
    {
        public float centerXMm, centerYMm, centerZMm;
        public float normalX, normalY, normalZ;
        public float sizeXMm, sizeYMm;
    }

    [Serializable]
    public class VertexInfo
    {
        public float xMm, yMm, zMm;
    }

    [Serializable]
    public class ElementDebugInfo
    {
        public string name = string.Empty;
        public string type = string.Empty;
        public AabbMmInfo? aabb;
        public FaceInfo[]? faces;
        public VertexInfo[]? vertices;
        public int dimXMm, dimYMm, dimZMm;
        public int effectiveDimXMm, effectiveDimYMm, effectiveDimZMm;
    }

    [Serializable]
    public class AxisGapInfo
    {
        public string axis = string.Empty;
        public string neighbor = string.Empty;
        public float gapMM;
        public bool touching;
        public bool isOverlap;
    }

    [Serializable]
    public class ElementGapsResult
    {
        public string name = string.Empty;
        public List<AxisGapInfo> gaps = new();
    }

    [Serializable]
    public class SimulateResult
    {
        public string name = string.Empty;
        public AabbInfo? currentAABB;
        public AabbInfo? simulatedAABB;
        public List<string> overlapsWith = new();
        public List<AxisGapInfo> faceGaps = new();
        public bool wouldHaveViolations;
    }

    [Serializable]
    public class DrawerInfo
    {
        public string system = string.Empty;
        public string drawerType = string.Empty;
        public int drawerLengthMM;
        public string drawerColor = string.Empty;
        public int internalWidthMM;
        public bool isDouble;
        public bool isUpper;
        public string pairedDrawerName = string.Empty;
        public string attachedFacadeName = string.Empty;
        public string doubleState = string.Empty;
        public bool isOpen;
    }

    [Serializable]
    public class TableInfo
    {
        public int legInsetMM;
        public string tabletopMaterialId = MaterialCatalog.DefaultId;
        public string legsMaterialId = MaterialCatalog.DefaultId;
    }

    [Serializable]
    public class RadiusTableInfo
    {
        public int legInsetMM;
        public string shape = "capsule";
        public string tabletopMaterialId = MaterialCatalog.DefaultId;
        public string legsMaterialId = MaterialCatalog.DefaultId;
    }

    [Serializable]
    public class StoolInfo
    {
        public int cornerRadiusMM;
        public string shape = string.Empty;
        public string tabletopMaterialId = MaterialCatalog.DefaultId;
        public string legsMaterialId = MaterialCatalog.DefaultId;
    }

    [Serializable]
    public class ChairInfo
    {
        public int cornerRadiusMM;
        public int seatHeightMM;
        public int backrestThicknessMM;
        public string tabletopMaterialId = MaterialCatalog.DefaultId;
        public string legsMaterialId = MaterialCatalog.DefaultId;
    }

	[Serializable]
	public class SofaInfo
	{
		public int cornerRadiusMM;
		public int seatHeightMM;
		public int backDepthMM;
		public int cushionCount;
		public string tabletopMaterialId = MaterialCatalog.DefaultId;
		public string legsMaterialId = MaterialCatalog.DefaultId;
	}

	[Serializable]
	public class PouffeInfo
	{
		public int cornerRadiusMM;
		public int maxCornerRadiusMM;
		public int seatThicknessMM;
		public int maxSeatThicknessMM;
		public int bodyHeightMM;
		public string tabletopMaterialId = MaterialCatalog.DefaultId;
		public string legsMaterialId = MaterialCatalog.DefaultId;
	}

	[Serializable]
	public class ToiletInfo
	{
		public int seatHeightMM;
		public int minSeatHeightMM;
		public int maxSeatHeightMM;
		public int cisternHeightMM;
		public string tabletopMaterialId = MaterialCatalog.DefaultId;
		public string legsMaterialId = MaterialCatalog.DefaultId;
	}

	[Serializable]
	public class WallHungToiletInfo
	{
		public int seatHeightMM;
		public int minSeatHeightMM;
		public int maxSeatHeightMM;
		public int bowlBottomMM;
		public int flushPlateHeightMM;
		public int minFlushPlateHeightMM;
		public int maxFlushPlateHeightMM;
		public string tabletopMaterialId = MaterialCatalog.DefaultId;
		public string legsMaterialId = MaterialCatalog.DefaultId;
	}

	[Serializable]
	public class BathtubInfo
	{
		public int rimWidthMM;
		public int maxRimWidthMM;
		public int bowlDepthMM;
		public int maxBowlDepthMM;
		public int bowlRadiusMM;
		public int maxBowlRadiusMM;
		public int bowlFilletMM;
		public int maxBowlFilletMM;
		public int shellCornerRadiusMM;
		public int bowlWidthMM;
		public int bowlDepthPlanMM;
	}

	[Serializable]
	public class BathMixerInfo
	{
		public int centresMM;
		public int maxCentresMM;
		public int bodyLengthMM;
		public int minBodyLengthMM;
		public int bodyDiameterMM;
		public int escutcheonReachMM;
		public int spoutLengthMM;
		public int outletDiameterMM;
	}

	[Serializable]
	public class ShowerColumnInfo
	{
		public int columnHeightMM;
		public int minColumnHeightMM;
		public int riserDiameterMM;
		public int headDiameterMM;
		public int headThicknessMM;
		public int armReachMM;
		public int minArmReachMM;
		public int wallOffsetMM;
		public int handShowerDiameterMM;
		public int hoseLengthMM;
	}

	[Serializable]
	public class WallDeviceInfo
	{
		public int plateWidthMM;
		public int plateHeightMM;
		public int protrusionMM;
		public int postCount;
	}

	[Serializable]
	public class LightSwitchInfo
	{
		public bool isOn;
		public string[] lights = Array.Empty<string>();
		public int maxLights;
	}

	[Serializable]
	public class BedInfo
	{
		public string size = string.Empty;
		public bool isDouble;
		public bool hasHeadboard;
		public int pillowCount;
		public int legCount;
		public int legHeightMM;
		public int mattressTopMM;
		public string tabletopMaterialId = MaterialCatalog.DefaultId;
		public string legsMaterialId = MaterialCatalog.DefaultId;
	}

	[Serializable]
	public class PillarInfo
	{
		public int midHeightMM;
		public int diameterMM;
	}

	[Serializable]
	public class PipeInfo
	{
		public string sizeId = KitchenDesigner.Core.Plumbing.PipeSpec.DEFAULT_SIZE;
		public string designation = "";
		public int nominalBoreMM;
		public int lengthMM;
		public float outerDiameterMM;
		public float innerDiameterMM;
		public float wallThicknessMM;
	}

	[Serializable]
	public class ScrewLegInfo
	{
		public string thread = ScrewLegSpec.DEFAULT_THREAD;
		public int threadLengthMM;
		public int insertionMM;
		public int baseDiameterMM;
		public int baseHeightMM;
		public int heightAboveFloorMM;
		public int leftInHostMM;
		public int rightInHostMM;
		public int topInHostMM;
		public int bottomInHostMM;
		public string hostName = "";
	}

	[Serializable]
	public class CooktopInfo
	{
		public string model = string.Empty;
		public bool fixedSize;
		public int cutoutWidthMM;
		public int cutoutDepthMM;
		public int plateHeightMM;
		public int bodyHeightMM;
		public string attachedPartName = string.Empty;
		public int offsetXMM;
		public int offsetYMM;
		public float yawDeg;
	}

	[Serializable]
	public class OvenInfo
	{
		public string model = string.Empty;
		public bool fixedSize;
		public float facadeThicknessMM;
		public int bodyWidthMM;
		public int bodyDepthMM;
		public int bodyHeightMM;
		public int controlPanelHeightMM;
		public int glassHeightMM;
		public int handleProtrusionMM;
		public bool isOpen;
	}

	[Serializable]
	public class DishwasherInfo
	{
		public string model = string.Empty;
		public bool fixedSize;
		public string attachedFacadeName = string.Empty;
		public int nicheWidthMM;
		public int nicheMinDepthMM;
		public int heightMinMM;
		public int heightMaxMM;
		public int facadeWidthMM;
		public int facadeMinHeightMM;
		public int facadeMaxHeightMM;
		public int facadeNominalHeightMM;
		public int plinthMM;
		public int plinthMinMM;
		public int plinthMaxMM;
		public int plinthSetbackMM;
		public int plinthNicheMM;
		public int baseHeightMM;
		public int baseSetbackMM;
		public float facadeMountGapMM;
		public bool isOpen;
	}

	[Serializable]
	public class WindowInfo
    {
        public string tint = "Clear";
        public int sillProtrusionMM;
        public string mode = string.Empty;
        public bool isOpen;
        public string attachedWallName = string.Empty;
    }

	[Serializable]
	public class DoorInfo
    {
        public string sashType = "Glass";
        public string mode = string.Empty;
        public bool isOpen;
        public string attachedWallName = string.Empty;
    }
}
