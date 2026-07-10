using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FacadeElement : KitchenElement
    {
        [SerializeField] private int _gapLeft = 2;
        [SerializeField] private int _gapRight = 2;
        [SerializeField] private int _gapTop = 2;
        [SerializeField] private int _gapBottom = 2;

        public int GapLeft
        {
            get => _gapLeft;
            set { _gapLeft = Mathf.Max(0, value); ApplyDimensions(); }
        }

        public int GapRight
        {
            get => _gapRight;
            set { _gapRight = Mathf.Max(0, value); ApplyDimensions(); }
        }

        public int GapTop
        {
            get => _gapTop;
            set { _gapTop = Mathf.Max(0, value); ApplyDimensions(); }
        }

        public int GapBottom
        {
            get => _gapBottom;
            set { _gapBottom = Mathf.Max(0, value); ApplyDimensions(); }
        }

        public int GapMM => _gapLeft + _gapRight + _gapTop + _gapBottom;

        protected override Vector3 EffectiveScale
        {
            get
            {
                var physical = transform.localScale;
                var gapX = (_gapLeft + _gapRight) * AppConstants.MM_TO_UNITS;
                var gapY = (_gapTop + _gapBottom) * AppConstants.MM_TO_UNITS;
                return physical + new Vector3(gapX, gapY, 0);
            }
        }
    }
}
