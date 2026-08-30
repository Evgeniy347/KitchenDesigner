using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class PillarFieldsEditor : ElementFieldsEditor
    {
        private TMP_InputField? _midHeight;

        public PillarFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is PillarElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override bool WidthEditable => false;

        public override bool DepthEditable => false;

        public override bool HeightShownFromDimensions => false;

        public override void Build() =>
            _midHeight = Rows.NumberField("Средняя секция", RowVisibility.For(ElementFacet.Pillar));

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _midHeight;
        }

        public override void Show(KitchenElement element)
        {
            if (_midHeight != null && element is PillarElement pillar)
                _midHeight.text = pillar.MidHeightMM.ToString();
        }

        public override void Refresh(KitchenElement element)
        {
            if (element is PillarElement pillar)
                Fields.RefreshUnfocused(_midHeight, pillar.MidHeightMM.ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (!(element is PillarElement pillar)) return;
            var height = Host.SizeFields.Height;

            int currentTotal = element.DimensionsMM.y;
            int requestedTotal = Fields.ParseInt(height, currentTotal);
            if (requestedTotal != currentTotal)
                pillar.MidHeightMM = Mathf.Clamp(
                    requestedTotal - PillarElement.TopHeightMM - PillarElement.BottomHeightMM,
                    PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);
            else if (_midHeight != null)
                pillar.MidHeightMM = Fields.ParseInt(_midHeight, pillar.MidHeightMM);

            if (_midHeight != null) _midHeight.text = pillar.MidHeightMM.ToString();
            if (height != null) height.text = pillar.TotalHeightMM.ToString();
        }

        public override void AfterApply(KitchenElement element)
        {
            if (_midHeight != null && element is PillarElement pillar)
                _midHeight.text = pillar.MidHeightMM.ToString();
        }

        public override void Track(KitchenElement element) =>
            Fields.Track(_midHeight, element is PillarElement pillar
                ? pillar.MidHeightMM.ToString()
                : PillarElement.MidHeightMM_Default.ToString());
    }
}
