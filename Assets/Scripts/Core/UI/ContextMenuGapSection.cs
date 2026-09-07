using TMPro;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuGapSection
    {
        private readonly IContextMenuHost _host;
        private readonly TMP_InputField?[] _fieldsBySide = new TMP_InputField?[GapSides.All.Length];

        private TMP_Text? _countLabel;
        private bool _expanded;

        public ContextMenuGapSection(IContextMenuHost host) => _host = host;

        private KitchenElement? Target => _host.Target;

        public TMP_InputField?[] Fields => _fieldsBySide;

        public bool Eligible() => Target != null && Target.SupportsGaps;

        public bool Expanded() => _expanded && Eligible();

        public void Collapse() => _expanded = false;

        public void Build()
        {
            _countLabel = _host.Rows.WideButton("CtxGaps", "Зазоры (0)", Toggle,
                RowVisibility.When(Eligible), RowGap);

            BuildRow("Слева / справа, мм", GapSide.Left, GapSide.Right);
            BuildRow("Сверху / снизу, мм", GapSide.Top, GapSide.Bottom);
            BuildRow("Спереди / сзади, мм", GapSide.Front, GapSide.Back);
        }

        public void Toggle()
        {
            _expanded = !_expanded;
            RefreshCounter();
            _host.Relayout();
        }

        public void RefreshCounter()
        {
            if (_countLabel == null) return;
            int filled = 0;
            foreach (var f in _fieldsBySide)
                if (f != null && _host.Fields.ParseInt(f, 0) != 0) filled++;
            _countLabel.text =
                $"Зазоры ({filled})  {(_expanded ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed)}";
        }

        public void WriteFrom(KitchenElement? element)
        {
            for (int i = 0; i < GapSides.All.Length; i++)
            {
                if (_fieldsBySide[i] == null) continue;
                _fieldsBySide[i]!.text = element != null && element.SupportsGaps
                    ? element.GapOf(GapSides.All[i]).ToString()
                    : "0";
            }
            RefreshCounter();
        }

        public void RefreshFromTarget()
        {
            if (Target != null && Target.SupportsGaps)
                for (int i = 0; i < GapSides.All.Length; i++)
                    _host.Fields.RefreshUnfocused(_fieldsBySide[i],
                        Target.GapOf(GapSides.All[i]).ToString());
            RefreshCounter();
        }

        public void ApplyTo(KitchenElement target)
        {
            if (!target.SupportsGaps) return;
            for (int i = 0; i < GapSides.All.Length; i++)
            {
                var side = GapSides.All[i];
                target.SetGap(side, _host.Fields.ParseInt(_fieldsBySide[i], target.GapOf(side)));
            }
        }

        public void Track()
        {
            for (int i = 0; i < GapSides.All.Length; i++)
                _host.Fields.Track(_fieldsBySide[i],
                    Target != null && Target.SupportsGaps
                        ? Target.GapOf(GapSides.All[i]).ToString() : "0");
        }

        public bool AnyFieldFocused()
        {
            foreach (var f in _fieldsBySide)
                if (f != null && f.isFocused) return true;
            return false;
        }

        private void BuildRow(string label, GapSide first, GapSide second)
        {
            var (firstField, secondField) = _host.Rows.PairField(label,
                $"gap{first}", $"gap{second}", "0", RowVisibility.When(Expanded));
            Remember(firstField, first);
            Remember(secondField, second);
        }

        private void Remember(TMP_InputField field, GapSide side)
        {
            PointerHover.Attach(field.gameObject,
                () => Hover(side, true), () => Hover(side, false));
            _fieldsBySide[System.Array.IndexOf(GapSides.All, side)] = field;
        }

        internal void Hover(GapSide side, bool entered)
        {
            if (Target == null || !Target.SupportsGaps) return;
            if (entered) SideHighlighter.ShowGapSide(Target, side);
            else SideHighlighter.Hide();
        }
    }
}
