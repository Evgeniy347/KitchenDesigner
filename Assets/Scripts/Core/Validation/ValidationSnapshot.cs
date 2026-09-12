using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ValidationSnapshot
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int _geometryBuilds;

        public static int GeometryBuildsInLastPass { get; private set; }

        public static int TakeGeometryBuilds()
        {
            int n = _geometryBuilds;
            _geometryBuilds = 0;
            return n;
        }
#endif

        private readonly struct Probe
        {
            public readonly Wall? Wall;
            public readonly bool IsFloor;
            public readonly bool Reusable;

            public Probe(Wall? wall, bool isFloor, bool reusable)
            {
                Wall = wall;
                IsFloor = isFloor;
                Reusable = reusable;
            }
        }

        private static readonly List<Probe> _probes = new List<Probe>();

        public static void Build(List<KitchenElement> elements, List<ValidationElement> into)
        {
            using var _ = PerfMarkers.ValidationSnapshotBuild.Auto();

            into.Clear();
            if (elements == null || elements.Count == 0) return;

            ElementSnapshotReuse.BeginPass();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GeometryBuildsInLastPass = 0;
#endif

            Dictionary<string, int>? wallIndexByName = null;
            Dictionary<string, int>? partIndexByName = null;
            bool hasHosted = false;
            _probes.Clear();
            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                if (e == null)
                {
                    _probes.Add(default);
                    continue;
                }

                var wall = e.GetComponent<Wall>();
                _probes.Add(new Probe(wall, e.GetComponent<BasePlate>() != null,
                    ReusesItsSnapshot(e)));

                if (wall != null)
                    (wallIndexByName ??= new Dictionary<string, int>())[e.gameObject.name] = i;
                if (e is CooktopElement || e is ScrewLegElement) hasHosted = true;
            }

            if (hasHosted)
            {
                partIndexByName = new Dictionary<string, int>();
                for (int i = 0; i < elements.Count; i++)
                    if (elements[i] != null) partIndexByName[elements[i].PartName] = i;
            }

            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                var probe = _probes[i];
                if (probe.Reusable && ElementSnapshotReuse.TryReuse(e, probe.Wall, probe.IsFloor,
                    PairedNameOf(e), out var reused))
                {
                    into.Add(reused);
                    continue;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _geometryBuilds++;
                GeometryBuildsInLastPass++;
#endif
                var built = Build(e, probe.Wall, probe.IsFloor, wallIndexByName, partIndexByName);
                if (probe.Reusable) ElementSnapshotReuse.Keep(e, probe.Wall, probe.IsFloor, built);
                into.Add(built);
            }

            ElementSnapshotReuse.DropWhatThisPassNeverSaw(elements.Count);
        }

        private static readonly System.Type[] ProvedBoxBuilders =
            { typeof(KitchenElement), typeof(FacadeElement), typeof(DrawerElement) };

        private static readonly Dictionary<System.Type, bool> _reusableByType =
            new Dictionary<System.Type, bool>();

        public static bool ReusesItsSnapshot(KitchenElement e)
        {
            var type = e.GetType();
            if (_reusableByType.TryGetValue(type, out bool known)) return known;

            bool reusable = SnapshotDependsOnNothingButTheElement(type)
                && ValidationGeometryContract.BoxIsBuiltOnlyBy(type, ProvedBoxBuilders);
            _reusableByType[type] = reusable;
            return reusable;
        }

        private static bool SnapshotDependsOnNothingButTheElement(System.Type type) =>
            !typeof(ScrewLegElement).IsAssignableFrom(type)
            && !typeof(CooktopElement).IsAssignableFrom(type)
            && !typeof(WallOpeningElement).IsAssignableFrom(type)
            && !typeof(ISnapPorts).IsAssignableFrom(type)
            && !typeof(IMountsOnTarget).IsAssignableFrom(type);

        private static string? PairedNameOf(KitchenElement e) =>
            (e as DrawerElement)?.PairedDrawerName ?? (e as ScrewLegElement)?.HostPartName;

        public static ElementGeometry MainBody(KitchenElement e) =>
            e is ScrewLegElement leg ? leg.BaseBody : e.ToGeometry();

        public static bool TryExtraBody(KitchenElement e, out ElementGeometry extra,
            out string? hostName)
        {
            if (e is ScrewLegElement leg)
            {
                extra = leg.ThreadBody;
                hostName = leg.HostPartName;
                return true;
            }
            if (e is CooktopElement cooktop && cooktop.IsAttached)
            {
                extra = ElementGeometry.Box(e.PartName + "/body",
                    cooktop.BodyCenter, cooktop.BodySize, cooktop.transform.rotation);
                hostName = cooktop.AttachedPartName;
                return true;
            }
            extra = default;
            hostName = null;
            return false;
        }

        public static ElementGeometry[] SolidBodies(KitchenElement e) =>
            TryExtraBody(e, out var extra, out _)
                ? new[] { MainBody(e), extra }
                : new[] { MainBody(e) };

        public static ElementKind KindOf(KitchenElement e) =>
            KindOf(e, e.GetComponent<Wall>(), e.GetComponent<BasePlate>() != null);

        public static bool IsAnchor(KitchenElement e) =>
            e != null && (KindOf(e) & ElementKind.Anchor) != 0;

        public static bool IsPanel(KitchenElement e) => e is PanelElement;

        public static bool IsDecor(KitchenElement e) => e is LightSourceElement;

        public static DishwasherElement? AsDishwasher(KitchenElement e) => e as DishwasherElement;

        public static FacadeElement? AsFacade(KitchenElement e) => e as FacadeElement;

        private static ValidationElement Build(KitchenElement e, Wall? wall, bool hasBasePlate,
            Dictionary<string, int>? wallIndexByName,
            Dictionary<string, int>? partIndexByName = null)
        {
            var kind = KindOf(e, wall, hasBasePlate);

            float centerY = wall != null ? wall.FullPosition.y : e.transform.position.y;
            var heightSpan = Span.FromCenter(centerY, e.DimensionsMM.y * AppConstants.MM_TO_UNITS);

            int wallIndex = -1;
            string? attachedWallName = e is WallOpeningElement opening ? opening.AttachedWallName : null;
            if (!string.IsNullOrEmpty(attachedWallName) && wallIndexByName != null
                && wallIndexByName.TryGetValue(attachedWallName!, out int found))
                wallIndex = found;

            bool hasExtraBody = TryExtraBody(e, out var extraBody, out string? hostName);
            int hostIndex = -1;
            if (hasExtraBody && partIndexByName != null && !string.IsNullOrEmpty(hostName)
                && partIndexByName.TryGetValue(hostName!, out int host))
                hostIndex = host;

            return new ValidationElement(
                MainBody(e),
                e.GetVertices(),
                kind,
                e.GroupId,
                PairedNameOf(e),
                heightSpan,
                wallIndex,
                extraBody,
                hasExtraBody,
                hostIndex,
                wall != null
                    ? WallCentreline.Of(wall.FullPosition, e.transform.rotation, e.DimensionsMM)
                    : default);
        }

        private static ElementKind KindOf(KitchenElement e, Wall? wall, bool hasBasePlate)
        {
            var kind = ElementKind.None;

            bool isFloor = hasBasePlate || e is FloorElement;
            bool isOpening = e is WallOpeningElement;

            if (isFloor) kind |= ElementKind.FloorAnchor;
            if (isOpening) kind |= ElementKind.Opening;
            if (isFloor || isOpening || wall != null) kind |= ElementKind.Anchor;

            if (e is DrawerElement) kind |= ElementKind.Drawer;
            if (e is ScrewLegElement) kind |= ElementKind.ScrewLeg;
            if (e is LightSourceElement) kind |= ElementKind.Decor;
            if (e is DishwasherElement) kind |= ElementKind.SelfSupported;
            if (e is SinkElement || e is CooktopElement) kind |= ElementKind.Recessed;
            if (e is FacadeElement fe)
            {
                kind |= ElementKind.Facade;
                if (fe.GapMM > 0) kind |= ElementKind.FloatingFacade;
            }

            return kind;
        }
    }
}
