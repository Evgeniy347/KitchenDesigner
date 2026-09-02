using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class FurniturePartSet
    {
        private readonly Transform _owner;
        private readonly Dictionary<string, GameObject> _parts =
            new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        private readonly List<string> _unwanted = new List<string>();
        private Material? _material;

        public FurniturePartSet(Transform owner) => _owner = owner;

        public void Place(IReadOnlyList<FurniturePartBox> boxes)
        {
            RemoveEveryPartNotIn(boxes);
            for (int i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                Place(box.Name, box.CentreMM,
                    FurnitureLayout.EulerAnglesFor(box.Orientation), MeshFor(box));
            }
        }

        public void Cushion(string name, Vector3 centreMM, Vector3 sizeMM, float radiusMM)
            => Place(name, centreMM, Vector3.zero, CushionMeshOf(sizeMM, radiusMM));

        public void SoftSlab(string name, Vector3 centreMM, Vector3 sizeMM,
            float planRadiusMM, float filletMM)
            => Place(name, centreMM, Vector3.zero,
                SoftSlabMeshOf(sizeMM.x, sizeMM.z, planRadiusMM, sizeMM.y, filletMM));

        public void Extrusion(string name, Vector3 centreMM, Vector3 eulerAngles,
            float profileWidthMM, float profileDepthMM, float thicknessMM, float radiusMM)
            => Place(name, centreMM, eulerAngles, ExtrusionMeshOf(profileWidthMM,
                profileDepthMM, thicknessMM, radiusMM));

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

        private void RemoveEveryPartNotIn(IReadOnlyList<FurniturePartBox> boxes)
        {
            _unwanted.Clear();
            foreach (var name in _parts.Keys)
            {
                bool wanted = false;
                for (int i = 0; i < boxes.Count && !wanted; i++) wanted = boxes[i].Name == name;
                if (!wanted) _unwanted.Add(name);
            }
            for (int i = 0; i < _unwanted.Count; i++) Remove(_unwanted[i]);
        }

        private static Mesh MeshFor(FurniturePartBox box) => box.Shape switch
        {
            FurniturePartShape.Cushion => CushionMeshOf(box.LocalSizeMM, box.RadiusMM),
            FurniturePartShape.SoftSlab => SoftSlabMeshOf(box.ProfileWidthMM, box.ProfileDepthMM,
                box.RadiusMM, box.ThicknessMM,
                box.ThicknessMM * SoftSlabSurface.MaxFilletThicknessRatio),
            _ => ExtrusionMeshOf(box.ProfileWidthMM, box.ProfileDepthMM, box.ThicknessMM,
                box.RadiusMM),
        };

        private static Mesh CushionMeshOf(Vector3 sizeMM, float radiusMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            return CushionMesh.Build(sizeMM * toU, radiusMM * toU);
        }

        private static Mesh SoftSlabMeshOf(float widthMM, float depthMM, float planRadiusMM,
            float thicknessMM, float filletMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            return SoftSlabMesh.Build(widthMM * toU, depthMM * toU, planRadiusMM * toU,
                thicknessMM * toU, filletMM * toU);
        }

        private static Mesh ExtrusionMeshOf(float profileWidthMM, float profileDepthMM,
            float thicknessMM, float radiusMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float width = profileWidthMM * toU;
            float depth = profileDepthMM * toU;
            var profile = RoundedRectProfile.Uniform(width, depth, radiusMM * toU,
                RoundedRectProfile.DefaultSegments);
            return ProfileExtrusionMesh.Build(profile, width, depth, thicknessMM * toU);
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
