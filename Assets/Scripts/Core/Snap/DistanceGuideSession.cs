using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Measure;

namespace KitchenDesigner.Core
{
    public sealed class DistanceGuideSession
    {
        private readonly List<KitchenElement> _others = new List<KitchenElement>();
        private readonly List<AxisBox> _boxes = new List<AxisBox>();
        private readonly List<GuideLine> _lines = new List<GuideLine>();
        private readonly HashSet<KitchenElement> _moving = new HashSet<KitchenElement>();
        private AxisGuideIndex? _index;
        private AxisBox _startBox;
        private Vector3 _startPosition;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static int IndexesBuilt { get; private set; }
#endif

        public AxisGuideIndex? Index => _index;

        public bool PulledLastFrame { get; private set; }

        public void Begin(KitchenElement target, IReadOnlyList<KitchenElement> scene,
            IEnumerable<KitchenElement> moving)
        {
            End();
            if (target == null || scene == null) return;

            _moving.Clear();
            _moving.Add(target);
            foreach (var e in moving) if (e != null) _moving.Add(e);

            _others.Clear();
            foreach (var e in scene)
                if (e != null && !_moving.Contains(e))
                    _others.Add(e);

            _boxes.Clear();
            foreach (var geometry in SnapSceneGeometry.For(_others, target))
                if (!geometry.IsEmpty) _boxes.Add(AxisBox.Of(geometry));

            _index = new AxisGuideIndex(_boxes);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            IndexesBuilt++;
#endif
            _startPosition = target.transform.position;
            _startBox = AxisBox.Of(target.ToGeometry());
        }

        public void BeginAlone(KitchenElement target, IReadOnlyList<KitchenElement> scene)
        {
            var own = new List<KitchenElement> { target };
            AttachMove.ExpandWithDescendants(own);
            Begin(target, scene, own);
        }

        public Vector3 Settle(Vector3 free, in SnapResult faceSnap, bool snapOn, int axisMask,
            float thresholdUnits)
        {
            PulledLastFrame = false;
            var faceSettled = faceSnap.snapped ? faceSnap.position : free;
            if (!snapOn || _index == null || axisMask == 0) return faceSettled;

            var settled = EqualGapSnap.Resolve(_index, MovingAt(free), free, faceSnap.snapped,
                faceSnap.position, axisMask, thresholdUnits, out int pulled);
            PulledLastFrame = pulled != 0;
            return settled;
        }

        public void ShowAt(Vector3 position) => Show(MovingAt(position));

        public void ShowFor(KitchenElement target)
        {
            if (target == null) return;
            Show(AxisBox.Of(target.ToGeometry()));
        }

        public void End()
        {
            _index = null;
            PulledLastFrame = false;
            _lines.Clear();
            DistanceGuideStore.Clear();
        }

        private void Show(in AxisBox moving)
        {
            if (_index == null) return;
            if (!KitchenSettings.Instance.DistanceGuides)
            {
                DistanceGuideStore.Clear();
                return;
            }
            DistanceGuides.Collect(_index, moving, _lines);
            DistanceGuideStore.Set(_lines);
        }

        private AxisBox MovingAt(Vector3 position) => _startBox.Moved(position - _startPosition);
    }
}
