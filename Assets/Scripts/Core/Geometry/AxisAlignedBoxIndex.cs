using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class AxisAlignedBoxIndex
    {
        public const float AxisDotThreshold = 0.9999f;

        private const int Unknown = 0;
        private const int IsBox = 1;
        private const int NotABox = 2;

        private int[] _state = System.Array.Empty<int>();
        private int[] _faceBySlot = System.Array.Empty<int>();
        private float[] _planeBySlot = System.Array.Empty<float>();

        public void Reset(int partCount)
        {
            if (_state.Length < partCount)
            {
                _state = new int[partCount];
                _faceBySlot = new int[partCount * Face.BoxFaceCount];
                _planeBySlot = new float[partCount * Face.BoxFaceCount];
            }
            for (int i = 0; i < partCount; i++) _state[i] = Unknown;
        }

        public bool Handles(int index, in ElementGeometry geometry)
        {
            if (index < 0 || index >= _state.Length) return false;
            if (_state[index] == Unknown)
                _state[index] = Map(index, geometry) ? IsBox : NotABox;
            return _state[index] == IsBox;
        }

        public bool TryAppendContacts(int aIdx, int bIdx,
            in ElementGeometry a, in ElementGeometry b, float contactDist, float overlapMargin,
            List<CoreContact> into)
        {
            if (!Handles(aIdx, a) || !Handles(bIdx, b)) return false;

            int baseA = aIdx * Face.BoxFaceCount;
            int baseB = bIdx * Face.BoxFaceCount;

            for (int axis = 0; axis < 3; axis++)
            {
                int u = axis == 2 ? 0 : axis + 1;
                int v = axis == 0 ? 2 : axis - 1;

                if (!Tolerance.IntervalsOverlap(a.Min[u], a.Max[u], b.Min[u], b.Max[u],
                        overlapMargin)
                    || !Tolerance.IntervalsOverlap(a.Min[v], a.Max[v], b.Min[v], b.Max[v],
                        overlapMargin))
                    continue;

                float overlapU = Mathf.Min(a.Max[u], b.Max[u]) - Mathf.Max(a.Min[u], b.Min[u]);
                float overlapV = Mathf.Min(a.Max[v], b.Max[v]) - Mathf.Max(a.Min[v], b.Min[v]);
                float ratio =
                    FaceRects.RatioOfSmallerSide(overlapU,
                        Mathf.Min(a.Max[u] - a.Min[u], b.Max[u] - b.Min[u])) *
                    FaceRects.RatioOfSmallerSide(overlapV,
                        Mathf.Min(a.Max[v] - a.Min[v], b.Max[v] - b.Min[v]));
                bool faceToFace = ratio >= Tolerance.MinSupportOverlap;
                float area = overlapU * overlapV;

                for (int sideA = 0; sideA < 2; sideA++)
                {
                    int slotA = baseA + axis * 2 + sideA;
                    for (int sideB = 0; sideB < 2; sideB++)
                    {
                        int slotB = baseB + axis * 2 + sideB;
                        if (Mathf.Abs(_planeBySlot[slotB] - _planeBySlot[slotA]) > contactDist)
                            continue;
                        into.Add(new CoreContact(aIdx, bIdx,
                            _faceBySlot[slotA], _faceBySlot[slotB], area, faceToFace));
                    }
                }
            }
            return true;
        }

        private bool Map(int index, in ElementGeometry geometry)
        {
            var faces = geometry.Faces;
            if (faces == null || faces.Length != Face.BoxFaceCount) return false;

            int start = index * Face.BoxFaceCount;
            for (int slot = 0; slot < Face.BoxFaceCount; slot++) _faceBySlot[start + slot] = -1;

            for (int i = 0; i < faces.Length; i++)
            {
                var face = faces[i];
                if (!IsWorldAxis(face.normal)) return false;

                int axis = DominantAxis(face.normal);
                int slot = start + axis * 2 + (face.normal[axis] > 0f ? 0 : 1);
                if (_faceBySlot[slot] >= 0) return false;
                if (!IsTheBoxsOwnFace(geometry, face, axis)) return false;

                _faceBySlot[slot] = i;
                _planeBySlot[slot] = face.center[axis];
            }

            for (int slot = 0; slot < Face.BoxFaceCount; slot++)
                if (_faceBySlot[start + slot] < 0) return false;
            return true;
        }

        private static bool IsTheBoxsOwnFace(in ElementGeometry geometry, in Face face, int axis)
        {
            int u = axis == 2 ? 0 : axis + 1;
            int v = axis == 0 ? 2 : axis - 1;
            return SpansTheBox(face, u, geometry.Min[u], geometry.Max[u])
                && SpansTheBox(face, v, geometry.Min[v], geometry.Max[v]);
        }

        private static bool SpansTheBox(in Face face, int axis, float min, float max)
        {
            float half = Mathf.Abs(face.rightAxis[axis]) * face.size.x * 0.5f
                + Mathf.Abs(face.upAxis[axis]) * face.size.y * 0.5f;
            float centre = face.center[axis];
            return Mathf.Abs(centre - half - min) <= Tolerance.SnapEpsilon
                && Mathf.Abs(centre + half - max) <= Tolerance.SnapEpsilon;
        }

        private static bool IsWorldAxis(Vector3 axis) =>
            Mathf.Max(Mathf.Abs(axis.x), Mathf.Max(Mathf.Abs(axis.y), Mathf.Abs(axis.z)))
            >= AxisDotThreshold;

        private static int DominantAxis(Vector3 normal)
        {
            float x = Mathf.Abs(normal.x), y = Mathf.Abs(normal.y), z = Mathf.Abs(normal.z);
            if (x >= y && x >= z) return 0;
            return y >= z ? 1 : 2;
        }
    }
}
