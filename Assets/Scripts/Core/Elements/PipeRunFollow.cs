using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public readonly struct PipeRunHold
    {
        public readonly PipeElement Pipe;
        public readonly int FittingPort;
        public readonly int FreeEnd;
        public readonly Vector3 FreeEndUnits;
        public readonly Vector3 AxisUnits;
        public readonly Vector3Int DimensionsBeforeMM;
        public readonly Vector3 PositionBefore;

        public PipeRunHold(PipeElement pipe, int fittingPort, int seatedEnd)
        {
            int freeEnd = 1 - seatedEnd;
            Vector3 freeEndUnits = pipe.SnapPortAt(freeEnd, pipe.transform.position).Position;
            Vector3 seatedUnits = pipe.SnapPortAt(seatedEnd, pipe.transform.position).Position;

            Pipe = pipe;
            FittingPort = fittingPort;
            FreeEnd = freeEnd;
            FreeEndUnits = freeEndUnits;
            AxisUnits = (seatedUnits - freeEndUnits).normalized;
            DimensionsBeforeMM = pipe.DimensionsMM;
            PositionBefore = pipe.transform.position;
        }

        public bool Stirred => Pipe != null
                               && (Pipe.DimensionsMM != DimensionsBeforeMM
                                   || (Pipe.transform.position - PositionBefore).sqrMagnitude
                                   > Tolerance.EpsilonSqr);
    }

    public static class PipeRunFollow
    {
        private const float SettleNoiseSqr = Tolerance.EpsilonUnits * Tolerance.EpsilonUnits;

        public static void Hold(PipeFittingElement fitting, IReadOnlyList<KitchenElement> scene,
            List<PipeRunHold> holds)
        {
            if (holds == null) return;
            holds.Clear();
            if (fitting == null || scene == null) return;

            var survey = ScenePipeSurvey.Of(scene);
            var ports = survey.Ports;
            for (int i = 0; i < ports.Count; i++)
            {
                if (!string.Equals(ports[i].ElementId, fitting.PartName, StringComparison.Ordinal))
                    continue;

                int partner = survey.Network.PartnerOf(i);
                if (partner == PipeNetwork.NoPartner) continue;

                var pipe = PipeNamed(ports[partner].ElementId, scene);
                if (pipe == null) continue;

                holds.Add(new PipeRunHold(pipe, ports[i].PortIndex, ports[partner].PortIndex));
            }
        }

        public static int FollowAll(PipeFittingElement fitting, IReadOnlyList<PipeRunHold> holds,
            List<KitchenElement>? followed = null)
        {
            if (fitting == null || holds == null) return 0;

            int following = 0;
            for (int i = 0; i < holds.Count; i++)
            {
                if (!Follow(fitting, holds[i])) continue;
                following++;
                followed?.Add(holds[i].Pipe);
            }

            return following;
        }

        public static bool Follow(PipeFittingElement fitting, in PipeRunHold hold)
        {
            if (fitting == null || hold.Pipe == null) return false;

            Vector3 mouth = fitting.SnapPortAt(hold.FittingPort, fitting.transform.position)
                .Position;
            Vector3 fromFreeEnd = mouth - hold.FreeEndUnits;
            float alongUnits = Vector3.Dot(fromFreeEnd, hold.AxisUnits);
            Vector3 sideways = fromFreeEnd - alongUnits * hold.AxisUnits;
            if (sideways.magnitude > BreakAwayUnits) return false;

            if (sideways.sqrMagnitude > SettleNoiseSqr)
                fitting.transform.position -= sideways;

            hold.Pipe.LengthMM = Mathf.RoundToInt(alongUnits / AppConstants.MM_TO_UNITS);

            Vector3 drift = hold.FreeEndUnits
                            - hold.Pipe.SnapPortAt(hold.FreeEnd, hold.Pipe.transform.position)
                                .Position;
            if (drift.sqrMagnitude > SettleNoiseSqr)
                hold.Pipe.transform.position += drift;

            return true;
        }

        public static void Release(IReadOnlyList<PipeRunHold> holds)
        {
            if (holds == null) return;

            for (int i = 0; i < holds.Count; i++)
            {
                var hold = holds[i];
                if (hold.Pipe == null) continue;
                hold.Pipe.DimensionsMM = hold.DimensionsBeforeMM;
                hold.Pipe.transform.position = hold.PositionBefore;
            }
        }

        private static PipeElement? PipeNamed(string elementId,
            IReadOnlyList<KitchenElement> scene)
        {
            for (int i = 0; i < scene.Count; i++)
                if (scene[i] is PipeElement pipe
                    && string.Equals(pipe.PartName, elementId, StringComparison.Ordinal))
                    return pipe;
            return null;
        }

        private static float BreakAwayUnits
        {
            get
            {
                var settings = KitchenSettings.Instance;
                float mm = settings != null ? settings.SnapThreshold : Tolerance.ContactMm;
                return mm * AppConstants.MM_TO_UNITS;
            }
        }
    }
}
