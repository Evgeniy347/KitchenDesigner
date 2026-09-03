using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class TabletopSurface
    {
        private readonly OwnedMeshBody _body;

        public TabletopSurface(GameObject owner, Action<Mesh> adoptMesh)
        {
            _body = new OwnedMeshBody(owner, adoptMesh);
        }

        public void Rebuild(float width, float depth, float radius, float thickness,
            float centreY)
        {
            var profile = RoundedRectProfile.Uniform(width, depth, radius,
                RoundedRectProfile.DefaultSegments);
            _body.Rebuild(ProfileExtrusionMesh.Build(profile, width, depth, thickness, centreY));
        }

        public void SetMaterial(Material material) => _body.SetMaterial(material);
    }
}
