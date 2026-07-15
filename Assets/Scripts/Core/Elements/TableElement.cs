using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class TableElement : KitchenElement
    {
        public const int LegCrossSectionMM = 50;
        public const int TabletopThicknessMM = 30;

        private readonly List<GameObject> _children = new List<GameObject>();
        private Material? _tabletopMaterial;
        private Material? _legsMaterial;

        [SerializeField] private int _legInsetMM = 100;
        [SerializeField] private string _tabletopMaterialId = MaterialCatalog.DefaultId;
        [SerializeField] private string _legsMaterialId = MaterialCatalog.DefaultId;

        public int LegInsetMM
        {
            get => _legInsetMM;
            set { _legInsetMM = Mathf.Max(0, value); ApplyDimensions(); }
        }

        public string TabletopMaterialId
        {
            get => _tabletopMaterialId;
            set { _tabletopMaterialId = value ?? MaterialCatalog.DefaultId; ApplyMaterial(); }
        }

        public string LegsMaterialId
        {
            get => _legsMaterialId;
            set { _legsMaterialId = value ?? MaterialCatalog.DefaultId; ApplyMaterial(); }
        }

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
            var rootMf = GetComponent<MeshFilter>();
            if (rootMf != null) Object.DestroyImmediate(rootMf);
            var rootMr = GetComponent<MeshRenderer>();
            if (rootMr != null) Object.DestroyImmediate(rootMr);

            var dims = DimensionsMM;
            int overallW = dims.x;
            int overallH = dims.y;
            int overallD = dims.z;

            int legH = Mathf.Max(1, overallH - TabletopThicknessMM);
            int topW = overallW;
            int topD = overallD;

            float toU = AppConstants.MM_TO_UNITS;
            float legCross = LegCrossSectionMM * toU;
            float topThicknessU = TabletopThicknessMM * toU;

            float legCenterY = (legH * 0.5f - overallH * 0.5f) * toU;
            float topCenterY = (overallH * 0.5f - TabletopThicknessMM * 0.5f) * toU;

            float halfW = overallW * 0.5f * toU;
            float halfD = overallD * 0.5f * toU;
            float legInset = legCross * 0.5f;
            float insetOffset = _legInsetMM * toU;

            var legPositions = new Vector3[]
            {
                new Vector3(-halfW + legInset + insetOffset, legCenterY, -halfD + legInset + insetOffset),
                new Vector3( halfW - legInset - insetOffset, legCenterY, -halfD + legInset + insetOffset),
                new Vector3(-halfW + legInset + insetOffset, legCenterY,  halfD - legInset - insetOffset),
                new Vector3( halfW - legInset - insetOffset, legCenterY,  halfD - legInset - insetOffset),
            };

            var legScale = new Vector3(legCross, legH * toU, legCross);
            var topScale = new Vector3(topW * toU, topThicknessU, topD * toU);
            var topPos = new Vector3(0f, topCenterY, 0f);

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

        public override Vector3[] GetVertices()
        {
            var size = new Vector3(
                DimensionsMM.x * AppConstants.MM_TO_UNITS,
                DimensionsMM.y * AppConstants.MM_TO_UNITS,
                DimensionsMM.z * AppConstants.MM_TO_UNITS);
            var half = size * 0.5f;
            var pos = transform.position;
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
