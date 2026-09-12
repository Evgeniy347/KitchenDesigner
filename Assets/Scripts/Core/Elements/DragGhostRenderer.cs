using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class DragGhostRenderer
    {
        private Mesh? _mesh;
        private Material? _material;
        private Vector3? _position;
        private Quaternion _rotation;
        private bool _shown;

        public void CreateMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            _material = TransparentMaterial.Make(shader, new Color(0.3f, 0.6f, 1f, 0.2f));
            _material.SetFloat("_Metallic", 0f);
            _material.SetFloat("_Smoothness", 0.1f);
        }

        public void DestroyMaterial() => DestroyNow.The(_material);

        public void ShowFor(KitchenElement target, Vector3 position)
        {
            _shown = true;
            _position = position;
            _rotation = target.transform.rotation;
            if (_mesh != null) return;
            var filter = target.GetComponent<MeshFilter>();
            if (filter != null) _mesh = filter.sharedMesh;
        }

        public void Hide() => _shown = false;

        public void Draw()
        {
            if (_shown && _mesh != null && _position.HasValue && _material != null)
                Graphics.DrawMesh(_mesh, _position.Value, _rotation, _material, 0);
        }
    }
}
