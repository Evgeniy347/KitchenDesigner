using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class PillarFieldsEditor : ElementFieldsEditor
    {
        private TMP_InputField? _diameter;
        private TMP_InputField? _midHeight;

        public PillarFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is PillarElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override bool HeightShownFromDimensions => false;

        public override void Build()
        {
            _diameter = Rows.NumberField("Диаметр", RowVisibility.For(ElementFacet.Pillar));
            _midHeight = Rows.NumberField("Средняя секция", RowVisibility.For(ElementFacet.Pillar));
        }

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _diameter;
            yield return _midHeight;
        }

        public override void Show(KitchenElement element)
        {
            if (!(element is PillarElement pillar)) return;
            if (_diameter != null) _diameter.text = pillar.DiameterMM.ToString();
            if (_midHeight != null) _midHeight.text = pillar.MidHeightMM.ToString();
        }

        public override void Refresh(KitchenElement element)
        {
            if (!(element is PillarElement pillar)) return;
            Fields.RefreshUnfocused(_diameter, pillar.DiameterMM.ToString());
            Fields.RefreshUnfocused(_midHeight, pillar.MidHeightMM.ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (!(element is PillarElement pillar)) return;
            var height = Host.SizeFields.Height;

            if (_diameter != null)
                pillar.DiameterMM = Fields.ParseInt(_diameter, pillar.DiameterMM);

            int currentTotal = element.DimensionsMM.y;
            int requestedTotal = Fields.ParseInt(height, currentTotal);
            if (requestedTotal != currentTotal)
                pillar.MidHeightMM = Mathf.Clamp(
                    requestedTotal - PillarElement.TopHeightMM - PillarElement.BottomHeightMM,
                    PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);
            else if (_midHeight != null)
                pillar.MidHeightMM = Fields.ParseInt(_midHeight, pillar.MidHeightMM);

            if (_diameter != null) _diameter.text = pillar.DiameterMM.ToString();
            if (_midHeight != null) _midHeight.text = pillar.MidHeightMM.ToString();
            if (height != null) height.text = pillar.TotalHeightMM.ToString();
        }

        public override void AfterApply(KitchenElement element)
        {
            if (!(element is PillarElement pillar)) return;
            if (_diameter != null) _diameter.text = pillar.DiameterMM.ToString();
            if (_midHeight != null) _midHeight.text = pillar.MidHeightMM.ToString();
        }

        public override void Track(KitchenElement element)
        {
            var pillar = element as PillarElement;
            Fields.Track(_diameter, pillar != null
                ? pillar.DiameterMM.ToString()
                : PillarElement.DiameterMM_Default.ToString());
            Fields.Track(_midHeight, pillar != null
                ? pillar.MidHeightMM.ToString()
                : PillarElement.MidHeightMM_Default.ToString());
        }
    }
}
