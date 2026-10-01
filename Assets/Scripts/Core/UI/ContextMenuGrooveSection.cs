using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuGrooveSection : ContextMenuListSection<GrooveSpec>
    {
        private const float RowHeight = 28f;
        private const float RowSpacing = 4f;
        private const float HintH = 16f;
        private const float SideX = -114f, SideW = 104f;
        private const float KindX = 26f, KindW = 168f;
        private const float DelX = 148f, DelW = 28f;
        private const float NewKindX = 0f, NewKindW = 116f;
        private const float AddX = 116f, AddW = 100f;

        private static readonly LocalizedCache<string[]> SideLabelsCache =
            new LocalizedCache<string[]>(() => new string[] { Loc.T("element.groove.sideTop"), Loc.T("element.groove.sideBottom"), Loc.T("element.groove.sideLeft"), Loc.T("element.groove.sideRight") });

        private static string[] SideLabels => SideLabelsCache.Value;
        private static readonly LocalizedCache<string[]> KindLabelsCache =
            new LocalizedCache<string[]>(() => new string[] { Loc.T("element.groove.kindThrough"), Loc.T("element.groove.kindStopped") });

        private static string[] KindLabels => KindLabelsCache.Value;

        private readonly TMP_Dropdown?[] _rowSide = new TMP_Dropdown?[AppConstants.GROOVE_MAX_PER_PART];
        private readonly TMP_Dropdown?[] _rowKind = new TMP_Dropdown?[AppConstants.GROOVE_MAX_PER_PART];

        private TMP_Text? _countLabel;
        private TMP_Dropdown? _newSide, _newKind;

        public ContextMenuGrooveSection(IContextMenuHost host) : base(host)
        {
        }

        private KitchenElement? Target => Host.Target;

        public override bool Eligible() => Target != null && Target.SupportsGrooves;

        protected override IReadOnlyList<GrooveSpec>? CurrentItems() => Target?.Grooves;

        public void Build(Transform parent)
        {
            var partOnly = RowVisibility.For(ElementFacet.Part);
            var expanded = RowVisibility.For(ElementFacet.Part, () => Expanded);

            _countLabel = Host.Rows.WideButton("CtxGrooves", Loc.T("element.groove.headerEmpty"), Toggle, partOnly, RowGap);
            Host.Rows.Hint("CtxGrooveHint",
                Loc.F("element.groove.hint", AppConstants.GROOVE_WIDTH_MM, AppConstants.GROOVE_DEPTH_MM, AppConstants.GROOVE_OFFSET_MM),
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
                Host.Layout.AddFor(ElementFacet.Part,
                    () => Expanded && Count() > index, RowHeight, RowSpacing,
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
            var addBtn = UIFactory.CreateButton("CtxGrooveAdd", parent, Loc.T("common.add"),
                new Vector2(AddX, 0), new Vector2(AddW, RowHeight), AddFromUI);
            Host.Layout.AddFor(ElementFacet.Part, () => Expanded, RowHeight, ActionGap,
                _newSide.GetComponent<RectTransform>(),
                _newKind.GetComponent<RectTransform>(),
                addBtn.GetComponent<RectTransform>());
        }

        internal void AddFromUI()
        {
            if (Target == null || _newSide == null || _newKind == null) return;
            var spec = new GrooveSpec((GrooveKind)_newKind.value, (GrooveSide)_newSide.value);

            var after = new List<GrooveSpec>(Target.Grooves);
            if (after.Contains(spec))
            {
                ToastNotification.ShowIfAvailable(Loc.T("toast.grooveExists"));
                return;
            }
            if (after.Count >= AppConstants.GROOVE_MAX_PER_PART)
            {
                ToastNotification.ShowIfAvailable(
                    Loc.F("toast.grooveLimit", AppConstants.GROOVE_MAX_PER_PART));
                return;
            }
            after.Add(spec);
            Apply(after);
            Expanded = true;
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
                    ToastNotification.ShowIfAvailable(Loc.T("toast.grooveExists"));
                    Refresh();
                    return;
                }
            after[index] = spec;
            Apply(after);
            AfterChange();
        }

        protected override void RefreshRows()
        {
            if (_countLabel != null)
                _countLabel.text =
                    Loc.F("element.groove.header", Count(), (Expanded ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed));

            IReadOnlyList<GrooveSpec>? grooves = CurrentItems();
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

        protected override void OnAfterChange()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }
    }
}
