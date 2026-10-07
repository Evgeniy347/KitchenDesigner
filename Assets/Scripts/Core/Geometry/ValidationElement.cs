using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct ValidationElement
    {
        public readonly ElementGeometry Geometry;
        public readonly Vector3[] Vertices;
        public readonly ElementKind Kind;
        public readonly int GroupId;
        public readonly string? PairedName;
        public readonly Span HeightSpan;
        public readonly int AttachedWallIndex;
        public readonly ElementGeometry ExtraBody;
        public readonly bool HasExtraBody;
        public readonly int HostIndex;
        public readonly WallCentreline Centreline;
        public readonly SofaBedReach? BedReach;

        public const int NoGroup = 0;
        public const int NoIndex = -1;

        public ValidationElement(ElementGeometry geometry, Vector3[] vertices, ElementKind kind,
            int groupId, string? pairedName, Span heightSpan, int attachedWallIndex,
            ElementGeometry extraBody = default, bool hasExtraBody = false,
            int hostIndex = NoIndex, WallCentreline centreline = default,
            SofaBedReach? bedReach = null)
        {
            Centreline = centreline;
            BedReach = bedReach;
            Geometry = geometry;
            Vertices = vertices;
            Kind = kind;
            GroupId = groupId;
            PairedName = pairedName;
            HeightSpan = heightSpan;
            AttachedWallIndex = attachedWallIndex;
            ExtraBody = extraBody;
            HasExtraBody = hasExtraBody;
            HostIndex = hostIndex;
        }

        public string Name => Geometry.Name;
        public Face[] Faces => Geometry.Faces;
        public bool IsPanel => Geometry.IsPanel;
        public bool Is(ElementKind kind) => (Kind & kind) != 0;

        public bool IgnoredInPairs => Is(ElementKind.Decor | ElementKind.Recessed);

        public bool StaysOutOfTheBedsWay => Is(ElementKind.Decor | ElementKind.Recessed
            | ElementKind.FloorAnchor | ElementKind.Foundation | ElementKind.Opening);

        public bool NeedsNoSupport => Is(ElementKind.Anchor | ElementKind.Drawer
            | ElementKind.SelfSupported
            | ElementKind.Decor | ElementKind.Recessed | ElementKind.FloatingFacade);

        public bool ValidatesTheSameAs(in ValidationElement other) =>
            Kind == other.Kind
            && GroupId == other.GroupId
            && AttachedWallIndex == other.AttachedWallIndex
            && HostIndex == other.HostIndex
            && HasExtraBody == other.HasExtraBody
            && string.Equals(PairedName, other.PairedName, StringComparison.Ordinal)
            && HeightSpan.Min.Equals(other.HeightSpan.Min)
            && HeightSpan.Max.Equals(other.HeightSpan.Max)
            && SameCentreline(Centreline, other.Centreline)
            && SameSolid(Geometry, other.Geometry)
            && (!HasExtraBody || SameEnvelope(ExtraBody, other.ExtraBody))
            && SamePoints(Vertices, other.Vertices);

        private static bool SameSolid(in ElementGeometry a, in ElementGeometry b) =>
            a.Id == b.Id
            && a.IsPanel == b.IsPanel
            && string.Equals(a.Name, b.Name, StringComparison.Ordinal)
            && SameEnvelope(a, b)
            && SameFaces(a.Faces, b.Faces)
            && SameFaces(a.GrooveSeatFaces, b.GrooveSeatFaces)
            && SameFaces(a.GrooveWallFaces, b.GrooveWallFaces);

        private static bool SameEnvelope(in ElementGeometry a, in ElementGeometry b) =>
            SamePoint(a.Min, b.Min) && SamePoint(a.Max, b.Max);

        private static bool SameCentreline(in WallCentreline a, in WallCentreline b) =>
            a.IsDefined == b.IsDefined
            && (!a.IsDefined
                || (SamePoint(a.Start, b.Start) && SamePoint(a.End, b.End)
                    && SamePoint(a.Direction, b.Direction)));

        private static bool SameFaces(Face[]? a, Face[]? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (!SameFace(a[i], b[i])) return false;
            return true;
        }

        private static bool SameFace(in Face a, in Face b) =>
            SamePoint(a.center, b.center)
            && SamePoint(a.normal, b.normal)
            && a.size.x.Equals(b.size.x) && a.size.y.Equals(b.size.y)
            && SamePoint(a.rightAxis, b.rightAxis)
            && SamePoint(a.upAxis, b.upAxis);

        private static bool SamePoints(Vector3[]? a, Vector3[]? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (!SamePoint(a[i], b[i])) return false;
            return true;
        }

        private static bool SamePoint(in Vector3 a, in Vector3 b) =>
            a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z);

        public bool SharesModuleWith(in ValidationElement other) =>
            GroupId != NoGroup && GroupId == other.GroupId;

        public bool IsPairedWith(in ValidationElement other) =>
            !string.IsNullOrEmpty(PairedName) && PairedName == other.Name;
    }
}
