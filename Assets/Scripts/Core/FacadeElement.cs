using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FacadeElement : KitchenElement
    {
        [SerializeField] private int _gapMM = 2;

        public int GapMM
        {
            get => _gapMM;
            set
            {
                _gapMM = Mathf.Max(0, value);
                ApplyDimensions();
            }
        }

        protected override Vector3 EffectiveScale
        {
            get
            {
                var physical = transform.localScale;
                var gapUnits = _gapMM * AppConstants.MM_TO_UNITS * 2;
                return physical + new Vector3(gapUnits, gapUnits, gapUnits);
            }
        }
    }
}
