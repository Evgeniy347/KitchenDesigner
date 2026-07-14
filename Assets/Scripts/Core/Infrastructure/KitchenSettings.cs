using UnityEngine;

namespace KitchenDesigner.Core
{
    [CreateAssetMenu(fileName = "KitchenSettings", menuName = "KitchenDesigner/KitchenSettings")]
    public class KitchenSettings : ScriptableObject
    {
        private static KitchenSettings _instance = null!;
        public static KitchenSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<KitchenSettings>("KitchenSettings");
                return _instance;
            }
        }

        [SerializeField] private int _gridStep = 1;
        [SerializeField] private bool _gridEnabled = true;
        [SerializeField] private bool _snapEnabled = true;
        [SerializeField] private float _snapThreshold = 50f;
        [SerializeField] private bool _blockOnViolation = true;
        [SerializeField] private bool _autoSave = false;
        [SerializeField] private int _autoSaveInterval = 60;
        [SerializeField] private bool _spatialGrid = false;
        [SerializeField] private bool _windowedMode = true;
        [SerializeField] private bool _edgeOutline = false;
        [SerializeField] private bool _wallsEnabled = true;
        [SerializeField] private bool _lowerNearWalls = false;

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

        public void Save()
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
                lowerNearWalls = _lowerNearWalls
            };
            var json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString("KitchenSettings", json);
            PlayerPrefs.Save();
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
                lowerNearWalls = _lowerNearWalls
            };
            return JsonUtility.ToJson(data, true);
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey("KitchenSettings"))
                return;
            var json = PlayerPrefs.GetString("KitchenSettings");
            if (string.IsNullOrEmpty(json)) return;
            var data = JsonUtility.FromJson<SettingsData>(json);
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
            _wallsEnabled = !data.wallsHidden;
            _lowerNearWalls = data.lowerNearWalls;
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
        }
    }
}
