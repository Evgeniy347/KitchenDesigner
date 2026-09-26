using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core
{
    public abstract class WallLayerElement : KitchenElement
    {
        private const float MinUsableSizeUnits = 0.001f;

        [SerializeField] private string _hostWallName = "";

        [NotUndoable("привязка к стене выставляется автоматически при спавне рядом со стеной")]
        public string HostWallName
        {
            get => _hostWallName;
            set => _hostWallName = value ?? "";
        }

        [Undoable]
        public int ThicknessMm
        {
            get => DimensionsMM.z;
            set
            {
                var d = DimensionsMM;
                d.z = Mathf.Clamp(value, WallLayerDefaults.MinThicknessMm, WallLayerDefaults.MaxThicknessMm);
                DimensionsMM = d;
                RestackWallLayers(_hostWallName);
            }
        }

        public override Vector2Int DecorSurfaceMM => new Vector2Int(DimensionsMM.x, DimensionsMM.y);

        public override bool CanFollowAnAttachParent => false;

        public override bool CanCarryAttachedParts => false;

        public override CutoutNeighbourRole CutoutRole => CutoutNeighbourRole.None;

        public override bool ParticipatesInGapChecks => false;

        public override ElementFront Front =>
            ElementFront.NoSeparateFacePart("слой идёт по всей грани стены: у него нет одной характерной стороны");

        public virtual int StackOrder => 0;

        private int _lastWallPoseVersion = -1;
        private int _lastOpeningsSignature;

        private Wall? _cachedHostWall;
        private string _cachedHostWallName = "";

        public Wall? ResolveHostWall()
        {
            if (_cachedHostWallName == _hostWallName && _cachedHostWall != null) return _cachedHostWall;
            _cachedHostWall = FindWallByName(_hostWallName);
            _cachedHostWallName = _hostWallName;
            return _cachedHostWall;
        }

        internal static Wall? FindWallByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var e in PartRegistry.GetAll())
            {
                if (e == null || e.PartName != name) continue;
                var wall = e.GetComponent<Wall>();
                if (wall != null) return wall;
            }
            return null;
        }

        public void SnapToNearestWall()
        {
            var wall = WallProximity.Nearest(this);
            if (wall == null) return;
            var wallElement = wall.GetComponent<KitchenElement>();
            _hostWallName = wallElement != null ? wallElement.PartName : "";
            ResyncToHost(wall);
            RestackWallLayers(_hostWallName);
        }

        public bool SnapToNamedWall(string wallName)
        {
            _hostWallName = wallName ?? "";
            var wall = FindWallByName(_hostWallName);
            if (wall == null) return false;
            ResyncToHost(wall);
            RestackWallLayers(_hostWallName);
            return true;
        }

        private static void RestackWallLayers(string wallName)
        {
            if (string.IsNullOrEmpty(wallName)) return;
            foreach (var e in PartRegistry.GetAll())
            {
                if (!(e is WallLayerElement layer) || layer.HostWallName != wallName) continue;
                var layerWall = layer.ResolveHostWall();
                if (layerWall != null) layer.ResyncToHost(layerWall);
            }
        }

        internal void Update()
        {
            var wall = ResolveHostWall();
            if (wall == null) return;

            var wallElement = wall.GetComponent<KitchenElement>();
            int wallPose = wallElement != null ? wallElement.PoseVersion : 0;
            int openingsSig = OpeningsSignature(wall);
            if (wallPose == _lastWallPoseVersion && openingsSig == _lastOpeningsSignature) return;

            _lastWallPoseVersion = wallPose;
            _lastOpeningsSignature = openingsSig;
            ResyncToHost(wall);
        }

        private static int OpeningsSignature(Wall wall)
        {
            int sig = 17;
            foreach (var w in wall.AttachedWindows)
                if (w != null) sig = sig * 31 + w.PoseVersion + w.DimensionsMM.GetHashCode();
            foreach (var d in wall.AttachedDoors)
                if (d != null) sig = sig * 31 + d.PoseVersion + d.DimensionsMM.GetHashCode();
            return sig * 31 + wall.AttachedWindows.Count * 7 + wall.AttachedDoors.Count * 13;
        }

        private void ResyncToHost(Wall wall)
        {
            var wallElement = wall.GetComponent<KitchenElement>();
            var wallDims = wallElement != null ? wallElement.DimensionsMM : new Vector3Int(3000, 2500, 100);
            int myThicknessMm = DimensionsMM.z;

            var wallNormal = WallProximity.FaceNormal(wall);
            var outward = WallMountedPose.OutwardNormal(wallNormal, wall.FullPosition, transform.position);
            var facing = Quaternion.Euler(0f, WallMountedPose.YawDegrees(outward), 0f);

            float standoffUnits = WallProximity.HalfThicknessUnits(wall)
                + InnerStackThicknessUnits(wall, outward)
                + AppConstants.HalfHeightUnits(myThicknessMm);
            var seated = wall.FullPosition + outward * standoffUnits;

            transform.SetPositionAndRotation(seated, facing);

            int lengthMm = WallCentreline.LengthMM(wallDims);
            DimensionsMM = new Vector3Int(lengthMm, wallDims.y, myThicknessMm);
        }

        private float InnerStackThicknessUnits(Wall wall, Vector3 outward)
        {
            var wallElement = wall.GetComponent<KitchenElement>();
            string wallName = wallElement != null ? wallElement.PartName : "";
            if (string.IsNullOrEmpty(wallName)) return 0f;

            int sumMm = 0;
            foreach (var e in PartRegistry.GetAll())
            {
                if (e == this || !(e is WallLayerElement other) || other.StackOrder >= StackOrder) continue;
                if (other.HostWallName != wallName) continue;

                var otherWall = other.ResolveHostWall();
                if (otherWall == null) continue;
                var otherOutward = WallMountedPose.OutwardNormal(
                    WallProximity.FaceNormal(otherWall), otherWall.FullPosition, other.transform.position);
                if (Vector3.Dot(otherOutward, outward) <= 0f) continue;

                sumMm += other.DimensionsMM.z;
            }
            return sumMm * AppConstants.MM_TO_UNITS;
        }

        public override void ApplyDimensions()
        {
            transform.localScale = new Vector3(
                DimensionsMM.x * AppConstants.MM_TO_UNITS,
                DimensionsMM.y * AppConstants.MM_TO_UNITS,
                DimensionsMM.z * AppConstants.MM_TO_UNITS);

            if (SuppressVisualRebuild) return;

            var wall = ResolveHostWall();
            var cutouts = wall != null ? BuildCutouts(wall) : new List<WallMeshBuilder.WindowCutout>();
            var mesh = WallMeshBuilder.Build(cutouts, thicknessAlongX: false, WallMeshBuilder.EndShape.Square);
            AdoptOwnedMesh(mesh);

            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();

            ElementRoot.UseMeshCollider(gameObject, mesh);
            MaterialManager.RefreshTiling(this);
        }

        private List<WallMeshBuilder.WindowCutout> BuildCutouts(Wall wall)
        {
            var cutouts = new List<WallMeshBuilder.WindowCutout>();
            float widthUnits = DimensionsMM.x * AppConstants.MM_TO_UNITS;
            float heightUnits = DimensionsMM.y * AppConstants.MM_TO_UNITS;
            if (widthUnits < MinUsableSizeUnits || heightUnits < MinUsableSizeUnits) return cutouts;

            void AddCutout(KitchenElement? opening, bool downToWallBase)
            {
                if (opening == null) return;
                var oDims = opening.DimensionsMM;
                Vector3 lp = Quaternion.Inverse(transform.rotation) * (opening.transform.position - wall.FullPosition);
                float u = lp.x / widthUnits;
                float v = lp.y / heightUnits;
                float halfV = oDims.y * AppConstants.MM_TO_UNITS * 0.5f / heightUnits;
                if (downToWallBase)
                {
                    var span = DoorOpeningLayout.GroundedSpanNorm(v, halfV);
                    halfV = span.Size * 0.5f;
                    v = span.Min + halfV;
                }
                cutouts.Add(new WallMeshBuilder.WindowCutout
                {
                    centerNorm = new Vector2(u, v),
                    halfSizeNorm = new Vector2(oDims.x * AppConstants.MM_TO_UNITS * 0.5f / widthUnits, halfV)
                });
            }

            foreach (var w in wall.AttachedWindows) AddCutout(w, false);
            foreach (var d in wall.AttachedDoors) AddCutout(d, true);
            return cutouts;
        }
    }
}
