using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Пресет качества фоторежима. Определяет тяжесть эффектов
    /// (тени, MSAA, render scale) — от «Низкое» для слабого железа до
    /// «Высокое» для дискретных карт уровня GTX 1060.</summary>
    public enum PhotoQualityPreset
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    [CreateAssetMenu(fileName = "KitchenSettings", menuName = "KitchenDesigner/KitchenSettings")]
    public class KitchenSettings : ScriptableObject
    {
        private static KitchenSettings? _instance;
        public static KitchenSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<KitchenSettings>("KitchenSettings");
                return _instance!;
            }
        }

        [SerializeField] private int _gridStep = 18;
        [SerializeField] private bool _gridEnabled = true;
        [SerializeField] private bool _snapEnabled = true;
        [SerializeField] private float _snapThreshold = 50f;
        [SerializeField] private bool _blockOnViolation = true;
        [SerializeField] private bool _autoSave = true;
        [SerializeField] private int _autoSaveInterval = 60;
        [SerializeField] private bool _spatialGrid = false;
        [SerializeField] private bool _windowedMode = true;
        [SerializeField] private bool _edgeOutline = true;
        [SerializeField] private bool _wallsEnabled = true;
        [SerializeField] private bool _lowerNearWalls = true;
        [SerializeField] private bool _cameraPanFree = false;

        // ── Фоторежим ──────────────────────────────────────
        // Активность фоторежима — рантайм-состояние (PhotoMode.Active), НЕ хранится:
        // проект открывается в обычном рабочем режиме. Здесь только настройки качества.
        [SerializeField] private PhotoQualityPreset _photoQuality = PhotoQualityPreset.High;
        [SerializeField] private bool _photoShadows = true;
        [SerializeField] private bool _photoAntiAliasing = true;
        [SerializeField] private bool _photoAmbientOcclusion = true;
        [SerializeField] private bool _photoBloom = true;
        [SerializeField] private bool _photoVignette = true;
        [SerializeField] private bool _photoCeiling = true;

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

        public bool EdgeOutline
        {
            get => _edgeOutline;
            set => _edgeOutline = value;
        }

        public bool WallsEnabled
        {
            get => _wallsEnabled;
            set => _wallsEnabled = value;
        }

        public bool LowerNearWalls
        {
            get => _lowerNearWalls;
            set => _lowerNearWalls = value;
        }

        public bool CameraPanFree
        {
            get => _cameraPanFree;
            set => _cameraPanFree = value;
        }

        public PhotoQualityPreset PhotoQuality
        {
            get => _photoQuality;
            set => _photoQuality = value;
        }

        public bool PhotoShadows
        {
            get => _photoShadows;
            set => _photoShadows = value;
        }

        public bool PhotoAntiAliasing
        {
            get => _photoAntiAliasing;
            set => _photoAntiAliasing = value;
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

        /// <summary>Значения «из коробки» — те же, что в инициализаторах полей.
        /// Инициализаторы срабатывают только при СОЗДАНИИ ассета, а Instance
        /// грузится из Resources с уже сохранённым состоянием, поэтому сброс
        /// нужен явный. Настройки — глобальный синглтон, и тест, который их
        /// правит, обязан начинать с известного состояния и возвращать прежнее.</summary>
        public void ResetToDefaults()
        {
            _gridStep = 18;
            _gridEnabled = true;
            _snapEnabled = true;
            _snapThreshold = 50f;
            _blockOnViolation = true;
            _autoSave = true;
            _autoSaveInterval = 60;
            _spatialGrid = false;
            _windowedMode = true;
            _edgeOutline = true;
            _wallsEnabled = true;
            _lowerNearWalls = true;
            _cameraPanFree = false;
            _photoQuality = PhotoQualityPreset.High;
            _photoShadows = true;
            _photoAntiAliasing = true;
            _photoAmbientOcclusion = true;
            _photoBloom = true;
            _photoVignette = true;
            _photoCeiling = true;
        }

        public KitchenSettingsData ToData()
        {
            return new KitchenSettingsData
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
                edgeOutline = _edgeOutline,
                wallsEnabled = _wallsEnabled,
                lowerNearWalls = _lowerNearWalls,
                cameraPanFree = _cameraPanFree,
                photoQuality = (int)_photoQuality,
                photoShadows = _photoShadows,
                photoAntiAliasing = _photoAntiAliasing,
                photoAmbientOcclusion = _photoAmbientOcclusion,
                photoBloom = _photoBloom,
                photoVignette = _photoVignette,
                photoCeiling = _photoCeiling
            };
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
            _edgeOutline = data.edgeOutline;
            _wallsEnabled = data.wallsEnabled;
            _lowerNearWalls = data.lowerNearWalls;
            _cameraPanFree = data.cameraPanFree;
            _photoQuality = (PhotoQualityPreset)Mathf.Clamp(data.photoQuality, 0, 2);
            _photoShadows = data.photoShadows;
            _photoAntiAliasing = data.photoAntiAliasing;
            _photoAmbientOcclusion = data.photoAmbientOcclusion;
            _photoBloom = data.photoBloom;
            _photoVignette = data.photoVignette;
            _photoCeiling = data.photoCeiling;
        }

        /// <summary>Возвращает текущий JSON настроек (для снапшот-тестов).</summary>
        public string GetSettingsJson()
        {
            var data = new SettingsData
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
                edgeOutline = _edgeOutline,
                wallsHidden = !_wallsEnabled,
                lowerNearWalls = _lowerNearWalls,
                cameraPanFree = _cameraPanFree,
                photoQuality = (int)_photoQuality,
                photoShadows = _photoShadows,
                photoAntiAliasing = _photoAntiAliasing,
                photoAmbientOcclusion = _photoAmbientOcclusion,
                photoBloom = _photoBloom,
                photoVignette = _photoVignette,
                photoCeiling = _photoCeiling
            };
            return JsonUtility.ToJson(data, true);
        }

        [System.Serializable]
        private struct SettingsData
        {
            public int gridStep;
            public bool gridEnabled;
            public bool snapEnabled;
            public float snapThreshold;
            public bool blockOnViolation;
            public bool autoSave;
            public int autoSaveInterval;
            public bool spatialGrid;
            public bool windowedMode;
            public bool edgeOutline;
            public bool wallsHidden;   // инверсия: старые сейвы (false) → стены включены
            public bool lowerNearWalls;
            public bool cameraPanFree;
            public int photoQuality;
            public bool photoShadows;
            public bool photoAntiAliasing;
            public bool photoAmbientOcclusion;
            public bool photoBloom;
            public bool photoVignette;
            public bool photoCeiling;
        }
    }
}
