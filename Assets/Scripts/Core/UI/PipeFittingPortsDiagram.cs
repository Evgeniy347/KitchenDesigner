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
    internal sealed class PipeFittingPortsDiagram
    {
        public static readonly int MaxPorts = PipeFittingDiagramLayout.MaxPortCount();

        internal static readonly IReadOnlyList<PipeNodeKind> Choices =
            PipeConnectionRule.ChoicesForAnyFitting();
        public const int RecomputeEveryNFrames = 15;

        private const float SchematicH = 150f;
        private const float HubSize = 24f;
        private const float LegLength = 50f;
        private const float SlotSize = 36f;
        private const float SlotBorder = 2f;
        private const float JointThickness = 8f;
        private const float HintH = 20f;

        private readonly IContextMenuHost _host;
        private readonly Func<PipeFittingElement?> _target;
        private readonly FrameThrottle _recompute = new(RecomputeEveryNFrames);
        private readonly Image?[] _joints = new Image?[MaxPorts];
        private readonly Image?[] _slots = new Image?[MaxPorts];
        private readonly Image?[] _holes = new Image?[MaxPorts];
        private readonly TMP_Dropdown?[] _choices = new TMP_Dropdown?[MaxPorts];
        private readonly PipePortHover _hover;

        public PipeFittingPortsDiagram(IContextMenuHost host, Func<PipeFittingElement?> target)
        {
            _host = host;
            _target = target;
            _hover = new PipePortHover(() => target(), Choices, Paint);
        }

        private static void Paint(KitchenElement owner, int port)
        {
            if (owner is PipeFittingElement fitting)
                PartHighlighter.ShowFittingMouth(fitting, port);
        }

        public void Build(Transform parent)
        {
            var root = UIFactory.CreateRect("CtxFittingPortsDiagram", parent);
            root.sizeDelta = new Vector2(RowWidth, SchematicH);
            _hover.Guard(root.gameObject);

            var hub = UIFactory.CreatePanel("CtxFittingHub", root, Vector2.zero,
                new Vector2(HubSize, HubSize), UIStyle.EdgeBoard);
            hub.raycastTarget = false;

            for (int i = 0; i < MaxPorts; i++)
            {
                var joint = UIFactory.CreatePanel("CtxFittingLeg" + i, root, Vector2.zero,
                    new Vector2(JointThickness, JointThickness), UIStyle.Separator);
                joint.raycastTarget = false;
                _joints[i] = joint;

                (_slots[i], _holes[i]) = BuildSlot(root, "CtxFittingPort" + i);
                _hover.WatchSlot(_slots[i]!.gameObject, i);
            }

            _host.Layout.AddFor(ElementFacet.PipeFitting, SchematicH, RowGap, root);

            for (int i = 0; i < MaxPorts; i++)
            {
                int index = i;
                var visibility = PortVisibility(i);
                var (_, dropdown) = _host.Rows.NamedDropdown("CtxFittingPortFitting" + i,
                    "Порт " + (i + 1), PipeEndsDiagram.Options(Choices),
                    option => OnChosen(index, option), visibility, "CtxFittingPortLbl" + i);
                _choices[i] = dropdown;
                _hover.Watch(dropdown, i);
            }

            _host.Rows.Hint("CtxFittingPortsHint",
                "Выбор детали ставит её на порт устье в устье", HintH, ActionGap,
                RowVisibility.For(ElementFacet.PipeFitting), TextAnchor.MiddleCenter);
        }

        private static RowVisibility PortVisibility(int portIndex) => portIndex switch
        {
            0 => RowVisibility.For(ElementFacet.PipeFitting),
            1 => RowVisibility.For(ElementFacet.PipeFittingSecondPort),
            _ => RowVisibility.For(ElementFacet.PipeFittingThirdPort),
        };

        public void Show(PipeFittingElement fitting)
        {
            _hover.Clear();
            _recompute.Reset();
            Repaint(fitting);
        }

        public void Tick(PipeFittingElement fitting)
        {
            if (_recompute.Due()) Repaint(fitting);
        }

        internal void OnChosen(int port, int option)
        {
            _hover.Commit();

            var fitting = _target();
            if (fitting == null) return;

            var kind = PipeConnectionRule.KindAt(Choices, option);

            var outcome = PipeEndFittings.Set(fitting, port, kind, PartRegistry.GetAll());
            if (outcome == PipeEndEdit.OccupiedByOther)
                Refuse("Порт занят другой деталью");

            Show(fitting);
        }

        private static void Refuse(string reason) =>
            StatusBarUI.Instance?.ShowTransient(reason, StatusLevel.Warning);

        private void Repaint(PipeFittingElement fitting)
        {
            if (fitting == null) return;
            var scene = PartRegistry.GetAll();
            var slots = PipeFittingDiagramLayout.SlotsFor(fitting.NodeKind);

            for (int i = 0; i < MaxPorts; i++)
            {
                bool active = i < slots.Count;
                _joints[i]?.gameObject.SetActive(active);
                _slots[i]?.gameObject.SetActive(active);
                if (!active) continue;

                PositionLeg(i, slots[i].DirX, slots[i].DirY);

                var state = PipeEndFittings.StateAt(fitting, i, scene);
                _choices[i]?.SetValueWithoutNotify(
                    PipeEndsDiagram.OptionOf(Choices, state.Fitting));

                var slot = _slots[i];
                if (slot != null) slot.color = state.Connected
                    ? UIStyle.EdgePresent
                    : UIStyle.EdgeAbsent;
                var hole = _holes[i];
                if (hole != null) hole.enabled = !state.Connected;
            }
        }

        private void PositionLeg(int index, float dirX, float dirY)
        {
            var jointRt = _joints[index]?.rectTransform;
            var slotRt = _slots[index]?.rectTransform;
            if (jointRt == null || slotRt == null) return;

            var slotPos = new Vector2(dirX * LegLength, dirY * LegLength);
            slotRt.anchoredPosition = slotPos;

            jointRt.anchoredPosition = slotPos * 0.5f;
            jointRt.sizeDelta = dirX != 0f
                ? new Vector2(LegLength, JointThickness)
                : new Vector2(JointThickness, LegLength);
        }

        private static (Image slot, Image hole) BuildSlot(Transform parent, string name)
        {
            var slot = UIFactory.CreatePanel(name, parent, Vector2.zero,
                new Vector2(SlotSize, SlotSize), UIStyle.EdgeAbsent);
            slot.raycastTarget = true;

            var hole = UIFactory.CreatePanel(name + "Hole", slot.transform, Vector2.zero,
                new Vector2(SlotSize - SlotBorder * 2f, SlotSize - SlotBorder * 2f),
                UIStyle.EdgeBoard);
            hole.raycastTarget = false;
            return (slot, hole);
        }
    }
}
