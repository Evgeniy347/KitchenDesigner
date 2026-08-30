using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class CooktopMesh
    {
        public const int PLATE_AND_BODY_COUNT = 2;
        public const int BURNER_COUNT = 4;
        public const int DECOR_COUNT = BURNER_COUNT + 1;

        public const int PANEL_WIDTH_MM = 260;
        public const int PANEL_DEPTH_MM = 20;
        public const int PANEL_EDGE_MM = 20;

        public const float DECOR_THICKNESS_MM = 0.6f;

        private const int PlateIndex = 0;
        private const int BodyIndex = 1;
        private const int FirstBurnerIndex = PLATE_AND_BODY_COUNT;
        private const int PanelIndex = PLATE_AND_BODY_COUNT + BURNER_COUNT;

        internal static readonly (int diameterMM, int x, int z)[] BoschBurners =
        {
            (180, -145, -130),
            (145, 150, -130),
            (180, -145, 110),
            (210, 150, 110),
        };

        private readonly Transform _root;
        private readonly List<GameObject> _children = new List<GameObject>();

        public CooktopMesh(Transform root) => _root = root;

        public int ChildCount => _children.Count;

        private static string ChildName(int idx) => idx switch
        {
            PlateIndex => "Top",
            BodyIndex => "Body",
            PanelIndex => "Panel",
            _ => "Burner" + (idx - FirstBurnerIndex + 1),
        };

        private static PrimitiveType ChildPrimitive(int idx) =>
            idx >= FirstBurnerIndex && idx < PanelIndex ? PrimitiveType.Cylinder : PrimitiveType.Cube;

        private void EnsureChildren(int required)
        {
            while (_children.Count < required)
            {
                int idx = _children.Count;
                var child = GameObject.CreatePrimitive(ChildPrimitive(idx));
                child.name = ChildName(idx);
                var col = child.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                child.transform.SetParent(_root, false);
                _children.Add(child);
            }
        }

        public void Rebuild(Vector3Int dimsMM, int bodyHeightMM, int cutoutWidthMM, int cutoutDepthMM,
            bool decorated)
        {
            EnsureChildren(decorated ? PLATE_AND_BODY_COUNT + DECOR_COUNT : PLATE_AND_BODY_COUNT);
            if (_children.Count < PLATE_AND_BODY_COUNT) return;

            float toU = AppConstants.MM_TO_UNITS;
            float plateH = CooktopElement.RIM_HEIGHT_MM * toU;
            float bodyH = bodyHeightMM * toU;

            Place(PlateIndex, new Vector3(0f, plateH * 0.5f, 0f),
                new Vector3(dimsMM.x * toU, plateH, dimsMM.z * toU));
            Place(BodyIndex, new Vector3(0f, -bodyH * 0.5f, 0f),
                new Vector3(cutoutWidthMM * toU, bodyH, cutoutDepthMM * toU));

            RebuildDecor(plateH, toU, dimsMM, decorated);
        }

        private void RebuildDecor(float plateH, float toU, Vector3Int dimsMM, bool decorated)
        {
            if (_children.Count < PLATE_AND_BODY_COUNT + DECOR_COUNT) return;
            if (!decorated)
            {
                for (int i = PLATE_AND_BODY_COUNT; i < _children.Count; i++)
                    if (_children[i] != null) _children[i].SetActive(false);
                return;
            }

            float lift = DECOR_THICKNESS_MM * toU;
            for (int i = 0; i < BURNER_COUNT; i++)
            {
                var (diameterMM, x, z) = BoschBurners[i];
                var go = _children[FirstBurnerIndex + i];
                if (go == null) continue;
                go.SetActive(true);
                go.transform.localPosition = new Vector3(x * toU, plateH + lift * 0.5f, z * toU);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = PrimitiveMesh.CylinderScale(diameterMM * toU, lift);
            }

            var panel = _children[PanelIndex];
            if (panel == null) return;
            panel.SetActive(true);
            float panelZ = (dimsMM.z * 0.5f - PANEL_EDGE_MM - PANEL_DEPTH_MM * 0.5f) * toU;
            panel.transform.localPosition = new Vector3(0f, plateH + lift * 0.5f, panelZ);
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = new Vector3(PANEL_WIDTH_MM * toU, lift, PANEL_DEPTH_MM * toU);
        }

        private void Place(int idx, Vector3 localPosition, Vector3 localScale)
        {
            var go = _children[idx];
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
        }

        public void ApplySurfaceMaterial(Material surface)
        {
            if (_children.Count < PLATE_AND_BODY_COUNT) return;
            var decor = ApplianceMaterials.CooktopDecor;
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (child == null) continue;
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = i < PLATE_AND_BODY_COUNT ? surface : decor;
            }
        }

        public void Destroy()
        {
            foreach (var child in _children)
            {
                if (child == null) continue;
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
            _children.Clear();
        }
    }
}
