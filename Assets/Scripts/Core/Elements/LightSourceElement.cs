using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Форма светового потока лампы. «Плафон» — широкий прожектор вниз
    /// плюс утечка вверх (как накладной светильник), «шар» — точечный источник,
    /// светящий во все стороны: самый мягкий и равномерный вариант.</summary>
    public enum LampShape
    {
        Plafond = 0,
        Sphere = 1,
    }

    /// <summary>Тени от конкретной лампы. Тени точечных источников дороги,
    /// поэтому по умолчанию их нет — свет «заворачивается» через ambient и SSGI.</summary>
    public enum LampShadow
    {
        None = 0,
        Hard = 1,
        Soft = 2,
    }

    /// <summary>Источник света: небольшой «плафон» с точечным светом внутри.
    /// Перемещается как обычная деталь; глобальная кнопка тулбара включает и
    /// выключает свет у всех источников сразу.
    ///
    /// Вся светотехника лампы — параметры, а не константы: свет в фоторежиме
    /// настраивается «на глаз», и любая зашитая цифра рано или поздно оказывается
    /// не той. Значения по умолчанию повторяют прежнее зашитое поведение.</summary>
    public class LightSourceElement : KitchenElement
    {

        public override string DisplayTypeName => "Источник света";
        public const int DEFAULT_SIZE_MM = 150;

        // Параметры лампы по умолчанию: тёплый LED ~ «60 Вт лампы накаливания».
        public const int DEFAULT_TEMPERATURE_K = 3000;
        public const int DEFAULT_POWER_W = 9;
        public const int MIN_TEMPERATURE_K = 1500;
        public const int MAX_TEMPERATURE_K = 10000;

        // Рассеивание, % → радиус освещения между RangeMinMM и RangeMaxMM.
        // 0 % = точечно и жёстко (ближнее ярко, дальнее черно), 100 % = широкий
        // мягкий разлёт по комнате.
        public const int DEFAULT_DIFFUSION_PCT = 50;

        // Границы радиуса (мм) — концы шкалы «Рассеивание».
        public const int DEFAULT_RANGE_MIN_MM = 2000;
        public const int DEFAULT_RANGE_MAX_MM = 16000;
        public const int MIN_RANGE_MM = 100;
        public const int MAX_RANGE_MM = 60000;

        // На сколько опустить источник от центра плафона (мм): вплотную к
        // потолку свет по 1/r² даёт пересвет — небольшой отступ убирает
        // «выжженное» пятно на потолке.
        public const int DEFAULT_DROP_MM = 120;
        public const int MAX_DROP_MM = 1000;

        // Распределение потока плафона: глухой купол сверху → основной свет в
        // нижнюю полусферу (широкий прожектор вниз). Купол свет НЕ теряет, а
        // отражает вниз — поэтому весь поток мощности идёт в нижний прожектор, а
        // вверх уходит лишь малая утечка (узкий тусклый прожектор вверх) для
        // лёгкой подсветки потолка.
        public const int DEFAULT_UP_RANGE_PCT = 50;   // радиус верхнего, % от нижнего
        public const int DEFAULT_UP_CONE_PCT = 66;    // верхний конус уже нижнего
        public const int MAX_FRACTION_PCT = 200;

        // Утечка вверх, % от нижнего потока: 0 = весь свет вниз (полностью
        // отражающий глухой купол), больше — заметнее подсветка потолка.
        public const int DEFAULT_UP_PCT = 8;
        public const int MAX_UP_PCT = 50;

        // Угол пучка (радиус рассеивания), ° — ширина светового конуса. Задаётся
        // для нижнего прожектора, верхний берёт пропорциональную (более узкую)
        // долю. Малый угол = узкое пятно, большой = широкий разлёт по комнате.
        public const int DEFAULT_BEAM_DEG = 150;
        public const int MIN_BEAM_DEG = 20;
        public const int MAX_BEAM_DEG = 175;

        // Мягкость края пучка, %: насколько внутренний конус уже внешнего.
        // 0 % = резкая граница светового пятна, 100 % = плавный градиент от
        // центра к краю. Главный рычаг «жёсткий точечный ↔ мягкий рассеянный».
        public const int DEFAULT_SOFTNESS_PCT = 30;

        // Светоотдача LED, лм/Вт, и калибровка «люмены → интенсивность Unity».
        // Делитель подобран так, чтобы 9 Вт при 110 лм/Вт давали прежнюю 1.4.
        public const int DEFAULT_EFFICACY_LM_PER_W = 110;
        public const int MAX_EFFICACY_LM_PER_W = 400;
        public const int DEFAULT_LUMENS_PER_UNIT = 700;
        public const int MIN_LUMENS_PER_UNIT = 10;
        public const int MAX_LUMENS_PER_UNIT = 10000;

        // Свечение плафона, % от расчётного (база + вклад мощности).
        public const int DEFAULT_GLOW_PCT = 100;
        public const int MAX_GLOW_PCT = 400;
        private const float GlowBase = 0.6f;
        private const float GlowPerWatt = 0.06f;

        // Сила тени лампы, % (когда тени у лампы включены).
        public const int DEFAULT_SHADOW_STRENGTH_PCT = 70;

        public const LampShape DEFAULT_SHAPE = LampShape.Plafond;
        public const LampShadow DEFAULT_SHADOW = LampShadow.None;

        // Глобальный выключатель: применяется ко ВСЕМ источникам (кнопка «Свет»).
        private static bool _globalOn = true;
        public static bool GlobalOn => _globalOn;

        [SerializeField] private int _temperatureK = DEFAULT_TEMPERATURE_K;
        [SerializeField] private int _powerW = DEFAULT_POWER_W;
        [SerializeField] private int _diffusionPct = DEFAULT_DIFFUSION_PCT;
        [SerializeField] private int _upLightPct = DEFAULT_UP_PCT;
        [SerializeField] private int _beamAngleDeg = DEFAULT_BEAM_DEG;
        [SerializeField] private int _softnessPct = DEFAULT_SOFTNESS_PCT;
        [SerializeField] private int _rangeMinMM = DEFAULT_RANGE_MIN_MM;
        [SerializeField] private int _rangeMaxMM = DEFAULT_RANGE_MAX_MM;
        [SerializeField] private int _dropMM = DEFAULT_DROP_MM;
        [SerializeField] private int _upConePct = DEFAULT_UP_CONE_PCT;
        [SerializeField] private int _upRangePct = DEFAULT_UP_RANGE_PCT;
        [SerializeField] private int _efficacyLmPerW = DEFAULT_EFFICACY_LM_PER_W;
        [SerializeField] private int _lumensPerUnit = DEFAULT_LUMENS_PER_UNIT;
        [SerializeField] private int _glowPct = DEFAULT_GLOW_PCT;
        [SerializeField] private int _shadowStrengthPct = DEFAULT_SHADOW_STRENGTH_PCT;
        [SerializeField] private LampShape _shape = DEFAULT_SHAPE;
        [SerializeField] private LampShadow _shadow = DEFAULT_SHADOW;

        private Light? _light;    // главный прожектор вниз
        private Light? _upLight;  // слабая подсветка потолка вверх
        private Material? _plafondMat; // собственный эмиссивный материал плафона
        public Light? PointLight => _light;
        public Light? UpLight => _upLight;

        /// <summary>Цветовая температура, K (тёплый ↔ холодный).</summary>
        [Undoable]
        public int TemperatureK
        {
            get => _temperatureK;
            set { _temperatureK = Mathf.Clamp(value, MIN_TEMPERATURE_K, MAX_TEMPERATURE_K); ApplyLightParams(); }
        }

        /// <summary>Мощность лампы, Вт (LED). Определяет яркость.</summary>
        [Undoable]
        public int PowerW
        {
            get => _powerW;
            set { _powerW = Mathf.Max(0, value); ApplyLightParams(); }
        }

        /// <summary>Рассеивание, % — как широко свет расходится (радиус).</summary>
        [Undoable]
        public int DiffusionPct
        {
            get => _diffusionPct;
            set { _diffusionPct = Mathf.Clamp(value, 0, 100); ApplyLightParams(); }
        }

        /// <summary>Свет вверх, % — доля потока, уходящая на подсветку потолка
        /// (утечка сквозь глухой купол). 0 = весь свет вниз.</summary>
        [Undoable]
        public int UpLightPct
        {
            get => _upLightPct;
            set { _upLightPct = Mathf.Clamp(value, 0, MAX_UP_PCT); ApplyLightParams(); }
        }

        /// <summary>Угол пучка (радиус рассеивания), ° — ширина конуса света.
        /// Влияет и на нижний прожектор, и на верхнюю подсветку.</summary>
        [Undoable]
        public int BeamAngleDeg
        {
            get => _beamAngleDeg;
            set { _beamAngleDeg = Mathf.Clamp(value, MIN_BEAM_DEG, MAX_BEAM_DEG); ApplyLightParams(); }
        }

        /// <summary>Мягкость края пучка, %: 0 — резкая граница пятна,
        /// 100 — свет плавно гаснет от центра к краю конуса.</summary>
        [Undoable]
        public int SoftnessPct
        {
            get => _softnessPct;
            set { _softnessPct = Mathf.Clamp(value, 0, 100); ApplyLightParams(); }
        }

        /// <summary>Радиус при рассеивании 0 %, мм.</summary>
        [Undoable]
        public int RangeMinMM
        {
            get => _rangeMinMM;
            set { _rangeMinMM = Mathf.Clamp(value, MIN_RANGE_MM, MAX_RANGE_MM); ApplyLightParams(); }
        }

        /// <summary>Радиус при рассеивании 100 %, мм.</summary>
        [Undoable]
        public int RangeMaxMM
        {
            get => _rangeMaxMM;
            set { _rangeMaxMM = Mathf.Clamp(value, MIN_RANGE_MM, MAX_RANGE_MM); ApplyLightParams(); }
        }

        /// <summary>Отступ источника вниз от центра плафона, мм.</summary>
        [Undoable]
        public int DropMM
        {
            get => _dropMM;
            set { _dropMM = Mathf.Clamp(value, 0, MAX_DROP_MM); ApplyLightParams(); }
        }

        /// <summary>Верхний конус, % от нижнего.</summary>
        [Undoable]
        public int UpConePct
        {
            get => _upConePct;
            set { _upConePct = Mathf.Clamp(value, 0, MAX_FRACTION_PCT); ApplyLightParams(); }
        }

        /// <summary>Радиус верхней подсветки, % от нижнего.</summary>
        [Undoable]
        public int UpRangePct
        {
            get => _upRangePct;
            set { _upRangePct = Mathf.Clamp(value, 0, MAX_FRACTION_PCT); ApplyLightParams(); }
        }

        /// <summary>Светоотдача лампы, лм/Вт.</summary>
        [Undoable]
        public int EfficacyLmPerW
        {
            get => _efficacyLmPerW;
            set { _efficacyLmPerW = Mathf.Clamp(value, 0, MAX_EFFICACY_LM_PER_W); ApplyLightParams(); }
        }

        /// <summary>Калибровка: сколько люменов приходится на единицу
        /// интенсивности Unity. Больше значение — тусклее вся сцена.</summary>
        [Undoable]
        public int LumensPerUnit
        {
            get => _lumensPerUnit;
            set { _lumensPerUnit = Mathf.Clamp(value, MIN_LUMENS_PER_UNIT, MAX_LUMENS_PER_UNIT); ApplyLightParams(); }
        }

        /// <summary>Свечение плафона, % — насколько ярко светится сам корпус.</summary>
        [Undoable]
        public int GlowPct
        {
            get => _glowPct;
            set { _glowPct = Mathf.Clamp(value, 0, MAX_GLOW_PCT); ApplyLightParams(); }
        }

        /// <summary>Сила тени от лампы, % (при включённых тенях).</summary>
        [Undoable]
        public int ShadowStrengthPct
        {
            get => _shadowStrengthPct;
            set { _shadowStrengthPct = Mathf.Clamp(value, 0, 100); ApplyLightParams(); }
        }

        /// <summary>Форма потока: плафон (вниз + утечка вверх) или шар (во все
        /// стороны). Шар даёт самый мягкий и равномерный свет.</summary>
        [Undoable]
        public LampShape Shape
        {
            get => _shape;
            set { _shape = value; ApplyLightParams(); }
        }

        /// <summary>Тени от этой лампы: нет / жёсткие / мягкие.</summary>
        [Undoable]
        public LampShadow Shadow
        {
            get => _shadow;
            set { _shadow = value; ApplyLightParams(); }
        }

        public static void SetGlobalOn(bool on)
        {
            _globalOn = on;
            // Поиск по сцене, а не свой список: EditMode-тесты не гоняют
            // OnEnable/OnDisable, а лишних источников единицы.
            foreach (var ls in Object.FindObjectsByType<LightSourceElement>(FindObjectsSortMode.None))
                ls.SyncLightState();
        }

        /// <summary>Пересчитать все лампы сцены. Нужен, когда меняется глобальная
        /// настройка, влияющая на лампы (разрешение теней от ламп).</summary>
        public static void RefreshAll()
        {
            foreach (var ls in Object.FindObjectsByType<LightSourceElement>(FindObjectsSortMode.None))
                ls.ApplyLightParams();
        }

        private void OnEnable()
        {
            EnsureLight();
            SyncLightState();
        }

        public void EnsureLight()
        {
            if (_light != null) return;

            _light = CreateChildLight("SpotDown", LightType.Spot);
            _upLight = CreateChildLight("SpotUp", LightType.Spot);
            ApplyLightParams();
        }

        private Light CreateChildLight(string name, LightType type)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(transform, false);
            var light = holder.AddComponent<Light>();
            light.type = type;
            light.shadows = LightShadows.None; // по умолчанию тени от ламп выключены
            return light;
        }

        /// <summary>Пересчитать цвет (по температуре), яркость (по мощности),
        /// радиус (по рассеиванию), направление потока (в основном вниз, чуть
        /// вверх), опустить источник от потолка и обновить свечение плафона.
        /// Плафон — собственный материал лампы, переживает сброс при загрузке.</summary>
        public void ApplyLightParams()
        {
            Color rgb = Mathf.CorrelatedColorTemperatureToRGB(_temperatureK);
            float intensity = _powerW * _efficacyLmPerW / (float)Mathf.Max(1, _lumensPerUnit);
            float range = Mathf.Lerp(_rangeMinMM, _rangeMaxMM, _diffusionPct / 100f) / 1000f;
            // Отступ от потолка задаём в мире, компенсируя масштаб корня.
            float sy = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 1e-3f);
            var drop = new Vector3(0f, -(_dropMM / 1000f) / sy, 0f);

            float downAngle = _beamAngleDeg;
            float upAngle = Mathf.Clamp(_beamAngleDeg * (_upConePct / 100f), MIN_BEAM_DEG, MAX_BEAM_DEG);
            float innerFactor = 1f - _softnessPct / 100f;
            bool sphere = _shape == LampShape.Sphere;

            if (_light != null)
            {
                _light.type = sphere ? LightType.Point : LightType.Spot;
                _light.color = rgb;
                _light.intensity = intensity;
                _light.range = range;
                _light.spotAngle = downAngle;
                _light.innerSpotAngle = downAngle * innerFactor;
                _light.transform.localPosition = drop;
                _light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // поток вниз
                ApplyShadow(_light);
            }

            if (_upLight != null)
            {
                _upLight.color = rgb;
                // Шар светит во все стороны сам — отдельная подсветка потолка не нужна.
                _upLight.intensity = sphere ? 0f : intensity * (_upLightPct / 100f);
                _upLight.range = range * (_upRangePct / 100f);
                _upLight.spotAngle = upAngle;
                _upLight.innerSpotAngle = upAngle * innerFactor;
                _upLight.transform.localPosition = drop;
                _upLight.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // поток вверх
                ApplyShadow(_upLight);
            }

            ApplyPlafondEmission(rgb);
            SyncLightState();
        }

        /// <summary>Тени лампы: режим самой лампы, но глобальная настройка
        /// «тени от ламп» может запретить их всем сразу (они дороги).</summary>
        private void ApplyShadow(Light light)
        {
            var s = KitchenSettings.Instance;
            bool allowed = s == null || s.PhotoLampShadows;
            light.shadows = !allowed || _shadow == LampShadow.None
                ? LightShadows.None
                : (_shadow == LampShadow.Soft ? LightShadows.Soft : LightShadows.Hard);
            light.shadowStrength = _shadowStrengthPct / 100f;
        }

        private void ApplyPlafondEmission(Color rgb)
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr == null) return;

            if (_plafondMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) return;
                _plafondMat = new Material(shader);
                _plafondMat.EnableKeyword("_EMISSION");
            }

            // Свечение растёт с мощностью, цвет — по температуре.
            float emiss = (GlowBase + _powerW * GlowPerWatt) * (_glowPct / 100f);
            _plafondMat.SetColor("_BaseColor", rgb);
            _plafondMat.SetColor("_EmissionColor", rgb * emiss);
            if (mr.sharedMaterial != _plafondMat) mr.sharedMaterial = _plafondMat;
        }

        private void OnDestroy()
        {
            // Производный OnDestroy скрывает приватный базовый — разрегистрацию
            // из PartRegistry дублируем явно (как в DoorElement).
            PartRegistry.Unregister(this);
            if (_plafondMat != null)
            {
                if (Application.isPlaying) Object.Destroy(_plafondMat);
                else Object.DestroyImmediate(_plafondMat);
                _plafondMat = null;
            }
        }

        /// <summary>Привести оба источника к текущему глобальному состоянию.
        /// У «шара» верхний источник не нужен вовсе.</summary>
        public void SyncLightState()
        {
            if (_light != null) _light.enabled = _globalOn;
            if (_upLight != null) _upLight.enabled = _globalOn && _shape != LampShape.Sphere;
        }
    }
}
