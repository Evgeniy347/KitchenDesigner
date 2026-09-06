using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class LegSet
    {
        private readonly Transform _owner;
        private readonly string _namePrefix;
        private readonly List<GameObject> _legs = new List<GameObject>();
        private Material? _material;

        public LegSet(Transform owner, string namePrefix)
        {
            _owner = owner;
            _namePrefix = namePrefix;
        }

        public static LegSet For(ref LegSet? slot, Transform owner) =>
            slot ??= new LegSet(owner, "Leg");

        public void Place(Vector2[] footprint, float centreY, Vector3 legScale)
        {
            Ensure(footprint.Length);
            Trim(footprint.Length);
            for (int i = 0; i < footprint.Length; i++)
            {
                var leg = _legs[i];
                if (leg == null) continue;
                leg.transform.localPosition =
                    new Vector3(footprint[i].x, centreY, footprint[i].y);
                leg.transform.localScale = legScale;
                leg.transform.localRotation = Quaternion.identity;
            }
        }

        public void SetMaterial(Material material)
        {
            _material = material;
            foreach (var leg in _legs)
            {
                if (leg == null) continue;
                var renderer = leg.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = _material;
            }
        }

        public void Destroy()
        {
            foreach (var leg in _legs)
            {
                if (leg == null) continue;
                if (Application.isPlaying) Object.Destroy(leg);
                else Object.DestroyImmediate(leg);
            }
            _legs.Clear();
        }

        private void Trim(int count)
        {
            for (int i = _legs.Count - 1; i >= count; i--)
            {
                var leg = _legs[i];
                _legs.RemoveAt(i);
                if (leg == null) continue;
                leg.transform.SetParent(null, false);
                if (Application.isPlaying) Object.Destroy(leg);
                else Object.DestroyImmediate(leg);
            }
        }

        private void Ensure(int count)
        {
            while (_legs.Count < count)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = _namePrefix + (_legs.Count + 1);
                leg.transform.SetParent(_owner, false);

                var collider = leg.GetComponent<BoxCollider>();
                if (collider != null) Object.DestroyImmediate(collider);

                var renderer = leg.GetComponent<MeshRenderer>();
                if (renderer != null && _material != null)
                    renderer.sharedMaterial = _material;

                _legs.Add(leg);
            }
        }
    }
}
