using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ScrewLegHosting
    {
        public const int NoHost = -1;

        public static int HostIndex(in ElementGeometry thread,
            IReadOnlyList<ElementGeometry> candidates, float marginUnits)
        {
            if (thread.IsEmpty || candidates == null) return NoHost;

            int best = NoHost;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c.IsEmpty) continue;
                if (!Reaches(thread, c, marginUnits)) continue;
                if (best == NoHost || EntersEarlier(c, candidates[best])) best = i;
            }
            return best;
        }

        public static float InsertionMM(in ElementGeometry thread, in ElementGeometry host)
        {
            if (thread.IsEmpty || host.IsEmpty) return 0f;
            float entry = host.Min.y > thread.Min.y ? host.Min.y : thread.Min.y;
            float inside = thread.Max.y - entry;
            return inside <= 0f ? 0f : inside / AppConstants.MM_TO_UNITS;
        }

        public static ScrewLegMargins Margins(Vector3 legCentre,
            Vector3 mountNormal, in ElementGeometry host)
        {
            if (host.IsEmpty) return default;

            var face = host.Faces[EntryFace(host, mountNormal)];
            var toCentre = legCentre - face.center;
            float across = Vector3.Dot(toCentre, face.rightAxis);
            float along = Vector3.Dot(toCentre, face.upAxis);
            float halfAcross = face.size.x * 0.5f;
            float halfAlong = face.size.y * 0.5f;
            float toMM = 1f / AppConstants.MM_TO_UNITS;

            return new ScrewLegMargins(face.rightAxis, face.upAxis,
                (halfAcross + across) * toMM, (halfAcross - across) * toMM,
                (halfAlong - along) * toMM, (halfAlong + along) * toMM);
        }

        public static int EntryFace(in ElementGeometry host, Vector3 mountNormal)
        {
            int best = 0;
            float bestDot = float.MaxValue;
            for (int i = 0; i < host.Faces.Length; i++)
            {
                float dot = Vector3.Dot(host.Faces[i].normal, mountNormal);
                if (dot >= bestDot) continue;
                bestDot = dot;
                best = i;
            }
            return best;
        }

        public static bool Reaches(in ElementGeometry thread, in ElementGeometry candidate,
            float marginUnits) =>
            !thread.IsEmpty && !candidate.IsEmpty
            && FaceContacts.AABBsIntersect(thread, candidate, marginUnits);

        private static bool EntersEarlier(in ElementGeometry candidate, in ElementGeometry best)
        {
            float delta = candidate.Min.y - best.Min.y;
            if (delta < -Tolerance.EpsilonUnits) return true;
            if (delta > Tolerance.EpsilonUnits) return false;
            return string.CompareOrdinal(candidate.Name, best.Name) < 0;
        }
    }
}
