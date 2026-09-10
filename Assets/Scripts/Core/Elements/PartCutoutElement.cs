using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public abstract class PartCutoutElement : KitchenElement, IPartCutout, IQuantifies, ICutsItsHost
    {
        public const int SNAP_CATCH_MM = 100;
        public const int SNAP_RELEASE_MM = 60;

        public const float ALIGNED_ROTATION_EPSILON_DEG = 0.05f;

        [SerializeField] private PartMount _mount = new PartMount();

        private bool _mountConfigured;

        protected KitchenElement? MemoPart;
        protected int MemoOffsetXMM = int.MinValue;
        protected int MemoOffsetYMM = int.MinValue;

        protected PartMount Mount
        {
            get
            {
                if (_mount == null) _mount = new PartMount();
                if (!_mountConfigured)
                {
                    _mountConfigured = true;
                    _mount.Configure(this, transform, AcceptsHost,
                        SNAP_CATCH_MM, SNAP_RELEASE_MM, ForgetAlignmentMemo);
                }
                return _mount;
            }
        }

        protected abstract (int widthMM, int depthMM) CutoutExtentsMM { get; }

        protected abstract int RimHeightMM { get; }

        protected abstract int MinEdgeMM { get; }

        protected abstract string HostRejectionMessage { get; }

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return PurchasedGoodsSpecItems.Piece(DisplayTypeName, DimensionsMM);
        }

        [NotUndoable("служебная привязка к детали, вычисляется SnapToPart")]
        public string AttachedPartName
        {
            get => Mount.AttachedPartName;
            set => Mount.AttachedPartName = value;
        }

        [NotUndoable("смещение от центра детали — производная позиции, откатывается MoveCommand")]
        public int OffsetXMM { get => Mount.OffsetXMM; set => Mount.OffsetXMM = value; }

        [NotUndoable("см. OffsetXMM")]
        public int OffsetYMM { get => Mount.OffsetYMM; set => Mount.OffsetYMM = value; }

        public bool IsAttached => Mount.IsAttached;

        public int HoleAxisIn(KitchenElement part) => PartPlane.Of(part).UpAxis;

        protected static bool IsHostSuitable(KitchenElement part, int minWidthMM, int minDepthMM)
        {
            if (part == null || !part.SupportsGrooves) return false;
            var plane = PartPlane.Of(part);
            if (!plane.IsHorizontal) return false;
            return plane.SizeAlongA >= minWidthMM && plane.SizeAlongB >= minDepthMM;
        }

        protected abstract bool AcceptsHost(KitchenElement part);

        protected abstract void AlignToPart(KitchenElement part);

        protected abstract string? FirstBlocker(KitchenElement part, int offXMM, int offYMM);

        protected abstract void DestroyChildren();

        protected virtual void OnAttached(KitchenElement part) { }

        protected virtual void TrackYaw(KitchenElement? part) { }

        protected virtual bool TryCandidate(
            KitchenElement candidate, PartPlane plane, ref int offXMM, ref int offYMM) => true;

        protected virtual void OnCatchFound(KitchenElement part) { }

        protected KitchenElement? SnapToPartCore()
        {
            var part = Mount.CurrentOrNamedPart();
            Mount.TrackDrift(part);
            TrackYaw(part);

            part = Mount.ReleaseIfLost(part);
            if (part == null) part = FindCatchingPart();
            if (part == null) return null;

            Mount.Adopt(part);
            AlignToPart(part);
            return part;
        }

        public void AttachToPart(KitchenElement part)
        {
            if (part == null || !AcceptsHost(part)) return;
            Mount.AttachTo(part);
            OnAttached(part);
            AlignToPart(part);
        }

        public abstract void SnapToPart();

        public void ReleaseHostCutout() => UnregisterFromPart();

        public void RestoreHostCutout() => SnapToPart();

        internal void UnregisterFromPart() => Mount.Detach();

        protected virtual void ForgetAlignmentMemo()
        {
            MemoPart = null;
            MemoOffsetXMM = int.MinValue;
            MemoOffsetYMM = int.MinValue;
        }

        private KitchenElement? FindCatchingPart()
        {
            KitchenElement? best = null;
            float bestHeight = float.MaxValue;
            int bestX = 0, bestY = 0;
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == this || !AcceptsHost(el)) continue;

                var plane = PartPlane.Of(el);
                var (offX, offY, heightMM) = plane.PoseOf(transform.position);
                if (!Mount.WithinCatchBand(heightMM)) continue;
                if (!plane.CoversOffset(offX, offY)) continue;
                if (!TryCandidate(el, plane, ref offX, ref offY)) continue;

                float h = Mathf.Abs(heightMM);
                if (h >= bestHeight) continue;
                bestHeight = h;
                best = el;
                bestX = offX;
                bestY = offY;
            }
            if (best == null) return null;

            OnCatchFound(best);
            var (clampedX, clampedY) = ClampOffsets(PartPlane.Of(best), bestX, bestY);
            Mount.CaptureCatch(clampedX, clampedY);
            return best;
        }

        protected (int offXMM, int offYMM) ClampOffsets(PartPlane plane, int offXMM, int offYMM)
        {
            var (cutW, cutD) = CutoutExtentsMM;
            int maxX = (plane.SizeAlongA - cutW) / 2 - MinEdgeMM;
            int maxY = (plane.SizeAlongB - cutD) / 2 - MinEdgeMM;
            return (Mathf.Clamp(offXMM, -maxX, maxX), Mathf.Clamp(offYMM, -maxY, maxY));
        }

        public string DescribeCatch(KitchenElement part)
        {
            if (!AcceptsHost(part)) return HostRejectionMessage;
            var plane = PartPlane.Of(part);
            var (offX, offY, height) = plane.PoseOf(transform.position);
            bool over = plane.CoversOffset(offX, offY);
            var (cx, cy) = ClampOffsets(plane, offX, offY);
            return $"height={height:F1}мм ({Mount.DescribeCatchBand()}) " +
                   $"over={over} off=({offX},{offY})→({cx},{cy}) " +
                   $"blocker={FirstBlocker(part, cx, cy) ?? "-"}";
        }

        public GrooveMesh.Rect2 CutoutRectIn(KitchenElement part)
        {
            if (part == null) return default;
            var plane = PartPlane.Of(part);
            if (plane.SizeAlongA <= 0 || plane.SizeAlongB <= 0) return default;

            var (cutW, cutD) = CutoutExtentsMM;
            float halfW = cutW * 0.5f;
            float halfD = cutD * 0.5f;
            return new GrooveMesh.Rect2
            {
                xMin = (Mount.OffsetXMM - halfW) / plane.SizeAlongA,
                xMax = (Mount.OffsetXMM + halfW) / plane.SizeAlongA,
                yMin = (Mount.OffsetYMM - halfD) / plane.SizeAlongB,
                yMax = (Mount.OffsetYMM + halfD) / plane.SizeAlongB,
            };
        }

        protected void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            float toU = AppConstants.MM_TO_UNITS;
            var dims = Data.DimensionsMM;
            box.size = new Vector3(dims.x * toU, dims.y * toU, dims.z * toU);
            box.center = new Vector3(0f, (RimHeightMM - dims.y) * 0.5f * toU, 0f);
        }

        public override void PrepareForDestruction()
        {
            UnregisterFromPart();
            DestroyChildren();
        }

        protected override void OnElementDestroyed()
        {
            UnregisterFromPart();
            DestroyChildren();
        }
    }
}
