using System;
using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class LaundryMachineFieldsEditor : ElementFieldsEditor
    {
        public const string KindNode = "CtxLaundryKind";
        public const string KindLabel = "Вид машины";

        public const string WasherOption = LaundryMachineBody.WasherName;
        public const string DryerOption = LaundryMachineBody.DryerName;

        private TMP_Dropdown? _kind;

        public LaundryMachineFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is LaundryMachineElement;

        public static List<string> KindOptions() => new List<string> { WasherOption, DryerOption };

        public override void Build()
        {
            var visibility = RowVisibility.When(() => Host.Target is LaundryMachineElement);
            _kind = Rows.Dropdown(KindLabel, KindOptions(), OnKindSelected, visibility, KindNode,
                hint: "element.laundry.kind");
        }

        public override void Show(KitchenElement element) => WriteDropdown(element);

        public override void Refresh(KitchenElement element) => WriteDropdown(element);

        public override void AfterApply(KitchenElement element) => WriteDropdown(element);

        private void WriteDropdown(KitchenElement element)
        {
            if (element is not LaundryMachineElement machine) return;
            _kind?.SetValueWithoutNotify(machine.Kind == LaundryMachineKind.Dryer ? 1 : 0);
        }

        private void OnKindSelected(int index)
        {
            if (Host.Target is not LaundryMachineElement machine) return;
            Commit(machine, () => machine.Kind =
                index == 1 ? LaundryMachineKind.Dryer : LaundryMachineKind.Washer);
        }

        private void Commit(LaundryMachineElement machine, Action change)
        {
            var before = UndoableProperties.Capture(machine);
            change();
            var after = UndoableProperties.Capture(machine);

            var command = SetPropertiesCommand.TryCreate(machine, before, after);
            if (command != null) CommandStack.Execute(command);

            WriteDropdown(machine);
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.RefreshHighlight(machine);
        }
    }
}
