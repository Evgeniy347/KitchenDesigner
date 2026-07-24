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
        // потолку точечный свет по 1/r² даёт пересвет — небольшой отступ убирает
        // «выжженное» пятно на потолке.
        private const float LightDropWorld = 0.12f;

        // LED ~110 лм/Вт; делитель подобран так, чтобы 9 Вт ≈ прежняя яркость 1.4.
        private const float LumensPerWatt = 110f;
        private const float LumensPerIntensityUnit = 700f;

        // Глобальный выключатель: применяется ко ВСЕМ источникам (кнопка «Свет»).
        private static bool _globalOn = true;
        public static bool GlobalOn => _globalOn;

        [SerializeField] private int _temperatureK = DEFAULT_TEMPERATURE_K;
        [SerializeField] private int _powerW = DEFAULT_POWER_W;
        [SerializeField] private int _diffusionPct = DEFAULT_DIFFUSION_PCT;

        private Light? _light;
        private Material? _plafondMat; // собственный эмиссивный материал плафона
        public Light? PointLight => _light;

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

            var holder = new GameObject("PointLight");
            holder.transform.SetParent(transform, false);
            _light = holder.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.shadows = LightShadows.None;         // тени от ламп дороги; свет отражаем через GI
            ApplyLightParams();
        }

        /// <summary>Пересчитать цвет (по температуре), яркость (по мощности),
        /// радиус (по рассеиванию), опустить источник от потолка и обновить
        /// свечение плафона. Плафон — собственный материал лампы, поэтому
        /// переживает сброс материала при загрузке/валидации.</summary>
        public void ApplyLightParams()
        {
            Color rgb = Mathf.CorrelatedColorTemperatureToRGB(_temperatureK);

            if (_light != null)
            {
                _light.color = rgb;
                _light.intensity = _powerW * LumensPerWatt / LumensPerIntensityUnit;
                _light.range = Mathf.Lerp(MinRangeUnits, MaxRangeUnits, _diffusionPct / 100f);

                // Отступ от потолка задаём в мире, компенсируя масштаб корня.
                float sy = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 1e-3f);
                _light.transform.localPosition = new Vector3(0f, -LightDropWorld / sy, 0f);
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

        /// <summary>Привести Light к текущему глобальному состоянию.</summary>
        public void SyncLightState()
        {
            if (_light != null)
                _light.enabled = _globalOn;
        }
    }
}
