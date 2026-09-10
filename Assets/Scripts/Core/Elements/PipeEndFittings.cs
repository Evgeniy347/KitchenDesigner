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
        OccupiedByPipe,
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
        public static PipeNodeKind? KindOf(KitchenElement? part) => part switch
        {
            PipeFittingElement fitting => fitting.NodeKind,
            PipeElement _ => PipeNodeKind.Pipe,
            _ => null,
        };

        public static bool HeldByAPipe(KitchenElement? seated, PipeNodeKind? kind) =>
            KindOf(seated) == PipeNodeKind.Pipe && kind != PipeNodeKind.Pipe;

        public static IReadOnlyList<PipeNodeKind> ChoicesFor(KitchenElement? owner) =>
            PipeConnectionRule.ChoicesFor(KindOf(owner) ?? PipeNodeKind.Pipe);

        public static int PortCountOf(KitchenElement? owner) =>
            owner is ISnapPorts ported ? ported.SnapPortCount : 0;

        public static KitchenElement? NeighbourAt(KitchenElement owner, int port,
            IReadOnlyList<KitchenElement> scene)
        {
            if (owner == null || scene == null) return null;
            var survey = ScenePipeSurvey.Of(scene);
            return NeighbourOf(survey, PartnerOfPort(survey, owner, port), scene);
        }

        public static PipeEndState StateAt(KitchenElement owner, int port,
            IReadOnlyList<KitchenElement> scene)
        {
            var neighbour = NeighbourAt(owner, port, scene);
            return new PipeEndState(neighbour != null, KindOf(neighbour));
        }

        public static PipeEndEdit Set(KitchenElement owner, int port, PipeNodeKind? kind,
            IReadOnlyList<KitchenElement> scene)
        {
            if (owner == null || scene == null) return PipeEndEdit.Unchanged;
            if (port < 0 || port >= PortCountOf(owner)) return PipeEndEdit.Unchanged;

            var ownerKind = KindOf(owner) ?? PipeNodeKind.Pipe;
            if (kind.HasValue && !PipeConnectionRule.CanConnect(ownerKind, kind.Value))
                return PipeEndEdit.Unchanged;

            var survey = ScenePipeSurvey.Of(scene);
            int partner = PartnerOfPort(survey, owner, port);
            var neighbour = NeighbourOf(survey, partner, scene);
            var seatedKind = KindOf(neighbour);
            if (neighbour == null && !kind.HasValue) return PipeEndEdit.Unchanged;
            if (seatedKind.HasValue && kind.HasValue && seatedKind.Value == kind.Value)
                return PipeEndEdit.Unchanged;
            if (HeldByAPipe(neighbour, kind)) return PipeEndEdit.OccupiedByPipe;

            CommandStack.BeginCapture();
            if (neighbour != null) CommandStack.Execute(new DeleteCommand(neighbour.gameObject));
            if (kind.HasValue)
                CommandStack.Execute(new CreateCommand(
                    SpawnFor(survey, owner, port, kind.Value, neighbour, scene)));
            CommandStack.EndCapture($"Порт {owner.PartName}", commit: true);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
            return PipeEndEdit.Changed;
        }

        public static string PreviewKey(KitchenElement owner, int port, PipeNodeKind kind) =>
            owner == null ? string.Empty : owner.PartName + ":" + port + ":" + kind;

        public static GameObject? Preview(KitchenElement owner, int port, PipeNodeKind kind,
            IReadOnlyList<KitchenElement> scene)
        {
            if (owner == null || scene == null) return null;
            if (port < 0 || port >= PortCountOf(owner)) return null;

            var ownerKind = KindOf(owner) ?? PipeNodeKind.Pipe;
            if (!PipeConnectionRule.CanConnect(ownerKind, kind)) return null;

            var survey = ScenePipeSurvey.Of(scene);
            var neighbour = NeighbourOf(survey, PartnerOfPort(survey, owner, port), scene);
            if (HeldByAPipe(neighbour, kind)) return null;
            return SpawnFor(survey, owner, port, kind, neighbour, scene);
        }

        private static GameObject SpawnFor(PipeSurvey survey, KitchenElement owner, int port,
            PipeNodeKind kind, KitchenElement? seated, IReadOnlyList<KitchenElement> scene) =>
            seated != null && kind != PipeNodeKind.Pipe
                ? Replace(survey, seated, kind, scene)
                : Spawn(owner, port, kind, scene);

        private static GameObject Spawn(KitchenElement owner, int port, PipeNodeKind kind,
            IReadOnlyList<KitchenElement> scene)
        {
            var go = Create(kind, owner.PartName + "_" + PipeFittingNames.TypeId(kind),
                owner.transform.position);
            var fitting = go.GetComponent<PipeFittingElement>();
            if (fitting != null) SeatNewFittingOnPort(fitting, owner, port, scene);
            else SeatNewPipeOnPort(go.GetComponent<PipeElement>(), owner, port);
            return go;
        }

        private static void SeatNewPipeOnPort(PipeElement? pipe, KitchenElement owner, int port)
        {
            if (pipe == null || !(owner is ISnapPorts ownerPorts)) return;

            var mouth = ownerPorts.SnapPortAt(port, owner.transform.position);
            PipeDocking.SeatPort(pipe, 0, pipe.transform.position, pipe.transform.rotation, mouth);
        }

        private static void SeatNewFittingOnPort(PipeFittingElement fitting, KitchenElement owner,
            int port, IReadOnlyList<KitchenElement> scene)
        {
            if (!(owner is ISnapPorts ownerPorts)) return;

            var mouth = ownerPorts.SnapPortAt(port, owner.transform.position);
            var otherFreePorts = FreePortsExceptOwner(ScenePipeSurvey.Of(scene), owner.PartName);

            float toMm = 1f / AppConstants.MM_TO_UNITS;
            var mandatoryAxis = new PipeAxis(mouth.Outward.x, mouth.Outward.y, mouth.Outward.z);
            var mandatoryPos = new PointMm(mouth.Position.x * toMm, mouth.Position.y * toMm,
                mouth.Position.z * toMm);

            var choice = PipeFittingSeatChoice.BestForNewFitting(fitting.NodeKind,
                fitting.PortFrameSizeId, mandatoryAxis, mandatoryPos, otherFreePorts);

            PipeDocking.SeatPort(fitting, choice.PortIndex, fitting.transform.position,
                fitting.transform.rotation, mouth, choice.TwistSteps);
        }

        private static List<PipePort> FreePortsExceptOwner(PipeSurvey survey, string ownerElementId)
        {
            var result = new List<PipePort>();
            for (int i = 0; i < survey.Ports.Count; i++)
            {
                if (!survey.Network.IsFree(i)) continue;
                if (string.Equals(survey.Ports[i].ElementId, ownerElementId,
                        StringComparison.Ordinal)) continue;
                result.Add(survey.Ports[i]);
            }
            return result;
        }

        private static GameObject Replace(PipeSurvey survey, KitchenElement seated,
            PipeNodeKind kind, IReadOnlyList<KitchenElement> scene)
        {
            var go = Create(kind, seated.PartName + "_" + PipeFittingNames.TypeId(kind),
                seated.transform.position);
            var fitting = go.GetComponent<PipeFittingElement>();
            if (fitting == null) return go;

            var neighbourPorts = NeighbourPortsOf(survey, seated);
            int anchor = PipeKindSwap.AnchorPortIndex(neighbourPorts);
            if (anchor == PipeKindSwap.NoPort) return go;

            var atAnchor = neighbourPorts[anchor];
            var mouth = atAnchor.HasValue ? AnchorMouth(atAnchor.Value, scene) : null;
            if (mouth == null) return go;

            int newPort = Math.Min(anchor, fitting.PortCount - 1);
            PipeDocking.SeatPort(fitting, newPort, fitting.transform.position,
                fitting.transform.rotation, mouth.Value);
            return go;
        }

        private static PipePort?[] NeighbourPortsOf(PipeSurvey survey, KitchenElement seated)
        {
            var ports = new PipePort?[PortCountOf(seated)];
            for (int i = 0; i < ports.Length; i++)
            {
                int index = IndexOfPort(survey, seated.PartName, i);
                int partner = index < 0 ? PipeNetwork.NoPartner : survey.Network.PartnerOf(index);
                ports[i] = partner == PipeNetwork.NoPartner ? (PipePort?)null : survey.Ports[partner];
            }
            return ports;
        }

        private static SnapPort? AnchorMouth(in PipePort neighbour,
            IReadOnlyList<KitchenElement> scene)
        {
            for (int i = 0; i < scene.Count; i++)
            {
                var candidate = scene[i];
                if (candidate == null) continue;
                if (!string.Equals(candidate.PartName, neighbour.ElementId, StringComparison.Ordinal))
                    continue;
                if (!(candidate is ISnapPorts ported)) continue;
                if (neighbour.PortIndex < 0 || neighbour.PortIndex >= ported.SnapPortCount) continue;
                return ported.SnapPortAt(neighbour.PortIndex, candidate.transform.position);
            }
            return null;
        }

        private static GameObject Create(PipeNodeKind kind, string name, Vector3 position) =>
            kind switch
            {
                PipeNodeKind.Pipe => ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE,
                    PipeElementSpec.DEFAULT_LENGTH_MM, name, position),
                PipeNodeKind.Elbow => ElementFactory.CreatePipeElbow(name, position),
                PipeNodeKind.Coupling => ElementFactory.CreatePipeCoupling(name, position),
                PipeNodeKind.Tee => ElementFactory.CreatePipeTee(name, position),
                PipeNodeKind.Cap => ElementFactory.CreatePipeCap(name, position),
                PipeNodeKind.Supply => ElementFactory.CreatePipeSupply(name, position),
                _ => ElementFactory.CreatePipeReturn(name, position),
            };

        private static int PartnerOfPort(PipeSurvey survey, KitchenElement owner, int port)
        {
            int index = IndexOfPort(survey, owner.PartName, port);
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
    }
}
