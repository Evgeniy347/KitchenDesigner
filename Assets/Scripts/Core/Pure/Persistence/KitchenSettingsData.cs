namespace KitchenDesigner.Core
{
    [System.Serializable]
    public class KitchenSettingsData
    {
        public int gridStep = 18;
        public bool gridEnabled = true;
        public bool snapEnabled = true;
        public float snapThreshold = KitchenSettings.SNAP_THRESHOLD_DEFAULT_MM;
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
        public ViewPreset? viewPhoto;

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
        public bool photoSSGI = false;

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

        public const int CURRENT_PHOTO_SCHEMA = 1;
        public int photoSchema = 0;

        public bool photoHdr = true;
        public int photoBloomClampPct = KitchenSettings.PHOTO_BLOOM_CLAMP_DEFAULT_PCT;
        public int photoTonemap = KitchenSettings.PHOTO_TONEMAP_DEFAULT;
        public bool photoAoFullRes = true;
        public int photoRenderScalePct = KitchenSettings.PHOTO_RENDER_SCALE_DEFAULT_PCT;
        public int photoShadowMapPx = KitchenSettings.PHOTO_SHADOWMAP_DEFAULT_PX;
        public int photoLightsPerObject = KitchenSettings.PHOTO_LIGHTS_PER_OBJECT_DEFAULT;
        public int photoAmbientSkyPct = KitchenSettings.PHOTO_AMBIENT_SKY_DEFAULT_PCT;
        public int photoAmbientEquatorPct = KitchenSettings.PHOTO_AMBIENT_EQUATOR_DEFAULT_PCT;
        public int photoBounceMaxPct = KitchenSettings.PHOTO_BOUNCE_MAX_DEFAULT_PCT;
        public int photoAoIntensityPct = KitchenSettings.PHOTO_AO_INTENSITY_DEFAULT_PCT;
        public int photoAoRadiusMM = KitchenSettings.PHOTO_AO_RADIUS_DEFAULT_MM;
        public int photoAoDirectPct = KitchenSettings.PHOTO_AO_DIRECT_DEFAULT_PCT;
        public int photoAoFalloffM = KitchenSettings.PHOTO_AO_FALLOFF_DEFAULT_M;
        public int photoSsgiStrengthPct = KitchenSettings.PHOTO_SSGI_STRENGTH_DEFAULT_PCT;
        public int photoSsgiRadiusMM = KitchenSettings.PHOTO_SSGI_RADIUS_DEFAULT_MM;
        public int photoSsgiSamples = KitchenSettings.PHOTO_SSGI_SAMPLES_DEFAULT;
        public int photoSsgiResolutionPct = KitchenSettings.PHOTO_SSGI_RESOLUTION_DEFAULT_PCT;
        public int photoSsgiBlurPx = KitchenSettings.PHOTO_SSGI_BLUR_DEFAULT_PX;

        public int constructionRegion = (int)ConstructionRegion.Urals;
        public int constructionFloorHeightMm = KitchenSettings.CONSTRUCTION_FLOOR_HEIGHT_DEFAULT_MM;
        public int constructionMasonry = (int)KitchenDesigner.Core.Construction.MasonryTechnology.BrickSingle;
        public int constructionJointMm = KitchenSettings.CONSTRUCTION_JOINT_DEFAULT_MM;
        public int constructionWastePct = KitchenSettings.CONSTRUCTION_WASTE_DEFAULT_PCT;
        public int constructionSoil = (int)SoilKind.Unknown;
        public int constructionConcrete = (int)ConcreteGrade.B20;
        public int constructionSandMm = KitchenSettings.CONSTRUCTION_SAND_DEFAULT_MM;
        public int constructionGravelMm = KitchenSettings.CONSTRUCTION_GRAVEL_DEFAULT_MM;
        public bool constructionCompacted = true;
    }
}
