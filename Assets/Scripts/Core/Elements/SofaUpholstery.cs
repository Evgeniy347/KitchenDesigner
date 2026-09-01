using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class SofaUpholstery
    {
        private readonly Transform _owner;
        private readonly List<GameObject> _parts = new List<GameObject>();
        private readonly List<Mesh?> _meshes = new List<Mesh?>();
        private Material? _material;

        public SofaUpholstery(Transform owner) => _owner = owner;

        public void Place(IReadOnlyList<SofaPartBox> boxes)
        {
            Ensure(boxes.Count);
            for (int i = 0; i < boxes.Count; i++) Rebuild(i, boxes[i]);
        }

        public void SetMaterial(Material material)
        {
            _material = material;
            foreach (var part in _parts)
            {
                if (part == null) continue;
                var renderer = part.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = _material;
            }
        }

        public void Destroy()
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                DestroyObject(_meshes[i]);
                DestroyObject(_parts[i]);
            }
            _parts.Clear();
            _meshes.Clear();
        }

        private void Rebuild(int index, SofaPartBox box)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float width = box.ProfileWidthMM * toU;
            float depth = box.ProfileDepthMM * toU;

            var profile = RoundedRectProfile.Uniform(width, depth, box.RadiusMM * toU,
                RoundedRectProfile.DefaultSegments);
            var mesh = ProfileExtrusionMesh.Build(profile, width, depth, box.ThicknessMM * toU);

            DestroyObject(_meshes[index]);
            _meshes[index] = mesh;

            var part = _parts[index];
            part.name = box.Name;
            part.transform.localPosition = box.CentreMM * toU;
            part.transform.localRotation = Quaternion.Euler(SofaLayout.EulerAnglesFor(box.Orientation));
            part.transform.localScale = Vector3.one;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void Ensure(int count)
        {
            while (_parts.Count < count)
            {
                var part = new GameObject();
                part.transform.SetParent(_owner, false);
                part.AddComponent<MeshFilter>();
                var renderer = part.AddComponent<MeshRenderer>();
                if (_material != null) renderer.sharedMaterial = _material;

                _parts.Add(part);
                _meshes.Add(null);
            }
        }

        private static void DestroyObject(Object? target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
