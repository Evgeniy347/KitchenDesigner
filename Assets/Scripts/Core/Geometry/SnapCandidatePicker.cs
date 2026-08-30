using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SnapCandidatePicker
    {
        private const float ZeroShiftEpsilon = SnapCandidateCollector.ZeroShiftEpsilon;

        public static bool TryPick(List<SnapCandidate> candidates, List<Vector3> locked,
            Vector3 basePos, bool primaryPass, HashSet<string> initialLineContacts,
            List<Vector3> alignedNormals, out SnapCandidate picked, out Vector3 pickedPos)
        {
            picked = default;
            pickedPos = basePos;
            float bestScore = float.MaxValue;
            float bestDist = float.MaxValue;
            bool bestIsLineContact = false;
            bool found = false;

            foreach (var c in candidates)
            {
                if (!primaryPass && c.hasLineContact
                    && (!initialLineContacts.Contains(c.TargetFaceKey)
                        || BreaksAnAlignedAxis(c, alignedNormals))) continue;

                float du = c.du, dv = c.dv;
                if (!primaryPass) { du = 0f; dv = 0f; }

                if (BreaksALockedAxis(c, locked, ref du, ref dv)) continue;

                Vector3 pos = basePos + c.planeShift * c.normal + du * c.u + dv * c.v;
                float dist = Vector3.Distance(pos, basePos);
                if (dist <= ZeroShiftEpsilon) continue;

                float shift = Mathf.Abs(c.planeShift);
                float score = shift > ZeroShiftEpsilon ? shift : dist;

                if (!IsBetter(score, dist, c.hasLineContact, bestScore, bestDist, bestIsLineContact))
                    continue;

                bestScore = score;
                bestDist = dist;
                bestIsLineContact = c.hasLineContact;
                picked = c;
                picked.du = du;
                picked.dv = dv;
                picked.result.position = pos;
                pickedPos = pos;
                found = true;
            }
            return found;
        }

        public static bool BreaksAnAlignedAxis(in SnapCandidate c, List<Vector3> alignedNormals)
        {
            foreach (var n in alignedNormals)
                if (Mathf.Abs(c.planeShift * Vector3.Dot(c.normal, n)) > ZeroShiftEpsilon) return true;
            return false;
        }

        private static bool BreaksALockedAxis(in SnapCandidate c, List<Vector3> locked,
            ref float du, ref float dv)
        {
            foreach (var n in locked)
            {
                if (Mathf.Abs(c.planeShift * Vector3.Dot(c.normal, n)) > ZeroShiftEpsilon) return true;
                if (Mathf.Abs(du * Vector3.Dot(c.u, n)) > ZeroShiftEpsilon) du = 0f;
                if (Mathf.Abs(dv * Vector3.Dot(c.v, n)) > ZeroShiftEpsilon) dv = 0f;
            }
            return false;
        }

        private static bool IsBetter(float score, float dist, bool isLineContact,
            float bestScore, float bestDist, bool bestIsLineContact)
        {
            if (score < bestScore - ZeroShiftEpsilon) return true;
            if (score > bestScore + ZeroShiftEpsilon) return false;
            if (bestIsLineContact != isLineContact) return bestIsLineContact;
            return dist < bestDist - ZeroShiftEpsilon;
        }
    }
}
