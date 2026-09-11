using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class FacadeFieldsEditor : ElementFieldsEditor
    {
        private TMP_Dropdown? _mode;

        public FacadeFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is FacadeElement;

        public override void Build()
        {
            var modeOptions = new List<string>();
            for (int i = 0; i < FacadeDoor.Count; i++)
                modeOptions.Add(FacadeDoor.Label((DoorMode)i));
            _mode = Rows.Dropdown("Дверца", modeOptions, OnModeSelected,
                RowVisibility.For(ElementFacet.Facade), "CtxMode", hint: "element.facade.doorMode");
        }

        public override void Show(KitchenElement element)
        {
            if (_mode == null) return;
            _mode.SetValueWithoutNotify(element is FacadeElement facade ? (int)facade.Mode : 0);
            _mode.RefreshShownValue();
        }

        private void OnModeSelected(int index)
        {
            if (Host.Target is FacadeElement facade)
                ChoiceRowUndo.Commit(facade, () => facade.Mode = (DoorMode)index);
        }
    }
}
