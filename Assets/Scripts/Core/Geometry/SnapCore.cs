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

            var collected = new SnapCandidates();
            SnapCandidateCollector.Collect(moved, others, testPosition, maxDist, verbose,
                isPrimaryPass: true, collected);

            var initialLineContacts = new HashSet<string>();
            foreach (var c in collected.Candidates)
                if (c.hasLineContact) initialLineContacts.Add(c.TargetFaceKey);

            Vector3 pos = testPosition;
            SnapResult primary = default;
            string? primaryLog = null;
            bool primaryIsLineContact = false;
            var locked = new List<Vector3>(collected.ZeroShiftNormals);

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
                    primaryIsLineContact = picked.hasLineContact;
                }
                primary.position = pos;
                locked.Add(picked.normal);

                if (Mathf.Abs(picked.du) > ZeroShiftEpsilon) locked.Add(picked.u);
                if (Mathf.Abs(picked.dv) > ZeroShiftEpsilon) locked.Add(picked.v);
            }

            if (primary.snapped && !(primaryIsLineContact && collected.HasFullAreaContactAlready))
            {
                if (primaryLog != null) logSink?.Invoke(primaryLog);
                return primary;
            }

            if (collected.ConfirmedContactLog != null) logSink?.Invoke(collected.ConfirmedContactLog);
            return collected.ConfirmedContact;
        }
    }
}
