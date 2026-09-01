using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class TabletopSurface
    {
        private readonly GameObject _owner;
        private readonly Action<Mesh> _adoptMesh;
        private Material? _material;

        public TabletopSurface(GameObject owner, Action<Mesh> adoptMesh)
        {
            _owner = owner;
            _adoptMesh = adoptMesh;
        }

        public void Rebuild(float width, float depth, float radius, float thickness,
            float centreY)
        {
            var profile = RoundedRectProfile.Uniform(width, depth, radius,
                RoundedRectProfile.DefaultSegments);
            var mesh = ProfileExtrusionMesh.Build(profile, width, depth, thickness, centreY);
            _adoptMesh(mesh);

            var meshFilter = _owner.GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = _owner.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = _owner.GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = _owner.AddComponent<MeshRenderer>();
            if (_material != null) meshRenderer.sharedMaterial = _material;

            UpdateCollider(mesh);
        }

        public void SetMaterial(Material material)
        {
            _material = material;
            var meshRenderer = _owner.GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.sharedMaterial = _material;
        }

        private void UpdateCollider(Mesh mesh)
        {
            var existing = _owner.GetComponent<Collider>();
            if (existing != null && !(existing is MeshCollider))
                UnityEngine.Object.DestroyImmediate(existing);

            var meshCollider = _owner.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = _owner.AddComponent<MeshCollider>();
            meshCollider.convex = true;
            meshCollider.sharedMesh = mesh;
        }
    }
}
