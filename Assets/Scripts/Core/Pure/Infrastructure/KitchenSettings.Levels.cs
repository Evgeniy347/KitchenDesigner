using UnityEngine;

namespace KitchenDesigner.Core
{
    public partial class KitchenSettings
    {
        [SerializeField] private NeighbourLevelsMode _neighbourLevelsMode = NeighbourLevelsMode.Show;

        public NeighbourLevelsMode NeighbourLevels
        {
            get => _neighbourLevelsMode;
            set => _neighbourLevelsMode = (NeighbourLevelsMode)Mathf.Clamp((int)value,
                0, NeighbourLevelsModeTitles.All.Length - 1);
        }

        internal void ResetLevels()
        {
            _neighbourLevelsMode = NeighbourLevelsMode.Show;
        }

        private void CaptureLevels(KitchenSettingsData data)
        {
            data.neighbourLevelsMode = (int)_neighbourLevelsMode;
        }

        private void ApplyLevels(KitchenSettingsData data)
        {
            NeighbourLevels = (NeighbourLevelsMode)data.neighbourLevelsMode;
        }
    }
}
