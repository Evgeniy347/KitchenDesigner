using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class ChairBackrest
    {
        private readonly Transform _owner;
        private readonly string _name;
        private GameObject? _panel;
        private Material? _material;

        public ChairBackrest(Transform owner, string name)
        {
            _owner = owner;
            _name = name;
        }

        public void Place(Vector3 localPosition, Vector3 localScale)
        {
            var panel = Ensure();
            panel.transform.localPosition = localPosition;
            panel.transform.localScale = localScale;
            panel.transform.localRotation = Quaternion.identity;
        }

        public void SetMaterial(Material material)
        {
            _material = material;
            if (_panel == null) return;
            var renderer = _panel.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = _material;
        }

        public void Destroy()
        {
            if (_panel == null) return;
            if (Application.isPlaying) Object.Destroy(_panel);
            else Object.DestroyImmediate(_panel);
            _panel = null;
        }

        private GameObject Ensure()
        {
            if (_panel != null) return _panel;

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = _name;
            panel.transform.SetParent(_owner, false);

            var collider = panel.GetComponent<BoxCollider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var renderer = panel.GetComponent<MeshRenderer>();
            if (renderer != null && _material != null) renderer.sharedMaterial = _material;

            _panel = panel;
            return panel;
        }
    }
}
