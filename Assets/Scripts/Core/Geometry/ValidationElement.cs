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

        public const int NoGroup = 0;
        public const int NoIndex = -1;

        public ValidationElement(ElementGeometry geometry, Vector3[] vertices, ElementKind kind,
            int groupId, string? pairedName, Span heightSpan, int attachedWallIndex,
            ElementGeometry extraBody = default, bool hasExtraBody = false,
            int hostIndex = NoIndex, WallCentreline centreline = default)
        {
            Centreline = centreline;
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

        public bool NeedsNoSupport => Is(ElementKind.Anchor | ElementKind.Drawer
            | ElementKind.SelfSupported
            | ElementKind.Decor | ElementKind.Recessed | ElementKind.FloatingFacade);

        public bool SharesModuleWith(in ValidationElement other) =>
            GroupId != NoGroup && GroupId == other.GroupId;

        public bool IsPairedWith(in ValidationElement other) =>
            !string.IsNullOrEmpty(PairedName) && PairedName == other.Name;
    }
}
