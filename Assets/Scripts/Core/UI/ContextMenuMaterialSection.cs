using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuMaterialSection
    {
        private readonly IContextMenuHost _host;

        public const string PrimarySlotLabelNode = "L_CtxTableTop";
        public const string SecondarySlotLabelNode = "L_CtxTableLegs";

        private TMP_Dropdown? _base, _primarySlot, _secondarySlot;
        private TMP_Text? _primarySlotLabel, _secondarySlotLabel;
        private KitchenElement? _previewTarget;
        private string? _previewBefore;
        private MaterialSlot _previewSlot;

        private bool HasTwoDecorSlots => _host.Target is IHasTwoDecorSlots;

        public ContextMenuMaterialSection(IContextMenuHost host) => _host = host;

        private KitchenElement? Target => _host.Target;

        public bool PreviewActive => _previewBefore != null;

        public List<string> Build()
        {
            _host.Rows.SectionHeader("CtxSecMat", "Материал");

            var options = MaterialOptions.DisplayNames();
            _base = _host.Rows.Dropdown("Текстура", options, index => Choose(MaterialSlot.Base, index),
                RowVisibility.When(() => !HasTwoDecorSlots), "CtxMaterial");
            (_primarySlotLabel, _primarySlot) = _host.Rows.NamedDropdown("CtxTableTop",
                DecorSlots.TabletopLabel, new List<string>(options),
                index => Choose(MaterialSlot.Base, index),
                RowVisibility.When(() => HasTwoDecorSlots), PrimarySlotLabelNode);
            (_secondarySlotLabel, _secondarySlot) = _host.Rows.NamedDropdown("CtxTableLegs",
                DecorSlots.LegsLabel, new List<string>(options),
                index => Choose(MaterialSlot.Legs, index),
                RowVisibility.When(() => HasTwoDecorSlots), SecondarySlotLabelNode);

            DropdownHover.Attach(_base, option => Preview(MaterialSlot.Base, option), EndPreview);
            DropdownHover.Attach(_primarySlot, option => Preview(MaterialSlot.Base, option), EndPreview);
            DropdownHover.Attach(_secondarySlot, option => Preview(MaterialSlot.Legs, option), EndPreview);

            return options;
        }

        public void ShowFor(KitchenElement element)
        {
            Show(_base, element.MaterialId);

            if (!(element is IHasTwoDecorSlots slots)) return;

            Show(_primarySlot, slots.PrimaryMaterialId);
            Show(_secondarySlot, slots.SecondaryMaterialId);
            Label(_primarySlotLabel, slots.PrimarySlotLabel);
            Label(_secondarySlotLabel, slots.SecondarySlotLabel);
        }

        private static void Label(TMP_Text? label, string text)
        {
            if (label != null) label.text = text;
        }

        public void ApplySecondarySlotChoice(KitchenElement target)
        {
            if (_secondarySlot == null || !(target is IHasTwoDecorSlots slots)) return;
            var all = MaterialCatalog.All;
            if (_secondarySlot.value < 0 || _secondarySlot.value >= all.Count) return;
            var def = all[_secondarySlot.value];

            slots.SecondaryMaterialId = def.id;
            MaterialManager.ApplySecondarySlot(slots, def);
        }

        internal void Choose(MaterialSlot requested, int index)
        {
            if (Target == null) return;
            var all = MaterialCatalog.All;
            if (index < 0 || index >= all.Count) return;

            EndPreview();
            CommandStack.Execute(new SetMaterialCommand(Target, SlotFor(requested), all[index].id));

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.RefreshHighlight(Target);
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        internal void Preview(MaterialSlot requested, int optionIndex)
        {
            if (Target == null) return;
            var all = MaterialCatalog.All;
            if (optionIndex < 0 || optionIndex >= all.Count) return;

            BeginPreview(SlotFor(requested));
            MaterialManager.ApplySlot(Target, _previewSlot, all[optionIndex]);
        }

        internal void EndPreview()
        {
            var before = _previewBefore;
            var target = _previewTarget;
            var slot = _previewSlot;
            _previewBefore = null;
            _previewTarget = null;
            if (before == null || target == null) return;

            MaterialManager.ApplySlot(target, slot, MaterialCatalog.Get(before));
            SelectionManager.Instance?.ResumeHighlight(target);
        }

        private MaterialSlot SlotFor(MaterialSlot requested) =>
            requested == MaterialSlot.Base && HasTwoDecorSlots
                ? MaterialSlot.Tabletop
                : requested;

        private void BeginPreview(MaterialSlot slot)
        {
            if (_previewBefore != null && _previewTarget == Target && _previewSlot == slot) return;
            EndPreview();
            _previewTarget = Target;
            _previewSlot = slot;
            _previewBefore = MaterialManager.MaterialIdOf(Target!, slot);
            SelectionManager.Instance?.SuppressHighlight(Target!);
        }

        private static void Show(TMP_Dropdown? dropdown, string materialId)
        {
            if (dropdown == null) return;
            MaterialOptions.Fill(dropdown);
            dropdown.SetValueWithoutNotify(MaterialOptions.IndexOf(materialId));
            dropdown.RefreshShownValue();
        }
    }
}
