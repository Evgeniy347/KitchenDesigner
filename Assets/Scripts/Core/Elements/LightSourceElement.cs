using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Источник света: небольшой «плафон» с точечным светом внутри.
    /// Перемещается как обычная деталь; глобальная кнопка тулбара включает и
    /// выключает свет у всех источников сразу.</summary>
    public class LightSourceElement : KitchenElement
    {
        public const int DEFAULT_SIZE_MM = 150;
        public const float LIGHT_RANGE_UNITS = 6f;   // метры
        public const float LIGHT_INTENSITY = 1.4f;

        // Глобальный выключатель: применяется ко ВСЕМ источникам (кнопка «Свет»).
        private static bool _globalOn = true;
        public static bool GlobalOn => _globalOn;

        private Light? _light;
        public Light? PointLight => _light;

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
            _light.range = LIGHT_RANGE_UNITS;
            _light.intensity = LIGHT_INTENSITY;
            _light.color = new Color(1f, 0.95f, 0.82f); // тёплый белый
            _light.shadows = LightShadows.None;         // WebGL: мягко по производительности
        }

        /// <summary>Привести Light к текущему глобальному состоянию.</summary>
        public void SyncLightState()
        {
            if (_light != null)
                _light.enabled = _globalOn;
        }
    }
}
