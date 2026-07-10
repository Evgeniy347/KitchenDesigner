using UnityEngine;
using UnityEngine.Pool;

namespace KitchenDesigner.Core
{
    public class ElementFactoryInstance : IElementFactory
    {
        private Material _defaultMaterial;
        private readonly ObjectPool<GameObject> _partPool;
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
            _partPool = new ObjectPool<GameObject>(
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
            el.PartName = "(pooled)";
            el.DimensionsMM = new Vector3Int(800, 400, 18);
            el.Movable = true;
            el.GroupId = 0;
            PartRegistry.Unregister(el);
        }

        public GameObject CreatePart(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = _partPool.Get();
            go.name = string.IsNullOrEmpty(name) ? "Board" : name;
            go.transform.position = position;

            var element = go.GetComponent<KitchenElement>();
            element.PartName = go.name;
            element.DimensionsMM = dimensionsMM;
            element.Movable = true;

            go.SetActive(true);
            PartRegistry.Register(element);

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
            return CreatePart(dims, name, position);
        }

        public GameObject CreateRadialShelf(int radiusMM, int thicknessMM, string name, Vector3 position)
        {
            thicknessMM = Mathf.Max(1, thicknessMM);
            radiusMM = Mathf.Max(1, radiusMM);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.IsNullOrEmpty(name) ? "Радиусная полка" : name;
            go.tag = "KitchenElement";
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Удаляем стандартный BoxCollider — заменим на MeshCollider в ApplyDimensions.
            var boxCollider = go.GetComponent<BoxCollider>();
            if (boxCollider != null) Object.DestroyImmediate(boxCollider);

            var shelf = go.AddComponent<RadialShelfElement>();
            shelf.PartName = go.name;
            shelf.DimensionsMM = new Vector3Int(radiusMM, thicknessMM, radiusMM);

            MaterialManager.ApplyById(shelf, MaterialCatalog.DefaultId);
            PartRegistry.Register(shelf);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return go;
        }

        public GameObject Duplicate(KitchenElement source)
        {
            if (source == null) return null;

            var dims = source.DimensionsMM;
            var offset = source.transform.position + new Vector3(0.1f, 0, 0);

            if (source is AssembledFacadeElement assembled)
            {
                var go = CreateAssembledFacade(dims, source.PartName + " (copy)", offset, assembled.Fill);
                go.transform.rotation = source.transform.rotation;
                var copy = go.GetComponent<AssembledFacadeElement>();
                if (copy != null) { copy.Mode = assembled.Mode; copy.GrooveCount = assembled.GrooveCount; }
                MaterialManager.ApplyById(go.GetComponent<KitchenElement>(), source.MaterialId);
                return go;
            }

            if (source is RadialShelfElement radial)
            {
                var go = CreateRadialShelf(radial.Radius, dims.y, source.PartName + " (copy)", offset);
                go.transform.rotation = source.transform.rotation;
                MaterialManager.ApplyById(go.GetComponent<KitchenElement>(), source.MaterialId);
                return go;
            }

            var facade = source as FacadeElement;
            if (facade != null)
            {
                var go = CreateFacade(dims, source.PartName + " (copy)", offset,
                    facade.GapLeft, facade.GapRight, facade.GapTop, facade.GapBottom);
                go.transform.rotation = source.transform.rotation;
                var copyFacade = go.GetComponent<FacadeElement>();
                if (copyFacade != null)
                    copyFacade.Mode = facade.Mode;
                return go;
            }

            var go2 = CreatePart(dims, source.PartName + " (copy)", offset);
            go2.transform.rotation = source.transform.rotation;

            if (source.GetComponent<Wall>() != null)
                go2.AddComponent<Wall>();

            return go2;
        }

        public GameObject CreateWall(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = CreatePart(dimensionsMM, name, position);
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
            facade.PartName = go.name;
            facade.DimensionsMM = dimensionsMM;
            facade.GapLeft = gapLeft;
            facade.GapRight = gapRight;
            facade.GapTop = gapTop;
            facade.GapBottom = gapBottom;

            go.SetActive(true);
            PartRegistry.Register(facade);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return go;
        }

        public GameObject CreateAssembledFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            AssembledFill fill = AssembledFill.Blind)
        {
            // Сборный фасад НЕ пулим: процедурный меш + дочерние объекты не переживают
            // сброс пула. Создаём свежий GameObject по образцу пула деталей.
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.IsNullOrEmpty(name) ? "Сборный фасад" : name;
            go.tag = "KitchenElement";
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var facade = go.AddComponent<AssembledFacadeElement>();
            facade.PartName = go.name;
            facade.DimensionsMM = dimensionsMM; // ApplyDimensions → RebuildMesh
            facade.Fill = fill;
            MaterialManager.ApplyById(facade, facade.MaterialId); // декор в сабмеш 0

            PartRegistry.Register(facade);
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return go;
        }

        public void DestroyPart(GameObject go)
        {
            if (go == null) return;
            _partPool.Release(go);
        }

        public void DestroyFacade(GameObject go)
        {
            if (go == null) return;
            _facadePool.Release(go);
        }

        public void DestroyElement(GameObject go)
        {
            if (go == null) return;
            // Сборный фасад и радиусная полка не из пула — уничтожаем напрямую.
            if (go.GetComponent<AssembledFacadeElement>() != null || go.GetComponent<RadialShelfElement>() != null)
            {
                PartRegistry.Unregister(go.GetComponent<KitchenElement>());
                if (Application.isPlaying)
                    Object.Destroy(go);
                else
                    Object.DestroyImmediate(go);
            }
            else if (go.GetComponent<FacadeElement>() != null)
                _facadePool.Release(go);
            else
                _partPool.Release(go);
        }

        public void ClearPools()
        {
            _partPool.Clear();
            _facadePool.Clear();
        }
    }
}
