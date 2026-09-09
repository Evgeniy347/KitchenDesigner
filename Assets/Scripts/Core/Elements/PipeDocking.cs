using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    internal static class PipeDocking
    {
        public const float RotationEpsilonDegrees = WallSeating.RotationEpsilonDegrees;

        public const float GridRepairMaxDistMm = 2f;

        public static void Seat(KitchenElement element, Vector3 poseOrigin,
            Quaternion poseRotation, IReadOnlyList<KitchenElement> scene, in SnapCursor cursor)
        {
            if (element == null || scene == null) return;

            var settings = KitchenSettings.Instance;
            if (settings == null || !settings.SnapEnabled) return;

            float maxDist = settings.SnapThreshold * AppConstants.MM_TO_UNITS
                            + Tolerance.SnapEpsilon;

            SeatWithinDistance(element, poseOrigin, poseRotation, scene, cursor, maxDist);
        }

        public static void RepairAfterGridSnap(KitchenElement element,
            IReadOnlyList<KitchenElement> scene)
        {
            if (element == null || scene == null) return;

            float maxDist = GridRepairMaxDistMm * AppConstants.MM_TO_UNITS;
            var others = FreePortedPartsExcept(scene, element);
            SeatUsingPorts(element, element.transform.position, element.transform.rotation,
                others, default, maxDist);
        }

        private static void SeatWithinDistance(KitchenElement element, Vector3 poseOrigin,
            Quaternion poseRotation, IReadOnlyList<KitchenElement> scene, in SnapCursor cursor,
            float maxDist)
        {
            if (element == null || scene == null) return;

            SeatUsingPorts(element, poseOrigin, poseRotation, scene.ToPortedParts(), cursor,
                maxDist);
        }

        private static void SeatUsingPorts(KitchenElement element, Vector3 poseOrigin,
            Quaternion poseRotation, List<PortedPart> others, in SnapCursor cursor,
            float maxDist)
        {
            var moved = element.ToPortedPart(element.transform.position);
            if (!moved.HasPorts) return;

            var dock = SnapPortDock.Best(moved, others, maxDist, cursor);
            if (!dock.taken) return;

            var turn = Quaternion.AngleAxis(dock.rotationDegrees, dock.rotationAxis);
            var rotation = turn * poseRotation;
            var seatedOrigin = dock.targetMouthUnits - turn * (dock.movedMouthUnits - poseOrigin);
            var position = element.transform.position + (seatedOrigin - poseOrigin);

            float alreadySeatedSqr = Tolerance.SnapEpsilon * Tolerance.SnapEpsilon;
            if ((position - element.transform.position).sqrMagnitude <= alreadySeatedSqr
                && Quaternion.Angle(rotation, element.transform.rotation)
                   <= RotationEpsilonDegrees)
                return;

            element.transform.SetPositionAndRotation(position, rotation);
        }

        public static void SeatPort(KitchenElement element, int portIndex, Vector3 poseOrigin,
            Quaternion poseRotation, in SnapPort mouth) =>
            SeatPort(element, portIndex, poseOrigin, poseRotation, mouth, 0);

        public static void SeatPort(KitchenElement element, int portIndex, Vector3 poseOrigin,
            Quaternion poseRotation, in SnapPort mouth, int twistSteps)
        {
            if (element == null || !(element is ISnapPorts ported)) return;
            if (portIndex < 0 || portIndex >= ported.SnapPortCount) return;

            var mine = ported.SnapPortAt(portIndex, element.transform.position);
            SnapPortDock.TurnOnto(mine.Outward, -mouth.Outward, out Vector3 axis,
                out float degrees);

            var turn = Quaternion.AngleAxis(degrees, axis);
            int steps = ((twistSteps % 4) + 4) % 4;
            if (steps != 0)
                turn = Quaternion.AngleAxis(90f * steps, -mouth.Outward) * turn;

            var seatedOrigin = mouth.Position - turn * (mine.Position - poseOrigin);
            element.transform.SetPositionAndRotation(
                element.transform.position + (seatedOrigin - poseOrigin), turn * poseRotation);
        }

        public static void SeatFittingOnPipeEnd(PipeFittingElement fitting, PipeElement pipe,
            int end, IReadOnlyList<KitchenElement> scene)
        {
            if (fitting == null || pipe == null) return;

            var mouth = pipe.SnapPortAt(end, pipe.transform.position);
            var otherFreePorts = FreePortsExcept(scene, pipe.PartName);

            float toMm = 1f / AppConstants.MM_TO_UNITS;
            var mandatoryAxis = new PipeAxis(mouth.Outward.x, mouth.Outward.y, mouth.Outward.z);
            var mandatoryPos = new PointMm(mouth.Position.x * toMm, mouth.Position.y * toMm,
                mouth.Position.z * toMm);

            var choice = PipeFittingSeatChoice.BestForNewFitting(fitting.NodeKind,
                fitting.PortFrameSizeId, mandatoryAxis, mandatoryPos, otherFreePorts);

            SeatPort(fitting, choice.PortIndex, fitting.transform.position,
                fitting.transform.rotation, mouth, choice.TwistSteps);
        }

        public static List<PipePort> ConnectedMouths(PipeFittingElement fitting,
            IReadOnlyList<KitchenElement> scene)
        {
            var result = new List<PipePort>();
            if (fitting == null || scene == null) return result;

            var survey = ScenePipeSurvey.Of(scene);
            for (int i = 0; i < survey.Ports.Count; i++)
            {
                if (!string.Equals(survey.Ports[i].ElementId, fitting.PartName,
                        System.StringComparison.Ordinal)) continue;
                int partner = survey.Network.PartnerOf(i);
                if (partner == PipeNetwork.NoPartner) continue;
                result.Add(survey.Ports[partner]);
            }
            return result;
        }

        public static bool ReseatAfterRotation(PipeFittingElement fitting,
            IReadOnlyList<PipePort> rememberedMouths)
        {
            if (fitting == null || rememberedMouths == null || rememberedMouths.Count == 0)
                return false;

            float toMm = 1f / AppConstants.MM_TO_UNITS;
            int bestLinks = 0;
            Vector3 bestPos = fitting.transform.position;

            for (int p = 0; p < fitting.SnapPortCount; p++)
            {
                var portNow = fitting.SnapPortAt(p, Vector3.zero);
                var portAxis = new PipeAxis(portNow.Outward.x, portNow.Outward.y, portNow.Outward.z);

                for (int a = 0; a < rememberedMouths.Count; a++)
                {
                    var mouth = rememberedMouths[a];
                    if (!PipeAxis.AreOpposite(portAxis, mouth.OutwardAxis)) continue;

                    var mouthPosUnits = new Vector3(mouth.PositionMm.XMm, mouth.PositionMm.YMm,
                        mouth.PositionMm.ZMm) * AppConstants.MM_TO_UNITS;
                    Vector3 candidatePos = mouthPosUnits - portNow.Position;

                    int links = LinksAt(fitting, candidatePos, rememberedMouths, toMm);
                    if (links > bestLinks)
                    {
                        bestLinks = links;
                        bestPos = candidatePos;
                    }
                }
            }

            if (bestLinks == 0) return false;
            fitting.transform.position = bestPos;
            return true;
        }

        private static int LinksAt(PipeFittingElement fitting, Vector3 candidatePos,
            IReadOnlyList<PipePort> rememberedMouths, float toMm)
        {
            int links = 0;
            for (int i = 0; i < fitting.SnapPortCount; i++)
            {
                var port = fitting.SnapPortAt(i, candidatePos);
                var probe = new PipePort(fitting.PartName, fitting.NodeKind, i,
                    new PointMm(port.Position.x * toMm, port.Position.y * toMm,
                        port.Position.z * toMm),
                    new PipeAxis(port.Outward.x, port.Outward.y, port.Outward.z));

                for (int a = 0; a < rememberedMouths.Count; a++)
                {
                    if (!PipeJoint.Connects(probe, rememberedMouths[a])) continue;
                    links++;
                    break;
                }
            }
            return links;
        }

        private static List<PipePort> FreePortsExcept(IReadOnlyList<KitchenElement> scene,
            string excludedElementId)
        {
            var result = new List<PipePort>();
            if (scene == null) return result;

            var survey = ScenePipeSurvey.Of(scene);
            for (int i = 0; i < survey.Ports.Count; i++)
            {
                if (!survey.Network.IsFree(i)) continue;
                if (string.Equals(survey.Ports[i].ElementId, excludedElementId,
                        System.StringComparison.Ordinal)) continue;
                result.Add(survey.Ports[i]);
            }
            return result;
        }

        private static List<PortedPart> FreePortedPartsExcept(IReadOnlyList<KitchenElement> scene,
            KitchenElement moving)
        {
            var result = new List<PortedPart>();
            if (scene == null) return result;

            var survey = ScenePipeSurvey.Of(scene);
            var freeIndexesByElementId = new Dictionary<string, List<int>>();
            for (int i = 0; i < survey.Ports.Count; i++)
            {
                if (!survey.Network.IsFree(i)) continue;
                var elementId = survey.Ports[i].ElementId;
                if (!freeIndexesByElementId.TryGetValue(elementId, out var indexes))
                    freeIndexesByElementId[elementId] = indexes = new List<int>();
                indexes.Add(survey.Ports[i].PortIndex);
            }

            foreach (var e in scene)
            {
                if (e == null || ReferenceEquals(e, moving)) continue;
                if (!(e is ISnapPorts ported)) continue;
                if (!freeIndexesByElementId.TryGetValue(e.PartName, out var freeIndexes)
                    || freeIndexes.Count == 0) continue;

                var ports = new SnapPort[freeIndexes.Count];
                for (int k = 0; k < freeIndexes.Count; k++)
                    ports[k] = ported.SnapPortAt(freeIndexes[k], e.transform.position);
                result.Add(new PortedPart(e.GetInstanceID(), e.PartName, ports));
            }
            return result;
        }
    }
}
