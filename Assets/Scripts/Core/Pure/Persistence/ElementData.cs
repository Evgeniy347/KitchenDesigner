using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [System.Serializable]
    public class GrooveEntry
    {
        public int kind;
        public int side;

        public GrooveEntry() { }

        public GrooveEntry(GrooveSpec spec)
        {
            kind = (int)spec.kind;
            side = (int)spec.side;
        }

        public GrooveSpec ToSpec() => new GrooveSpec((GrooveKind)kind, (GrooveSide)side);
    }

    [System.Serializable]
    public class TextureOverlayEntry
    {
        public int side;
        public string materialId = AppConstants.DEFAULT_MATERIAL_ID;
        public int u0MM;
        public int v0MM;
        public int widthMM;
        public int heightMM;

        public TextureOverlayEntry() { }

        public TextureOverlayEntry(TextureOverlaySpec spec)
        {
            side = (int)spec.side;
            materialId = spec.MaterialId;
            u0MM = spec.u0MM;
            v0MM = spec.v0MM;
            widthMM = spec.widthMM;
            heightMM = spec.heightMM;
        }

        public TextureOverlaySpec ToSpec() =>
            new TextureOverlaySpec((OverlaySide)side, materialId, u0MM, v0MM, widthMM, heightMM);
    }

    [System.Serializable]
    public class ElementData
    {
        public string name = string.Empty;
        public int[] dimensionsMM = new int[3];
        public float[] position = new float[3];
        public float[] rotation = new float[4];
        public bool movable = true;
        public bool isWall = false;
        public string wallKind = "";
        public float[] wallEndShape = System.Array.Empty<float>();
        public bool isFacade = false;
        public bool isRadialShelf = false;
        public bool isTable = false;
        public bool isRadiusTable = false;
        public bool isStool = false;
        public bool isChair = false;
        public bool isSofa = false;
        public int seatHeightMM = AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT;
        public int legInsetMM = 100;
        public int gapLeft = 2;
        public int gapRight = 2;
        public int gapTop = 2;
        public int gapBottom = 2;
        public int gapFront = 0;
        public int gapBack = 0;
        public int groupId = 0;
        public string materialId = AppConstants.DEFAULT_MATERIAL_ID;
        public string legsMaterialId = AppConstants.DEFAULT_MATERIAL_ID;
        public string tabletopMaterialId = AppConstants.DEFAULT_MATERIAL_ID;
        public bool transparent = false;
        public int doorMode = 0;
        public bool doorOpen = false;
        public bool assembled = false;
        public int assembledFill = 0;
        public int grooveCount = AppConstants.ASSEMBLED_DEFAULT_GROOVES;
        public int cornerRadius = AppConstants.RADIAL_CORNER_RADIUS_DEFAULT;
		public bool isDrawer = false;
		public int drawerSystem = 0;
		public int drawerType = 0;
		public int drawerNominalLength = 350;
		public int drawerColor = 0;
		public int drawerInternalWidth = 400;
		public bool drawerIsDouble = false;
		public bool drawerIsUpper = false;
		public string drawerPairedName = "";
		public string drawerAttachedFacadeName = "";
		public string attachedToName = "";
		public int doubleDrawerState = 0;
		public bool isWindow = false;
		public int windowTint = 0;
		public int windowSillProtrusionMM = 50;
		public int windowDoorMode = 0;
		public bool windowIsOpen = false;
		public string windowAttachedWallName = "";
		public bool isDoor = false;
		public int doorSashType = 0;
		public int doorDoorMode = 0;
		public bool doorIsOpen = false;
		public string doorAttachedWallName = "";
		public bool isPillar = false;
		public int midHeightMM = 75;
		public bool isFloor = false;
		public int[] floorPolygonXZ = System.Array.Empty<int>();
		public bool isLightSource = false;
		public int lightTemperatureK = LampSpec.DEFAULT_TEMPERATURE_K;
		public int lightPowerW = LampSpec.DEFAULT_POWER_W;
		public int lightDiffusionPct = LampSpec.DEFAULT_DIFFUSION_PCT;
		public int lightUpPct = LampSpec.DEFAULT_UP_PCT;
		public int lightBeamDeg = LampSpec.DEFAULT_BEAM_DEG;
		public int lightSoftnessPct = LampSpec.DEFAULT_SOFTNESS_PCT;
		public int lightRangeMinMM = LampSpec.DEFAULT_RANGE_MIN_MM;
		public int lightRangeMaxMM = LampSpec.DEFAULT_RANGE_MAX_MM;
		public int lightDropMM = LampSpec.DEFAULT_DROP_MM;
		public int lightUpConePct = LampSpec.DEFAULT_UP_CONE_PCT;
		public int lightUpRangePct = LampSpec.DEFAULT_UP_RANGE_PCT;
		public int lightEfficacyLmPerW = LampSpec.DEFAULT_EFFICACY_LM_PER_W;
		public int lightLumensPerUnit = LampSpec.DEFAULT_LUMENS_PER_UNIT;
		public int lightGlowPct = LampSpec.DEFAULT_GLOW_PCT;
		public int lightShadowStrengthPct = LampSpec.DEFAULT_SHADOW_STRENGTH_PCT;
		public int lightShape = (int)LampSpec.DEFAULT_SHAPE;
		public int lightShadow = (int)LampSpec.DEFAULT_SHADOW;
		public bool isPanel = false;
		public bool isSink = false;
		public string sinkAttachedPartName = "";
		public int sinkOffsetXMM = 0;
		public int sinkOffsetYMM = 0;
		public bool isCooktop = false;
		public string cooktopModel = "";
		public string cooktopAttachedPartName = "";
		public int cooktopOffsetXMM = 0;
		public int cooktopOffsetYMM = 0;
		public int cooktopCutoutWidthMM = 0;
		public int cooktopCutoutDepthMM = 0;
		public float cooktopYawDeg = 0f;
		public bool isOven = false;
		public bool isDishwasher = false;
		public string dishwasherAttachedFacadeName = "";
		public GrooveEntry[] grooves = System.Array.Empty<GrooveEntry>();
		public TextureOverlayEntry[] textureOverlays = System.Array.Empty<TextureOverlayEntry>();
		public bool edgeBanding = true;
		public float edgeThicknessMM = AppConstants.EDGE_THICKNESS_DEFAULT_MM;
		public bool edgeSkipValidation = false;
		public int edgeManualMask = 0;

        public ElementData() { }

        public List<GrooveSpec> GrooveSpecs()
        {
            var result = new List<GrooveSpec>();
            if (grooves == null) return result;
            foreach (var g in grooves)
                if (g != null) result.Add(g.ToSpec());
            return result;
        }

        public List<TextureOverlaySpec> TextureOverlaySpecs()
        {
            var result = new List<TextureOverlaySpec>();
            if (textureOverlays == null) return result;
            foreach (var t in textureOverlays)
                if (t != null) result.Add(t.ToSpec());
            return result;
        }

        public Vector3Int Dimensions =>
            dimensionsMM != null && dimensionsMM.Length >= 3
                ? new Vector3Int(dimensionsMM[0], dimensionsMM[1], dimensionsMM[2])
                : new Vector3Int(1, 1, 1);

        public Vector3 Position =>
            position != null && position.Length >= 3
                ? new Vector3(position[0], position[1], position[2])
                : Vector3.zero;

        public Quaternion Rotation =>
            rotation != null && rotation.Length >= 4
                ? new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3])
                : Quaternion.identity;

		public List<Vector2Int> FloorPolygon()
		{
			var result = new List<Vector2Int>();
			if (floorPolygonXZ == null || floorPolygonXZ.Length < 6 || floorPolygonXZ.Length % 2 != 0)
				return result;
			for (int i = 0; i < floorPolygonXZ.Length; i += 2)
				result.Add(new Vector2Int(floorPolygonXZ[i], floorPolygonXZ[i + 1]));
			return result;
		}
    }
}
