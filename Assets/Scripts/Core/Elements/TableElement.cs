using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class TableElement : KitchenElement
    {
        public const int LegCrossSectionMM = 50;
        public const int TabletopThicknessMM = 30;

        private readonly List<GameObject> _children = new List<GameObject>();
        private Material? _material;

        public override void ApplyDimensions()
        {
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

            var legPositions = new Vector3[]
            {
                new Vector3(-halfW + legInset, legCenterY, -halfD + legInset),
                new Vector3( halfW - legInset, legCenterY, -halfD + legInset),
                new Vector3(-halfW + legInset, legCenterY,  halfD - legInset),
                new Vector3( halfW - legInset, legCenterY,  halfD - legInset),
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
                child.name = _children.Count < 4 ? $"Leg{_children.Count + 1}" : "Tabletop";
                child.transform.SetParent(transform, false);

                var childCollider = child.GetComponent<BoxCollider>();
                if (childCollider != null) Object.DestroyImmediate(childCollider);

                var renderer = child.GetComponent<MeshRenderer>();
                if (renderer != null && _material != null)
                    renderer.sharedMaterial = _material;

                _children.Add(child);
            }
        }

        public void SetMaterial(Material material)
        {
            _material = material;
            foreach (var child in _children)
            {
                var renderer = child.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sharedMaterial = _material;
            }
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
