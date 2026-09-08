using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    internal enum PipeEndEdit
    {
        Unchanged,
        Changed,
        OccupiedByBranch,
        OccupiedByOther,
    }

    internal readonly struct PipeEndState
    {
        public readonly bool Connected;
        public readonly PipeNodeKind? Fitting;

        public PipeEndState(bool connected, PipeNodeKind? fitting)
        {
            Connected = connected;
            Fitting = fitting;
        }
    }

    internal static class PipeEndFittings
    {
        public static readonly PipeNodeKind[] Kinds = PipeFittingNames.Kinds;

        public static KitchenElement? NeighbourAt(PipeElement pipe, int end,
            IReadOnlyList<KitchenElement> scene)
        {
            if (pipe == null || scene == null) return null;
            var survey = ScenePipeSurvey.Of(scene);
            return NeighbourOf(survey, PartnerOfEnd(survey, pipe, end), scene);
        }

        public static PipeEndState StateAt(PipeElement pipe, int end,
            IReadOnlyList<KitchenElement> scene)
        {
            var neighbour = NeighbourAt(pipe, end, scene);
            return new PipeEndState(neighbour != null,
                neighbour is PipeFittingElement fitting ? fitting.NodeKind : (PipeNodeKind?)null);
        }

        public static PipeEndEdit Set(PipeElement pipe, int end, PipeNodeKind? kind,
            IReadOnlyList<KitchenElement> scene)
        {
            if (pipe == null || scene == null) return PipeEndEdit.Unchanged;
            if (end < 0 || end >= PipeNodePorts.CountOf(PipeNodeKind.Pipe)) return PipeEndEdit.Unchanged;

            var survey = ScenePipeSurvey.Of(scene);
            int partner = PartnerOfEnd(survey, pipe, end);
            var neighbour = NeighbourOf(survey, partner, scene);
            var seated = neighbour as PipeFittingElement;
            if (neighbour != null && seated == null) return PipeEndEdit.OccupiedByOther;

            if (seated == null && !kind.HasValue) return PipeEndEdit.Unchanged;
            if (seated != null)
            {
                if (kind.HasValue && seated.NodeKind == kind.Value) return PipeEndEdit.Unchanged;
                if (CarriesMore(survey, partner)) return PipeEndEdit.OccupiedByBranch;
            }

            CommandStack.BeginCapture();
            if (seated != null) CommandStack.Execute(new DeleteCommand(seated.gameObject));
            if (kind.HasValue)
                CommandStack.Execute(new CreateCommand(Spawn(pipe, end, kind.Value)));
            CommandStack.EndCapture($"Конец трубы {pipe.PartName}", commit: true);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
            return PipeEndEdit.Changed;
        }

        private static GameObject Spawn(PipeElement pipe, int end, PipeNodeKind kind)
        {
            var go = Create(kind, pipe.PartName + "_" + PipeFittingNames.TypeId(kind),
                pipe.transform.position);
            var fitting = go.GetComponent<PipeFittingElement>();
            if (fitting != null) fitting.SeatOnPipeEnd(pipe, end);
            return go;
        }

        private static GameObject Create(PipeNodeKind kind, string name, Vector3 position) =>
            kind switch
            {
                PipeNodeKind.Elbow => ElementFactory.CreatePipeElbow(name, position),
                PipeNodeKind.Coupling => ElementFactory.CreatePipeCoupling(name, position),
                PipeNodeKind.Tee => ElementFactory.CreatePipeTee(name, position),
                PipeNodeKind.Cap => ElementFactory.CreatePipeCap(name, position),
                PipeNodeKind.Supply => ElementFactory.CreatePipeSupply(name, position),
                _ => ElementFactory.CreatePipeReturn(name, position),
            };

        private static int PartnerOfEnd(PipeSurvey survey, PipeElement pipe, int end)
        {
            int index = IndexOfPort(survey, pipe.PartName, end);
            return index < 0 ? PipeNetwork.NoPartner : survey.Network.PartnerOf(index);
        }

        private static int IndexOfPort(PipeSurvey survey, string elementId, int portIndex)
        {
            var ports = survey.Ports;
            for (int i = 0; i < ports.Count; i++)
                if (ports[i].PortIndex == portIndex
                    && string.Equals(ports[i].ElementId, elementId, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        private static KitchenElement? NeighbourOf(PipeSurvey survey, int partnerPort,
            IReadOnlyList<KitchenElement> scene)
        {
            if (partnerPort < 0) return null;
            var owner = survey.Ports[partnerPort].ElementId;
            for (int i = 0; i < scene.Count; i++)
                if (scene[i] != null
                    && string.Equals(scene[i].PartName, owner, StringComparison.Ordinal))
                    return scene[i];
            return null;
        }

        private static bool CarriesMore(PipeSurvey survey, int facingPort)
        {
            var ports = survey.Ports;
            var owner = ports[facingPort].ElementId;
            for (int i = 0; i < ports.Count; i++)
            {
                if (i == facingPort) continue;
                if (!string.Equals(ports[i].ElementId, owner, StringComparison.Ordinal)) continue;
                if (!survey.Network.IsFree(i)) return true;
            }

            return false;
        }
    }
}
