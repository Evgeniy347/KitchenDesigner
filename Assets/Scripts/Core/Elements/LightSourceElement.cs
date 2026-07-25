using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Источник света: небольшой «плафон» с точечным светом внутри.
    /// Перемещается как обычная деталь; глобальная кнопка тулбара включает и
    /// выключает свет у всех источников сразу.</summary>
    public class LightSourceElement : KitchenElement
    {
        public const int DEFAULT_SIZE_MM = 150;
        public const float LIGHT_INTENSITY = 1.4f;

        // Параметры лампы по умолчанию: тёплый LED ~ «60 Вт лампы накаливания».
        public const int DEFAULT_TEMPERATURE_K = 3000;
        public const int DEFAULT_POWER_W = 9;
        public const int MIN_TEMPERATURE_K = 1500;
        public const int MAX_TEMPERATURE_K = 10000;

        // Рассеивание, % → радиус освещения. 0 % = точечно и жёстко (ближнее
        // ярко, дальнее черно), 100 % = широкий мягкий разлёт по комнате.
        public const int DEFAULT_DIFFUSION_PCT = 50;
        private const float MinRangeUnits = 2f;
        private const float MaxRangeUnits = 16f;

        // На сколько опустить источник от центра плафона (мир, м): вплотную к
        // потолку свет по 1/r² даёт пересвет — небольшой отступ убирает
        // «выжженное» пятно на потолке.
        private const float LightDropWorld = 0.12f;

        // Распределение потока плафона: глухой купол сверху → основной свет в
        // нижнюю полусферу (широкий прожектор вниз). Купол свет НЕ теряет, а
        // отражает вниз — поэтому весь поток мощности идёт в нижний прожектор, а
        // вверх уходит лишь малая утечка (узкий тусклый прожектор вверх) для
        // лёгкой подсветки потолка. Доля утечки — настраиваемый параметр лампы.
        private const float UpRangeFraction = 0.5f;
        private const float UpConeFraction = 0.66f;   // верхний конус уже нижнего

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

        // LED ~110 лм/Вт; делитель подобран так, чтобы 9 Вт ≈ прежняя яркость 1.4.
        private const float LumensPerWatt = 110f;
        private const float LumensPerIntensityUnit = 700f;

        // Глобальный выключатель: применяется ко ВСЕМ источникам (кнопка «Свет»).
        private static bool _globalOn = true;
        public static bool GlobalOn => _globalOn;

        [SerializeField] private int _temperatureK = DEFAULT_TEMPERATURE_K;
        [SerializeField] private int _powerW = DEFAULT_POWER_W;
        [SerializeField] private int _diffusionPct = DEFAULT_DIFFUSION_PCT;
        [SerializeField] private int _upLightPct = DEFAULT_UP_PCT;
        [SerializeField] private int _beamAngleDeg = DEFAULT_BEAM_DEG;

        private Light? _light;    // главный прожектор вниз
        private Light? _upLight;  // слабая подсветка потолка вверх
        private Material? _plafondMat; // собственный эмиссивный материал плафона
        public Light? PointLight => _light;
        public Light? UpLight => _upLight;

        /// <summary>Цветовая температура, K (тёплый ↔ холодный).</summary>
        public int TemperatureK
        {
            get => _temperatureK;
            set { _temperatureK = Mathf.Clamp(value, MIN_TEMPERATURE_K, MAX_TEMPERATURE_K); ApplyLightParams(); }
        }

        /// <summary>Мощность лампы, Вт (LED). Определяет яркость.</summary>
        public int PowerW
        {
            get => _powerW;
            set { _powerW = Mathf.Max(0, value); ApplyLightParams(); }
        }

        /// <summary>Рассеивание, % — как широко свет расходится (радиус).</summary>
        public int DiffusionPct
        {
            get => _diffusionPct;
            set { _diffusionPct = Mathf.Clamp(value, 0, 100); ApplyLightParams(); }
        }

        /// <summary>Свет вверх, % — доля потока, уходящая на подсветку потолка
        /// (утечка сквозь глухой купол). 0 = весь свет вниз.</summary>
        public int UpLightPct
        {
            get => _upLightPct;
            set { _upLightPct = Mathf.Clamp(value, 0, MAX_UP_PCT); ApplyLightParams(); }
        }

        /// <summary>Угол пучка (радиус рассеивания), ° — ширина конуса света.
        /// Влияет и на нижний прожектор, и на верхнюю подсветку.</summary>
        public int BeamAngleDeg
        {
            get => _beamAngleDeg;
            set { _beamAngleDeg = Mathf.Clamp(value, MIN_BEAM_DEG, MAX_BEAM_DEG); ApplyLightParams(); }
        }

        public static void SetGlobalOn(bool on)
        {
            _globalOn = on;
            // Поиск по сцене, а не свой список: EditMode-тесты не гоняют
            // OnEnable/OnDisable, а лишних источников единицы.
            foreach (var ls in Object.FindObjectsByType<LightSourceElement>(FindObjectsSortMode.None))
                ls.SyncLightState();
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
            light.shadows = LightShadows.None; // тени от ламп дороги; свет отражаем через GI
            return light;
        }

        /// <summary>Пересчитать цвет (по температуре), яркость (по мощности),
        /// радиус (по рассеиванию), направление потока (в основном вниз, чуть
        /// вверх), опустить источник от потолка и обновить свечение плафона.
        /// Плафон — собственный материал лампы, переживает сброс при загрузке.</summary>
        public void ApplyLightParams()
        {
            Color rgb = Mathf.CorrelatedColorTemperatureToRGB(_temperatureK);
            float intensity = _powerW * LumensPerWatt / LumensPerIntensityUnit;
            float range = Mathf.Lerp(MinRangeUnits, MaxRangeUnits, _diffusionPct / 100f);
            // Отступ от потолка задаём в мире, компенсируя масштаб корня.
            float sy = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 1e-3f);
            var drop = new Vector3(0f, -LightDropWorld / sy, 0f);

            float downAngle = _beamAngleDeg;
            float upAngle = Mathf.Clamp(_beamAngleDeg * UpConeFraction, MIN_BEAM_DEG, MAX_BEAM_DEG);

            if (_light != null)
            {
                _light.color = rgb;
                _light.intensity = intensity;
                _light.range = range;
                _light.spotAngle = downAngle;
                _light.innerSpotAngle = downAngle * 0.7f;
                _light.transform.localPosition = drop;
                _light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // поток вниз
            }

            if (_upLight != null)
            {
                _upLight.color = rgb;
                _upLight.intensity = intensity * (_upLightPct / 100f);  // утечка сквозь купол
                _upLight.range = range * UpRangeFraction;
                _upLight.spotAngle = upAngle;
                _upLight.innerSpotAngle = upAngle * 0.7f;
                _upLight.transform.localPosition = drop;
                _upLight.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // поток вверх
            }

            ApplyPlafondEmission(rgb);
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
            float emiss = 0.6f + _powerW * 0.06f;
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

        /// <summary>Привести оба источника к текущему глобальному состоянию.</summary>
        public void SyncLightState()
        {
            if (_light != null) _light.enabled = _globalOn;
            if (_upLight != null) _upLight.enabled = _globalOn;
        }
    }
}
