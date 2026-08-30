using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class TableElement : KitchenElement, ITabletop
    {

        public override string DisplayTypeName => "Стол";
        public const int LegCrossSectionMM = 50;
        public const int TabletopThicknessMM = 30;

        private readonly List<GameObject> _children = new List<GameObject>();
        private Material? _tabletopMaterial;
        private Material? _legsMaterial;

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
        public new string MaterialId
        {
            get => TabletopMaterialId;
            set => TabletopMaterialId = value;
        }

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

            int legH = Mathf.Max(1, overallH - TabletopThicknessMM);

            float toU = AppConstants.MM_TO_UNITS;
            float legCross = LegCrossSectionMM * toU;
            float topThicknessU = TabletopThicknessMM * toU;

            float psX = overallW * toU;
            float psY = overallH * toU;
            float psZ = overallD * toU;

            float legCenterY_world = (legH * 0.5f - overallH * 0.5f) * toU;
            float topCenterY_world = (overallH * 0.5f - TabletopThicknessMM * 0.5f) * toU;

            float halfW_world = overallW * 0.5f * toU;
            float halfD_world = overallD * 0.5f * toU;
            float legInset_world = legCross * 0.5f;
            float insetOffset_world = _legInsetMM * toU;

            float leftX_norm   = (-halfW_world + legInset_world + insetOffset_world) / psX;
            float rightX_norm  = ( halfW_world - legInset_world - insetOffset_world) / psX;
            float frontZ_norm  = (-halfD_world + legInset_world + insetOffset_world) / psZ;
            float backZ_norm   = ( halfD_world - legInset_world - insetOffset_world) / psZ;

            var legPositions = new Vector3[]
            {
                new Vector3(leftX_norm,  legCenterY_world / psY, frontZ_norm),
                new Vector3(rightX_norm, legCenterY_world / psY, frontZ_norm),
                new Vector3(leftX_norm,  legCenterY_world / psY, backZ_norm),
                new Vector3(rightX_norm, legCenterY_world / psY, backZ_norm),
            };

            var legScale = new Vector3(legCross / psX, legH * toU / psY, legCross / psZ);
            var topScale = new Vector3(1f, topThicknessU / psY, 1f);
            var topPos = new Vector3(0f, topCenterY_world / psY, 0f);

            EnsureChildren(5);

            for (int i = 0; i < 4; i++)
            {
                _children[i].transform.localPosition = legPositions[i];
                _children[i].transform.localScale = legScale;
                _children[i].transform.localRotation = Quaternion.identity;
            }

            _children[4].transform.localPosition = topPos;
            _children[4].transform.localScale = topScale;
            _children[4].transform.localRotation = Quaternion.identity;
        }

        private void EnsureChildren(int count)
        {
            while (_children.Count < count)
            {
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bool isLeg = _children.Count < 4;
                child.name = isLeg ? $"Leg{_children.Count + 1}" : "Tabletop";
                child.transform.SetParent(transform, false);

                var childCollider = child.GetComponent<BoxCollider>();
                if (childCollider != null) Object.DestroyImmediate(childCollider);

                var renderer = child.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = isLeg ? _legsMaterial : _tabletopMaterial;
                    if (mat != null) renderer.sharedMaterial = mat;
                }

                _children.Add(child);
            }
        }

        public void SetTabletopMaterial(Material material)
        {
            _tabletopMaterial = material;
            if (_children.Count > 4)
            {
                var renderer = _children[4].GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = _tabletopMaterial;
            }
        }

        public void SetLegsMaterial(Material material)
        {
            _legsMaterial = material;
            for (int i = 0; i < _children.Count && i < 4; i++)
            {
                var renderer = _children[i].GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = _legsMaterial;
            }
        }

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

        public void DestroyChildren()
        {
            foreach (var child in _children)
                if (child != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(child);
                    else
                        Object.DestroyImmediate(child);
                }
            _children.Clear();
        }
    }
}
