using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ValidationCore
    {
        [ThreadStatic] private static OverlapMarks? _marksPerThread;

        private static OverlapMarks Marks => _marksPerThread ??= new OverlapMarks();

        public static float ContactDistUnits => Tolerance.ContactMm * AppConstants.MM_TO_UNITS;

        [ThreadStatic] private static int _pairsProcessedPerThread;

        public static int PairsProcessed => _pairsProcessedPerThread;

        public static int TakePairsProcessed()
        {
            int n = _pairsProcessedPerThread;
            _pairsProcessedPerThread = 0;
            return n;
        }

        public static CoreValidationResult Validate(IReadOnlyList<ValidationElement> all)
        {
            var result = new CoreValidationResult();
            Validate(all, result);
            return result;
        }

        public static void Validate(IReadOnlyList<ValidationElement> all, CoreValidationResult result)
        {
            result.Clear();
            ValidationBroadPhase.Clear();
            var marks = Marks;
            marks.Clear();

            int n = all?.Count ?? 0;
            if (n == 0)
            {
                result.IsValid = true;
                return;
            }

            var candidates = ValidationBroadPhase.CandidatePairsInNestedLoopOrder(
                all!, ContactDistUnits);
            ProcessPairs(all!, candidates, result, marks);
            Finish(all!, result, marks);
        }

        public static void ProcessPairs(IReadOnlyList<ValidationElement> all,
            IReadOnlyList<(int lo, int hi)> pairs, CoreValidationResult result, OverlapMarks marks)
        {
            float contactDist = ContactDistUnits;
            _pairsProcessedPerThread += pairs.Count;
            for (int c = 0; c < pairs.Count; c++)
                ProcessPair(all, pairs[c].lo, pairs[c].hi, contactDist, result, marks);
        }

        public static void Finish(IReadOnlyList<ValidationElement> all,
            CoreValidationResult result, OverlapMarks marks)
        {
            CheckConnectivity(all, result);
            CheckWallHeightConstraints(all, result);

            var overlapping = marks.InOrder;
            for (int k = 0; k < overlapping.Count; k++)
            {
                int i = overlapping[k];
                if (all[i].Is(ElementKind.Anchor) && !marks.AnchorIsInIllegalOverlap(i)) continue;
                if (!result.Violations.Contains(i))
                    result.Violations.Add(i);
            }
            result.IsValid = result.Violations.Count == 0;
        }

        private static void ProcessPair(IReadOnlyList<ValidationElement> all,
            int aIdx, int bIdx, float contactDist, CoreValidationResult result, OverlapMarks marks)
        {
            var a = all[aIdx];
            var b = all[bIdx];

            CheckExtraBodyAgainstNeighbour(a, aIdx, b, bIdx, contactDist, result, marks);
            CheckExtraBodyAgainstNeighbour(b, bIdx, a, aIdx, contactDist, result, marks);

            if (a.IgnoredInPairs || b.IgnoredInPairs) return;

            if (TryScrewLegContact(a, b, aIdx, bIdx, contactDist, result)) return;

            if (!FaceContacts.AABBsIntersect(a.Geometry, b.Geometry, contactDist))
            {
                AddFaceContacts(aIdx, bIdx, a.Faces, b.Faces, contactDist, result);
                return;
            }

            if (SharesSpaceLegitimately(a, b)) return;
            if (TrySeatedGrooveContact(a, b, aIdx, bIdx, result)) return;

            marks.Mark(aIdx);
            marks.Mark(bIdx);

            if (a.Is(ElementKind.Anchor) && b.Is(ElementKind.Anchor) && IsLegitAnchorPair(a, b)) return;

            result.AddDiagnostic(aIdx, bIdx, ViolationKind.Overlap);
            if (a.Is(ElementKind.Anchor)) marks.MarkAnchorInIllegalOverlap(aIdx);
            if (b.Is(ElementKind.Anchor)) marks.MarkAnchorInIllegalOverlap(bIdx);
        }

        private static bool TryScrewLegContact(in ValidationElement a, in ValidationElement b,
            int aIdx, int bIdx, float contactDist, CoreValidationResult result)
        {
            bool aLeg = a.Is(ElementKind.ScrewLeg);
            bool bLeg = b.Is(ElementKind.ScrewLeg);
            if (aLeg == bLeg) return false;
            if (!a.IsPairedWith(b) && !b.IsPairedWith(a)) return false;

            var leg = aLeg ? a : b;
            var host = aLeg ? b : a;
            if (!SolidReaches(leg, host.Geometry, contactDist)) return false;

            int legFace = FaceContacts.FaceIndexByNormal(leg.Faces, UpNormal);
            int hostFace = FaceContacts.FaceIndexByNormal(host.Faces, -UpNormal);
            float area = LegSectionArea(leg);

            result.Contacts.Add(aLeg
                ? new CoreContact(aIdx, bIdx, legFace, hostFace, area, true)
                : new CoreContact(aIdx, bIdx, hostFace, legFace, area, true));
            return !FaceContacts.AABBsIntersect(leg.Geometry, host.Geometry, contactDist);
        }

        private static bool SolidReaches(in ValidationElement e, in ElementGeometry other,
            float contactDist) =>
            FaceContacts.AABBsIntersect(e.Geometry, other, contactDist)
            || (e.HasExtraBody && FaceContacts.AABBsIntersect(e.ExtraBody, other, contactDist));

        private static readonly Vector3 UpNormal = new Vector3(0f, 1f, 0f);

        private static float LegSectionArea(in ValidationElement leg)
        {
            var size = leg.Geometry.Max - leg.Geometry.Min;
            return Mathf.Abs(size.x * size.z);
        }

        private static bool SharesSpaceLegitimately(in ValidationElement a, in ValidationElement b)
        {
            if (a.Is(ElementKind.ScrewLeg) || b.Is(ElementKind.ScrewLeg)) return false;

            bool aDrawer = a.Is(ElementKind.Drawer);
            bool bDrawer = b.Is(ElementKind.Drawer);

            if (aDrawer && bDrawer) return a.IsPairedWith(b) || b.IsPairedWith(a);
            if (aDrawer != bDrawer) return a.SharesModuleWith(b);
            return false;
        }

        private static void CheckExtraBodyAgainstNeighbour(in ValidationElement owner, int ownerIdx,
            in ValidationElement other, int otherIdx, float contactDist, CoreValidationResult result,
            OverlapMarks marks)
        {
            if (!owner.HasExtraBody || owner.HostIndex == otherIdx) return;
            if (!BlocksExtraBody(owner, other)) return;
            if (!FaceContacts.AABBsIntersect(owner.ExtraBody, other.Geometry, contactDist)) return;

            marks.Mark(ownerIdx);
            marks.Mark(otherIdx);
            result.AddDiagnostic(ownerIdx, otherIdx, ViolationKind.Overlap);
        }

        private static bool BlocksExtraBody(in ValidationElement owner, in ValidationElement other) =>
            owner.Is(ElementKind.ScrewLeg)
                ? !other.IgnoredInPairs && !other.Is(ElementKind.Anchor | ElementKind.ScrewLeg)
                : IsCarcass(other);

        private static bool IsCarcass(in ValidationElement e) =>
            !e.Is(ElementKind.Anchor | ElementKind.Opening | ElementKind.Drawer
                  | ElementKind.Decor | ElementKind.Recessed | ElementKind.Facade)
            && !e.IsPanel;

        private static bool IsLegitAnchorPair(in ValidationElement a, in ValidationElement b) =>
            a.Is(ElementKind.FloorAnchor) || b.Is(ElementKind.FloorAnchor) ||
            a.Is(ElementKind.Opening) || b.Is(ElementKind.Opening) ||
            WallCentreline.MeetAtSharedCorner(a.Centreline, b.Centreline);

        private static void AddFaceContacts(int aIdx, int bIdx, Face[] facesA, Face[] facesB,
            float contactDist, CoreValidationResult result)
        {
            foreach (var hit in new FaceContactScan(facesA, facesB, FaceAlignment.ParallelEitherWay,
                         FaceContactScan.NoLowerGapBound, contactDist, 0f))
            {
                result.Contacts.Add(new CoreContact(aIdx, bIdx, hit.IndexA, hit.IndexB,
                    hit.OverlapArea, hit.OverlapRatio >= Tolerance.MinSupportOverlap));
            }
        }

        private static bool TrySeatedGrooveContact(in ValidationElement a, in ValidationElement b,
            int aIdx, int bIdx, CoreValidationResult result)
        {
            if (GrooveSeating.IsSeatedInGroove(a, b, out var seat))
            {
                result.Contacts.Add(GrooveSeating.SeatContact(a, b, aIdx, bIdx, seat, panelIsA: true));
                return true;
            }
            if (GrooveSeating.IsSeatedInGroove(b, a, out seat))
            {
                result.Contacts.Add(GrooveSeating.SeatContact(a, b, aIdx, bIdx, seat, panelIsA: false));
                return true;
            }
            return false;
        }

        public static bool HasAnchor(IReadOnlyList<ValidationElement> all)
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i].Is(ElementKind.Anchor)) return true;
            return false;
        }

        private static void CheckConnectivity(IReadOnlyList<ValidationElement> all,
            CoreValidationResult result)
        {
            int n = all.Count;
            var adjacency = new List<int>[n];
            for (int i = 0; i < n; i++) adjacency[i] = new List<int>();

            var hasContact = new bool[n];
            foreach (var contact in result.Contacts)
            {
                if (!contact.IsFaceToFace) continue;
                hasContact[contact.A] = true;
                hasContact[contact.B] = true;
                adjacency[contact.A].Add(contact.B);
                adjacency[contact.B].Add(contact.A);
            }

            var visited = new bool[n];
            var queue = new Queue<int>();

            for (int i = 0; i < n; i++)
            {
                if (!all[i].Is(ElementKind.Anchor)) continue;
                visited[i] = true;
                queue.Enqueue(i);
            }

            if (!HasAnchor(all))
            {
                int start = 0;
                for (int i = 0; i < n; i++)
                    if (hasContact[i]) { start = i; break; }
                visited[start] = true;
                queue.Enqueue(start);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighbor in adjacency[current])
                {
                    if (visited[neighbor]) continue;
                    visited[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (all[i].NeedsNoSupport) continue;
                if (!visited[i] || !hasContact[i])
                {
                    result.Violations.Add(i);
                    result.AddDiagnostic(i, ValidationElement.NoIndex, ViolationKind.Unsupported);
                }
            }

            GroupUnsupportedByConnectivity(adjacency, result);

            result.IsValid = result.Violations.Count == 0;
        }

        private static void GroupUnsupportedByConnectivity(List<int>[] adjacency,
            CoreValidationResult result)
        {
            var ungrouped = new List<int>(result.Violations);
            while (ungrouped.Count > 0)
            {
                var group = new List<int>();
                var gq = new Queue<int>();
                gq.Enqueue(ungrouped[0]);

                while (gq.Count > 0)
                {
                    int current = gq.Dequeue();
                    if (!ungrouped.Remove(current)) continue;
                    group.Add(current);

                    foreach (int neighbor in adjacency[current])
                        if (ungrouped.Contains(neighbor) && !group.Contains(neighbor))
                            gq.Enqueue(neighbor);
                }

                if (group.Count > 0)
                    result.IsolatedGroups.Add(group);
            }
        }

        private static void CheckWallHeightConstraints(IReadOnlyList<ValidationElement> all,
            CoreValidationResult result)
        {
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (!e.Is(ElementKind.Opening)) continue;

                int wallIndex = e.AttachedWallIndex;
                if (wallIndex < 0 || wallIndex >= all.Count) continue;

                var wall = all[wallIndex];
                if (wall.HeightSpan.Size <= 0f) continue;

                if (e.HeightSpan.Max > wall.HeightSpan.Max + Tolerance.EpsilonUnits ||
                    e.HeightSpan.Min < wall.HeightSpan.Min - Tolerance.EpsilonUnits)
                {
                    if (!result.Violations.Contains(i))
                        result.Violations.Add(i);
                    result.AddDiagnostic(i, ValidationElement.NoIndex, ViolationKind.OutOfWallBounds);
                }
            }
        }
    }
}
