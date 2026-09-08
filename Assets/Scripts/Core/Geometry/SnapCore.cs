using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SnapCore
    {
        private const float ThresholdEpsilon = Tolerance.SnapEpsilon;

        private const float ZeroShiftEpsilon = SnapCandidateCollector.ZeroShiftEpsilon;

        public const int FillInPasses = 3;

        public static SnapResult TrySnap(IPosedGeometry moved, IReadOnlyList<ElementGeometry> others,
            Vector3 testPosition, float threshold, System.Action<string>? logSink = null)
        {
            if (moved == null || others == null) return default;

            float maxDist = threshold + ThresholdEpsilon;
            bool verbose = logSink != null;

            ElementGeometry movedNow = moved.At(testPosition);

            var seat = SnapPortSeat.BestOf(movedNow, others, maxDist);
            if (seat.taken)
            {
                if (verbose) logSink?.Invoke(PortSeatLog(seat));
                return Seated(seat, testPosition);
            }

            var collected = new SnapCandidates();
            SnapCandidateCollector.Collect(movedNow, others, testPosition, maxDist, verbose,
                isPrimaryPass: true, collected);

            var initialLineContacts = new HashSet<string>();
            foreach (var c in collected.Candidates)
                if (c.hasLineContact) initialLineContacts.Add(c.TargetFaceKey);

            Vector3 pos = testPosition;
            SnapResult primary = default;
            string? primaryLog = null;
            var locked = new List<Vector3>(collected.ZeroShiftNormals);
            bool primaryBreaksAlignment = false;

            for (int pass = 0; pass < FillInPasses; pass++)
            {
                if (pass > 0)
                {
                    collected.ClearForRefill();
                    SnapCandidateCollector.Collect(moved, others, pos, maxDist, verbose,
                        isPrimaryPass: false, collected);
                    foreach (var n in collected.ZeroShiftNormals) locked.Add(n);
                }

                if (!SnapCandidatePicker.TryPick(collected.Candidates, locked, pos, pass == 0,
                        initialLineContacts, collected.AlreadyAlignedNormals,
                        out SnapCandidate picked, out Vector3 pickedPos))
                    break;

                pos = pickedPos;
                if (!primary.snapped)
                {
                    primary = picked.result;
                    primaryLog = picked.log;
                    primaryBreaksAlignment = picked.hasLineContact && SnapCandidatePicker
                        .BreaksAnAlignedAxis(picked, collected.AlreadyAlignedNormals);
                }
                primary.position = pos;
                locked.Add(picked.normal);

                if (Mathf.Abs(picked.du) > ZeroShiftEpsilon) locked.Add(picked.u);
                if (Mathf.Abs(picked.dv) > ZeroShiftEpsilon) locked.Add(picked.v);
            }

            if (primary.snapped && !(primaryBreaksAlignment && collected.ConfirmedContact.snapped))
            {
                if (primaryLog != null) logSink?.Invoke(primaryLog);
                return primary;
            }

            if (collected.ConfirmedContactLog != null) logSink?.Invoke(collected.ConfirmedContactLog);
            return collected.ConfirmedContact;
        }

        private static SnapResult Seated(in SnapPortSeat seat, Vector3 testPosition) => new SnapResult
        {
            snapped = true,
            position = seat.alreadySeated ? testPosition : testPosition + seat.delta,
            targetName = seat.targetName,
            faceIndex = -1,
            snapPoint = seat.mouthUnits,
            targetPoint = seat.targetMouthUnits,
        };

        private static string PortSeatLog(in SnapPortSeat seat) =>
            $"[Snap] → {seat.targetName} | устье {seat.movedPort} на устье {seat.otherPort} "
            + $"сдвиг={seat.delta.magnitude / AppConstants.MM_TO_UNITS:F2}мм "
            + $"встречные={(seat.opposed ? "да" : "нет")}";
    }
}
