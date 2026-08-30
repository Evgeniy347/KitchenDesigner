using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuMaterialSection
    {
        private readonly IContextMenuHost _host;

        private TMP_Dropdown? _base, _tabletop, _legs;
        private KitchenElement? _previewTarget;
        private string? _previewBefore;
        private MaterialSlot _previewSlot;

        public ContextMenuMaterialSection(IContextMenuHost host) => _host = host;

        private KitchenElement? Target => _host.Target;

        public bool PreviewActive => _previewBefore != null;

        public List<string> Build()
        {
            _host.Rows.SectionHeader("CtxSecMat", "Материал");

            var options = MaterialOptions.DisplayNames();
            _base = _host.Rows.Dropdown("Текстура", options, index => Choose(MaterialSlot.Base, index),
                RowVisibility.When(() => !_host.TargetFacets.Has(ElementFacet.Table)), "CtxMaterial");
            _tabletop = _host.Rows.Dropdown("Столешница", new List<string>(options),
                index => Choose(MaterialSlot.Base, index),
                RowVisibility.For(ElementFacet.Table), "CtxTableTop");
            _legs = _host.Rows.Dropdown("Ножки", new List<string>(options),
                index => Choose(MaterialSlot.Legs, index),
                RowVisibility.For(ElementFacet.Table), "CtxTableLegs");

            DropdownHover.Attach(_base, option => Preview(MaterialSlot.Base, option), EndPreview);
            DropdownHover.Attach(_tabletop, option => Preview(MaterialSlot.Base, option), EndPreview);
            DropdownHover.Attach(_legs, option => Preview(MaterialSlot.Legs, option), EndPreview);

            return options;
        }

        public void ShowFor(KitchenElement element)
        {
            Show(_base, element.MaterialId);

            if (!(element is ITabletop tabletop)) return;

            Show(_tabletop, tabletop.TabletopMaterialId);
            Show(_legs, tabletop.LegsMaterialId);
        }

        public void ApplyLegsChoice(KitchenElement target)
        {
            if (_legs == null || !(target is ITabletop tabletop)) return;
            var all = MaterialCatalog.All;
            if (_legs.value < 0 || _legs.value >= all.Count) return;
            var def = all[_legs.value];

            tabletop.LegsMaterialId = def.id;
            MaterialManager.ApplyLegs(tabletop, def);
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
            requested == MaterialSlot.Base && _host.TargetFacets.Has(ElementFacet.Table)
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
