using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SunController
    {
        public const float DEFAULT_TIME = 12f;
        public const float DEFAULT_AZIMUTH = 135f;
        public const float DEFAULT_INTENSITY = 1f;
        public const float MAX_ELEVATION_DEG = 65f;
        public const float SUNRISE_HOUR = 6f;
        public const float SUNSET_HOUR = 18f;
        public const float MOON_ELEVATION_DEG = 45f;
        public const float MOON_AZIMUTH_OFFSET_DEG = 180f;

        private const float MoonIntensityFactor = 0.08f;
        private const float HorizonIntensityFactor = 0.25f;
        private const float ZenithIntensityFactor = 1.15f;
        private const float NightAmbientIntensity = 0.25f;
        private const float DayAmbientIntensity = 1f;

        private static readonly Color DayColor = Color.white;
        private static readonly Color SunriseSunsetColor = new Color(1f, 0.62f, 0.36f);
        private static readonly Color MoonlightColor = new Color(0.55f, 0.65f, 0.95f);
        private static readonly Color NightAmbientColor = new Color(0.10f, 0.12f, 0.18f);
        private static readonly Color DayAmbientColor = new Color(0.54f, 0.56f, 0.60f);

        private static Light? _cachedSun;

        public static float TimeOfDay { get; private set; } = DEFAULT_TIME;
        public static float Azimuth { get; private set; } = DEFAULT_AZIMUTH;
        public static float Intensity { get; private set; } = DEFAULT_INTENSITY;

        public static float ElevationDeg =>
            Mathf.Sin((TimeOfDay - SUNRISE_HOUR) / (SUNSET_HOUR - SUNRISE_HOUR) * Mathf.PI)
            * MAX_ELEVATION_DEG;

        public static bool IsNight => ElevationDeg <= 0f;

        public static float HorizonToZenith01 => Mathf.Clamp01(ElevationDeg / MAX_ELEVATION_DEG);

        public static Light? Sun
        {
            get
            {
                var sceneAssignedSun = RenderSettings.sun;
                if (sceneAssignedSun != null) return sceneAssignedSun;
                if (_cachedSun == null) _cachedSun = FindFirstDirectionalLight();
                return _cachedSun;
            }
        }

        private static Light? FindFirstDirectionalLight()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l != null && l.type == LightType.Directional) return l;
            return null;
        }

        public static void SetTimeOfDay(float hours)
        {
            TimeOfDay = Mathf.Clamp(hours, 0f, 24f);
            Apply();
        }

        public static void SetAzimuth(float degrees)
        {
            Azimuth = Mathf.Repeat(degrees, 360f);
            Apply();
        }

        public static void SetIntensity(float multiplier)
        {
            Intensity = Mathf.Clamp(multiplier, 0f, 3f);
            Apply();
        }

        public static void Reset()
        {
            TimeOfDay = DEFAULT_TIME;
            Azimuth = DEFAULT_AZIMUTH;
            Intensity = DEFAULT_INTENSITY;
            Apply();
        }

        public static void Apply()
        {
            var sun = Sun;
            if (sun == null) return;

            if (IsNight) ApplyMoonlight(sun);
            else ApplyDaylight(sun);

            ApplyAmbient();
        }

        private static void ApplyDaylight(Light sun)
        {
            float t = HorizonToZenith01;
            sun.transform.rotation = Quaternion.Euler(ElevationDeg, Azimuth, 0f);
            sun.color = Color.Lerp(SunriseSunsetColor, DayColor, t);
            sun.intensity = Intensity
                * Mathf.Lerp(HorizonIntensityFactor, ZenithIntensityFactor, Mathf.Sqrt(t));
        }

        private static void ApplyMoonlight(Light sun)
        {
            sun.transform.rotation = Quaternion.Euler(
                MOON_ELEVATION_DEG, Azimuth + MOON_AZIMUTH_OFFSET_DEG, 0f);
            sun.color = MoonlightColor;
            sun.intensity = Intensity * MoonIntensityFactor;
        }

        private static void ApplyAmbient()
        {
            float t = HorizonToZenith01;
            RenderSettings.ambientIntensity = Mathf.Lerp(NightAmbientIntensity, DayAmbientIntensity, t);
            RenderSettings.ambientLight = Color.Lerp(NightAmbientColor, DayAmbientColor, t);
        }
    }
}
