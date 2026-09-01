using UnityEngine;

namespace KitchenDesigner.Core
{
    public class TableElement : KitchenElement, ITabletop
    {

        public override string DisplayTypeName => "Стол";
        public const int LegCrossSectionMM = 50;
        public const int TabletopThicknessMM = 30;

        private LegSet? _legSet;
        private GameObject? _tabletop;
        private Material? _tabletopMaterial;

        [SerializeField] private int _legInsetMM = 100;
        [SerializeField] private string _tabletopMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _legsMaterialId = MaterialCatalog.DefaultId;

        [Undoable]
        public int LegInsetMM
        {
            get => _legInsetMM;
            set { _legInsetMM = Mathf.Max(0, value); ApplyDimensions(); }
        }

        [NotUndoable("декор ставится через SetMaterialCommand (MaterialSlot.Tabletop)")]
        public string TabletopMaterialId
        {
            get => _tabletopMaterialId;
            set { _tabletopMaterialId = value ?? MaterialCatalog.DefaultId; ApplyMaterial(); }
        }

        [NotUndoable("декор ставится через SetMaterialCommand (MaterialSlot.Legs)")]
        public string LegsMaterialId
        {
            get => _legsMaterialId;
            set { _legsMaterialId = value ?? MaterialCatalog.DefaultId; ApplyMaterial(); }
        }

        [NotUndoable("псевдоним TabletopMaterialId — см. его причину")]
        public override string MaterialId
        {
            get => TabletopMaterialId;
            set => TabletopMaterialId = value;
        }

        public override MeshRenderer? DecorRenderer
            => _tabletop != null ? _tabletop.GetComponent<MeshRenderer>() : null;

        public override Vector2Int DecorSurfaceMM
            => new Vector2Int(DimensionsMM.x, DimensionsMM.z);

        private LegSet Legs => _legSet ??= new LegSet(transform, "Leg");

        private void ApplyMaterial()
        {
            var topDef = MaterialCatalog.Get(_tabletopMaterialId);
            var legsDef = MaterialCatalog.Get(_legsMaterialId);
            if (topDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(topDef);
                if (mat != null) SetTabletopMaterial(mat);
            }
            if (legsDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(legsDef);
                if (mat != null) SetLegsMaterial(mat);
            }
        }

        public override void ApplyDimensions()
        {
            base.ApplyDimensions();

            var rootMf = GetComponent<MeshFilter>();
            if (rootMf != null) Object.DestroyImmediate(rootMf);
            var rootMr = GetComponent<MeshRenderer>();
            if (rootMr != null) Object.DestroyImmediate(rootMr);

            var dims = DimensionsMM;
            int overallW = dims.x;
            int overallH = dims.y;
            int overallD = dims.z;

            int legH = FurnitureLayout.LegHeightMM(overallH, TabletopThicknessMM);

            float toU = AppConstants.MM_TO_UNITS;
            float legCross = LegCrossSectionMM * toU;
            float topThicknessU = TabletopThicknessMM * toU;

            float psX = overallW * toU;
            float psY = overallH * toU;
            float psZ = overallD * toU;

            float legCenterY_world = FurnitureLayout.LegCentreY(overallH, TabletopThicknessMM);
            float topCenterY_world = FurnitureLayout.TopCentreY(overallH, TabletopThicknessMM);

            float halfW_world = overallW * 0.5f * toU;
            float halfD_world = overallD * 0.5f * toU;
            float legInset_world = legCross * 0.5f;
            float insetOffset_world = _legInsetMM * toU;

            float leftX_norm   = (-halfW_world + legInset_world + insetOffset_world) / psX;
            float rightX_norm  = ( halfW_world - legInset_world - insetOffset_world) / psX;
            float frontZ_norm  = (-halfD_world + legInset_world + insetOffset_world) / psZ;
            float backZ_norm   = ( halfD_world - legInset_world - insetOffset_world) / psZ;

            var footprint = new Vector2[]
            {
                new Vector2(leftX_norm,  frontZ_norm),
                new Vector2(rightX_norm, frontZ_norm),
                new Vector2(leftX_norm,  backZ_norm),
                new Vector2(rightX_norm, backZ_norm),
            };

            var legScale = new Vector3(legCross / psX, legH * toU / psY, legCross / psZ);

            Legs.Place(footprint, legCenterY_world / psY, legScale);

            var tabletop = EnsureTabletop();
            tabletop.transform.localPosition = new Vector3(0f, topCenterY_world / psY, 0f);
            tabletop.transform.localScale = new Vector3(1f, topThicknessU / psY, 1f);
            tabletop.transform.localRotation = Quaternion.identity;
        }

        private GameObject EnsureTabletop()
        {
            if (_tabletop != null) return _tabletop;

            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Tabletop";
            top.transform.SetParent(transform, false);

            var topCollider = top.GetComponent<BoxCollider>();
            if (topCollider != null) Object.DestroyImmediate(topCollider);

            var renderer = top.GetComponent<MeshRenderer>();
            if (renderer != null && _tabletopMaterial != null)
                renderer.sharedMaterial = _tabletopMaterial;

            _tabletop = top;
            return top;
        }

        public void SetTabletopMaterial(Material material)
        {
            _tabletopMaterial = material;
            var renderer = DecorRenderer;
            if (renderer != null) renderer.sharedMaterial = _tabletopMaterial;
        }

        public void SetLegsMaterial(Material material) => Legs.SetMaterial(material);

        public void SetMaterial(Material material)
        {
            SetTabletopMaterial(material);
            SetLegsMaterial(material);
        }

        public override Face[] GetFaces() => GetFacesAt(transform.position);

        public override Face[] GetFacesAt(Vector3 position)
        {
            var size = new Vector3(
                DimensionsMM.x * AppConstants.MM_TO_UNITS,
                DimensionsMM.y * AppConstants.MM_TO_UNITS,
                DimensionsMM.z * AppConstants.MM_TO_UNITS);
            var pos = position;
            var rot = ValidationRotation;
            var half = size * 0.5f;

            var axes = new Vector3[] { rot * Vector3.right, rot * Vector3.up, rot * Vector3.forward };

            var faceDims = new Vector2[]
            {
                new Vector2(size.y, size.z),
                new Vector2(size.x, size.z),
                new Vector2(size.x, size.y),
            };

            var offsets = new Vector3[]
            {
                 axes[0] * half.x, -axes[0] * half.x,
                 axes[1] * half.y, -axes[1] * half.y,
                 axes[2] * half.z, -axes[2] * half.z,
            };

            var normals = new Vector3[]
            {
                 axes[0], -axes[0],
                 axes[1], -axes[1],
                 axes[2], -axes[2],
            };

            var rightAxis = new Vector3[] { axes[1], axes[1], axes[0], axes[0], axes[0], axes[0] };
            var upAxis = new Vector3[] { axes[2], axes[2], axes[2], axes[2], axes[1], axes[1] };

            var faces = new Face[6];
            for (int i = 0; i < 6; i++)
            {
                int dimIdx = i / 2;
                faces[i] = new Face(
                    pos + offsets[i],
                    normals[i],
                    faceDims[dimIdx],
                    rightAxis[i],
                    upAxis[i]
                );
            }
            return faces;
        }

        public override Vector3[] GetVertices() => GetVerticesAt(transform.position);

        public override Vector3[] GetVerticesAt(Vector3 position)
        {
            var size = new Vector3(
                DimensionsMM.x * AppConstants.MM_TO_UNITS,
                DimensionsMM.y * AppConstants.MM_TO_UNITS,
                DimensionsMM.z * AppConstants.MM_TO_UNITS);
            var half = size * 0.5f;
            var pos = position;
            var rot = transform.rotation;

            var localCorners = new Vector3[]
            {
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y,  half.z),
                new Vector3(-half.x, -half.y,  half.z),
                new Vector3(-half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y,  half.z),
                new Vector3(-half.x,  half.y,  half.z),
            };

            var result = new Vector3[8];
            for (int i = 0; i < 8; i++)
                result[i] = pos + rot * localCorners[i];
            return result;
        }

        public override void PrepareForDestruction() => DestroyChildren();

        public void DestroyChildren()
        {
            _legSet?.Destroy();
            if (_tabletop == null) return;
            if (Application.isPlaying)
                Object.Destroy(_tabletop);
            else
                Object.DestroyImmediate(_tabletop);
            _tabletop = null;
        }
    }
}
