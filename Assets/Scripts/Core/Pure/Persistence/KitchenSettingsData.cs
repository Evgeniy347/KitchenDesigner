namespace KitchenDesigner.Core
{
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
