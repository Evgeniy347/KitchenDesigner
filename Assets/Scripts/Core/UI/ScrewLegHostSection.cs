using TMPro;
using UnityEngine;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ScrewLegHostSection
    {
        public const string NoHostText = "—";

        private const float FieldWidth = 45f;
        private const float FieldSpacing = 4f;
        private const float FirstFieldX = FieldX - 27f;
        private const float LabelWidth = 140f;
        private const float LabelHeight = 20f;
        private const int LabelFontSize = 13;
        private const float FieldHeight = 22f;
        private const float CaptionHeight = 18f;

        private readonly IContextMenuHost _host;

        private TMP_InputField? _insertion;
        private TMP_InputField? _left;
        private TMP_InputField? _right;
        private TMP_InputField? _top;
        private TMP_InputField? _bottom;

        public ScrewLegHostSection(IContextMenuHost host) => _host = host;

        public void Build()
        {
            var visibility = RowVisibility.For(ElementFacet.ScrewLeg);

            var caption = _host.Rows.Hint("CtxSecScrewHost", "Корпус",
                CaptionHeight, RowGap, visibility);

            _insertion = _host.Rows.NumberField("Заход в корпус", visibility);
            _insertion.readOnly = true;
            _insertion.interactable = false;

            var parent = caption.transform.parent;
            BuildPair(parent, visibility, "Слева / справа, мм",
                out _left, "screwLeft", out _right, "screwRight");
            BuildPair(parent, visibility, "Сверху / снизу, мм",
                out _top, "screwTop", out _bottom, "screwBottom");
        }

        public System.Collections.Generic.IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _left;
            yield return _right;
            yield return _top;
            yield return _bottom;
        }

        public void WriteFrom(ScrewLegElement leg)
        {
            var margins = ScrewLegSeat.Of(leg);
            SetText(_insertion, leg.InsertionIntoHostMM);
            Write(margins);
        }

        public void RefreshFrom(ScrewLegElement leg)
        {
            var margins = ScrewLegSeat.Of(leg);
            SetText(_insertion, leg.InsertionIntoHostMM);
            if (!margins.HasHost) { Write(margins); return; }
            Refresh(_left, ScrewLegSeat.LeftMM(margins), true);
            Refresh(_right, ScrewLegSeat.RightMM(margins), true);
            Refresh(_top, ScrewLegSeat.TopMM(margins), true);
            Refresh(_bottom, ScrewLegSeat.BottomMM(margins), true);
        }

        public void Track(ScrewLegElement? leg)
        {
            var margins = ScrewLegSeat.Of(leg);
            _host.Fields.Track(_left, Shown(margins, ScrewLegSeat.LeftMM(margins)));
            _host.Fields.Track(_right, Shown(margins, ScrewLegSeat.RightMM(margins)));
            _host.Fields.Track(_top, Shown(margins, ScrewLegSeat.TopMM(margins)));
            _host.Fields.Track(_bottom, Shown(margins, ScrewLegSeat.BottomMM(margins)));
        }

        public void ApplyTo(ScrewLegElement leg)
        {
            ApplyAxis(leg, _left, _right,
                m => ScrewLegSeat.LeftMM(m), m => ScrewLegSeat.RightMM(m),
                (l, mm) => l.LeftInHostMM = mm, (l, mm) => l.RightInHostMM = mm);
            ApplyAxis(leg, _top, _bottom,
                m => ScrewLegSeat.TopMM(m), m => ScrewLegSeat.BottomMM(m),
                (l, mm) => l.TopInHostMM = mm, (l, mm) => l.BottomInHostMM = mm);
        }

        private void ApplyAxis(ScrewLegElement leg, TMP_InputField? first, TMP_InputField? second,
            System.Func<ScrewLegMargins, int> firstOf, System.Func<ScrewLegMargins, int> secondOf,
            System.Action<ScrewLegElement, int> setFirst,
            System.Action<ScrewLegElement, int> setSecond)
        {
            var margins = ScrewLegSeat.Of(leg);
            if (!margins.HasHost) return;

            int wasFirst = firstOf(margins);
            int asked = _host.Fields.ParseInt(first, wasFirst);
            if (asked != wasFirst) { setFirst(leg, asked); return; }

            int wasSecond = secondOf(margins);
            asked = _host.Fields.ParseInt(second, wasSecond);
            if (asked != wasSecond) setSecond(leg, asked);
        }

        private void Write(in ScrewLegMargins margins)
        {
            Refresh(_left, ScrewLegSeat.LeftMM(margins), margins.HasHost);
            Refresh(_right, ScrewLegSeat.RightMM(margins), margins.HasHost);
            Refresh(_top, ScrewLegSeat.TopMM(margins), margins.HasHost);
            Refresh(_bottom, ScrewLegSeat.BottomMM(margins), margins.HasHost);
        }

        private void Refresh(TMP_InputField? field, int valueMM, bool hasHost)
        {
            if (field == null) return;
            field.interactable = hasHost;
            if (!hasHost) { field.SetTextWithoutNotify(NoHostText); return; }
            _host.Fields.RefreshUnfocused(field, valueMM.ToString());
        }

        private static string Shown(in ScrewLegMargins margins, int valueMM) =>
            margins.HasHost ? valueMM.ToString() : NoHostText;

        private static void SetText(TMP_InputField? field, int? valueMM)
        {
            if (field == null) return;
            field.SetTextWithoutNotify(valueMM.HasValue ? valueMM.Value.ToString() : NoHostText);
        }

        private void BuildPair(Transform parent, RowVisibility visibility, string label,
            out TMP_InputField first, string firstNode,
            out TMP_InputField second, string secondNode)
        {
            var lbl = UIFactory.CreateLabel("L_" + firstNode, parent, label, LabelFontSize,
                new Vector2(LabelX, 0), new Vector2(LabelWidth, LabelHeight), TextAnchor.MiddleLeft);
            first = BuildField(parent, firstNode, FirstFieldX);
            second = BuildField(parent, secondNode, FirstFieldX + FieldWidth + FieldSpacing);

            visibility.Register(_host.Layout, FieldHeight, FieldSpacing, lbl.rectTransform,
                first.GetComponent<RectTransform>(), second.GetComponent<RectTransform>());
        }

        private static TMP_InputField BuildField(Transform parent, string node, float x)
        {
            var field = UIFactory.CreateInputField("F_" + node, parent, NoHostText,
                new Vector2(x, 0), new Vector2(FieldWidth, FieldHeight));
            field.contentType = TMP_InputField.ContentType.Custom;
            field.onValidateInput = DimensionFieldValidation.Char();
            return field;
        }
    }
}
