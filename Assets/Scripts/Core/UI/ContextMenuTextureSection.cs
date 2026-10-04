using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuTextureSection : ContextMenuListSection<TextureOverlaySpec>
    {
        public const string SectionId = "Textures";
        public const string AddNode = "CtxTexAdd";

        private const float SideCellW = 72f;
        private const float OrderBtnH = 13f;
        private const float OrderGap = 2f;

        private readonly TMP_Dropdown?[] _rowSide =
            new TMP_Dropdown?[TextureOverlayGeometry.MAX_PER_ELEMENT];
        private readonly TMP_Dropdown?[] _rowMaterial =
            new TMP_Dropdown?[TextureOverlayGeometry.MAX_PER_ELEMENT];
        private readonly Button?[] _rowUp =
            new Button?[TextureOverlayGeometry.MAX_PER_ELEMENT];
        private readonly Button?[] _rowDown =
            new Button?[TextureOverlayGeometry.MAX_PER_ELEMENT];
        private List<TextureOverlaySpec>? _previewBefore;
        private KitchenElement? _previewTarget;

        public ContextMenuTextureSection(IContextMenuHost host) : base(host)
        {
        }

        public bool PreviewActive => _previewBefore != null;

        private KitchenElement? Target => Host.Target;

        public override bool Eligible() => Target != null && Target.SupportsTextureOverlays;

        protected override IReadOnlyList<TextureOverlaySpec>? CurrentItems() => Target?.TextureOverlays;

        public void Build(List<string> matOptions)
        {
            var rows = Host.Rows;
            var sideOptions = new List<string>();
            for (int i = 0; i <= (int)OverlaySide.All; i++)
                sideOptions.Add(TextureOverlaySpec.SideLabel((OverlaySide)i));

            BeginSection(SectionId, Loc.T("element.texture.title"), true, "+ " + Loc.T("common.add"), AddFromUI);
            Section.View.ActionButton!.gameObject.name = AddNode;
            rows.Note("CtxTexEmptyHint", Loc.T("element.texture.emptyHint"),
                RowVisibility.When(() => Eligible() && Count() == 0));

            float cell = UIStyle.ControlHCompact;
            float gap = UIStyle.Space1;
            float matW = rows.Metrics.Width - SideCellW - 3f * cell - 4f * gap;
            for (int i = 0; i < TextureOverlayGeometry.MAX_PER_ELEMENT; i++)
            {
                int index = i;
                var row = rows.NewRowRect("CtxTexRow" + i);
                var sideDd = UIFactory.CreateDropdown($"CtxTexSide{i}", row,
                    new List<string>(sideOptions), Vector2.zero,
                    new Vector2(SideCellW, rows.Metrics.ControlH), _ => { SideHighlighter.Hide(); Edit(index); });
                var matDd = UIFactory.CreateDropdown($"CtxTexMat{i}", row,
                    new List<string>(matOptions), Vector2.zero,
                    new Vector2(matW, rows.Metrics.ControlH), _ => Edit(index));
                DropdownHover.Attach(matDd, option => PreviewMaterial(index, option), EndPreview);
                var orderCol = BuildOrderColumn(row, i, index, cell);
                var editBtn = UIFactory.CreateIconButton($"CtxTexEdit{i}", row, IconFactory.Pencil,
                    Vector2.zero, new Vector2(cell, cell), () => ToggleAreaHandles(index));
                QuietButton.Apply(editBtn);
                TooltipUI.Attach(editBtn.gameObject, Loc.T("element.texture.editArea"));
                var delBtn = QuietDeleteButton.Create($"CtxTexDel{i}", row, cell,
                    Loc.T("common.delete"), () => Remove(index));
                UIFactory.FitDropdownItems(sideDd);
                UIFactory.FitDropdownItems(matDd);

                float x = 0f;
                rows.PlaceCell((RectTransform)sideDd.transform, x, SideCellW);
                x += SideCellW + gap;
                rows.PlaceCell((RectTransform)matDd.transform, x, matW);
                x += matW + gap;
                rows.PlaceCell(orderCol, x, cell);
                x += cell + gap;
                rows.PlaceCell((RectTransform)editBtn.transform, x, cell);
                x += cell + gap;
                rows.PlaceCell((RectTransform)delBtn.transform, x, cell);

                AttachSideHover(sideDd);
                _rowSide[i] = sideDd;
                _rowMaterial[i] = matDd;
                rows.Custom(row, rows.Metrics.ControlH,
                    RowVisibility.When(() => Eligible() && Count() > index));
            }
        }

        public void RebuildMaterialOptions()
        {
            foreach (var dd in _rowMaterial) MaterialOptions.Fill(dd);
        }

        private RectTransform BuildOrderColumn(Transform parent, int slot, int index, float cell)
        {
            var column = UIFactory.CreateRect($"CtxTexOrder{slot}", parent);
            column.sizeDelta = new Vector2(cell, cell);

            float half = (OrderBtnH + OrderGap) * 0.5f;
            var size = new Vector2(cell, OrderBtnH);
            _rowUp[slot] = UIFactory.CreateIconButton($"CtxTexUp{slot}", column,
                IconFactory.CaretUp, new Vector2(0, half), size,
                () => Move(index, -1), iconPaddingBothEdges: 4f);
            _rowDown[slot] = UIFactory.CreateIconButton($"CtxTexDown{slot}", column,
                IconFactory.CaretDown, new Vector2(0, -half), size,
                () => Move(index, +1), iconPaddingBothEdges: 4f);
            return column;
        }

        private void AttachSideHover(TMP_Dropdown dropdown) =>
            DropdownHover.Attach(dropdown, HoverSide, SideHighlighter.Hide);

        internal void HoverSide(int optionIndex)
        {
            if (Target == null || optionIndex < 0 || optionIndex >= TextureOverlayGeometry.FACE_COUNT)
            {
                SideHighlighter.Hide();
                return;
            }
            SideHighlighter.ShowFace(Target, optionIndex);
        }

        internal void AddFromUI()
        {
            if (Target == null) return;
            EndPreview();
            var all = MaterialCatalog.All;
            if (all.Count == 0) return;

            var spec = TextureOverlaySpec.FullFace(OverlaySide.A, all[0].id);
            var after = new List<TextureOverlaySpec>(Target.TextureOverlays);
            if (after.Count >= TextureOverlayGeometry.MAX_PER_ELEMENT)
            {
                ToastNotification.ShowIfAvailable(
                    Loc.F("toast.textureLimit", TextureOverlayGeometry.MAX_PER_ELEMENT));
                return;
            }
            after.Add(spec);
            Apply(after);
            EnsureExpanded();
            AfterChange();
        }

        private void Remove(int index)
        {
            if (Target == null || index < 0 || index >= Target.TextureOverlays.Count) return;
            TextureOverlayHandles.End();
            var after = new List<TextureOverlaySpec>(Target.TextureOverlays);
            after.RemoveAt(index);
            Apply(after);
            AfterChange();
        }

        private void Edit(int index)
        {
            if (Target == null || index < 0 || index >= Target.TextureOverlays.Count) return;
            var sideDd = _rowSide[index];
            var matDd = _rowMaterial[index];
            if (sideDd == null || matDd == null) return;
            EndPreview();

            var all = MaterialCatalog.All;
            if (matDd.value < 0 || matDd.value >= all.Count) return;

            var spec = Target.TextureOverlays[index]
                .WithSide((OverlaySide)sideDd.value)
                .WithMaterial(all[matDd.value].id);

            var after = new List<TextureOverlaySpec>(Target.TextureOverlays);
            if (after[index].Equals(spec)) return;
            after[index] = spec;
            Apply(after);
            AfterChange();
        }

        internal void PreviewMaterial(int row, int optionIndex)
        {
            if (Target == null) return;
            var all = MaterialCatalog.All;
            if (optionIndex < 0 || optionIndex >= all.Count) return;

            BeginPreview();
            var preview = new List<TextureOverlaySpec>(_previewBefore!);

            if (row < 0 || row >= preview.Count) return;
            preview[row] = preview[row].WithMaterial(all[optionIndex].id);

            Target.SetTextureOverlays(preview);
        }

        private void BeginPreview()
        {
            if (_previewBefore != null && _previewTarget == Target) return;
            EndPreview();
            _previewTarget = Target;
            _previewBefore = new List<TextureOverlaySpec>(Target!.TextureOverlays);
        }

        public void EndPreview()
        {
            var before = _previewBefore;
            var target = _previewTarget;
            _previewBefore = null;
            _previewTarget = null;
            if (before == null || target == null) return;
            target.SetTextureOverlays(before);
        }

        private void Move(int index, int delta)
        {
            if (Target == null) return;
            int other = index + delta;
            var after = new List<TextureOverlaySpec>(Target.TextureOverlays);
            if (index < 0 || index >= after.Count || other < 0 || other >= after.Count) return;

            int edited = -1;
            if (TextureOverlayHandles.IsEditing(Target, index)) edited = other;
            else if (TextureOverlayHandles.IsEditing(Target, other)) edited = index;

            (after[index], after[other]) = (after[other], after[index]);
            Apply(after);

            if (edited >= 0) TextureOverlayHandles.Begin(Target, edited);
            AfterChange();
        }

        private void ToggleAreaHandles(int index)
        {
            if (Target == null || index < 0 || index >= Target.TextureOverlays.Count) return;
            TextureOverlayHandles.Toggle(Target, index);
        }

        private void Apply(List<TextureOverlaySpec> after)
        {
            if (Target == null) return;
            CommandStack.Execute(new SetListCommand<TextureOverlaySpec>(
                $"Textures {Target.PartName}", Target.TextureOverlays, after, Target.SetTextureOverlays));
        }

        protected override void RefreshRows()
        {
            Section.SetCount(NumberFormat.Integer(Count()));

            IReadOnlyList<TextureOverlaySpec>? overlays = CurrentItems();
            for (int i = 0; i < _rowSide.Length; i++)
            {
                if (overlays == null || i >= overlays.Count) continue;
                _rowSide[i]?.SetValueWithoutNotify((int)overlays[i].side);
                _rowSide[i]?.RefreshShownValue();
                _rowMaterial[i]?.SetValueWithoutNotify(MaterialOptions.IndexOf(overlays[i].MaterialId));
                _rowMaterial[i]?.RefreshShownValue();
                if (_rowUp[i] != null) _rowUp[i]!.interactable = i > 0;
                if (_rowDown[i] != null) _rowDown[i]!.interactable = i < overlays.Count - 1;
            }
        }
    }
}
