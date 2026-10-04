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
        private readonly bool _solid;
        private bool _visible = true;
        private Material? _material;

        public FurniturePartSet(Transform owner) : this(owner, false) { }

        public FurniturePartSet(Transform owner, bool solid)
        {
            _owner = owner;
            _solid = solid;
        }

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

        public void SetVisible(bool visible)
        {
            _visible = visible;
            foreach (var part in _parts.Values)
            {
                if (part == null) continue;
                var renderer = part.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = visible;
            }
        }

        public MeshRenderer? RendererOf(string name)
            => _parts.TryGetValue(name, out var part) && part != null
                ? part.GetComponent<MeshRenderer>()
                : null;

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
                box.RadiusMM, box.RearRadiusMM, box.ThicknessMM,
                box.ThicknessMM * SoftSlabSurface.MaxFilletThicknessRatio),
            _ => ExtrusionMeshOf(box.ProfileWidthMM, box.ProfileDepthMM, box.ThicknessMM,
                box.RadiusMM, box.RearRadiusMM),
        };

        private static Mesh CushionMeshOf(Vector3 sizeMM, float radiusMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            return CushionMesh.Build(sizeMM * toU, radiusMM * toU);
        }

        private static Mesh SoftSlabMeshOf(float widthMM, float depthMM, float planRadiusMM,
            float thicknessMM, float filletMM)
            => SoftSlabMeshOf(widthMM, depthMM, planRadiusMM, planRadiusMM, thicknessMM,
                filletMM);

        private static Mesh SoftSlabMeshOf(float widthMM, float depthMM, float frontRadiusMM,
            float rearRadiusMM, float thicknessMM, float filletMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var radii = new CornerRadii(rearRadiusMM * toU, rearRadiusMM * toU,
                frontRadiusMM * toU, frontRadiusMM * toU);
            return SoftSlabMesh.Build(new SoftSlabSurface(widthMM * toU, depthMM * toU, radii,
                thicknessMM * toU, filletMM * toU));
        }

        private static Mesh ExtrusionMeshOf(float profileWidthMM, float profileDepthMM,
            float thicknessMM, float radiusMM)
            => ExtrusionMeshOf(profileWidthMM, profileDepthMM, thicknessMM, radiusMM, radiusMM);

        private static Mesh ExtrusionMeshOf(float profileWidthMM, float profileDepthMM,
            float thicknessMM, float frontRadiusMM, float rearRadiusMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float width = profileWidthMM * toU;
            float depth = profileDepthMM * toU;
            var radii = new CornerRadii(rearRadiusMM * toU, rearRadiusMM * toU,
                frontRadiusMM * toU, frontRadiusMM * toU);
            var profile = RoundedRectProfile.Build(width, depth, radii,
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
            if (_solid) FitCollider(part, mesh);
        }

        private static void FitCollider(GameObject part, Mesh mesh)
        {
            var collider = part.GetComponent<BoxCollider>();
            if (collider == null) collider = part.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;
        }

        private GameObject Ensure(string name)
        {
            if (_parts.TryGetValue(name, out var existing) && existing != null) return existing;

            var part = new GameObject(name);
            part.transform.SetParent(_owner, false);
            part.AddComponent<MeshFilter>();
            var renderer = part.AddComponent<MeshRenderer>();
            if (_material != null) renderer.sharedMaterial = _material;
            renderer.enabled = _visible;

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
