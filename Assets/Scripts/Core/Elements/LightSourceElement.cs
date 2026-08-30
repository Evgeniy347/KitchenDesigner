using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum LampShape
    {
        Plafond = 0,
        Sphere = 1,
    }

    public enum LampShadow
    {
        None = 0,
        Hard = 1,
        Soft = 2,
    }

    public class LightSourceElement : KitchenElement
    {

        public override string DisplayTypeName => "Источник света";

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;
        public const int DEFAULT_SIZE_MM = 150;

        public const int DEFAULT_TEMPERATURE_K = 3000;
        public const int DEFAULT_POWER_W = 9;
        public const int MIN_TEMPERATURE_K = 1500;
        public const int MAX_TEMPERATURE_K = 10000;

        public const int DEFAULT_DIFFUSION_PCT = 50;

        public const int DEFAULT_RANGE_MIN_MM = 2000;
        public const int DEFAULT_RANGE_MAX_MM = 16000;
        public const int MIN_RANGE_MM = 100;
        public const int MAX_RANGE_MM = 60000;

        public const int DEFAULT_DROP_MM = 120;
        public const int MAX_DROP_MM = 1000;

        public const int DEFAULT_UP_RANGE_PCT = 50;
        public const int DEFAULT_UP_CONE_PCT = 66;
        public const int MAX_FRACTION_PCT = 200;

        public const int DEFAULT_UP_PCT = 8;
        public const int MAX_UP_PCT = 50;

        public const int DEFAULT_BEAM_DEG = 150;
        public const int MIN_BEAM_DEG = 20;
        public const int MAX_BEAM_DEG = 175;

        public const int DEFAULT_SOFTNESS_PCT = 30;

        public const int DEFAULT_EFFICACY_LM_PER_W = 110;
        public const int MAX_EFFICACY_LM_PER_W = 400;
        public const int DEFAULT_LUMENS_PER_UNIT = 700;
        public const int MIN_LUMENS_PER_UNIT = 10;
        public const int MAX_LUMENS_PER_UNIT = 10000;

        public const int DEFAULT_GLOW_PCT = 100;
        public const int MAX_GLOW_PCT = 400;
        private const float GlowBase = 0.6f;
        private const float GlowPerWatt = 0.06f;

        public const int DEFAULT_SHADOW_STRENGTH_PCT = 70;

        public const LampShape DEFAULT_SHAPE = LampShape.Plafond;
        public const LampShadow DEFAULT_SHADOW = LampShadow.None;

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

        private Light? _light;
        private Light? _upLight;
        private Material? _plafondMat;
        public Light? PointLight => _light;
        public Light? UpLight => _upLight;

        [Undoable]
        public int TemperatureK
        {
            get => _temperatureK;
            set { _temperatureK = Mathf.Clamp(value, MIN_TEMPERATURE_K, MAX_TEMPERATURE_K); ApplyLightParams(); }
        }

        [Undoable]
        public int PowerW
        {
            get => _powerW;
            set { _powerW = Mathf.Max(0, value); ApplyLightParams(); }
        }

        [Undoable]
        public int DiffusionPct
        {
            get => _diffusionPct;
            set { _diffusionPct = Mathf.Clamp(value, 0, 100); ApplyLightParams(); }
        }

        [Undoable]
        public int UpLightPct
        {
            get => _upLightPct;
            set { _upLightPct = Mathf.Clamp(value, 0, MAX_UP_PCT); ApplyLightParams(); }
        }

        [Undoable]
        public int BeamAngleDeg
        {
            get => _beamAngleDeg;
            set { _beamAngleDeg = Mathf.Clamp(value, MIN_BEAM_DEG, MAX_BEAM_DEG); ApplyLightParams(); }
        }

        [Undoable]
        public int SoftnessPct
        {
            get => _softnessPct;
            set { _softnessPct = Mathf.Clamp(value, 0, 100); ApplyLightParams(); }
        }

        [Undoable]
        public int RangeMinMM
        {
            get => _rangeMinMM;
            set { _rangeMinMM = Mathf.Clamp(value, MIN_RANGE_MM, MAX_RANGE_MM); ApplyLightParams(); }
        }

        [Undoable]
        public int RangeMaxMM
        {
            get => _rangeMaxMM;
            set { _rangeMaxMM = Mathf.Clamp(value, MIN_RANGE_MM, MAX_RANGE_MM); ApplyLightParams(); }
        }

        [Undoable]
        public int DropMM
        {
            get => _dropMM;
            set { _dropMM = Mathf.Clamp(value, 0, MAX_DROP_MM); ApplyLightParams(); }
        }

        [Undoable]
        public int UpConePct
        {
            get => _upConePct;
            set { _upConePct = Mathf.Clamp(value, 0, MAX_FRACTION_PCT); ApplyLightParams(); }
        }

        [Undoable]
        public int UpRangePct
        {
            get => _upRangePct;
            set { _upRangePct = Mathf.Clamp(value, 0, MAX_FRACTION_PCT); ApplyLightParams(); }
        }

        [Undoable]
        public int EfficacyLmPerW
        {
            get => _efficacyLmPerW;
            set { _efficacyLmPerW = Mathf.Clamp(value, 0, MAX_EFFICACY_LM_PER_W); ApplyLightParams(); }
        }

        [Undoable]
        public int LumensPerUnit
        {
            get => _lumensPerUnit;
            set { _lumensPerUnit = Mathf.Clamp(value, MIN_LUMENS_PER_UNIT, MAX_LUMENS_PER_UNIT); ApplyLightParams(); }
        }

        [Undoable]
        public int GlowPct
        {
            get => _glowPct;
            set { _glowPct = Mathf.Clamp(value, 0, MAX_GLOW_PCT); ApplyLightParams(); }
        }

        [Undoable]
        public int ShadowStrengthPct
        {
            get => _shadowStrengthPct;
            set { _shadowStrengthPct = Mathf.Clamp(value, 0, 100); ApplyLightParams(); }
        }

        [Undoable]
        public LampShape Shape
        {
            get => _shape;
            set { _shape = value; ApplyLightParams(); }
        }

        [Undoable]
        public LampShadow Shadow
        {
            get => _shadow;
            set { _shadow = value; ApplyLightParams(); }
        }

        public static void SetGlobalOn(bool on)
        {
            _globalOn = on;
            foreach (var ls in Object.FindObjectsByType<LightSourceElement>(FindObjectsSortMode.None))
                ls.SyncLightState();
        }

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
            light.shadows = LightShadows.None;
            return light;
        }

        public void ApplyLightParams()
        {
            Color rgb = Mathf.CorrelatedColorTemperatureToRGB(_temperatureK);
            float intensity = _powerW * _efficacyLmPerW / (float)Mathf.Max(1, _lumensPerUnit);
            float range = Mathf.Lerp(_rangeMinMM, _rangeMaxMM, _diffusionPct / 100f) / 1000f;
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
                _light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ApplyShadow(_light);
            }

            if (_upLight != null)
            {
                _upLight.color = rgb;
                _upLight.intensity = sphere ? 0f : intensity * (_upLightPct / 100f);
                _upLight.range = range * (_upRangePct / 100f);
                _upLight.spotAngle = upAngle;
                _upLight.innerSpotAngle = upAngle * innerFactor;
                _upLight.transform.localPosition = drop;
                _upLight.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                ApplyShadow(_upLight);
            }

            ApplyPlafondEmission(rgb);
            SyncLightState();
        }

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

            float emiss = (GlowBase + _powerW * GlowPerWatt) * (_glowPct / 100f);
            _plafondMat.SetColor("_BaseColor", rgb);
            _plafondMat.SetColor("_EmissionColor", rgb * emiss);
            if (mr.sharedMaterial != _plafondMat) mr.sharedMaterial = _plafondMat;
        }

        private void OnDestroy()
        {
            PartRegistry.Unregister(this);
            if (_plafondMat != null)
            {
                if (Application.isPlaying) Object.Destroy(_plafondMat);
                else Object.DestroyImmediate(_plafondMat);
                _plafondMat = null;
            }
        }

        public void SyncLightState()
        {
            if (_light != null) _light.enabled = _globalOn;
            if (_upLight != null) _upLight.enabled = _globalOn && _shape != LampShape.Sphere;
        }
    }
}
