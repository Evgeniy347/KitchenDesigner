using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [SelectionBase]
    public class KitchenElement : MonoBehaviour
    {
        [SerializeField] private PartData _data = new PartData();

        public PartData Data => _data;

        [Undoable]
        public string PartName
        {
            get => _data.PartName;
            set => _data.PartName = value;
        }

        [Undoable(Order = -100)]
        public Vector3Int DimensionsMM
        {
            get => _data.DimensionsMM;
            set
            {
                _data.DimensionsMM = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public bool Movable
        {
            get => _data.Movable;
            set => _data.Movable = value;
        }

        public virtual bool PoseFollowsTransform => !_attachRidden;

        public bool Transformable => Movable && PoseFollowsTransform;

        [NotUndoable("группировка идёт своей командой SetGroupCommand")]
        public int GroupId
        {
            get => _data.GroupId;
            set => _data.GroupId = value;
        }

        [NotUndoable("декор ставится через SetMaterialCommand — одной записи в поле мало, нужен MaterialManager")]
        public virtual string MaterialId
        {
            get => _data.MaterialId;
            set => _data.MaterialId = value;
        }

        public virtual MeshRenderer? DecorRenderer => GetComponentInChildren<MeshRenderer>();

        public virtual Vector2Int DecorSurfaceMM
            => new Vector2Int(_data.DimensionsMM.x, _data.DimensionsMM.y);

        [Undoable]
        public bool Transparent
        {
            get => _data.Transparent;
            set => _data.Transparent = value;
        }

        [Undoable]
        public string AttachedToName
        {
            get => _data.AttachedToName;
            set => _data.AttachedToName = value;
        }

        public virtual bool AttachIsDerived => false;

        public virtual bool AttachContactHolds(KitchenElement parent) =>
            ConstraintValidator.AreInFaceToFaceContact(this, parent);

        public virtual bool ParticipatesInGapChecks => true;

        private Vector3 _attachRestPos;
        private Quaternion _attachRestRot = Quaternion.identity;
        private bool _attachRidden;

        public bool IsAttachRidden => _attachRidden;

        public virtual Vector3 AttachRestPosition => _attachRidden ? _attachRestPos : transform.position;
        public virtual Quaternion AttachRestRotation => _attachRidden ? _attachRestRot : transform.rotation;

        internal void BeginAttachRide(Vector3 restPos, Quaternion restRot)
        {
            _attachRestPos = restPos;
            _attachRestRot = restRot;
            _attachRidden = true;
        }

        internal void EndAttachRide() => _attachRidden = false;

        [Undoable]
        public int GapLeft
        {
            get => _data.GapLeft;
            set { _data.GapLeft = value; ApplyDimensions(); }
        }

        [Undoable]
        public int GapRight
        {
            get => _data.GapRight;
            set { _data.GapRight = value; ApplyDimensions(); }
        }

        [Undoable]
        public int GapTop
        {
            get => _data.GapTop;
            set { _data.GapTop = value; ApplyDimensions(); }
        }

        [Undoable]
        public int GapBottom
        {
            get => _data.GapBottom;
            set { _data.GapBottom = value; ApplyDimensions(); }
        }

        [Undoable]
        public int GapFront
        {
            get => _data.GapFront;
            set { _data.GapFront = value; ApplyDimensions(); }
        }

        [Undoable]
        public int GapBack
        {
            get => _data.GapBack;
            set { _data.GapBack = value; ApplyDimensions(); }
        }

        public int GapMM => _data.GapMM;

        public BoxGaps Gaps => _data.Gaps;

        public int GapOf(GapSide side) => _data.GapOf(side);

        public void SetGap(GapSide side, int valueMM)
        {
            _data.SetGap(side, valueMM);
            ApplyDimensions();
        }

        public virtual bool CanFollowAnAttachParent => true;

        public virtual bool CanCarryAttachedParts => CanFollowAnAttachParent;

        public virtual bool SupportsGaps => SupportsGrooves;

        public virtual string DisplayTypeName => "Деталь";

        public virtual bool IsFlatBoardElement =>
            GetType() == typeof(KitchenElement)
            && GetComponent<Wall>() == null
            && GetComponent<BasePlate>() == null;

        public virtual CutoutNeighbourRole CutoutRole =>
            GetComponent<BasePlate>() == null ? CutoutNeighbourRole.Carcass : CutoutNeighbourRole.None;

        public bool BlocksCutout => (CutoutRole & CutoutNeighbourRole.BlocksCutout) != 0;

        public bool AlignsCutout => (CutoutRole & CutoutNeighbourRole.AlignsCutout) != 0;

        public virtual ElementDisposal Disposal =>
            GetType() == typeof(KitchenElement) ? ElementDisposal.PartPool : ElementDisposal.Destroy;

        public virtual void PrepareForDestruction() { }

        internal void ResetToPristineState()
        {
            _data = new PartData();
            _cutouts.Clear();
            _bareFaceMask = 0;
            _attachRestPos = Vector3.zero;
            _attachRestRot = Quaternion.identity;
            _attachRidden = false;
            PoseVersion = 0;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            OnResetToPristineState();
            ApplyDimensions();
            RebuildGrooveMesh();
            EdgeSubstrate.Sync(this);
        }

        protected virtual void OnResetToPristineState() { }

        public virtual KitchenElement InspectedElement => this;

        private Mesh? _ownedMesh;
        private Vector3Int _meshDims;
        private int _bareFaceMask;
        private int _meshBareFaceMask;
        private GrooveMesh.SubmeshLayout _meshLayout = new GrooveMesh.SubmeshLayout(-1, -1);

        public bool SupportsGrooves =>
            GetType() == typeof(KitchenElement)
            && GetComponent<Wall>() == null
            && GetComponent<BasePlate>() == null;

        public IReadOnlyList<GrooveSpec> Grooves => _data.Grooves;

        public bool SupportsTextureOverlays =>
            GetComponent<Wall>() != null || this is FloorElement;

        public IReadOnlyList<TextureOverlaySpec> TextureOverlays => _data.TextureOverlays;

        public void SetTextureOverlays(IEnumerable<TextureOverlaySpec>? overlays)
        {
            if (!SupportsTextureOverlays) return;
            _data.TextureOverlays.Clear();
            if (overlays != null)
                foreach (var o in overlays)
                {
                    if (_data.TextureOverlays.Count >= TextureOverlayGeometry.MAX_PER_ELEMENT) break;
                    _data.TextureOverlays.Add(o);
                }
            TextureOverlayRenderer.Refresh(this);
        }

        public bool SupportsEdges => SupportsGrooves && EdgeBanding.IsSheet(_data.DimensionsMM);

        [NotUndoable("кромка целиком идёт через SetEdgeBandingCommand: три поля одним шагом")]
        public bool EdgeBandingEnabled
        {
            get => SupportsEdges && _data.EdgeBanding;
            set
            {
                if (_data.EdgeBanding == value) return;
                _data.EdgeBanding = value;
                if (!SuppressVisualRebuild) EdgeSubstrate.Sync(this);
            }
        }

        [NotUndoable("см. EdgeBandingEnabled — SetEdgeBandingCommand")]
        public float EdgeThicknessMM
        {
            get => _data.EdgeThicknessMM;
            set => _data.EdgeThicknessMM = value;
        }

        [NotUndoable("см. EdgeBandingEnabled — SetEdgeBandingCommand")]
        public int EdgeForcedMask
        {
            get => _data.EdgeForcedMask;
            set
            {
                if (_data.EdgeForcedMask == value) return;
                _data.EdgeForcedMask = value;
                if (!SuppressVisualRebuild) EdgeSubstrate.Sync(this);
            }
        }

        [NotUndoable("см. EdgeBandingEnabled — SetEdgeBandingCommand")]
        public int EdgeSuppressedMask
        {
            get => _data.EdgeSuppressedMask;
            set
            {
                if (_data.EdgeSuppressedMask == value) return;
                _data.EdgeSuppressedMask = value;
                if (!SuppressVisualRebuild) EdgeSubstrate.Sync(this);
            }
        }

        public EdgeSideState EdgeStateOf(EdgeSide side) => _data.EdgeStateOf(side);

        public void SetEdgeState(EdgeSide side, EdgeSideState state)
        {
            if (_data.EdgeStateOf(side) == state) return;
            _data.SetEdgeState(side, state);
            if (!SuppressVisualRebuild) EdgeSubstrate.Sync(this);
        }

        private readonly List<IPartCutout> _cutouts = new List<IPartCutout>();

        public IReadOnlyList<IPartCutout> AttachedCutouts => _cutouts;

        public bool HasCutout(IPartCutout cutout) => !IsGone(cutout) && _cutouts.Contains(cutout);

        public void RegisterCutout(IPartCutout cutout)
        {
            if (IsGone(cutout) || !SupportsGrooves || _cutouts.Contains(cutout)) return;
            _cutouts.Add(cutout);
            RebuildGrooveMesh();
        }

        public void UnregisterCutout(IPartCutout cutout)
        {
            if (cutout == null) return;
            if (_cutouts.Remove(cutout)) RebuildGrooveMesh();
        }

        public void ClearCutouts()
        {
            if (_cutouts.Count == 0) return;
            _cutouts.Clear();
            RebuildGrooveMesh();
        }

        private static bool IsGone(IPartCutout? cutout) =>
            cutout == null || (cutout is Object obj && obj == null);

        public int CutoutHoleAxis
        {
            get
            {
                foreach (var cutout in _cutouts)
                    if (!IsGone(cutout)) return cutout.HoleAxisIn(this);
                return 2;
            }
        }

        public List<GrooveMesh.Rect2> CutoutHoleRects()
        {
            var result = new List<GrooveMesh.Rect2>();
            foreach (var cutout in _cutouts)
            {
                if (IsGone(cutout)) continue;
                var rect = cutout.CutoutRectIn(this);
                if (rect.IsValid) result.Add(rect);
            }
            return result;
        }

        public bool AddGroove(GrooveSpec spec)
        {
            if (!SupportsGrooves) return false;
            if (_data.Grooves.Count >= AppConstants.GROOVE_MAX_PER_PART) return false;
            if (_data.Grooves.Contains(spec)) return false;
            _data.Grooves.Add(spec);
            RebuildGrooveMesh();
            return true;
        }

        public bool RemoveGrooveAt(int index)
        {
            if (index < 0 || index >= _data.Grooves.Count) return false;
            _data.Grooves.RemoveAt(index);
            RebuildGrooveMesh();
            return true;
        }

        public void ClearGrooves()
        {
            if (_data.Grooves.Count == 0) return;
            _data.Grooves.Clear();
            RebuildGrooveMesh();
        }

        public void SetGrooves(IEnumerable<GrooveSpec>? grooves)
        {
            if (!SupportsGrooves) return;
            _data.Grooves.Clear();
            if (grooves != null)
                foreach (var g in grooves)
                    if (_data.Grooves.Count < AppConstants.GROOVE_MAX_PER_PART
                        && !_data.Grooves.Contains(g))
                        _data.Grooves.Add(g);
            RebuildGrooveMesh();
        }

        public void RebuildGrooveMesh()
        {
            if (!SupportsGrooves) return;
            var filter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            if (filter == null || meshRenderer == null) return;

            var mats = meshRenderer.sharedMaterials;
            var decor = mats != null && mats.Length > 0 && mats[0] != null
                ? mats[0] : meshRenderer.sharedMaterial;

            var holes = CutoutHoleRects();

            var mesh = GrooveMesh.Build(_data.DimensionsMM, _data.Grooves, holes,
                CutoutHoleAxis, _bareFaceMask, out var layout);
            AdoptOwnedMesh(mesh);
            _meshDims = _data.DimensionsMM;
            _meshBareFaceMask = _bareFaceMask;
            _meshLayout = layout;
            filter.sharedMesh = mesh;

            var slots = new Material[mesh.subMeshCount];
            slots[0] = decor!;
            meshRenderer.sharedMaterials = slots;
            RefreshSubmeshMaterials();
        }

        public virtual void RefreshSubmeshMaterials()
        {
            if (!SupportsGrooves || _ownedMesh == null) return;
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;

            var slots = meshRenderer.sharedMaterials;
            if (slots == null || slots.Length != _ownedMesh.subMeshCount) return;

            if (_meshLayout.Grooves >= 0)
                slots[_meshLayout.Grooves] = GrooveMesh.GrooveMaterial();
            if (_meshLayout.BareEnds >= 0)
                slots[_meshLayout.BareEnds] = EdgeSubstrate.Material() ?? slots[0];
            meshRenderer.sharedMaterials = slots;
        }

        public int BareFaceMask => _bareFaceMask;

        public void SetBareFaceMask(int mask)
        {
            if (mask == _bareFaceMask) return;
            _bareFaceMask = mask;
            if (!SuppressVisualRebuild) RebuildGrooveMesh();
        }

        protected void AdoptOwnedMesh(Mesh mesh)
        {
            if (ReferenceEquals(_ownedMesh, mesh)) return;
            DestroyOwnedMesh();
            _ownedMesh = mesh;
        }

        private void DestroyOwnedMesh()
        {
            if (_ownedMesh == null) return;
            DestroyImmediate(_ownedMesh);
            _ownedMesh = null;
        }

        public Face[] GetGrooveSeatFaces() => GetGrooveSeatFacesAt(transform.position);

        public Face[] GetGrooveSeatFacesAt(Vector3 position)
        {
            int count = _data.Grooves.Count;
            if (count == 0) return System.Array.Empty<Face>();

            var scale = transform.localScale;
            var rot = transform.rotation;
            var pos = position;
            var normal = rot * Vector3.forward;
            var right = rot * Vector3.right;
            var up = rot * Vector3.up;
            float floorZ = 0.5f - GrooveMesh.DepthFraction(_data.DimensionsMM);

            var seats = new List<Face>(count);
            foreach (var groove in _data.Grooves)
            {
                var rect = GrooveMesh.ComputeRect(_data.DimensionsMM, groove);
                if (!rect.IsValid) continue;

                var localCenter = new Vector3(
                    (rect.xMin + rect.xMax) * 0.5f * scale.x,
                    (rect.yMin + rect.yMax) * 0.5f * scale.y,
                    floorZ * scale.z);
                var size = new Vector2(
                    (rect.xMax - rect.xMin) * scale.x,
                    (rect.yMax - rect.yMin) * scale.y);

                seats.Add(new Face(pos + rot * localCenter, normal, size, right, up));
            }
            return seats.ToArray();
        }

        public Face[] GetGrooveWallFaces() => GetGrooveWallFacesAt(transform.position);

        public Face[] GetGrooveWallFacesAt(Vector3 position)
        {
            int count = _data.Grooves.Count;
            if (count == 0) return System.Array.Empty<Face>();

            var scale = transform.localScale;
            var rot = transform.rotation;
            var pos = position;
            float depthFrac = GrooveMesh.DepthFraction(_data.DimensionsMM);
            float wallCenterZ = 0.5f - depthFrac * 0.5f;

            var walls = new List<Face>(count * 4);
            foreach (var groove in _data.Grooves)
            {
                var rect = GrooveMesh.ComputeRect(_data.DimensionsMM, groove);
                if (!rect.IsValid) continue;

                bool horizontal = groove.side == GrooveSide.Top || groove.side == GrooveSide.Bottom;
                if (horizontal)
                {
                    var axis = rot * Vector3.up;
                    var size = new Vector2((rect.xMax - rect.xMin) * scale.x, depthFrac * scale.z);
                    float cx = (rect.xMin + rect.xMax) * 0.5f * scale.x;
                    foreach (float y in new[] { rect.yMin, rect.yMax })
                    {
                        var c = pos + rot * new Vector3(cx, y * scale.y, wallCenterZ * scale.z);
                        walls.Add(new Face(c, axis, size, rot * Vector3.right, rot * Vector3.forward));
                    }
                }
                else
                {
                    var axis = rot * Vector3.right;
                    var size = new Vector2((rect.yMax - rect.yMin) * scale.y, depthFrac * scale.z);
                    float cy = (rect.yMin + rect.yMax) * 0.5f * scale.y;
                    foreach (float x in new[] { rect.xMin, rect.xMax })
                    {
                        var c = pos + rot * new Vector3(x * scale.x, cy, wallCenterZ * scale.z);
                        walls.Add(new Face(c, axis, size, rot * Vector3.up, rot * Vector3.forward));
                    }
                }
            }
            return walls.ToArray();
        }

        public int PoseVersion { get; private set; }

        internal void BumpPoseVersion()
        {
            PoseVersion++;
            OnOwnPoseVersionBumped();
            foreach (var cutout in _cutouts)
                if (!IsGone(cutout) && cutout is KitchenElement hosted) hosted.enabled = true;
        }

        protected virtual void OnOwnPoseVersionBumped() { }

        private void Awake()
        {
            ApplyDimensions();
            if (!ElementFactorySandbox.IsActive) PartRegistry.Register(this);
        }

        private void OnDestroy()
        {
            if (!ElementFactorySandbox.IsActive) PartRegistry.Unregister(this);
            DestroyOwnedMesh();
            OnElementDestroyed();
        }

        protected virtual void OnElementDestroyed() { }

        protected virtual Vector3 EffectiveScale => transform.localScale;

        protected virtual Vector3 ValidationPosition => AttachRestPosition;
        protected virtual Quaternion ValidationRotation => AttachRestRotation;

        protected virtual Vector3 ValidationPositionAt(Vector3 transformPosition)
            => _attachRidden ? _attachRestPos : transformPosition;

        public static bool SuppressVisualRebuild;

        public virtual void ApplyDimensions()
        {
            transform.localScale = new Vector3(
                _data.DimensionsMM.x * AppConstants.MM_TO_UNITS,
                _data.DimensionsMM.y * AppConstants.MM_TO_UNITS,
                _data.DimensionsMM.z * AppConstants.MM_TO_UNITS
            );

            if (SuppressVisualRebuild) return;

            if (_meshDims != _data.DimensionsMM) EdgeSubstrate.Sync(this);

            if (_meshDims != _data.DimensionsMM || _ownedMesh == null
                || _meshBareFaceMask != _bareFaceMask) RebuildGrooveMesh();

            MaterialManager.RefreshTiling(this);
        }

        public virtual Vector3[] GetVertices()
        {
            return GetVerticesAt(transform.position);
        }

        public virtual Vector3[] GetVerticesAt(Vector3 position)
        {
            var size = EffectiveScale;
            var pos = ValidationPositionAt(position);
            var rot = ValidationRotation;

            var wall = GetComponent<Wall>();
            if (wall != null && wall.IsLowered)
            {
                size.y = wall.FullScaleY;
                pos.y = wall.FullPosition.y;
            }

            return GappedBox.Vertices(size, _data.Gaps, pos, rot);
        }

        public virtual Face[] GetFaces() => GetFacesAt(transform.position);

        public virtual Face[] GetFacesAt(Vector3 position)
        {
            var size = EffectiveScale;
            var pos = ValidationPositionAt(position);
            var rot = ValidationRotation;

            var wall = GetComponent<Wall>();
            if (wall != null && wall.IsLowered)
            {
                size.y = wall.FullScaleY;
                pos.y = wall.FullPosition.y;
            }

            return GappedBox.Faces(size, _data.Gaps, pos, rot);
        }

        public void SetDimensionsFromUI(int w, int h, int d)
        {
            DimensionsMM = new Vector3Int(w, h, d);
        }

        public string Describe()
        {
            var p = transform.position;
            return $"{_data.PartName} ({_data.DimensionsMM.x}x{_data.DimensionsMM.y}x{_data.DimensionsMM.z}мм @ " +
                   $"{p.x:F3},{p.y:F3},{p.z:F3})";
        }

        public void Rotate(Quaternion rotation)
        {
            transform.rotation = rotation * transform.rotation;
        }

        public void RotateAroundAxis(Vector3 axis, float angle)
        {
            Rotate(Quaternion.AngleAxis(angle, axis));
        }
    }
}
