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
                renderer.sharedMaterial = mat;
            }

            var collider = go.GetComponent<BoxCollider>();
            if (collider != null)
                collider.enabled = true;

            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            go.tag = "KitchenElement";

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

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

            var facade = source as FacadeElement;
            if (facade != null)
            {
                var go = CreateFacade(dims, source.BoardName + " (copy)", offset,
                    facade.GapLeft, facade.GapRight, facade.GapTop, facade.GapBottom);
                go.transform.rotation = source.transform.rotation;
                return go;
            }

            var go2 = CreateBoard(dims, source.BoardName + " (copy)", offset);
            go2.transform.rotation = source.transform.rotation;

            if (source.GetComponent<Wall>() != null)
                go2.AddComponent<Wall>();

            return go2;
        }

        /// <summary>Стена: та же геометрия, что у доски, плюс маркер Wall
        /// (структурный якорь, исключён из спецификации/подсветки).</summary>
        public static GameObject CreateWall(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = CreateBoard(dimensionsMM, name, position);
            go.AddComponent<Wall>();
            return go;
        }

        public static GameObject CreateFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            int gapLeft = 2, int gapRight = 2, int gapTop = 2, int gapBottom = 2)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.IsNullOrEmpty(name) ? "Facade" : name;

            var facade = go.AddComponent<FacadeElement>();
            facade.BoardName = go.name;
            facade.DimensionsMM = dimensionsMM;
            facade.GapLeft = gapLeft;
            facade.GapRight = gapRight;
            facade.GapTop = gapTop;
            facade.GapBottom = gapBottom;

            go.transform.position = position;

            var mat = DefaultMaterial;
            if (mat != null)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
            }

            var collider = go.GetComponent<BoxCollider>();
            if (collider != null)
                collider.enabled = true;

            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            go.tag = "KitchenElement";

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return go;
        }
    }
}
