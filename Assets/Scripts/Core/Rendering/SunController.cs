using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Глобальное освещение «солнце»: время суток двигает directional
    /// light по небосводу (восход 6:00 → зенит 12:00 → закат 18:00), ночью
    /// сцена подсвечивается тусклой холодной «луной». Панель UI (DayNightPanelUI)
    /// крутит время, азимут и яркость. Чистая статика — покрывается тестами.</summary>
    public static class SunController
    {
        public const float DEFAULT_TIME = 12f;
        public const float DEFAULT_AZIMUTH = 135f;
        public const float DEFAULT_INTENSITY = 1f;
        public const float MAX_ELEVATION_DEG = 65f;

        private const float NightIntensity = 0.08f;
        private static readonly Color DayColor = Color.white;
        private static readonly Color DawnColor = new Color(1f, 0.62f, 0.36f);   // рассвет/закат
        private static readonly Color NightColor = new Color(0.55f, 0.65f, 0.95f); // «луна»

        private static Light? _sun;

        public static float TimeOfDay { get; private set; } = DEFAULT_TIME;
        public static float Azimuth { get; private set; } = DEFAULT_AZIMUTH;
        public static float Intensity { get; private set; } = DEFAULT_INTENSITY;

        /// <summary>Высота солнца над горизонтом, градусы. Отрицательная — ночь.</summary>
        public static float ElevationDeg =>
            Mathf.Sin((TimeOfDay - 6f) / 12f * Mathf.PI) * MAX_ELEVATION_DEG;

        public static bool IsNight => ElevationDeg <= 0f;

        public static Light? Sun
        {
            get
            {
                // RenderSettings.sun приоритетен и не кэшируется: сцена или
                // тесты могут подменить солнце в любой момент.
                var explicitSun = RenderSettings.sun;
                if (explicitSun != null) return explicitSun;
                if (_sun == null) _sun = FindSun();
                return _sun;
            }
        }

        private static Light? FindSun()
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

        /// <summary>Применить текущие время/азимут/яркость к солнцу сцены.</summary>
        public static void Apply()
        {
            var sun = Sun;
            if (sun == null) return;

            float elev = ElevationDeg;
            // 0 у горизонта → 1 в зените; ночью 0.
            float dayFactor = Mathf.Clamp01(elev / MAX_ELEVATION_DEG);

            if (elev > 0f)
            {
                sun.transform.rotation = Quaternion.Euler(elev, Azimuth, 0f);
                sun.color = Color.Lerp(DawnColor, DayColor, dayFactor);
                sun.intensity = Intensity * Mathf.Lerp(0.25f, 1.15f, Mathf.Sqrt(dayFactor));
            }
            else
            {
                // Ночь: тусклая «луна» с противоположной стороны неба.
                sun.transform.rotation = Quaternion.Euler(45f, Azimuth + 180f, 0f);
                sun.color = NightColor;
                sun.intensity = Intensity * NightIntensity;
            }

            RenderSettings.ambientIntensity = Mathf.Lerp(0.25f, 1f, dayFactor);
            RenderSettings.ambientLight = Color.Lerp(
                new Color(0.10f, 0.12f, 0.18f), new Color(0.54f, 0.56f, 0.60f), dayFactor);
        }
    }
}
