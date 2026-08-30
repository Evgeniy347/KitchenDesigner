using TMPro;
using UnityEngine;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuGapSection
    {
        private const float FieldWidth = 45f;
        private const float FieldSpacing = 4f;
        private const float FirstFieldX = FieldX - 27f;
        private const float LabelWidth = 140f;
        private const float LabelHeight = 20f;
        private const int LabelFontSize = 13;
        private const float FieldHeight = 22f;

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

        public void Build(Transform parent)
        {
            _countLabel = _host.Rows.WideButton("CtxGaps", "Зазоры (0)", Toggle,
                RowVisibility.When(Eligible), RowGap);

            BuildRow(parent, "Слева / справа, мм", GapSide.Left, GapSide.Right);
            BuildRow(parent, "Сверху / снизу, мм", GapSide.Top, GapSide.Bottom);
            BuildRow(parent, "Спереди / сзади, мм", GapSide.Front, GapSide.Back);
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

        private void BuildRow(Transform parent, string label, GapSide first, GapSide second)
        {
            var lbl = UIFactory.CreateLabel($"L_Gap_{first}{second}", parent, label, LabelFontSize,
                new Vector2(LabelX, 0), new Vector2(LabelWidth, LabelHeight), TextAnchor.MiddleLeft);
            var firstField = BuildField(parent, first, FirstFieldX);
            var secondField = BuildField(parent, second, FirstFieldX + FieldWidth + FieldSpacing);

            _host.Layout.AddWhen(Expanded, FieldH, FieldSpacing, lbl.rectTransform,
                firstField.GetComponent<RectTransform>(),
                secondField.GetComponent<RectTransform>());
        }

        private TMP_InputField BuildField(Transform parent, GapSide side, float x)
        {
            var field = UIFactory.CreateInputField($"F_gap{side}", parent, "0",
                new Vector2(x, 0), new Vector2(FieldWidth, FieldHeight));
            field.contentType = TMP_InputField.ContentType.Custom;
            field.onValidateInput = (text, index, ch) =>
                ExpressionParser.IsValidDimensionChar(ch) ? ch : '\0';
            PointerHover.Attach(field.gameObject,
                () => Hover(side, true), () => Hover(side, false));
            _fieldsBySide[System.Array.IndexOf(GapSides.All, side)] = field;
            return field;
        }

        internal void Hover(GapSide side, bool entered)
        {
            if (Target == null || !Target.SupportsGaps) return;
            if (entered) SideHighlighter.ShowGapSide(Target, side);
            else SideHighlighter.Hide();
        }
    }
}
