using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct ContactViolation
    {
        public readonly KitchenElement element;
        public readonly KitchenElement? other;
        public readonly ViolationKind kind;

        public ContactViolation(KitchenElement element, KitchenElement? other, ViolationKind kind)
        {
            this.element = element;
            this.other = other;
            this.kind = kind;
        }
    }

    public class ValidationResult
    {
        public List<FaceContact> contacts = new List<FaceContact>();
        public List<KitchenElement> violations = new List<KitchenElement>();
        public List<List<KitchenElement>> isolatedGroups = new List<List<KitchenElement>>();
        public bool isValid;

        public List<ContactViolation>? diagnostics;

        public void AddDiagnostic(KitchenElement element, KitchenElement? other, ViolationKind kind)
        {
            (diagnostics ??= new List<ContactViolation>()).Add(new ContactViolation(element, other, kind));
        }
    }

    public static class ConstraintValidator
    {
        public const float GapNoiseMm = 0.01f;

        public const float BroadPhaseFloorMm = 8f;

        public const float MinSeatedFractionOfGrooveDepth = 0.5f;

        private static readonly List<KitchenElement> _elems = new List<KitchenElement>();
        private static readonly List<ValidationElement> _snapshots = new List<ValidationElement>();
        private static readonly CoreValidationResult _core = new CoreValidationResult();

        public static ValidationResult Validate(List<KitchenElement> all)
        {
            var result = new ValidationResult();

            _elems.Clear();
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null) _elems.Add(all[i]);

            if (_elems.Count == 0)
            {
                result.isValid = true;
                return result;
            }

            ValidationSnapshot.Build(_elems, _snapshots);
            var core = _core;
            ValidationCore.Validate(_snapshots, core);

            foreach (var c in core.Contacts)
                result.contacts.Add(new FaceContact(_elems[c.A], _elems[c.B],
                    c.FaceA, c.FaceB, c.Area, c.IsFaceToFace));

            foreach (int v in core.Violations)
                result.violations.Add(_elems[v]);

            foreach (var group in core.IsolatedGroups)
            {
                var mapped = new List<KitchenElement>(group.Count);
                foreach (int i in group) mapped.Add(_elems[i]);
                result.isolatedGroups.Add(mapped);
            }

            if (core.Diagnostics != null)
                foreach (var d in core.Diagnostics)
                    result.AddDiagnostic(_elems[d.Element],
                        d.Other >= 0 ? _elems[d.Other] : null, d.Kind);

            result.isValid = core.IsValid;
            return result;
        }

        public static bool HasViolationNear(ValidationResult result, KitchenElement element, float radiusUnits)
        {
            if (result == null || element == null || result.violations.Count == 0) return false;

            var ea = element.ToGeometry();
            foreach (var v in result.violations)
            {
                if (v == element) return true;
                if (v == null) continue;
                var va = v.ToGeometry();
                if (va.Min.x <= ea.Max.x + radiusUnits && va.Max.x >= ea.Min.x - radiusUnits &&
                    va.Min.y <= ea.Max.y + radiusUnits && va.Max.y >= ea.Min.y - radiusUnits &&
                    va.Min.z <= ea.Max.z + radiusUnits && va.Max.z >= ea.Min.z - radiusUnits)
                    return true;
            }
            return false;
        }

        public static bool AreInFaceToFaceContact(KitchenElement a, KitchenElement b) =>
            FaceContacts.AreInFaceToFaceContact(a.GetFaces(), b.GetFaces(),
                Tolerance.ContactMm * AppConstants.MM_TO_UNITS);

        public static bool AreFacadeMountable(KitchenElement host, KitchenElement facade,
            float mountGapMm)
        {
            if (host == null || facade == null) return false;
            if (AreInFaceToFaceContact(host, facade)) return true;
            if (mountGapMm <= Tolerance.ContactMm) return false;
            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            return FaceContacts.MinParallelGap(host.GetFaces(), facade.GetFaces(),
                contactDist, mountGapMm * AppConstants.MM_TO_UNITS) > 0f;
        }

        public readonly struct NearContact
        {
            public readonly KitchenElement a;
            public readonly KitchenElement b;
            public readonly float gapMm;
            public readonly NearContactKind kind;
            public NearContact(KitchenElement a, KitchenElement b, float gapMm, NearContactKind kind)
            {
                this.a = a; this.b = b; this.gapMm = gapMm; this.kind = kind;
            }
        }

        public enum NearContactKind
        {
            TooSmall,
            TooLarge,
        }

        public static List<NearContact> FindNearContacts(List<KitchenElement> all,
            float minGapMm, float maxGapMm)
        {
            var result = new List<NearContact>();
            if (all == null || all.Count < 2) return result;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float broadPhaseMm = Mathf.Max(maxGapMm, BroadPhaseFloorMm);
            float broadPhase = broadPhaseMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;

            int n = all.Count;
            var geo = new ElementGeometry[n];
            var ok = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var e = all[i];
                if (e == null || IsAnchor(e) || IsIgnoredInPairs(e)) continue;
                ok[i] = true;
                geo[i] = e.ToGeometry();
            }

            for (int i = 0; i < n; i++)
            {
                if (!ok[i]) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (!ok[j]) continue;
                    if (!FaceContacts.AABBsIntersect(geo[i], geo[j], -broadPhase)) continue;
                    if (FaceContacts.AreInFaceToFaceContact(geo[i].Faces, geo[j].Faces, contactDist)) continue;
                    if (PanelEngagesGroove(all[i], all[j]) || PanelEngagesGroove(all[j], all[i])) continue;
                    if (IsDishwasherFacadePair(all[i], all[j])) continue;

                    float sum = FaceContacts.SumParallelGaps(geo[i].Faces, geo[j].Faces, contactDist, broadPhase);
                    if (sum <= 0f) continue;
                    float sumMm = sum * toMm;
                    if (sumMm + GapNoiseMm < minGapMm)
                        result.Add(new NearContact(all[i], all[j], sumMm, NearContactKind.TooSmall));
                    else if (sumMm > maxGapMm + GapNoiseMm)
                        result.Add(new NearContact(all[i], all[j], sumMm, NearContactKind.TooLarge));
                }
            }
            return result;
        }

        private static bool IsDishwasherFacadePair(KitchenElement a, KitchenElement b)
        {
            KitchenElement? dishwasher = null;
            KitchenElement? facade = null;
            if (a is DishwasherElement && b is FacadeElement) { dishwasher = a; facade = b; }
            else if (b is DishwasherElement && a is FacadeElement) { dishwasher = b; facade = a; }
            if (dishwasher == null || facade == null) return false;
            return !string.IsNullOrEmpty(((DishwasherElement)dishwasher).AttachedFacadeName)
                && ((DishwasherElement)dishwasher).AttachedFacadeName == ((FacadeElement)facade).PartName;
        }

        public readonly struct DishwasherBackGapIssue
        {
            public readonly DishwasherElement dishwasher;
            public readonly FacadeElement facade;
            public readonly float gapMm;
            public DishwasherBackGapIssue(DishwasherElement dishwasher, FacadeElement facade, float gapMm)
            {
                this.dishwasher = dishwasher;
                this.facade = facade;
                this.gapMm = gapMm;
            }
        }

        public static List<DishwasherBackGapIssue> FindDishwasherFacadeBackGaps(List<KitchenElement> all)
        {
            var result = new List<DishwasherBackGapIssue>();
            if (all == null) return result;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float mountMm = DishwasherElement.FACADE_MOUNT_GAP_MM;
            float maxGap = mountMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;

            foreach (var e in all)
            {
                if (!(e is DishwasherElement dw)) continue;
                var facade = dw.FindAttachedFacade();
                if (facade == null) continue;

                float sum = DrawerLinks.WithFacadeClosed(facade, DrawerLinks.IsFacadeDisplacedBy(dw),
                    () => FaceContacts.SumParallelGaps(e.GetFaces(), facade.GetFaces(), contactDist, maxGap));
                float sumMm = sum * toMm;
                if (sumMm <= 0f) continue;
                if (sumMm + GapNoiseMm < mountMm)
                    result.Add(new DishwasherBackGapIssue(dw, facade, sumMm));
            }
            return result;
        }

        public readonly struct DishwasherSupportIssue
        {
            public readonly DishwasherElement dishwasher;
            public readonly KitchenElement? blocker;
            public readonly float sinkMm;
            public DishwasherSupportIssue(DishwasherElement dishwasher, KitchenElement? blocker, float sinkMm)
            {
                this.dishwasher = dishwasher;
                this.blocker = blocker;
                this.sinkMm = sinkMm;
            }
        }

        public static List<DishwasherSupportIssue> FindDishwasherSupportIssues(List<KitchenElement> all)
        {
            var result = new List<DishwasherSupportIssue>();
            if (all == null) return result;

            float eps = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;
            float reach = DishwasherElement.FEET_ADJUST_MM * AppConstants.MM_TO_UNITS;

            foreach (var e in all)
            {
                if (!(e is DishwasherElement dw)) continue;

                float soleY = dw.SoleCenterWorld.y;
                var dwGeo = dw.ToGeometry();
                var facade = dw.FindAttachedFacade();

                KitchenElement? blocker = null;
                float deepest = 0f;
                bool supported = false;

                foreach (var other in all)
                {
                    if (other == null || ReferenceEquals(other, e)) continue;
                    if (ReferenceEquals(other, facade)) continue;
                    if (other is LightSourceElement) continue;

                    var g = other.ToGeometry();
                    if (g.Min.x >= dwGeo.Max.x - eps || g.Max.x <= dwGeo.Min.x + eps) continue;
                    if (g.Min.z >= dwGeo.Max.z - eps || g.Max.z <= dwGeo.Min.z + eps) continue;

                    if (g.Max.y <= soleY + eps && g.Max.y >= soleY - reach - eps)
                    {
                        supported = true;
                        break;
                    }

                    if (g.Min.y < soleY - eps && g.Max.y > soleY + eps)
                    {
                        float sink = (g.Max.y - soleY) * toMm;
                        if (sink > deepest) { deepest = sink; blocker = other; }
                    }
                }

                if (supported) continue;
                result.Add(new DishwasherSupportIssue(dw, blocker, deepest));
            }
            return result;
        }

        public readonly struct UnseatedPanel
        {
            public readonly KitchenElement panel;
            public readonly KitchenElement board;
            public readonly float insertionMm;
            public readonly float depthMm;
            public UnseatedPanel(KitchenElement panel, KitchenElement board, float insertionMm, float depthMm)
            {
                this.panel = panel; this.board = board;
                this.insertionMm = insertionMm; this.depthMm = depthMm;
            }
        }

        public static List<UnseatedPanel> FindUnseatedPanels(List<KitchenElement> all)
        {
            var result = new List<UnseatedPanel>();
            if (all == null || all.Count < 2) return result;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float engageMargin = GrooveSeating.PanelEngageMarginMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;

            foreach (var p in all)
            {
                if (!(p is PanelElement)) continue;
                var pverts = p.GetVertices();

                foreach (var b in all)
                {
                    if (b == null || b == p || b.Grooves.Count == 0) continue;
                    var seats = b.GetGrooveSeatFaces();
                    if (seats.Length == 0) continue;

                    float depthUnits = GrooveDepthUnits(b);
                    if (depthUnits <= 0f) continue;

                    foreach (var seat in seats)
                    {
                        if (!GrooveSeating.PanelEngagesSeat(pverts, seat, depthUnits, engageMargin,
                                contactDist, out float minAlong))
                            continue;

                        float insertion = depthUnits - minAlong;
                        if (insertion < depthUnits * MinSeatedFractionOfGrooveDepth)
                            result.Add(new UnseatedPanel(p, b, Mathf.Max(0f, insertion) * toMm, depthUnits * toMm));
                    }
                }
            }
            return result;
        }

        private static bool PanelEngagesGroove(KitchenElement panel, KitchenElement board)
        {
            if (!(panel is PanelElement) || board == null || board.Grooves.Count == 0) return false;

            var seats = board.GetGrooveSeatFaces();
            if (seats.Length == 0) return false;

            float depthUnits = GrooveDepthUnits(board);
            if (depthUnits <= 0f) return false;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float engageMargin = GrooveSeating.PanelEngageMarginMm * AppConstants.MM_TO_UNITS;

            var pverts = panel.GetVertices();
            foreach (var seat in seats)
                if (GrooveSeating.PanelEngagesSeat(pverts, seat, depthUnits, engageMargin, contactDist, out _))
                    return true;
            return false;
        }

        private static float GrooveDepthUnits(KitchenElement board) =>
            GrooveMesh.DepthFraction(board.DimensionsMM) * board.transform.localScale.z;

        private static bool IsAnchor(KitchenElement e) =>
            e != null && (e.GetComponent<BasePlate>() != null || e.GetComponent<Wall>() != null
                || e is WindowElement || e is DoorElement || e is FloorElement);

        private static bool IsIgnoredInPairs(KitchenElement e) =>
            e is LightSourceElement || e is SinkElement || e is CooktopElement;
    }
}
