using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuEdgeSection
    {
        public const int RecomputeEveryNFrames = 15;

        private const float DiagramH = 140f;
        private const float BoardW = 200f, BoardH = 110f, StripW = 10f;
        private const float BoardX = -50f, BoardY = -10f;
        private const float SideLabelOffsetY = 36f, SideLabelOffsetX = 76f;
        private const float HintH = 20f;

        private readonly IContextMenuHost _host;
        private readonly FrameThrottle _recompute = new(RecomputeEveryNFrames);

        private Toggle? _enabledToggle;
        private TMP_InputField? _thickness;
        private RectTransform? _diagram;
        private Image? _stripL1, _stripL2, _stripW1, _stripW2;
        private TMP_Text? _lengthLabel, _widthLabel;

        public ContextMenuEdgeSection(IContextMenuHost host) => _host = host;

        private KitchenElement? Target => _host.Target;

        public TMP_InputField? ThicknessField => _thickness;

        public bool Eligible() => Target != null && Target.SupportsEdges;

        public bool Shown() => Eligible() && Target!.EdgeBandingEnabled;

        public void Build(Transform parent)
        {
            var shown = RowVisibility.For(ElementFacet.Part, Shown);

            _enabledToggle = _host.Rows.Toggle("CtxEdges", "Кромки", true, OnEnabledToggled,
                RowVisibility.For(ElementFacet.Part, Eligible), RowGap);

            _diagram = BuildDiagram(parent);
            _host.Layout.AddFor(ElementFacet.Part, Shown, DiagramH, RowGap, _diagram);

            _thickness = _host.Rows.NumberField("Толщина кромки", shown, "мм", "EdgeThickness");
            _host.Rows.Hint("CtxEdgeHint", "Клик по стороне — кромка вручную", HintH, ActionGap,
                shown, TextAnchor.MiddleCenter);
        }

        public void Tick()
        {
            if (!Eligible()) return;
            if (_recompute.Due()) Refresh();
        }

        public void Track()
        {
            if (Target == null) return;
            _host.Fields.Track(_thickness, EdgeBanding.FormatThickness(Target.EdgeThicknessMM));
        }

        public void ApplyThickness(KitchenElement target)
        {
            if (!target.SupportsEdges || _thickness == null) return;
            var before = EdgeBandingState.Of(target);
            var after = new EdgeBandingState(before.enabled,
                _host.Fields.ParseDecimalInRange(_thickness, before.thicknessMM,
                    AppConstants.EDGE_THICKNESS_MIN_MM, AppConstants.EDGE_THICKNESS_MAX_MM),
                before.manualMask);
            if (!after.Equals(before))
                CommandStack.Execute(new SetEdgeBandingCommand(target, before, after));
            _thickness.text = EdgeBanding.FormatThickness(target.EdgeThicknessMM);
        }

        public void Refresh()
        {
            _recompute.Reset();
            if (Target == null || !Target.SupportsEdges) return;

            _enabledToggle?.SetIsOnWithoutNotify(Target.EdgeBandingEnabled);
            _host.Fields.RefreshUnfocused(_thickness,
                EdgeBanding.FormatThickness(Target.EdgeThicknessMM));

            if (!Target.EdgeBandingEnabled) return;

            var layout = EdgeBanding.LayoutOf(Target.DimensionsMM);
            if (_lengthLabel != null) _lengthLabel.text = $"{layout.LengthMM} мм";
            if (_widthLabel != null) _widthLabel.text = $"{layout.WidthMM} мм";

            var coverage = EdgeBanding.Coverage(Target, PartRegistry.GetAll());
            PaintStrip(_stripL1, coverage, EdgeSide.L1);
            PaintStrip(_stripL2, coverage, EdgeSide.L2);
            PaintStrip(_stripW1, coverage, EdgeSide.W1);
            PaintStrip(_stripW2, coverage, EdgeSide.W2);
        }

        private RectTransform BuildDiagram(Transform parent)
        {
            var root = UIFactory.CreateRect("CtxEdgeDiagram", parent);
            root.sizeDelta = new Vector2(RowWidth, DiagramH);

            UIFactory.CreatePanel("CtxEdgeBoard", root, new Vector2(BoardX, BoardY),
                new Vector2(BoardW, BoardH), UIStyle.EdgeBoard);

            _stripL1 = BuildStrip(root, "CtxEdgeL1", EdgeSide.L1,
                new Vector2(BoardX, BoardY + (BoardH - StripW) * 0.5f), new Vector2(BoardW, StripW));
            _stripL2 = BuildStrip(root, "CtxEdgeL2", EdgeSide.L2,
                new Vector2(BoardX, BoardY - (BoardH - StripW) * 0.5f), new Vector2(BoardW, StripW));
            _stripW1 = BuildStrip(root, "CtxEdgeW1", EdgeSide.W1,
                new Vector2(BoardX + (BoardW - StripW) * 0.5f, BoardY), new Vector2(StripW, BoardH));
            _stripW2 = BuildStrip(root, "CtxEdgeW2", EdgeSide.W2,
                new Vector2(BoardX - (BoardW - StripW) * 0.5f, BoardY), new Vector2(StripW, BoardH));

            SideLabel(root, "CtxEdgeLblL1", "L1", new Vector2(BoardX, BoardY + SideLabelOffsetY));
            SideLabel(root, "CtxEdgeLblL2", "L2", new Vector2(BoardX, BoardY - SideLabelOffsetY));
            SideLabel(root, "CtxEdgeLblW1", "W1", new Vector2(BoardX + SideLabelOffsetX, BoardY));
            SideLabel(root, "CtxEdgeLblW2", "W2", new Vector2(BoardX - SideLabelOffsetX, BoardY));

            _lengthLabel = UIFactory.CreateLabel("CtxEdgeLen", root, "", 12,
                new Vector2(BoardX, BoardY + BoardH * 0.5f + 11f), new Vector2(120, 18),
                TextAnchor.MiddleCenter);
            _lengthLabel.color = UIStyle.TextSecondary;
            _widthLabel = UIFactory.CreateLabel("CtxEdgeWid", root, "", 12,
                new Vector2(BoardX + BoardW * 0.5f + 43f, BoardY), new Vector2(70, 18),
                TextAnchor.MiddleLeft);
            _widthLabel.color = UIStyle.TextSecondary;

            return root;
        }

        private Image BuildStrip(Transform parent, string name, EdgeSide side, Vector2 pos, Vector2 size)
        {
            var strip = UIFactory.CreatePanel(name, parent, pos, size, UIStyle.EdgeAbsent);
            strip.raycastTarget = true;

            var button = strip.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OnStripClicked(side));

            PointerHover.Attach(strip.gameObject,
                () => OnStripHover(side, true), () => OnStripHover(side, false));
            return strip;
        }

        private static void SideLabel(Transform parent, string name, string text, Vector2 pos)
        {
            var lbl = UIFactory.CreateLabel(name, parent, text, 11, pos, new Vector2(30, 14),
                TextAnchor.MiddleCenter);
            lbl.color = UIStyle.TextSecondary;
            lbl.raycastTarget = false;
        }

        private void OnEnabledToggled(bool on)
        {
            if (Target == null || !Target.SupportsEdges) return;
            var before = EdgeBandingState.Of(Target);
            ApplyState(before, new EdgeBandingState(on, before.thicknessMM, before.manualMask));
        }

        internal void OnStripClicked(EdgeSide side)
        {
            if (Target == null || !Target.SupportsEdges) return;
            var before = EdgeBandingState.Of(Target);
            ApplyState(before, before.WithManual(side, !Target.IsEdgeManual(side)));
        }

        internal void OnStripHover(EdgeSide side, bool entered)
        {
            if (Target == null || !Target.SupportsEdges) return;
            if (entered) SideHighlighter.ShowEdgeSide(Target, side);
            else SideHighlighter.Hide();
        }

        private void ApplyState(EdgeBandingState before, EdgeBandingState after)
        {
            if (Target == null || after.Equals(before)) return;
            CommandStack.Execute(new SetEdgeBandingCommand(Target, before, after));
            Refresh();
            _host.Relayout();
        }

        private void PaintStrip(Image? strip, EdgeCoverage coverage, EdgeSide side)
        {
            if (strip == null || Target == null) return;
            strip.color = Target.IsEdgeManual(side) ? UIStyle.EdgeManualSide
                : coverage.HasEdge(side) ? UIStyle.EdgePresent
                : UIStyle.EdgeAbsent;
        }
    }
}
