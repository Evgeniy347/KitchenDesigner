using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class SinkMesh
    {
        public const int CHILD_COUNT = 14;

        private const int RimFront = 0;
        private const int RimBack = 1;
        private const int RimLeft = 2;
        private const int RimRight = 3;
        private const int BowlFront = 4;
        private const int BowlBack = 5;
        private const int BowlLeft = 6;
        private const int BowlRight = 7;
        private const int BowlBottom = 8;
        private const int FaucetBase = 9;
        private const int FaucetStand = 10;
        private const int FaucetSpout = 11;
        private const int FaucetOutlet = 12;
        private const int FaucetHandle = 13;

        private const float FaucetBaseDiameterMM = 55f;
        private const float FaucetBaseHeightMM = 20f;
        private const float FaucetStandDiameterMM = 35f;
        private const float FaucetStandTopMM = 250f;
        private const float FaucetSpoutLengthMM = 210f;
        private const float FaucetSpoutHeightMM = 25f;
        private const float FaucetSpoutWidthMM = 30f;
        private const float FaucetOutletDiameterMM = 18f;
        private const float FaucetOutletHeightMM = 30f;
        private const float FaucetOutletDropMM = 15f;
        private const float FaucetHandleLiftMM = 22f;
        private const float FaucetHandleOffsetMM = 30f;
        private const float FaucetHandleTiltDeg = 25f;
        private const float FaucetHandleThicknessMM = 16f;
        private const float FaucetHandleLengthMM = 90f;

        private readonly Transform _root;
        private readonly List<GameObject> _children = new List<GameObject>();

        public SinkMesh(Transform root) => _root = root;

        private static bool IsCylinder(int idx) =>
            idx == FaucetBase || idx == FaucetStand || idx == FaucetOutlet;

        private static string ChildName(int idx) => idx switch
        {
            RimFront => "RimFront", RimBack => "RimBack", RimLeft => "RimLeft", RimRight => "RimRight",
            BowlFront => "BowlFront", BowlBack => "BowlBack", BowlLeft => "BowlLeft",
            BowlRight => "BowlRight", BowlBottom => "BowlBottom",
            FaucetBase => "FaucetBase", FaucetStand => "FaucetStand", FaucetSpout => "FaucetSpout",
            FaucetOutlet => "FaucetOutlet", FaucetHandle => "FaucetHandle",
            _ => "Child" + idx,
        };

        private void EnsureChildren()
        {
            while (_children.Count < CHILD_COUNT)
            {
                int idx = _children.Count;
                GameObject child;
                if (IsCylinder(idx))
                {
                    child = new GameObject(ChildName(idx));
                    child.AddComponent<MeshFilter>().sharedMesh =
                        Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
                    child.AddComponent<MeshRenderer>();
                }
                else
                {
                    child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    child.name = ChildName(idx);
                    var col = child.GetComponent<BoxCollider>();
                    if (col != null) Object.DestroyImmediate(col);
                }
                child.transform.SetParent(_root, false);
                _children.Add(child);
            }
        }

        public void Rebuild(int faucetSign, Material? decor)
        {
            EnsureChildren();
            if (_children.Count < CHILD_COUNT) return;

            float toU = AppConstants.MM_TO_UNITS;
            float outerW = SinkElement.OUTER_WIDTH_MM * toU;
            float outerD = SinkElement.OUTER_DEPTH_MM * toU;
            float rimH = SinkElement.RIM_HEIGHT_MM * toU;
            float rimW = SinkElement.RIM_WIDTH_MM * toU;
            float bowlW = outerW - 2f * rimW;
            float bowlD = outerD - 2f * rimW;
            float bowlH = SinkElement.BOWL_DEPTH_MM * toU;
            float wall = SinkElement.BOWL_WALL_MM * toU;

            Cube(RimFront, new Vector3(0f, rimH * 0.5f, (outerD - rimW) * 0.5f), new Vector3(outerW, rimH, rimW));
            Cube(RimBack, new Vector3(0f, rimH * 0.5f, -(outerD - rimW) * 0.5f), new Vector3(outerW, rimH, rimW));
            Cube(RimLeft, new Vector3(-(outerW - rimW) * 0.5f, rimH * 0.5f, 0f), new Vector3(rimW, rimH, bowlD));
            Cube(RimRight, new Vector3((outerW - rimW) * 0.5f, rimH * 0.5f, 0f), new Vector3(rimW, rimH, bowlD));

            float wallH = bowlH - wall;
            float wallCY = -wallH * 0.5f;
            Cube(BowlFront, new Vector3(0f, wallCY, (bowlD - wall) * 0.5f), new Vector3(bowlW, wallH, wall));
            Cube(BowlBack, new Vector3(0f, wallCY, -(bowlD - wall) * 0.5f), new Vector3(bowlW, wallH, wall));
            Cube(BowlLeft, new Vector3(-(bowlW - wall) * 0.5f, wallCY, 0f), new Vector3(wall, wallH, bowlD - 2f * wall));
            Cube(BowlRight, new Vector3((bowlW - wall) * 0.5f, wallCY, 0f), new Vector3(wall, wallH, bowlD - 2f * wall));
            Cube(BowlBottom, new Vector3(0f, -bowlH + wall * 0.5f, 0f), new Vector3(bowlW, wall, bowlD));

            RebuildFaucet(faucetSign, toU, outerD, rimH, rimW);
            ApplyMaterials(decor);
        }

        private void RebuildFaucet(int faucetSign, float toU, float outerD, float rimH, float rimW)
        {
            float s = faucetSign;
            float baseZ = s * (outerD - rimW) * 0.5f;
            float floorY = rimH;
            float baseH = FaucetBaseHeightMM * toU;
            float standTop = floorY + FaucetStandTopMM * toU;
            float spoutLen = FaucetSpoutLengthMM * toU;
            float spoutH = FaucetSpoutHeightMM * toU;

            Cylinder(FaucetBase, new Vector3(0f, floorY + baseH * 0.5f, baseZ),
                FaucetBaseDiameterMM * toU, baseH);
            Cylinder(FaucetStand, new Vector3(0f, (floorY + baseH + standTop) * 0.5f, baseZ),
                FaucetStandDiameterMM * toU, standTop - floorY - baseH);
            Cube(FaucetSpout, new Vector3(0f, standTop + spoutH * 0.5f, baseZ - s * spoutLen * 0.5f),
                new Vector3(FaucetSpoutWidthMM * toU, spoutH, spoutLen));
            Cylinder(FaucetOutlet,
                new Vector3(0f, standTop - FaucetOutletDropMM * toU,
                    baseZ - s * (spoutLen - FaucetOutletDropMM * toU)),
                FaucetOutletDiameterMM * toU, FaucetOutletHeightMM * toU);

            var handle = _children[FaucetHandle];
            handle.transform.localPosition = new Vector3(0f,
                standTop + spoutH + FaucetHandleLiftMM * toU, baseZ - s * FaucetHandleOffsetMM * toU);
            handle.transform.localRotation = Quaternion.Euler(s * FaucetHandleTiltDeg, 0f, 0f);
            handle.transform.localScale = new Vector3(
                FaucetHandleThicknessMM * toU, FaucetHandleThicknessMM * toU, FaucetHandleLengthMM * toU);
        }

        private void Cube(int idx, Vector3 localPosition, Vector3 localScale)
        {
            var go = _children[idx];
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
        }

        private void Cylinder(int idx, Vector3 localPosition, float diameter, float height)
        {
            var go = _children[idx];
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = PrimitiveMesh.CylinderScale(diameter, height);
        }

        public void ApplyMaterials(Material? decor)
        {
            var steel = ApplianceMaterials.SinkSteel;
            var bottom = ApplianceMaterials.SinkBowlBottom;
            for (int i = 0; i < _children.Count; i++)
            {
                var mr = _children[i].GetComponent<MeshRenderer>();
                if (mr == null) continue;
                mr.sharedMaterial = i >= FaucetBase
                    ? steel
                    : decor ?? (i == BowlBottom ? bottom : steel);
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
