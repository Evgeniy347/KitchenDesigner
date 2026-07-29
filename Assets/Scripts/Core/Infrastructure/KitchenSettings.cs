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
        High = 2,
        // «Свои настройки»: комбинация тумблеров не совпадает ни с одним пресетом.
        // Встаёт автоматически, когда пользователь меняет опцию вручную.
        Custom = 3
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
        // Что показывать — своё для каждого режима работы: погашенные в обычном
        // режиме стены не должны мешать правке помещения и наоборот. Читать эти
        // пресеты напрямую нельзя: режим часть значений форсирует, поэтому и
        // рендер, и UI ходят через ViewResolver.
        [SerializeField] private ViewPreset? _normalView = new ViewPreset();
        [SerializeField] private ViewPreset? _roomView = new ViewPreset();
        [SerializeField] private bool _cameraPanFree = false;
        // Ниже этого процента перекрытие торца соседом считается технологическим
        // (планка, царга, наезд на пару миллиметров) и ошибкой EDG-01 не является.
        [SerializeField] private int _edgePartialThresholdPct = EDGE_PARTIAL_THRESHOLD_DEFAULT_PCT;

        // ── Управление ─────────────────────────────────────
        // Множители к базовым скоростям камеры (1 = как было до настройки).
        [SerializeField] private float _mouseSensitivity = 1f;
        [SerializeField] private float _wasdSpeed = 1f;
        [SerializeField] private float _arrowSpeed = 1f;

        public const float MIN_INPUT_SPEED = 0.1f;
        public const float MAX_INPUT_SPEED = 3f;

        // ── Фоторежим ──────────────────────────────────────
        // Активность фоторежима — рантайм-состояние (PhotoMode.Active), НЕ хранится:
        // проект открывается в обычном рабочем режиме. Здесь только настройки качества.
        [SerializeField] private PhotoQualityPreset _photoQuality = PhotoQualityPreset.High;
        [SerializeField] private bool _photoShadows = true;
        [SerializeField] private bool _photoSoftShadows = true;
        [SerializeField] private bool _photoAntiAliasing = true;
        [SerializeField] private bool _photoSupersampling = true;
        [SerializeField] private bool _photoAmbientOcclusion = true;
        [SerializeField] private bool _photoBloom = true;
        [SerializeField] private bool _photoVignette = true;
        [SerializeField] private bool _photoCeiling = true;
        [SerializeField] private bool _photoSSGI = true;

        // ── Свет фоторежима ────────────────────────────────
        // Всё, что раньше было зашито в PhotoQualityController. Дефолты равны
        // прежним константам, поэтому картинка «из коробки» не меняется.
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

        // Границы «крутилок» света — они же диапазоны ползунков в настройках.
        public const int PHOTO_AMBIENT_DEFAULT_PCT = 100;
        public const int PHOTO_AMBIENT_MAX_PCT = 400;
        public const int PHOTO_FLOOR_BOUNCE_DEFAULT_PCT = 100;
        public const int PHOTO_FLOOR_BOUNCE_MAX_PCT = 300;
        // Экспозиция в сотых EV: −300 = −3 EV (темнее), +300 = +3 EV (светлее).
        public const int PHOTO_EXPOSURE_DEFAULT_PCT = 0;
        public const int PHOTO_EXPOSURE_MIN_PCT = -300;
        public const int PHOTO_EXPOSURE_MAX_PCT = 300;
        public const int PHOTO_CONTRAST_DEFAULT_PCT = 8;
        public const int PHOTO_SATURATION_DEFAULT_PCT = 6;
        public const int PHOTO_COLOR_MIN_PCT = -100;
        public const int PHOTO_COLOR_MAX_PCT = 100;
        public const int PHOTO_BLOOM_DEFAULT_PCT = 35;
        public const int PHOTO_BLOOM_MAX_PCT = 300;
        public const int PHOTO_BLOOM_THRESHOLD_DEFAULT_PCT = 110;
        public const int PHOTO_BLOOM_THRESHOLD_MAX_PCT = 500;
        public const int PHOTO_VIGNETTE_DEFAULT_PCT = 22;
        public const int PHOTO_SUN_SHADOW_DEFAULT_PCT = 100;
        public const int PHOTO_SHADOW_DISTANCE_DEFAULT_M = 22;
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

        /// <summary>Пресет вида обычного режима. Фоторежим правит его же —
        /// он «обычный + фоторендер». Эффективные значения — через
        /// <see cref="ViewResolver"/>, здесь лежит «что хотел пользователь».</summary>
        public ViewPreset NormalView => _normalView ??= new ViewPreset();

        /// <summary>Пресет вида режима «помещение».</summary>
        public ViewPreset RoomView => _roomView ??= new ViewPreset();

        /// <summary>Нижний порог «частичного перекрытия» торца, %. Кромку клеят на
        /// весь торец, поэтому наехавший сосед — ошибка (EDG-01); но планка или
        /// царга, задевающая торец на пару процентов, — нормальная конструкция, и
        /// без порога такой шум забивал отчёт. 0 — сообщать о любом наезде.</summary>
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

        /// <summary>Чувствительность мыши (орбита и панорамирование), множитель.</summary>
        public float MouseSensitivity
        {
            get => _mouseSensitivity;
            set => _mouseSensitivity = Mathf.Clamp(value, MIN_INPUT_SPEED, MAX_INPUT_SPEED);
        }

        /// <summary>Скорость перемещения камеры на WASD, множитель.</summary>
        public float WasdSpeed
        {
            get => _wasdSpeed;
            set => _wasdSpeed = Mathf.Clamp(value, MIN_INPUT_SPEED, MAX_INPUT_SPEED);
        }

        /// <summary>Скорость орбиты на стрелках ←→↑↓, множитель.</summary>
        public float ArrowSpeed
        {
            get => _arrowSpeed;
            set => _arrowSpeed = Mathf.Clamp(value, MIN_INPUT_SPEED, MAX_INPUT_SPEED);
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

        /// <summary>Экранное непрямое освещение (SSGI) в фоторежиме.</summary>
        public bool PhotoSSGI
        {
            get => _photoSSGI;
            set => _photoSSGI = value;
        }

        /// <summary>Окружающий (заполняющий) свет, % — множитель к ambient.
        /// 0 = только прямой свет и глухая тень, 200 % = мягкая «пасмурная»
        /// подсветка без чёрных провалов.</summary>
        public int PhotoAmbientPct
        {
            get => _photoAmbientPct;
            set => _photoAmbientPct = Mathf.Clamp(value, 0, PHOTO_AMBIENT_MAX_PCT);
        }

        /// <summary>Отскок от пола, % — насколько цвет пола подсвечивает низ
        /// полок и столешниц.</summary>
        public int PhotoFloorBouncePct
        {
            get => _photoFloorBouncePct;
            set => _photoFloorBouncePct = Mathf.Clamp(value, 0, PHOTO_FLOOR_BOUNCE_MAX_PCT);
        }

        /// <summary>Экспозиция кадра в сотых EV (−300…+300 = −3…+3 EV).</summary>
        public int PhotoExposurePct
        {
            get => _photoExposurePct;
            set => _photoExposurePct = Mathf.Clamp(value, PHOTO_EXPOSURE_MIN_PCT, PHOTO_EXPOSURE_MAX_PCT);
        }

        /// <summary>Контраст пост-обработки, %.</summary>
        public int PhotoContrastPct
        {
            get => _photoContrastPct;
            set => _photoContrastPct = Mathf.Clamp(value, PHOTO_COLOR_MIN_PCT, PHOTO_COLOR_MAX_PCT);
        }

        /// <summary>Насыщенность пост-обработки, %.</summary>
        public int PhotoSaturationPct
        {
            get => _photoSaturationPct;
            set => _photoSaturationPct = Mathf.Clamp(value, PHOTO_COLOR_MIN_PCT, PHOTO_COLOR_MAX_PCT);
        }

        /// <summary>Сила свечения (bloom), %.</summary>
        public int PhotoBloomPct
        {
            get => _photoBloomPct;
            set => _photoBloomPct = Mathf.Clamp(value, 0, PHOTO_BLOOM_MAX_PCT);
        }

        /// <summary>Порог свечения, % яркости: ниже него bloom не появляется.</summary>
        public int PhotoBloomThresholdPct
        {
            get => _photoBloomThresholdPct;
            set => _photoBloomThresholdPct = Mathf.Clamp(value, 0, PHOTO_BLOOM_THRESHOLD_MAX_PCT);
        }

        /// <summary>Сила виньетки, %.</summary>
        public int PhotoVignettePct
        {
            get => _photoVignettePct;
            set => _photoVignettePct = Mathf.Clamp(value, 0, 100);
        }

        /// <summary>Сила тени от солнца, %. Меньше — мягче и «воздушнее».</summary>
        public int PhotoSunShadowStrengthPct
        {
            get => _photoSunShadowStrengthPct;
            set => _photoSunShadowStrengthPct = Mathf.Clamp(value, 0, 100);
        }

        /// <summary>Дальность прорисовки теней, м.</summary>
        public int PhotoShadowDistanceM
        {
            get => _photoShadowDistanceM;
            set => _photoShadowDistanceM = Mathf.Clamp(value, PHOTO_SHADOW_DISTANCE_MIN_M, PHOTO_SHADOW_DISTANCE_MAX_M);
        }

        /// <summary>Разрешены ли тени от ламп. Режим тени задаётся у каждой
        /// лампы отдельно, а этот тумблер запрещает их всем разом: тени
        /// точечных источников — самая дорогая часть кадра.</summary>
        public bool PhotoLampShadows
        {
            get => _photoLampShadows;
            set => _photoLampShadows = value;
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
            NormalView.ResetToDefaults();
            RoomView.ResetToDefaults();
            _cameraPanFree = false;
            _edgePartialThresholdPct = EDGE_PARTIAL_THRESHOLD_DEFAULT_PCT;
            _mouseSensitivity = 1f;
            _wasdSpeed = 1f;
            _arrowSpeed = 1f;
            _photoQuality = PhotoQualityPreset.High;
            _photoShadows = true;
            _photoSoftShadows = true;
            _photoAntiAliasing = true;
            _photoSupersampling = true;
            _photoAmbientOcclusion = true;
            _photoBloom = true;
            _photoVignette = true;
            _photoCeiling = true;
            _photoSSGI = true;
            _photoAmbientPct = PHOTO_AMBIENT_DEFAULT_PCT;
            _photoFloorBouncePct = PHOTO_FLOOR_BOUNCE_DEFAULT_PCT;
            _photoExposurePct = PHOTO_EXPOSURE_DEFAULT_PCT;
            _photoContrastPct = PHOTO_CONTRAST_DEFAULT_PCT;
            _photoSaturationPct = PHOTO_SATURATION_DEFAULT_PCT;
            _photoBloomPct = PHOTO_BLOOM_DEFAULT_PCT;
            _photoBloomThresholdPct = PHOTO_BLOOM_THRESHOLD_DEFAULT_PCT;
            _photoVignettePct = PHOTO_VIGNETTE_DEFAULT_PCT;
            _photoSunShadowStrengthPct = PHOTO_SUN_SHADOW_DEFAULT_PCT;
            _photoShadowDistanceM = PHOTO_SHADOW_DISTANCE_DEFAULT_M;
            _photoLampShadows = true;
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
                viewSchema = KitchenSettingsData.CURRENT_VIEW_SCHEMA,
                viewNormal = NormalView.Clone(),
                viewRoom = RoomView.Clone(),
                // Плоские поля больше не читаются, но пишутся из «обычного»
                // пресета: сборка без пресетов откроет такой проект осмысленно,
                // а не с чужими значениями инициализаторов.
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
            _photoQuality = (PhotoQualityPreset)Mathf.Clamp(data.photoQuality, 0, 3);
            _photoShadows = data.photoShadows;
            _photoSoftShadows = data.photoSoftShadows;
            _photoAntiAliasing = data.photoAntiAliasing;
            _photoSupersampling = data.photoSupersampling;
            _photoAmbientOcclusion = data.photoAmbientOcclusion;
            _photoBloom = data.photoBloom;
            _photoVignette = data.photoVignette;
            _photoCeiling = data.photoCeiling;
            _photoSSGI = data.photoSSGI;
            PhotoAmbientPct = data.photoAmbientPct;
            PhotoFloorBouncePct = data.photoFloorBouncePct;
            PhotoExposurePct = data.photoExposurePct;
            PhotoContrastPct = data.photoContrastPct;
            PhotoSaturationPct = data.photoSaturationPct;
            PhotoBloomPct = data.photoBloomPct;
            PhotoBloomThresholdPct = data.photoBloomThresholdPct;
            PhotoVignettePct = data.photoVignettePct;
            PhotoSunShadowStrengthPct = data.photoSunShadowStrengthPct;
            PhotoShadowDistanceM = data.photoShadowDistanceM;
            _photoLampShadows = data.photoLampShadows;
        }

        /// <summary>Пресеты вида появились позже плоских флагов. У старого проекта
        /// (viewSchema = 0) единственный набор флагов был общим на все режимы —
        /// кладём его в «обычный», а «помещение» получает значения из коробки.</summary>
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
                return;
            }

            NormalView.CopyFrom(data.viewNormal);
            RoomView.CopyFrom(data.viewRoom);
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
                viewNormal = NormalView.Clone(),
                viewRoom = RoomView.Clone(),
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
            // Пресеты вида — по одному на режим («обычный», «помещение»).
            public ViewPreset? viewNormal;
            public ViewPreset? viewRoom;
            public bool cameraPanFree;
            public int edgePartialThresholdPct;
            public float mouseSensitivity;
            public float wasdSpeed;
            public float arrowSpeed;
            public int photoQuality;
            public bool photoShadows;
            public bool photoSoftShadows;
            public bool photoAntiAliasing;
            public bool photoSupersampling;
            public bool photoAmbientOcclusion;
            public bool photoBloom;
            public bool photoVignette;
            public bool photoCeiling;
            public bool photoSSGI;
            public int photoAmbientPct;
            public int photoFloorBouncePct;
            public int photoExposurePct;
            public int photoContrastPct;
            public int photoSaturationPct;
            public int photoBloomPct;
            public int photoBloomThresholdPct;
            public int photoVignettePct;
            public int photoSunShadowStrengthPct;
            public int photoShadowDistanceM;
            public bool photoLampShadows;
        }
    }
}
