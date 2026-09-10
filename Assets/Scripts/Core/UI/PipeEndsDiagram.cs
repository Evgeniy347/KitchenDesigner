using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Core.Update;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class PipeEndsDiagram
    {
        public const int EndCount = 2;
        public const int RecomputeEveryNFrames = 15;
        public const string NoFittingOption = "нет";

        private const float DiagramH = 116f;
        private const float RunHalfW = 150f, BarH = 16f, BarY = 26f;
        private const float SlotW = 76f, SlotBorder = 2f, JointW = 2f;
        private const float LabelY = 46f, LabelW = 90f, LabelH = 14f;
        private const int LabelFont = 12;
        private const float ChoiceY = -14f, ChoiceW = 152f, ChoiceH = 28f, ChoiceX = 84f;
        private const float HintH = 20f;

        private static readonly string[] EndCaptions = { "Начало", "Конец" };

        private readonly IContextMenuHost _host;
        private readonly Func<PipeElement?> _target;
        private readonly FrameThrottle _recompute = new(RecomputeEveryNFrames);
        private readonly Image?[] _slots = new Image?[EndCount];
        private readonly Image?[] _holes = new Image?[EndCount];
        private readonly TMP_Dropdown?[] _choices = new TMP_Dropdown?[EndCount];

        public PipeEndsDiagram(IContextMenuHost host, Func<PipeElement?> target)
        {
            _host = host;
            _target = target;
        }

        internal static readonly IReadOnlyList<PipeNodeKind> Choices =
            PipeConnectionRule.ChoicesFor(PipeNodeKind.Pipe);

        public static List<string> Options() => Options(Choices);

        public static List<string> Options(IReadOnlyList<PipeNodeKind> choices)
        {
            var options = new List<string> { NoFittingOption };
            foreach (var kind in choices) options.Add(PipeFittingNames.Title(kind));
            return options;
        }

        public void Build(Transform parent)
        {
            var root = UIFactory.CreateRect("CtxPipeEndsDiagram", parent);
            root.sizeDelta = new Vector2(RowWidth, DiagramH);

            UIFactory.CreatePanel("CtxPipeBody", root, new Vector2(0f, BarY),
                new Vector2((RunHalfW - SlotW) * 2f, BarH), UIStyle.EdgeBoard);

            for (int end = 0; end < EndCount; end++)
            {
                float side = end == 0 ? -1f : 1f;
                float slotX = side * (RunHalfW - SlotW * 0.5f);

                (_slots[end], _holes[end]) = BuildSlot(root, "CtxPipeEnd" + end,
                    new Vector2(slotX, BarY));

                UIFactory.CreatePanel("CtxPipeJoint" + end, root,
                    new Vector2(side * (RunHalfW - SlotW), BarY),
                    new Vector2(JointW, BarH), UIStyle.Separator);

                Caption(root, "CtxPipeEndLbl" + end, EndCaptions[end],
                    new Vector2(side * ChoiceX, LabelY));

                int index = end;
                _choices[end] = UIFactory.CreateDropdown("CtxPipeEndFitting" + end, root,
                    Options(), new Vector2(side * ChoiceX, ChoiceY),
                    new Vector2(ChoiceW, ChoiceH), option => OnChosen(index, option));
            }

            _host.Layout.AddFor(ElementFacet.Pipe, DiagramH, RowGap, root);
            _host.Rows.Hint("CtxPipeEndsHint",
                "Выбор детали ставит её на конец трубы устье в устье", HintH, ActionGap,
                RowVisibility.For(ElementFacet.Pipe), TextAnchor.MiddleCenter);
        }

        public void Show(PipeElement pipe)
        {
            _recompute.Reset();
            Repaint(pipe);
        }

        public void Tick(PipeElement pipe)
        {
            if (_recompute.Due()) Repaint(pipe);
        }

        internal void OnChosen(int end, int option)
        {
            var pipe = _target();
            if (pipe == null) return;

            var kind = PipeConnectionRule.KindAt(Choices, option);

            var outcome = PipeEndFittings.Set(pipe, end, kind, PartRegistry.GetAll());
            if (outcome == PipeEndEdit.OccupiedByOther)
                Refuse("Конец трубы занят другой деталью");

            Show(pipe);
        }

        private static void Refuse(string reason) =>
            StatusBarUI.Instance?.ShowTransient(reason, StatusLevel.Warning);

        private void Repaint(PipeElement pipe)
        {
            if (pipe == null) return;
            var scene = PartRegistry.GetAll();

            for (int end = 0; end < EndCount; end++)
            {
                var state = PipeEndFittings.StateAt(pipe, end, scene);
                _choices[end]?.SetValueWithoutNotify(OptionOf(state.Fitting));

                var slot = _slots[end];
                if (slot != null) slot.color = state.Connected
                    ? UIStyle.EdgePresent
                    : UIStyle.EdgeAbsent;
                var hole = _holes[end];
                if (hole != null) hole.enabled = !state.Connected;
            }
        }

        public static int OptionOf(PipeNodeKind? fitting) => OptionOf(Choices, fitting);

        public static int OptionOf(IReadOnlyList<PipeNodeKind> choices, PipeNodeKind? fitting) =>
            PipeConnectionRule.OptionOf(choices, fitting);

        private static (Image slot, Image hole) BuildSlot(Transform parent, string name,
            Vector2 pos)
        {
            var slot = UIFactory.CreatePanel(name, parent, pos, new Vector2(SlotW, BarH),
                UIStyle.EdgeAbsent);
            slot.raycastTarget = false;

            var hole = UIFactory.CreatePanel(name + "Hole", slot.transform, Vector2.zero,
                new Vector2(SlotW - SlotBorder * 2f, BarH - SlotBorder * 2f), UIStyle.EdgeBoard);
            hole.raycastTarget = false;
            return (slot, hole);
        }

        private static void Caption(Transform parent, string name, string text, Vector2 pos)
        {
            var label = UIFactory.CreateLabel(name, parent, text, LabelFont, pos,
                new Vector2(LabelW, LabelH), TextAnchor.MiddleCenter);
            label.color = UIStyle.TextSecondary;
            label.raycastTarget = false;
        }
    }
}
