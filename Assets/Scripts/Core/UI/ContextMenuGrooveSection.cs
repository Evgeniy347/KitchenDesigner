using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuGrooveSection
    {
        private const float RowHeight = 28f;
        private const float RowSpacing = 4f;
        private const float HintH = 16f;
        private const float SideX = -114f, SideW = 104f;
        private const float KindX = 26f, KindW = 168f;
        private const float DelX = 148f, DelW = 28f;
        private const float NewKindX = 0f, NewKindW = 116f;
        private const float AddX = 116f, AddW = 100f;

        private static readonly string[] SideLabels = { "Верх", "Низ", "Лево", "Право" };
        private static readonly string[] KindLabels = { "Сквозной", "Глухой" };

        private readonly IContextMenuHost _host;
        private readonly TMP_Dropdown?[] _rowSide = new TMP_Dropdown?[AppConstants.GROOVE_MAX_PER_PART];
        private readonly TMP_Dropdown?[] _rowKind = new TMP_Dropdown?[AppConstants.GROOVE_MAX_PER_PART];

        private TMP_Text? _countLabel;
        private TMP_Dropdown? _newSide, _newKind;
        private bool _expanded;
        private int _fingerprint;

        public ContextMenuGrooveSection(IContextMenuHost host) => _host = host;

        private KitchenElement? Target => _host.Target;

        public int Count() => Target != null ? Target.Grooves.Count : 0;

        public void Collapse() => _expanded = false;

        public bool ChangedOutsideTheMenu() =>
            Target != null && Target.SupportsGrooves
            && Fingerprint(Target.Grooves) != _fingerprint;

        public void Build(Transform parent)
        {
            var partOnly = RowVisibility.For(ElementFacet.Part);
            var expanded = RowVisibility.For(ElementFacet.Part, () => _expanded);

            _countLabel = _host.Rows.WideButton("CtxGrooves", "Пазы (0)", Toggle, partOnly, RowGap);
            _host.Rows.Hint("CtxGrooveHint",
                $"Паз: ширина {AppConstants.GROOVE_WIDTH_MM} мм, глубина {AppConstants.GROOVE_DEPTH_MM} мм,"
                + $" отступ от кромки {AppConstants.GROOVE_OFFSET_MM} мм",
                HintH, RowSpacing, expanded);

            for (int i = 0; i < AppConstants.GROOVE_MAX_PER_PART; i++)
            {
                int index = i;
                var sideDd = UIFactory.CreateDropdown($"CtxGrooveSide{i}", parent,
                    new List<string>(SideLabels), new Vector2(SideX, 0), new Vector2(SideW, RowHeight),
                    _ => Edit(index));
                var kindDd = UIFactory.CreateDropdown($"CtxGrooveKind{i}", parent,
                    new List<string>(KindLabels), new Vector2(KindX, 0), new Vector2(KindW, RowHeight),
                    _ => Edit(index));
                var delBtn = UIFactory.CreateConfirmDeleteButton($"CtxGrooveDel{i}", parent,
                    UIStyle.GlyphClose, new Vector2(DelX, 0), new Vector2(DelW, RowHeight),
                    () => Remove(index));
                _rowSide[i] = sideDd;
                _rowKind[i] = kindDd;
                _host.Layout.AddFor(ElementFacet.Part,
                    () => _expanded && Count() > index, RowHeight, RowSpacing,
                    sideDd.GetComponent<RectTransform>(),
                    kindDd.GetComponent<RectTransform>(),
                    delBtn.GetComponent<RectTransform>());
            }

            _newSide = UIFactory.CreateDropdown("CtxGrooveSide", parent,
                new List<string>(SideLabels), new Vector2(SideX, 0), new Vector2(SideW, RowHeight),
                _ => { });
            _newKind = UIFactory.CreateDropdown("CtxGrooveKind", parent,
                new List<string>(KindLabels), new Vector2(NewKindX, 0), new Vector2(NewKindW, RowHeight),
                _ => { });
            var addBtn = UIFactory.CreateButton("CtxGrooveAdd", parent, "Добавить",
                new Vector2(AddX, 0), new Vector2(AddW, RowHeight), AddFromUI);
            _host.Layout.AddFor(ElementFacet.Part, () => _expanded, RowHeight, ActionGap,
                _newSide.GetComponent<RectTransform>(),
                _newKind.GetComponent<RectTransform>(),
                addBtn.GetComponent<RectTransform>());
        }

        public void Toggle()
        {
            _expanded = !_expanded;
            Refresh();
            _host.Relayout();
        }

        internal void AddFromUI()
        {
            if (Target == null || _newSide == null || _newKind == null) return;
            var spec = new GrooveSpec((GrooveKind)_newKind.value, (GrooveSide)_newSide.value);

            var after = new List<GrooveSpec>(Target.Grooves);
            if (after.Contains(spec))
            {
                ToastNotification.ShowIfAvailable("Такой паз уже есть");
                return;
            }
            if (after.Count >= AppConstants.GROOVE_MAX_PER_PART)
            {
                ToastNotification.ShowIfAvailable(
                    $"Не больше {AppConstants.GROOVE_MAX_PER_PART} пазов на деталь");
                return;
            }
            after.Add(spec);
            Apply(after);
            _expanded = true;
            AfterChange();
        }

        internal void Remove(int index)
        {
            if (Target == null || index < 0 || index >= Target.Grooves.Count) return;
            var after = new List<GrooveSpec>(Target.Grooves);
            after.RemoveAt(index);
            Apply(after);
            AfterChange();
        }

        internal void Edit(int index)
        {
            if (Target == null || index < 0 || index >= Target.Grooves.Count) return;
            var sideDd = _rowSide[index];
            var kindDd = _rowKind[index];
            if (sideDd == null || kindDd == null) return;

            var spec = new GrooveSpec((GrooveKind)kindDd.value, (GrooveSide)sideDd.value);
            var after = new List<GrooveSpec>(Target.Grooves);
            if (after[index].Equals(spec)) return;
            for (int i = 0; i < after.Count; i++)
                if (i != index && after[i].Equals(spec))
                {
                    ToastNotification.ShowIfAvailable("Такой паз уже есть");
                    Refresh();
                    return;
                }
            after[index] = spec;
            Apply(after);
            AfterChange();
        }

        public void Refresh()
        {
            ConfirmDeleteButton.DisarmAll();
            if (_countLabel != null)
                _countLabel.text =
                    $"Пазы ({Count()})  {(_expanded ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed)}";

            IReadOnlyList<GrooveSpec>? grooves = Target != null ? Target.Grooves : null;
            _fingerprint = Fingerprint(grooves);
            for (int i = 0; i < _rowSide.Length; i++)
            {
                if (grooves == null || i >= grooves.Count) continue;
                _rowSide[i]?.SetValueWithoutNotify((int)grooves[i].side);
                _rowSide[i]?.RefreshShownValue();
                _rowKind[i]?.SetValueWithoutNotify((int)grooves[i].kind);
                _rowKind[i]?.RefreshShownValue();
            }
        }

        private void Apply(List<GrooveSpec> after)
        {
            if (Target == null) return;
            CommandStack.Execute(new SetListCommand<GrooveSpec>(
                $"Grooves {Target.PartName}", Target.Grooves, after, Target.SetGrooves));
        }

        private void AfterChange()
        {
            Refresh();
            _host.Relayout();
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private static int Fingerprint(IReadOnlyList<GrooveSpec>? grooves)
        {
            if (grooves == null) return 0;
            unchecked
            {
                int h = 17;
                foreach (var g in grooves) h = h * 31 + g.GetHashCode();
                return h;
            }
        }
    }
}
