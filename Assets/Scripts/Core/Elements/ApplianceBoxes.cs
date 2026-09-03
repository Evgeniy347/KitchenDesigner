using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class ApplianceBoxes
    {
        private readonly Transform _root;
        private readonly Func<int, string> _nameOf;
        private readonly List<GameObject> _children = new List<GameObject>();

        public ApplianceBoxes(Transform root, Func<int, string> nameOf)
        {
            _root = root;
            _nameOf = nameOf;
        }

        public int Count => _children.Count;

        public void Ensure(int count)
        {
            while (_children.Count < count)
            {
                int idx = _children.Count;
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = _nameOf(idx);
                var col = child.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
                child.transform.SetParent(_root, false);
                _children.Add(child);
            }
        }

        public void Place(int idx, Vector3 centerMM, Vector3 sizeMM, Quaternion localRotation)
        {
            if (idx < 0 || idx >= _children.Count) return;
            var go = _children[idx];
            if (go == null) return;
            float toU = AppConstants.MM_TO_UNITS;
            go.transform.localPosition = centerMM * toU;
            go.transform.localRotation = localRotation;
            go.transform.localScale = sizeMM * toU;
        }

        public MeshRenderer? RendererOf(int idx)
        {
            if (idx < 0 || idx >= _children.Count) return null;
            var child = _children[idx];
            return child != null ? child.GetComponent<MeshRenderer>() : null;
        }

        public void SetMaterial(int idx, Material material)
        {
            if (idx < 0 || idx >= _children.Count) return;
            var child = _children[idx];
            if (child == null) return;
            var mr = child.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = material;
        }

        public void Destroy()
        {
            foreach (var child in _children)
            {
                if (child == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
            _children.Clear();
        }
    }
}
