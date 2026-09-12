using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ElementSnapshotReuse
    {
        private readonly struct Stamp
        {
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Vector3 _scale;
            private readonly Vector3 _restPosition;
            private readonly Quaternion _restRotation;
            private readonly Vector3Int _dimensions;
            private readonly BoxGaps _gaps;
            private readonly string? _name;
            private readonly int _groupId;
            private readonly bool _poseFollowsTransform;
            private readonly bool _isFloor;
            private readonly bool _hasWall;
            private readonly bool _wallLowered;
            private readonly float _wallFullScaleY;
            private readonly Vector3 _wallFullPosition;

            private Stamp(Vector3 position, Quaternion rotation, Vector3 scale,
                Vector3 restPosition, Quaternion restRotation, Vector3Int dimensions,
                BoxGaps gaps, string? name, int groupId, bool poseFollowsTransform,
                bool isFloor, bool hasWall, bool wallLowered, float wallFullScaleY,
                Vector3 wallFullPosition)
            {
                _isFloor = isFloor;
                _hasWall = hasWall;
                _wallLowered = wallLowered;
                _wallFullScaleY = wallFullScaleY;
                _wallFullPosition = wallFullPosition;
                _poseFollowsTransform = poseFollowsTransform;
                _position = position;
                _rotation = rotation;
                _scale = scale;
                _restPosition = restPosition;
                _restRotation = restRotation;
                _dimensions = dimensions;
                _gaps = gaps;
                _name = name;
                _groupId = groupId;
            }

            public static Stamp Of(KitchenElement element, Wall? wall, bool isFloor)
            {
                var pose = element.transform;
                return new Stamp(pose.position, pose.rotation, pose.localScale,
                    element.AttachRestPosition, element.AttachRestRotation,
                    element.DimensionsMM, element.Gaps, element.PartName, element.GroupId,
                    element.PoseFollowsTransform, isFloor, wall != null,
                    wall != null && wall.IsLowered,
                    wall != null ? wall.FullScaleY : 0f,
                    wall != null ? wall.FullPosition : Vector3.zero);
            }

            public bool Matches(in Stamp other) =>
                SamePoint(_position, other._position)
                && SameTurn(_rotation, other._rotation)
                && SamePoint(_scale, other._scale)
                && SamePoint(_restPosition, other._restPosition)
                && SameTurn(_restRotation, other._restRotation)
                && _dimensions == other._dimensions
                && SameGaps(_gaps, other._gaps)
                && string.Equals(_name, other._name, System.StringComparison.Ordinal)
                && _groupId == other._groupId
                && _poseFollowsTransform == other._poseFollowsTransform
                && _isFloor == other._isFloor
                && _hasWall == other._hasWall
                && _wallLowered == other._wallLowered
                && _wallFullScaleY.Equals(other._wallFullScaleY)
                && SamePoint(_wallFullPosition, other._wallFullPosition);

            private static bool SamePoint(Vector3 a, Vector3 b) =>
                a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z);

            private static bool SameTurn(Quaternion a, Quaternion b) =>
                a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z) && a.w.Equals(b.w);

            private static bool SameGaps(BoxGaps a, BoxGaps b) =>
                a.Left == b.Left && a.Right == b.Right && a.Top == b.Top
                && a.Bottom == b.Bottom && a.Front == b.Front && a.Back == b.Back;
        }

        private sealed class Entry
        {
            public Stamp Stamp;
            public Stamp GeometryStamp;
            public GrooveSpec[] Grooves = System.Array.Empty<GrooveSpec>();
            public ValidationElement Snapshot;
            public int Touched;
        }

        private static readonly Dictionary<KitchenElement, Entry> _entries =
            new Dictionary<KitchenElement, Entry>();

        private static int _pass;

        public static void BeginPass() => _pass++;

        public static bool TryReuse(KitchenElement element, Wall? wall, bool isFloor,
            string? pairedName, out ValidationElement snapshot)
        {
            snapshot = default;
            if (element == null || FaceCache.Enabled) return false;
            if (!_entries.TryGetValue(element, out var entry)) return false;
            if (!string.Equals(entry.Snapshot.PairedName, pairedName,
                System.StringComparison.Ordinal)) return false;
            if (!Stamp.Of(element, wall, isFloor).Matches(entry.Stamp)) return false;
            if (!SameGrooves(entry.Grooves, element.Grooves)) return false;

            entry.Touched = _pass;
            snapshot = entry.Snapshot;
            return true;
        }

        public static bool TryReuseGeometry(KitchenElement element, out ElementGeometry geometry)
        {
            geometry = default;
            if (element == null || FaceCache.Enabled) return false;
            if (!_entries.TryGetValue(element, out var entry)) return false;
            if (!GeometryStampOf(element).Matches(entry.GeometryStamp)) return false;
            if (!SameGrooves(entry.Grooves, element.Grooves)) return false;

            geometry = entry.Snapshot.Geometry;
            return !geometry.IsEmpty;
        }

        private static Stamp GeometryStampOf(KitchenElement element) =>
            Stamp.Of(element, null, false);

        public static void Keep(KitchenElement element, Wall? wall, bool isFloor,
            in ValidationElement snapshot)
        {
            if (element == null || FaceCache.Enabled) return;
            if (!_entries.TryGetValue(element, out var entry))
                _entries[element] = entry = new Entry();

            entry.Stamp = Stamp.Of(element, wall, isFloor);
            entry.GeometryStamp = GeometryStampOf(element);
            entry.Grooves = CopyOf(element.Grooves);
            entry.Snapshot = snapshot;
            entry.Touched = _pass;
        }

        public static void DropWhatThisPassNeverSaw(int liveCount)
        {
            if (_entries.Count <= liveCount) return;

            var stale = new List<KitchenElement>();
            foreach (var pair in _entries)
                if (pair.Value.Touched != _pass || IsGone(pair.Key)) stale.Add(pair.Key);
            foreach (var key in stale) _entries.Remove(key);
        }

        private static bool IsGone(KitchenElement element) => element == null;

        public static void Clear() => _entries.Clear();

        private static bool SameGrooves(GrooveSpec[] kept, IReadOnlyList<GrooveSpec> now)
        {
            if (kept.Length != now.Count) return false;
            for (int i = 0; i < kept.Length; i++)
                if (!kept[i].Equals(now[i])) return false;
            return true;
        }

        private static GrooveSpec[] CopyOf(IReadOnlyList<GrooveSpec> grooves)
        {
            if (grooves.Count == 0) return System.Array.Empty<GrooveSpec>();
            var copy = new GrooveSpec[grooves.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = grooves[i];
            return copy;
        }
    }
}
