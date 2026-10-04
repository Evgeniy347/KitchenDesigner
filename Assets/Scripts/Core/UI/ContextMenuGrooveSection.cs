using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core.Update;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuGrooveSection : ContextMenuListSection<GrooveSpec>
    {
        public const string SectionId = "Grooves";
        public const string AddNode = "CtxGrooveAdd";

        private static readonly LocalizedCache<string[]> SideLabelsCache =
            new LocalizedCache<string[]>(() => new string[] { Loc.T("element.groove.sideTop"), Loc.T("element.groove.sideBottom"), Loc.T("element.groove.sideLeft"), Loc.T("element.groove.sideRight") });

        private static string[] SideLabels => SideLabelsCache.Value;
        private static readonly LocalizedCache<string[]> KindLabelsCache =
            new LocalizedCache<string[]>(() => new string[] { Loc.T("element.groove.kindThrough"), Loc.T("element.groove.kindStopped") });

        private static string[] KindLabels => KindLabelsCache.Value;

        private readonly TMP_Dropdown?[] _rowSide = new TMP_Dropdown?[AppConstants.GROOVE_MAX_PER_PART];
        private readonly TMP_Dropdown?[] _rowKind = new TMP_Dropdown?[AppConstants.GROOVE_MAX_PER_PART];

        public ContextMenuGrooveSection(IContextMenuHost host) : base(host)
        {
        }

        private KitchenElement? Target => Host.Target;

        public override bool Eligible() => Target != null && Target.SupportsGrooves;

        protected override IReadOnlyList<GrooveSpec>? CurrentItems() => Target?.Grooves;

        public void Build()
        {
            var rows = Host.Rows;
            var partOnly = RowVisibility.For(ElementFacet.Part);
            BeginSection(SectionId, Loc.T("element.groove.title"), false, "+ " + Loc.T("common.add"), AddFromUI);
            Section.View.ActionButton!.gameObject.name = AddNode;

            rows.Note("CtxGrooveHint",
                Loc.F("element.groove.hint", AppConstants.GROOVE_WIDTH_MM, AppConstants.GROOVE_DEPTH_MM, AppConstants.GROOVE_OFFSET_MM),
                partOnly);

            float deleteW = UIStyle.ControlHCompact;
            float sideW = Mathf.Round((rows.Metrics.Width - deleteW - 2f * UIStyle.Space1) * 0.4f);
            float kindW = rows.Metrics.Width - sideW - deleteW - 2f * UIStyle.Space1;
            for (int i = 0; i < AppConstants.GROOVE_MAX_PER_PART; i++)
            {
                int index = i;
                var row = rows.NewRowRect("CtxGrooveRow" + i);
                var sideDd = UIFactory.CreateDropdown($"CtxGrooveSide{i}", row,
                    new List<string>(SideLabels), Vector2.zero, new Vector2(sideW, rows.Metrics.ControlH),
                    _ => Edit(index));
                var kindDd = UIFactory.CreateDropdown($"CtxGrooveKind{i}", row,
                    new List<string>(KindLabels), Vector2.zero, new Vector2(kindW, rows.Metrics.ControlH),
                    _ => Edit(index));
                var delBtn = QuietDeleteButton.Create($"CtxGrooveDel{i}", row, deleteW,
                    Loc.T("common.delete"), () => Remove(index));
                UIFactory.FitDropdownItems(sideDd);
                UIFactory.FitDropdownItems(kindDd);
                rows.PlaceCell((RectTransform)sideDd.transform, 0f, sideW);
                rows.PlaceCell((RectTransform)kindDd.transform, sideW + UIStyle.Space1, kindW);
                rows.PlaceCell((RectTransform)delBtn.transform, rows.Metrics.Width - deleteW, deleteW);
                _rowSide[i] = sideDd;
                _rowKind[i] = kindDd;
                rows.Custom(row, rows.Metrics.ControlH,
                    RowVisibility.For(ElementFacet.Part, () => Expanded && Count() > index));
            }
        }

        internal void AddFromUI()
        {
            if (Target == null) return;
            var after = new List<GrooveSpec>(Target.Grooves);
            if (after.Count >= AppConstants.GROOVE_MAX_PER_PART)
            {
                ToastNotification.ShowIfAvailable(
                    Loc.F("toast.grooveLimit", AppConstants.GROOVE_MAX_PER_PART), level: StatusLevel.Warning);
                return;
            }
            if (!TryFirstFree(after, out var spec))
            {
                ToastNotification.ShowIfAvailable(Loc.T("toast.grooveExists"), level: StatusLevel.Warning);
                return;
            }
            after.Add(spec);
            Apply(after);
            EnsureExpanded();
            AfterChange();
        }

        private static bool TryFirstFree(List<GrooveSpec> existing, out GrooveSpec spec)
        {
            foreach (GrooveKind kind in Enum.GetValues(typeof(GrooveKind)))
                foreach (GrooveSide side in Enum.GetValues(typeof(GrooveSide)))
                {
                    var candidate = new GrooveSpec(kind, side);
                    if (existing.Contains(candidate)) continue;
                    spec = candidate;
                    return true;
                }
            spec = default;
            return false;
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
                    ToastNotification.ShowIfAvailable(Loc.T("toast.grooveExists"), level: StatusLevel.Warning);
                    Refresh();
                    return;
                }
            after[index] = spec;
            Apply(after);
            AfterChange();
        }

        protected override void RefreshRows()
        {
            Section.SetCount(NumberFormat.Integer(Count()));

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
