using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ElementFactory
    {
        private static Material _defaultMaterial;
        private static Material DefaultMaterial
        {
            get
            {
                if (_defaultMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader != null)
                    {
                        _defaultMaterial = new Material(shader);
                        _defaultMaterial.color = new Color(0.8f, 0.8f, 0.8f);
                    }
                }
                return _defaultMaterial;
            }
        }

        public static GameObject CreateBoard(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.IsNullOrEmpty(name) ? "Board" : name;

            var element = go.AddComponent<KitchenElement>();
            element.BoardName = go.name;
            element.DimensionsMM = dimensionsMM;

            go.transform.position = position;

            var mat = DefaultMaterial;
            if (mat != null)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.material = mat;
            }

            var collider = go.GetComponent<BoxCollider>();
            if (collider != null)
                collider.enabled = true;

            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            go.tag = "KitchenElement";

            return go;
        }

        public static GameObject CreatePreset(int presetIndex, Vector3 position)
        {
            if (presetIndex < 0 || presetIndex >= AppConstants.PRESET_DIMENSIONS_MM.Length)
                presetIndex = 0;

            var dims = AppConstants.PRESET_DIMENSIONS_MM[presetIndex];
            var name = $"Board {dims.x}x{dims.y}x{dims.z}";
            return CreateBoard(dims, name, position);
        }

        public static GameObject Duplicate(KitchenElement source)
        {
            if (source == null) return null;

            var dims = source.DimensionsMM;
            var offset = source.transform.position + new Vector3(0.1f, 0, 0);
            var go = CreateBoard(dims, source.BoardName + " (copy)", offset);
            go.transform.rotation = source.transform.rotation;
            return go;
        }
    }
}
