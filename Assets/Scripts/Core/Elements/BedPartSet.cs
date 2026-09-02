using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class BedPartSet
    {
        private readonly Transform _owner;
        private readonly Dictionary<string, GameObject> _parts =
            new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        private Material? _material;

        public BedPartSet(Transform owner) => _owner = owner;

        public void Cushion(string name, Vector3 centreMM, Vector3 sizeMM, float radiusMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            Place(name, centreMM, Vector3.zero,
                CushionMesh.Build(sizeMM * toU, radiusMM * toU));
        }

        public void SoftSlab(string name, Vector3 centreMM, Vector3 sizeMM,
            float planRadiusMM, float filletMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            Place(name, centreMM, Vector3.zero, SoftSlabMesh.Build(
                sizeMM.x * toU, sizeMM.z * toU, planRadiusMM * toU,
                sizeMM.y * toU, filletMM * toU));
        }

        public void Extrusion(string name, Vector3 centreMM, Vector3 eulerAngles,
            float profileWidthMM, float profileDepthMM, float thicknessMM, float radiusMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float width = profileWidthMM * toU;
            float depth = profileDepthMM * toU;
            var profile = RoundedRectProfile.Uniform(width, depth, radiusMM * toU,
                RoundedRectProfile.DefaultSegments);
            Place(name, centreMM, eulerAngles,
                ProfileExtrusionMesh.Build(profile, width, depth, thicknessMM * toU));
        }

        public void Remove(string name)
        {
            if (_meshes.TryGetValue(name, out var mesh))
            {
                DestroyObject(mesh);
                _meshes.Remove(name);
            }
            if (!_parts.TryGetValue(name, out var part)) return;
            _parts.Remove(name);
            if (part != null) part.transform.SetParent(null, false);
            DestroyObject(part);
        }

        public void SetMaterial(Material material)
        {
            _material = material;
            foreach (var part in _parts.Values)
            {
                if (part == null) continue;
                var renderer = part.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = _material;
            }
        }

        public void Destroy()
        {
            foreach (var mesh in _meshes.Values) DestroyObject(mesh);
            foreach (var part in _parts.Values) DestroyObject(part);
            _meshes.Clear();
            _parts.Clear();
        }

        private void Place(string name, Vector3 centreMM, Vector3 eulerAngles, Mesh mesh)
        {
            if (_meshes.TryGetValue(name, out var previous)) DestroyObject(previous);
            _meshes[name] = mesh;

            var part = Ensure(name);
            part.transform.localPosition = centreMM * AppConstants.MM_TO_UNITS;
            part.transform.localRotation = Quaternion.Euler(eulerAngles);
            part.transform.localScale = Vector3.one;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private GameObject Ensure(string name)
        {
            if (_parts.TryGetValue(name, out var existing) && existing != null) return existing;

            var part = new GameObject(name);
            part.transform.SetParent(_owner, false);
            part.AddComponent<MeshFilter>();
            var renderer = part.AddComponent<MeshRenderer>();
            if (_material != null) renderer.sharedMaterial = _material;

            _parts[name] = part;
            return part;
        }

        private static void DestroyObject(Object? target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
