using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FacadeElement : KitchenElement
    {
        public int GapLeft
        {
            get => Data.GapLeft;
            set { Data.GapLeft = value; ApplyDimensions(); }
        }

        public int GapRight
        {
            get => Data.GapRight;
            set { Data.GapRight = value; ApplyDimensions(); }
        }

        public int GapTop
        {
            get => Data.GapTop;
            set { Data.GapTop = value; ApplyDimensions(); }
        }

        public int GapBottom
        {
            get => Data.GapBottom;
            set { Data.GapBottom = value; ApplyDimensions(); }
        }

        public int GapMM => Data.GapMM;

        protected override Vector3 EffectiveScale
        {
            get
            {
                var physical = transform.localScale;
                var gapX = (Data.GapLeft + Data.GapRight) * AppConstants.MM_TO_UNITS;
                var gapY = (Data.GapTop + Data.GapBottom) * AppConstants.MM_TO_UNITS;
                return physical + new Vector3(gapX, gapY, 0);
            }
        }
    }
}
