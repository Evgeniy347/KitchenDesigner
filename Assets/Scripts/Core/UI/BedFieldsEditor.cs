using System;
using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class BedFieldsEditor : ElementFieldsEditor
    {
        public const string SizeNode = "CtxBedSize";
        public const string HeadboardNode = "CtxBedHeadboard";
        public const string SizeLabel = "Тип кровати";
        public const string HeadboardLabel = "Изголовье";

        public const string SingleOption = "Односпальная";
        public const string DoubleOption = "Двуспальная";
        public const string WithHeadboardOption = "Со спинкой";
        public const string WithoutHeadboardOption = "Без спинки";

        private TMP_Dropdown? _size;
        private TMP_Dropdown? _headboard;

        public BedFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is BedElement;

        public static List<string> SizeOptions() =>
            new List<string> { SingleOption, DoubleOption };

        public static List<string> HeadboardOptions() =>
            new List<string> { WithoutHeadboardOption, WithHeadboardOption };

        public override void Build()
        {
            var visibility = RowVisibility.For(ElementFacet.Bed);
            _size = Rows.Dropdown(SizeLabel, SizeOptions(), OnSizeSelected, visibility, SizeNode,
                hint: "element.bed.size");
            _headboard = Rows.Dropdown(HeadboardLabel, HeadboardOptions(), OnHeadboardSelected,
                visibility, HeadboardNode);
        }

        public override void Show(KitchenElement element) => WriteDropdowns(element);

        public override void Refresh(KitchenElement element) => WriteDropdowns(element);

        public override void AfterApply(KitchenElement element) => WriteDropdowns(element);

        private void WriteDropdowns(KitchenElement element)
        {
            if (element is not BedElement bed) return;
            _size?.SetValueWithoutNotify(bed.IsDouble ? 1 : 0);
            _headboard?.SetValueWithoutNotify(bed.HasHeadboard ? 1 : 0);
        }

        private void OnSizeSelected(int index)
        {
            if (Host.Target is not BedElement bed) return;
            Commit(bed, () => bed.IsDouble = index == 1);
        }

        private void OnHeadboardSelected(int index)
        {
            if (Host.Target is not BedElement bed) return;
            Commit(bed, () => bed.HasHeadboard = index == 1);
        }

        private void Commit(BedElement bed, Action change)
        {
            var before = UndoableProperties.Capture(bed);
            change();
            var after = UndoableProperties.Capture(bed);

            var command = SetPropertiesCommand.TryCreate(bed, before, after);
            if (command != null) CommandStack.Execute(command);

            WriteSizeFields(bed);
            WriteDropdowns(bed);
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.RefreshHighlight(bed);
        }

        private void WriteSizeFields(BedElement bed)
        {
            var dims = bed.DimensionsMM;
            var fields = Host.SizeFields;
            if (fields.Width != null) fields.Width.text = dims.x.ToString();
            if (fields.Height != null) fields.Height.text = dims.y.ToString();
            if (fields.Depth != null) fields.Depth.text = dims.z.ToString();
        }
    }
}
