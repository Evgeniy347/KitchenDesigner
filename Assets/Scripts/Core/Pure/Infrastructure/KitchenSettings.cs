using UnityEngine;

namespace KitchenDesigner.Core
{
    public partial class KitchenSettings
    {
        private static KitchenSettings? _instance;
        public static KitchenSettings Instance => _instance ??= new KitchenSettings();

        [SerializeField] private int _gridStep = 18;
        [SerializeField] private bool _gridEnabled = true;
        [SerializeField] private bool _snapEnabled = true;
        [SerializeField] private float _snapThreshold = SNAP_THRESHOLD_DEFAULT_MM;
        [SerializeField] private bool _blockOnViolation = true;
        [SerializeField] private bool _autoSave = true;
        [SerializeField] private int _autoSaveInterval = 60;
        [SerializeField] private bool _spatialGrid = false;
        [SerializeField] private bool _windowedMode = true;
        [SerializeField] private ViewPreset? _normalView = new ViewPreset();
        [SerializeField] private ViewPreset? _roomView = new ViewPreset();
        [SerializeField] private ViewPreset? _photoView = ViewPreset.PhotoDefaults();
        [SerializeField] private bool _cameraPanFree = false;
        [SerializeField] private int _edgePartialThresholdPct = EDGE_PARTIAL_THRESHOLD_DEFAULT_PCT;

        [SerializeField] private float _mouseSensitivity = 1f;
        [SerializeField] private float _wasdSpeed = 1f;
        [SerializeField] private float _arrowSpeed = 1f;

        public const float MIN_INPUT_SPEED = 0.1f;
        public const float MAX_INPUT_SPEED = 3f;

        [SerializeField] private PhotoQualityPreset _photoQuality = PhotoQualityPreset.High;
        [SerializeField] private bool _photoShadows = true;
        [SerializeField] private bool _photoSoftShadows = true;
        [SerializeField] private bool _photoAntiAliasing = true;
        [SerializeField] private bool _photoSupersampling = true;
        [SerializeField] private bool _photoAmbientOcclusion = true;
        [SerializeField] private bool _photoBloom = true;
        [SerializeField] private bool _photoVignette = true;
        [SerializeField] private bool _photoCeiling = true;
        [SerializeField] private bool _photoSSGI = false;

        [SerializeField] private int _photoAmbientPct = PHOTO_AMBIENT_DEFAULT_PCT;
        [SerializeField] private int _photoFloorBouncePct = PHOTO_FLOOR_BOUNCE_DEFAULT_PCT;
        [SerializeField] private int _photoExposurePct = PHOTO_EXPOSURE_DEFAULT_PCT;
        [SerializeField] private int _photoContrastPct = PHOTO_CONTRAST_DEFAULT_PCT;
        [SerializeField] private int _photoSaturationPct = PHOTO_SATURATION_DEFAULT_PCT;
        [SerializeField] private int _photoBloomPct = PHOTO_BLOOM_DEFAULT_PCT;
        [SerializeField] private int _photoBloomThresholdPct = PHOTO_BLOOM_THRESHOLD_DEFAULT_PCT;
        [SerializeField] private int _photoVignettePct = PHOTO_VIGNETTE_DEFAULT_PCT;
        [SerializeField] private int _photoSunShadowStrengthPct = PHOTO_SUN_SHADOW_DEFAULT_PCT;
        [SerializeField] private int _photoShadowDistanceM = PHOTO_SHADOW_DISTANCE_DEFAULT_M;
        [SerializeField] private bool _photoLampShadows = true;

        public const int PHOTO_AMBIENT_DEFAULT_PCT = 100;
        public const int PHOTO_AMBIENT_MAX_PCT = 400;
        public const int PHOTO_FLOOR_BOUNCE_DEFAULT_PCT = 250;
        public const int PHOTO_FLOOR_BOUNCE_MAX_PCT = 300;
        public const int PHOTO_EXPOSURE_DEFAULT_PCT = 50;
        public const int PHOTO_EXPOSURE_MIN_PCT = -300;
        public const int PHOTO_EXPOSURE_MAX_PCT = 300;
        public const int PHOTO_CONTRAST_DEFAULT_PCT = 8;
        public const int PHOTO_SATURATION_DEFAULT_PCT = 6;
        public const int PHOTO_COLOR_MIN_PCT = -100;
        public const int PHOTO_COLOR_MAX_PCT = 100;
        public const int PHOTO_BLOOM_DEFAULT_PCT = 30;
        public const int PHOTO_BLOOM_MAX_PCT = 300;
        public const int PHOTO_BLOOM_THRESHOLD_DEFAULT_PCT = 150;
        public const int PHOTO_BLOOM_THRESHOLD_MAX_PCT = 500;
        public const int PHOTO_VIGNETTE_DEFAULT_PCT = 22;
        public const int PHOTO_SUN_SHADOW_DEFAULT_PCT = 100;
        public const int PHOTO_SHADOW_DISTANCE_DEFAULT_M = 10;
        public const int PHOTO_SHADOW_DISTANCE_MIN_M = 5;
        public const int PHOTO_SHADOW_DISTANCE_MAX_M = 100;

        public int GridStep
        {
            get => _gridStep;
            set => _gridStep = Mathf.Max(1, value);
        }

        public bool GridEnabled
        {
            get => _gridEnabled;
            set => _gridEnabled = value;
        }

        public bool SnapEnabled
        {
            get => _snapEnabled;
            set => _snapEnabled = value;
        }

        public const float SNAP_THRESHOLD_DEFAULT_MM = 50f;

        public float SnapThreshold
        {
            get => _snapThreshold;
            set => _snapThreshold = Mathf.Max(1f, value);
        }

        public bool BlockOnViolation
        {
            get => _blockOnViolation;
            set => _blockOnViolation = value;
        }

        public bool AutoSave
        {
            get => _autoSave;
            set => _autoSave = value;
        }

        public int AutoSaveInterval
        {
            get => _autoSaveInterval;
            set => _autoSaveInterval = Mathf.Max(10, value);
        }

        public bool SpatialGrid
        {
            get => _spatialGrid;
            set => _spatialGrid = value;
        }

        public bool WindowedMode
        {
            get => _windowedMode;
            set => _windowedMode = value;
        }

        public ViewPreset NormalView => _normalView ??= new ViewPreset();

        public ViewPreset RoomView => _roomView ??= new ViewPreset();

        public ViewPreset PhotoView => _photoView ??= ViewPreset.PhotoDefaults();

        public const int EDGE_PARTIAL_THRESHOLD_DEFAULT_PCT = 5;
        public const int EDGE_PARTIAL_THRESHOLD_MAX_PCT = 50;

        public int EdgePartialThresholdPct
        {
            get => _edgePartialThresholdPct;
            set => _edgePartialThresholdPct = Mathf.Clamp(value, 0, EDGE_PARTIAL_THRESHOLD_MAX_PCT);
        }

        public bool CameraPanFree
        {
            get => _cameraPanFree;
            set => _cameraPanFree = value;
        }

        public float MouseSensitivity
        {
            get => _mouseSensitivity;
            set => _mouseSensitivity = Mathf.Clamp(value, MIN_INPUT_SPEED, MAX_INPUT_SPEED);
        }

        public float WasdSpeed
        {
            get => _wasdSpeed;
            set => _wasdSpeed = Mathf.Clamp(value, MIN_INPUT_SPEED, MAX_INPUT_SPEED);
        }

        public float ArrowSpeed
        {
            get => _arrowSpeed;
            set => _arrowSpeed = Mathf.Clamp(value, MIN_INPUT_SPEED, MAX_INPUT_SPEED);
        }

        public PhotoQualityPreset PhotoQuality
        {
            get => _photoQuality;
            set
            {
                _photoQuality = value;
                PhotoQualityPresetTable.ApplyToggles(value, this);
            }
        }

        public bool PhotoShadows
        {
            get => _photoShadows;
            set => _photoShadows = value;
        }

        public bool PhotoSoftShadows
        {
            get => _photoSoftShadows;
            set => _photoSoftShadows = value;
        }

        public bool PhotoAntiAliasing
        {
            get => _photoAntiAliasing;
            set => _photoAntiAliasing = value;
        }

        public bool PhotoSupersampling
        {
            get => _photoSupersampling;
            set => _photoSupersampling = value;
        }

        public bool PhotoAmbientOcclusion
        {
            get => _photoAmbientOcclusion;
            set => _photoAmbientOcclusion = value;
        }

        public bool PhotoBloom
        {
            get => _photoBloom;
            set => _photoBloom = value;
        }

        public bool PhotoVignette
        {
            get => _photoVignette;
            set => _photoVignette = value;
        }

        public bool PhotoCeiling
        {
            get => _photoCeiling;
            set => _photoCeiling = value;
        }

        public bool PhotoSSGI
        {
            get => _photoSSGI;
            set => _photoSSGI = value;
        }

        public int PhotoAmbientPct
        {
            get => _photoAmbientPct;
            set => _photoAmbientPct = Mathf.Clamp(value, 0, PHOTO_AMBIENT_MAX_PCT);
        }

        public int PhotoFloorBouncePct
        {
            get => _photoFloorBouncePct;
            set => _photoFloorBouncePct = Mathf.Clamp(value, 0, PHOTO_FLOOR_BOUNCE_MAX_PCT);
        }

        public int PhotoExposurePct
        {
            get => _photoExposurePct;
            set => _photoExposurePct = Mathf.Clamp(value, PHOTO_EXPOSURE_MIN_PCT, PHOTO_EXPOSURE_MAX_PCT);
        }

        public int PhotoContrastPct
        {
            get => _photoContrastPct;
            set => _photoContrastPct = Mathf.Clamp(value, PHOTO_COLOR_MIN_PCT, PHOTO_COLOR_MAX_PCT);
        }

        public int PhotoSaturationPct
        {
            get => _photoSaturationPct;
            set => _photoSaturationPct = Mathf.Clamp(value, PHOTO_COLOR_MIN_PCT, PHOTO_COLOR_MAX_PCT);
        }

        public int PhotoBloomPct
        {
            get => _photoBloomPct;
            set => _photoBloomPct = Mathf.Clamp(value, 0, PHOTO_BLOOM_MAX_PCT);
        }

        public int PhotoBloomThresholdPct
        {
            get => _photoBloomThresholdPct;
            set => _photoBloomThresholdPct = Mathf.Clamp(value, 0, PHOTO_BLOOM_THRESHOLD_MAX_PCT);
        }

        public int PhotoVignettePct
        {
            get => _photoVignettePct;
            set => _photoVignettePct = Mathf.Clamp(value, 0, 100);
        }

        public int PhotoSunShadowStrengthPct
        {
            get => _photoSunShadowStrengthPct;
            set => _photoSunShadowStrengthPct = Mathf.Clamp(value, 0, 100);
        }

        public int PhotoShadowDistanceM
        {
            get => _photoShadowDistanceM;
            set => _photoShadowDistanceM = Mathf.Clamp(value, PHOTO_SHADOW_DISTANCE_MIN_M, PHOTO_SHADOW_DISTANCE_MAX_M);
        }

        public bool PhotoLampShadows
        {
            get => _photoLampShadows;
            set => _photoLampShadows = value;
        }

        public void ResetToDefaults()
        {
            _gridStep = 18;
            _gridEnabled = true;
            _snapEnabled = true;
            _snapThreshold = SNAP_THRESHOLD_DEFAULT_MM;
            _blockOnViolation = true;
            _autoSave = true;
            _autoSaveInterval = 60;
            _spatialGrid = false;
            _windowedMode = true;
            NormalView.ResetToDefaults();
            RoomView.ResetToDefaults();
            PhotoView.ResetToPhotoDefaults();
            _cameraPanFree = false;
            _edgePartialThresholdPct = EDGE_PARTIAL_THRESHOLD_DEFAULT_PCT;
            _mouseSensitivity = 1f;
            _wasdSpeed = 1f;
            _arrowSpeed = 1f;
            ResetPhotoLook();
            ResetConstruction();
        }

        public KitchenSettingsData ToData()
        {
            var data = new KitchenSettingsData
            {
                gridStep = _gridStep,
                gridEnabled = _gridEnabled,
                snapEnabled = _snapEnabled,
                snapThreshold = _snapThreshold,
                blockOnViolation = _blockOnViolation,
                autoSave = _autoSave,
                autoSaveInterval = _autoSaveInterval,
                spatialGrid = _spatialGrid,
                windowedMode = _windowedMode,
                viewSchema = KitchenSettingsData.CURRENT_VIEW_SCHEMA,
                viewNormal = NormalView.Clone(),
                viewRoom = RoomView.Clone(),
                viewPhoto = PhotoView.Clone(),
                wallsEnabled = NormalView.wallsEnabled,
                wallOutline = NormalView.wallOutline,
                lowerNearWalls = NormalView.lowerNearWalls,
                hideOpeningsOnLoweredWalls = NormalView.hideOpeningsOnLoweredWalls,
                objectsVisible = NormalView.objectsVisible,
                edgeOutline = NormalView.edgeOutline,
                hideLightSources = NormalView.hideLightSources,
                cameraPanFree = _cameraPanFree,
                edgePartialThresholdPct = _edgePartialThresholdPct,
                mouseSensitivity = _mouseSensitivity,
                wasdSpeed = _wasdSpeed,
                arrowSpeed = _arrowSpeed,
                photoQuality = (int)_photoQuality,
                photoShadows = _photoShadows,
                photoSoftShadows = _photoSoftShadows,
                photoAntiAliasing = _photoAntiAliasing,
                photoSupersampling = _photoSupersampling,
                photoAmbientOcclusion = _photoAmbientOcclusion,
                photoBloom = _photoBloom,
                photoVignette = _photoVignette,
                photoCeiling = _photoCeiling,
                photoSSGI = _photoSSGI,
                photoAmbientPct = _photoAmbientPct,
                photoFloorBouncePct = _photoFloorBouncePct,
                photoExposurePct = _photoExposurePct,
                photoContrastPct = _photoContrastPct,
                photoSaturationPct = _photoSaturationPct,
                photoBloomPct = _photoBloomPct,
                photoBloomThresholdPct = _photoBloomThresholdPct,
                photoVignettePct = _photoVignettePct,
                photoSunShadowStrengthPct = _photoSunShadowStrengthPct,
                photoShadowDistanceM = _photoShadowDistanceM,
                photoLampShadows = _photoLampShadows
            };
            CapturePhotoTuning(data);
            CaptureConstruction(data);
            return data;
        }

        public void ApplyFrom(KitchenSettingsData? data)
        {
            if (data == null) return;
            _gridStep = Mathf.Max(1, data.gridStep);
            _gridEnabled = data.gridEnabled;
            _snapEnabled = data.snapEnabled;
            _snapThreshold = Mathf.Max(1f, data.snapThreshold);
            _blockOnViolation = data.blockOnViolation;
            _autoSave = data.autoSave;
            _autoSaveInterval = Mathf.Max(10, data.autoSaveInterval);
            _spatialGrid = data.spatialGrid;
            _windowedMode = data.windowedMode;
            ApplyViewPresets(data);
            _cameraPanFree = data.cameraPanFree;
            _edgePartialThresholdPct = Mathf.Clamp(data.edgePartialThresholdPct, 0, EDGE_PARTIAL_THRESHOLD_MAX_PCT);
            _mouseSensitivity = Mathf.Clamp(data.mouseSensitivity, MIN_INPUT_SPEED, MAX_INPUT_SPEED);
            _wasdSpeed = Mathf.Clamp(data.wasdSpeed, MIN_INPUT_SPEED, MAX_INPUT_SPEED);
            _arrowSpeed = Mathf.Clamp(data.arrowSpeed, MIN_INPUT_SPEED, MAX_INPUT_SPEED);
            ApplyPhotoSettings(data);
            ApplyConstruction(data);
        }

        private void ApplyViewPresets(KitchenSettingsData data)
        {
            if (data.viewSchema < KitchenSettingsData.CURRENT_VIEW_SCHEMA)
            {
                NormalView.CopyFrom(new ViewPreset
                {
                    wallsEnabled = data.wallsEnabled,
                    wallOutline = data.wallOutline,
                    lowerNearWalls = data.lowerNearWalls,
                    hideOpeningsOnLoweredWalls = data.hideOpeningsOnLoweredWalls,
                    objectsVisible = data.objectsVisible,
                    edgeOutline = data.edgeOutline,
                    hideLightSources = data.hideLightSources,
                });
                RoomView.ResetToDefaults();
                PhotoView.ResetToPhotoDefaults();
                return;
            }

            NormalView.CopyFrom(data.viewNormal);
            RoomView.CopyFrom(data.viewRoom);
            PhotoView.CopyFrom(data.viewPhoto);
        }
    }
}
