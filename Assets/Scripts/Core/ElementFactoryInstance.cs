using UnityEngine;
using UnityEngine.Pool;

namespace KitchenDesigner.Core
{
    public class ElementFactoryInstance : IElementFactory
    {
        private Material _defaultMaterial;
        private readonly ObjectPool<GameObject> _boardPool;
        private readonly ObjectPool<GameObject> _facadePool;

        private Material DefaultMaterial
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

        public ElementFactoryInstance()
        {
            _boardPool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.AddComponent<KitchenElement>();
                    var rb = go.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    go.tag = "KitchenElement";
                    go.SetActive(false);
                    return go;
                },
                actionOnGet: (go) =>
                {
                    var renderer = go.GetComponent<MeshRenderer>();
                    if (renderer != null) renderer.sharedMaterial = DefaultMaterial;
                    var collider = go.GetComponent<BoxCollider>();
                    if (collider != null) collider.enabled = true;
                },
                actionOnRelease: (go) =>
                {
                    go.SetActive(false);
                    go.name = "(pooled)";
                    RemoveCustomComponents(go);
                    ResetComponent(go);
                },
                actionOnDestroy: (go) => Object.DestroyImmediate(go),
                defaultCapacity: 20,
                maxSize: 100
            );

            _facadePool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.AddComponent<FacadeElement>();
                    var rb = go.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    go.tag = "KitchenElement";
                    go.SetActive(false);
                    return go;
                },
                actionOnGet: (go) =>
                {
                    var renderer = go.GetComponent<MeshRenderer>();
                    if (renderer != null) renderer.sharedMaterial = DefaultMaterial;
                    var collider = go.GetComponent<BoxCollider>();
                    if (collider != null) collider.enabled = true;
                },
                actionOnRelease: (go) =>
                {
                    go.SetActive(false);
                    go.name = "(pooled)";
                    RemoveCustomComponents(go);
                    ResetComponent(go);
                },
                actionOnDestroy: (go) => Object.DestroyImmediate(go),
                defaultCapacity: 10,
                maxSize: 50
            );
        }

        private void RemoveCustomComponents(GameObject go)
        {
            var wall = go.GetComponent<Wall>();
            if (wall != null) Object.DestroyImmediate(wall);
        }

        private void ResetComponent(GameObject go)
        {
            var el = go.GetComponent<KitchenElement>();
            if (el == null) return;
            el.BoardName = "(pooled)";
            el.DimensionsMM = new Vector3Int(800, 400, 18);
            el.Movable = true;
            el.GroupId = 0;
            BoardRegistry.Unregister(el);
        }

        public GameObject CreateBoard(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = _boardPool.Get();
            go.name = string.IsNullOrEmpty(name) ? "Board" : name;
            go.transform.position = position;

            var element = go.GetComponent<KitchenElement>();
            element.BoardName = go.name;
            element.DimensionsMM = dimensionsMM;
            element.Movable = true;

            go.SetActive(true);
            BoardRegistry.Register(element);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return go;
        }

        public GameObject CreatePreset(int presetIndex, Vector3 position)
        {
            if (presetIndex < 0 || presetIndex >= AppConstants.PRESET_DIMENSIONS_MM.Length)
                presetIndex = 0;

            var dims = AppConstants.PRESET_DIMENSIONS_MM[presetIndex];
            var name = $"Board {dims.x}x{dims.y}x{dims.z}";
            return CreateBoard(dims, name, position);
        }

        public GameObject Duplicate(KitchenElement source)
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

        public GameObject CreateWall(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = CreateBoard(dimensionsMM, name, position);
            go.AddComponent<Wall>();
            return go;
        }

        public GameObject CreateFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            int gapLeft = 2, int gapRight = 2, int gapTop = 2, int gapBottom = 2)
        {
            var go = _facadePool.Get();
            go.name = string.IsNullOrEmpty(name) ? "Facade" : name;
            go.transform.position = position;

            var facade = go.GetComponent<FacadeElement>();
            facade.BoardName = go.name;
            facade.DimensionsMM = dimensionsMM;
            facade.GapLeft = gapLeft;
            facade.GapRight = gapRight;
            facade.GapTop = gapTop;
            facade.GapBottom = gapBottom;

            go.SetActive(true);
            BoardRegistry.Register(facade);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return go;
        }

        public void DestroyBoard(GameObject go)
        {
            if (go == null) return;
            _boardPool.Release(go);
        }

        public void DestroyFacade(GameObject go)
        {
            if (go == null) return;
            _facadePool.Release(go);
        }

        public void DestroyElement(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<FacadeElement>() != null)
                _facadePool.Release(go);
            else
                _boardPool.Release(go);
        }

        public void ClearPools()
        {
            _boardPool.Clear();
            _facadePool.Clear();
        }
    }
}
