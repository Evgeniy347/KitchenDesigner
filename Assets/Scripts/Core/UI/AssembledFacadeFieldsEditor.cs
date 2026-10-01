using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class AssembledFacadeFieldsEditor : ElementFieldsEditor
    {
        private static readonly AssembledFill[] FillOrder =
            { AssembledFill.Blind, AssembledFill.Open, AssembledFill.Glass };

        private TMP_Dropdown? _fill;

        public AssembledFacadeFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is AssembledFacadeElement;

        public override void Build()
        {
            var fillOptions = new List<string> { Loc.T("element.assembled.fillBlind"), Loc.T("element.assembled.fillOpen"), Loc.T("element.assembled.fillGlass") };
            _fill = Rows.Dropdown(Loc.T("element.assembled.fill"), fillOptions, OnFillSelected,
                RowVisibility.For(ElementFacet.Assembled), "CtxFill",
                hint: "element.assembled.fill");
        }

        public override void Show(KitchenElement element)
        {
            if (_fill != null && element is AssembledFacadeElement assembled)
                _fill.SetValueWithoutNotify(IndexOf(assembled.Fill));
        }

        private static int IndexOf(AssembledFill fill)
        {
            for (int i = 0; i < FillOrder.Length; i++)
                if (FillOrder[i] == fill) return i;
            return 0;
        }

        private void OnFillSelected(int index)
        {
            if (!(Host.Target is AssembledFacadeElement assembled)) return;
            if (index < 0 || index >= FillOrder.Length) return;
            ChoiceRowUndo.Commit(assembled, () => assembled.Fill = FillOrder[index]);
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.RefreshHighlight(assembled);
        }
    }
}
