using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuTextureSection
    {
        private const float TexSideX = -128f, TexSideW = 76f;
        private const float TexMatX = -13f, TexMatW = 138f;
        private const float TexAddMatX = -13f, TexAddMatW = 138f;
        private const float TexOrderX = 78f;
        private const float TexEditX = 114f, TexDelX = 150f, TexBtnW = 28f;
        private const float TexRowH = 28f;
        private const float TexOrderBtnH = 13f;
        private const float TexOrderGap = 2f;

        private const int PreviewNewRow = -1;

        private readonly IContextMenuHost _host;
        private TMP_Text? _countLabel;
        private TMP_Dropdown? _addSideDropdown, _addMaterialDropdown;
        private readonly TMP_Dropdown?[] _rowSide =
            new TMP_Dropdown?[TextureOverlayGeometry.MAX_PER_ELEMENT];
        private readonly TMP_Dropdown?[] _rowMaterial =
            new TMP_Dropdown?[TextureOverlayGeometry.MAX_PER_ELEMENT];
        private readonly Button?[] _rowUp =
            new Button?[TextureOverlayGeometry.MAX_PER_ELEMENT];
        private readonly Button?[] _rowDown =
            new Button?[TextureOverlayGeometry.MAX_PER_ELEMENT];
        private bool _expanded;
        private int _fingerprint;
        private List<TextureOverlaySpec>? _previewBefore;
        private KitchenElement? _previewTarget;

        public ContextMenuTextureSection(IContextMenuHost host) => _host = host;

        public bool PreviewActive => _previewBefore != null;

        private KitchenElement? Target => _host.Target;

        public bool Eligible() => Target != null && Target.SupportsTextureOverlays;

        public int Count() => Target != null ? Target.TextureOverlays.Count : 0;

        public bool ChangedOutsideTheMenu() =>
            Eligible() && Fingerprint(Target!.TextureOverlays) != _fingerprint;

        public void Build(Transform parent, List<string> matOptions)
        {
            var sideOptions = new List<string>();
            for (int i = 0; i <= (int)OverlaySide.All; i++)
                sideOptions.Add(TextureOverlaySpec.SideLabel((OverlaySide)i));

            var headerBtn = UIFactory.CreateButton("CtxTextures", parent, "Текстуры (0)",
                new Vector2(0, 0), new Vector2(RowWidth, BtnH), Toggle);
            _countLabel = headerBtn.GetComponentInChildren<TMP_Text>();
            _host.Layout.AddWhen(Eligible, BtnH, RowGap, headerBtn.GetComponent<RectTransform>());

            for (int i = 0; i < TextureOverlayGeometry.MAX_PER_ELEMENT; i++)
            {
                int index = i;
                var sideDd = UIFactory.CreateDropdown($"CtxTexSide{i}", parent,
                    new List<string>(sideOptions), new Vector2(TexSideX, 0),
                    new Vector2(TexSideW, TexRowH), _ => { SideHighlighter.Hide(); Edit(index); });
                var matDd = UIFactory.CreateDropdown($"CtxTexMat{i}", parent,
                    new List<string>(matOptions), new Vector2(TexMatX, 0),
                    new Vector2(TexMatW, TexRowH), _ => Edit(index));
                DropdownHover.Attach(matDd,
                    option => PreviewMaterial(index, option), EndPreview);
                var orderCol = BuildOrderColumn(parent, i, index);
                var editBtn = UIFactory.CreateIconButton($"CtxTexEdit{i}", parent, IconFactory.Pencil,
                    new Vector2(TexEditX, 0), new Vector2(TexBtnW, TexRowH),
                    () => ToggleAreaHandles(index));
                var delBtn = UIFactory.CreateConfirmDeleteButton($"CtxTexDel{i}", parent,
                    UIStyle.GlyphClose, new Vector2(TexDelX, 0), new Vector2(TexBtnW, TexRowH),
                    () => Remove(index));

                AttachSideHover(sideDd);
                _rowSide[i] = sideDd;
                _rowMaterial[i] = matDd;

                _host.Layout.AddWhen(
                    () => Eligible() && _expanded && Count() > index, TexRowH, 4f,
                    sideDd.GetComponent<RectTransform>(), matDd.GetComponent<RectTransform>(),
                    orderCol, editBtn.GetComponent<RectTransform>(), delBtn.GetComponent<RectTransform>());
            }

            _addSideDropdown = UIFactory.CreateDropdown("CtxTexSide", parent,
                new List<string>(sideOptions), new Vector2(TexSideX, 0),
                new Vector2(TexSideW, TexRowH), _ => SideHighlighter.Hide());
            _addMaterialDropdown = UIFactory.CreateDropdown("CtxTexMat", parent,
                new List<string>(matOptions), new Vector2(TexAddMatX, 0),
                new Vector2(TexAddMatW, TexRowH), _ => { });
            DropdownHover.Attach(_addMaterialDropdown,
                option => PreviewMaterial(PreviewNewRow, option), EndPreview);
            var addBtn = UIFactory.CreateButton("CtxTexAdd", parent, "Добавить",
                new Vector2(114f, 0), new Vector2(100, TexRowH), AddFromUI);
            AttachSideHover(_addSideDropdown);

            _host.Layout.AddWhen(() => Eligible() && _expanded, TexRowH, ActionGap,
                _addSideDropdown.GetComponent<RectTransform>(),
                _addMaterialDropdown.GetComponent<RectTransform>(),
                addBtn.GetComponent<RectTransform>());
        }

        public void RebuildMaterialOptions()
        {
            MaterialOptions.Fill(_addMaterialDropdown);
            foreach (var dd in _rowMaterial) MaterialOptions.Fill(dd);
        }


        private RectTransform BuildOrderColumn(Transform parent, int slot, int index)
        {
            var column = UIFactory.CreateRect($"CtxTexOrder{slot}", parent);
            column.sizeDelta = new Vector2(TexBtnW, TexRowH);
            column.anchoredPosition = new Vector2(TexOrderX, 0);

            float half = (TexOrderBtnH + TexOrderGap) * 0.5f;
            var size = new Vector2(TexBtnW, TexOrderBtnH);
            _rowUp[slot] = UIFactory.CreateIconButton($"CtxTexUp{slot}", column,
                IconFactory.CaretUp, new Vector2(0, half), size,
                () => Move(index, -1), iconPad: 4f);
            _rowDown[slot] = UIFactory.CreateIconButton($"CtxTexDown{slot}", column,
                IconFactory.CaretDown, new Vector2(0, -half), size,
                () => Move(index, +1), iconPad: 4f);
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

        public void Collapse() => _expanded = false;

        public void Toggle()
        {
            _expanded = !_expanded;
            Refresh();
            _host.Relayout();
        }

        private void AddFromUI()
        {
            if (Target == null || _addSideDropdown == null || _addMaterialDropdown == null) return;
            EndPreview();
            var all = MaterialCatalog.All;
            int matIndex = _addMaterialDropdown.value;
            if (matIndex < 0 || matIndex >= all.Count) return;

            var spec = TextureOverlaySpec.FullFace(
                (OverlaySide)_addSideDropdown.value, all[matIndex].id);

            var after = new List<TextureOverlaySpec>(Target.TextureOverlays);
            if (after.Count >= TextureOverlayGeometry.MAX_PER_ELEMENT)
            {
                ToastNotification.ShowIfAvailable(
                    $"Не больше {TextureOverlayGeometry.MAX_PER_ELEMENT} текстур на элемент");
                return;
            }
            after.Add(spec);
            Apply(after);
            _expanded = true;
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

            if (row == PreviewNewRow)
            {
                if (_addSideDropdown == null
                    || preview.Count >= TextureOverlayGeometry.MAX_PER_ELEMENT) return;
                preview.Add(TextureOverlaySpec.FullFace(
                    (OverlaySide)_addSideDropdown.value, all[optionIndex].id));
            }
            else
            {
                if (row < 0 || row >= preview.Count) return;
                preview[row] = preview[row].WithMaterial(all[optionIndex].id);
            }

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
            CommandStack.Execute(new SetTextureOverlaysCommand(Target, Target.TextureOverlays, after));
        }

        private void AfterChange()
        {
            Refresh();
            _host.Relayout();
        }

        public void Refresh()
        {
            ConfirmDeleteButton.DisarmAll();
            if (_countLabel != null)
                _countLabel.text =
                    $"Текстуры ({Count()})  {(_expanded ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed)}";

            IReadOnlyList<TextureOverlaySpec>? overlays =
                Target != null ? Target.TextureOverlays : null;
            _fingerprint = Fingerprint(overlays);
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

        private static int Fingerprint(IReadOnlyList<TextureOverlaySpec>? overlays)
        {
            if (overlays == null) return 0;
            unchecked
            {
                int h = 17;
                foreach (var o in overlays) h = h * 31 + o.GetHashCode();
                return h;
            }
        }
    }
}
