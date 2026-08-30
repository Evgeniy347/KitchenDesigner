using UnityEngine;
using UnityEngine.Pool;

namespace KitchenDesigner.Core
{
    public class ElementFactoryInstance : IElementFactory
    {
        public const float DUPLICATE_OFFSET_UNITS = 0.1f;

        private const int PartPoolCapacity = 20;
        private const int PartPoolMaxSize = 100;
        private const int FacadePoolCapacity = 10;
        private const int FacadePoolMaxSize = 50;

        private static readonly Vector3Int PooledPartDimensionsMM = new Vector3Int(800, 400, 18);
        private const string PooledName = "(pooled)";

        private Material? _defaultMaterial;
        private readonly ObjectPool<GameObject> _partPool;
        private readonly ObjectPool<GameObject> _facadePool;

        private Material? DefaultMaterial
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
            _partPool = NewPool<KitchenElement>(PartPoolCapacity, PartPoolMaxSize);
            _facadePool = NewPool<FacadeElement>(FacadePoolCapacity, FacadePoolMaxSize);
        }

        private ObjectPool<GameObject> NewPool<T>(int capacity, int maxSize) where T : KitchenElement =>
            new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.AddComponent<T>();
                    var rb = go.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    go.tag = ElementRoot.ELEMENT_TAG;
                    go.SetActive(false);
                    return go;
                },
                actionOnGet: go =>
                {
                    var renderer = go.GetComponent<MeshRenderer>();
                    if (renderer != null) renderer.sharedMaterial = DefaultMaterial;
                    var collider = go.GetComponent<BoxCollider>();
                    if (collider != null) collider.enabled = true;
                },
                actionOnRelease: go =>
                {
                    go.SetActive(false);
                    go.name = PooledName;
                    RemoveCustomComponents(go);
                    ResetComponent(go);
                },
                actionOnDestroy: go => Object.DestroyImmediate(go),
                defaultCapacity: capacity,
                maxSize: maxSize);

        private void RemoveCustomComponents(GameObject go)
        {
            var wall = go.GetComponent<Wall>();
            if (wall != null) Object.DestroyImmediate(wall);
            var window = go.GetComponent<WindowElement>();
            if (window != null) Object.DestroyImmediate(window);
            var door = go.GetComponent<DoorElement>();
            if (door != null) Object.DestroyImmediate(door);
        }

        private void ResetComponent(GameObject go)
        {
            var el = go.GetComponent<KitchenElement>();
            if (el == null) return;
            el.PartName = PooledName;
            el.DimensionsMM = PooledPartDimensionsMM;
            el.Movable = true;
            el.GroupId = 0;
            el.ClearCutouts();
            el.ClearGrooves();
            el.EdgeBandingEnabled = true;
            el.EdgeThicknessMM = AppConstants.EDGE_THICKNESS_DEFAULT_MM;
            el.EdgeManualMask = 0;
            PartRegistry.Unregister(el);
        }

        public GameObject CreatePart(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = _partPool.Get();
            go.name = ElementNaming.Normalize(string.IsNullOrEmpty(name) ? "Board" : name);
            go.transform.position = position;

            var element = go.GetComponent<KitchenElement>();
            element.PartName = go.name;
            element.DimensionsMM = dimensionsMM;
            element.Movable = true;

            go.SetActive(true);
            return ElementRoot.Publish(go, element);
        }

        public GameObject CreatePreset(int presetIndex, Vector3 position)
        {
            if (presetIndex < 0 || presetIndex >= AppConstants.PRESET_DIMENSIONS_MM.Length)
                presetIndex = 0;

            var dims = AppConstants.PRESET_DIMENSIONS_MM[presetIndex];
            return CreatePart(dims, $"Board {dims.x}x{dims.y}x{dims.z}", position);
        }

        public GameObject CreateRadialShelf(int widthMM, int depthMM, int thicknessMM,
            int cornerRadiusMM, string name, Vector3 position)
        {
            widthMM = Mathf.Max(1, widthMM);
            depthMM = Mathf.Max(1, depthMM);
            thicknessMM = Mathf.Max(1, thicknessMM);

            var go = ElementRoot.NewCube(name, "Радиусная полка", position);
            var box = go.GetComponent<BoxCollider>();
            if (box != null) Object.DestroyImmediate(box);

            var shelf = go.AddComponent<RadialShelfElement>();
            shelf.PartName = go.name;
            shelf.DimensionsMM = new Vector3Int(widthMM, thicknessMM, depthMM);
            shelf.CornerRadius = cornerRadiusMM;

            MaterialManager.ApplyById(shelf, MaterialCatalog.DefaultId);
            return ElementRoot.Publish(go, shelf);
        }

        public GameObject Duplicate(KitchenElement source)
        {
            if (source == null) return null!;
            var offset = source.transform.position + new Vector3(DUPLICATE_OFFSET_UNITS, 0f, 0f);
            return ElementDuplicators.Copy(this, source, offset);
        }

        public GameObject CreateWall(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = ElementRoot.NewEmpty(name, "Wall", position);

            var mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            mf.sharedMesh = WallMeshBuilder.Build(
                new System.Collections.Generic.List<WallMeshBuilder.WindowCutout>());

            var el = go.AddComponent<KitchenElement>();
            el.PartName = go.name;
            el.DimensionsMM = dimensionsMM;
            MaterialManager.ApplyById(el, MaterialCatalog.DefaultId);

            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mf.sharedMesh;
            collider.convex = false;

            go.AddComponent<Wall>();
            return ElementRoot.Publish(go, el);
        }

        public GameObject CreateFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            int gapLeft = FacadeElement.DEFAULT_GAP_MM, int gapRight = FacadeElement.DEFAULT_GAP_MM,
            int gapTop = FacadeElement.DEFAULT_GAP_MM, int gapBottom = FacadeElement.DEFAULT_GAP_MM,
            int gapFront = 0, int gapBack = 0)
        {
            var go = _facadePool.Get();
            go.name = ElementNaming.Normalize(string.IsNullOrEmpty(name) ? "Facade" : name);
            go.transform.position = position;

            var facade = go.GetComponent<FacadeElement>();
            facade.PartName = go.name;
            facade.DimensionsMM = dimensionsMM;
            facade.GapLeft = gapLeft;
            facade.GapRight = gapRight;
            facade.GapTop = gapTop;
            facade.GapBottom = gapBottom;
            facade.GapFront = gapFront;
            facade.GapBack = gapBack;

            go.SetActive(true);
            return ElementRoot.Publish(go, facade);
        }

        public GameObject CreatePanel(Vector3Int dimensionsMM, string name, Vector3 position,
            int gapLeft = PanelElement.DEFAULT_GAP_MM, int gapRight = PanelElement.DEFAULT_GAP_MM,
            int gapTop = PanelElement.DEFAULT_GAP_MM, int gapBottom = PanelElement.DEFAULT_GAP_MM,
            int gapFront = PanelElement.DEFAULT_GAP_MM, int gapBack = PanelElement.DEFAULT_GAP_MM)
        {
            var go = ElementRoot.NewCube(name, "ДВП/ХДФ", position);

            var panel = go.AddComponent<PanelElement>();
            panel.PartName = go.name;
            panel.DimensionsMM = dimensionsMM;
            panel.GapLeft = gapLeft;
            panel.GapRight = gapRight;
            panel.GapTop = gapTop;
            panel.GapBottom = gapBottom;
            panel.GapFront = gapFront;
            panel.GapBack = gapBack;

            MaterialManager.ApplyById(panel, MaterialCatalog.DefaultId);
            return ElementRoot.Publish(go, panel);
        }

        public GameObject CreateAssembledFacade(Vector3Int dimensionsMM, string name, Vector3 position,
            AssembledFill fill = AssembledFill.Blind)
        {
            var go = ElementRoot.NewCube(name, "Сборный фасад", position);

            var facade = go.AddComponent<AssembledFacadeElement>();
            facade.PartName = go.name;
            facade.DimensionsMM = dimensionsMM;
            facade.Fill = fill;
            MaterialManager.ApplyById(facade, facade.MaterialId);

            return ElementRoot.Publish(go, facade);
        }

        public GameObject CreateDrawer(DrawerType type, int nominalLength, DrawerColor color,
            int internalWidth, string name, Vector3 position, DrawerSystem system = DrawerSystem.Gtv)
        {
            var go = ElementRoot.NewCube(name, DrawerConstants.GetDefaultName(system), position);

            var drawer = go.AddComponent<DrawerElement>();
            drawer.PartName = go.name;
            drawer.System = system;
            drawer.Type = type;
            drawer.NominalLength = nominalLength;
            drawer.InternalWidth = internalWidth;
            drawer.Color = color;

            MaterialManager.ApplyById(drawer, DrawerConstants.GetColorMaterialId(color));
            return ElementRoot.Publish(go, drawer);
        }

        public GameObject CreateTable(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = ElementRoot.NewCube(name, "Прямоугольный стол", position);
            ElementRoot.SwapBoxColliderForMeshCollider(go);

            var table = go.AddComponent<TableElement>();
            table.PartName = go.name;
            table.DimensionsMM = dimensionsMM;

            if (DefaultMaterial != null) table.SetMaterial(DefaultMaterial);
            return ElementRoot.Publish(go, table);
        }

        public GameObject CreateRadiusTable(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = ElementRoot.NewEmpty(name, "Радиусный стол", position);

            var radiusTable = go.AddComponent<RadiusTableElement>();
            radiusTable.PartName = go.name;
            radiusTable.DimensionsMM = dimensionsMM;

            if (DefaultMaterial != null) radiusTable.SetMaterial(DefaultMaterial);
            return ElementRoot.Publish(go, radiusTable);
        }

        public GameObject CreatePillar(int midHeightMM, string name, Vector3 position)
        {
            var go = ElementRoot.NewCube(name, "Опора", position);
            ElementRoot.SwapBoxColliderForMeshCollider(go);

            var pillar = go.AddComponent<PillarElement>();
            pillar.PartName = go.name;
            pillar.MidHeightMM = midHeightMM;
            pillar.DimensionsMM = new Vector3Int(
                PillarElement.TopDiameterMM, pillar.TotalHeightMM, PillarElement.TopDiameterMM);

            if (DefaultMaterial != null) MaterialManager.ApplyById(pillar, MaterialCatalog.DefaultId);
            return ElementRoot.Publish(go, pillar);
        }

        public GameObject CreateFloor(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = ElementRoot.NewCube(name, "Пол", position);

            var floor = go.AddComponent<FloorElement>();
            floor.PartName = go.name;
            floor.DimensionsMM = dimensionsMM;
            floor.Movable = true;

            MaterialManager.ApplyById(floor, MaterialCatalog.DefaultId);
            FloorElement.RefreshBasePlateVisibility();

            return ElementRoot.Publish(go, floor);
        }

        public GameObject CreateLightSource(string name, Vector3 position)
        {
            var go = ElementRoot.NewEmpty(name, "Источник света", position);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            go.AddComponent<MeshRenderer>();
            go.AddComponent<SphereCollider>();

            var lamp = go.AddComponent<LightSourceElement>();
            lamp.PartName = go.name;
            lamp.DimensionsMM = new Vector3Int(
                LightSourceElement.DEFAULT_SIZE_MM,
                LightSourceElement.DEFAULT_SIZE_MM,
                LightSourceElement.DEFAULT_SIZE_MM);
            lamp.Movable = true;
            lamp.EnsureLight();
            lamp.SyncLightState();

            return ElementRoot.Publish(go, lamp);
        }

        public GameObject CreateSink(string name, Vector3 position)
        {
            var go = ElementRoot.NewEmpty(name, "Мойка", position);

            var sink = go.AddComponent<SinkElement>();
            sink.PartName = go.name;
            sink.DimensionsMM = new Vector3Int(
                SinkElement.OUTER_WIDTH_MM, SinkElement.TotalHeightMM, SinkElement.OUTER_DEPTH_MM);
            sink.Movable = true;

            return ElementRoot.Publish(go, sink);
        }

        public GameObject CreateCooktop(string name, Vector3 position, string model = "")
        {
            var go = ElementRoot.NewEmpty(name, "Варочная", position);

            var cooktop = go.AddComponent<CooktopElement>();
            cooktop.PartName = go.name;
            cooktop.Model = model ?? "";
            var modelDims = CooktopElement.ModelDimensionsMM(cooktop.Model);
            cooktop.DimensionsMM = modelDims.x > 0
                ? modelDims
                : new Vector3Int(CooktopElement.DEFAULT_WIDTH_MM,
                    CooktopElement.DEFAULT_HEIGHT_MM, CooktopElement.DEFAULT_DEPTH_MM);
            cooktop.Movable = true;

            return ElementRoot.Publish(go, cooktop);
        }

        public GameObject CreateOven(string name, Vector3 position)
        {
            var go = ElementRoot.NewEmpty(name, "Духовка", position);

            var oven = go.AddComponent<OvenElement>();
            oven.PartName = go.name;
            oven.DimensionsMM = OvenElement.ModelDimensionsMM;
            oven.Movable = true;

            return ElementRoot.Publish(go, oven);
        }

        public GameObject CreateDishwasher(string name, Vector3 position)
        {
            var go = ElementRoot.NewEmpty(name, "Посудомойка", position);

            var dishwasher = go.AddComponent<DishwasherElement>();
            dishwasher.PartName = go.name;
            dishwasher.DimensionsMM = DishwasherElement.ModelDimensionsMM;
            dishwasher.Movable = true;

            return ElementRoot.Publish(go, dishwasher);
        }

        public GameObject CreateWindow(Vector3Int dimensionsMM, string name, Vector3 position,
            GlassTint tint = GlassTint.Clear, int sillProtrusionMM = 50)
        {
            var go = ElementRoot.NewEmpty(name, "Window", position);

            var window = go.AddComponent<WindowElement>();
            window.PartName = go.name;
            window.DimensionsMM = dimensionsMM;
            window.Tint = tint;
            window.SillProtrusionMM = sillProtrusionMM;
            MaterialManager.ApplyById(window, MaterialCatalog.DefaultId);

            return ElementRoot.Publish(go, window);
        }

        public GameObject CreateDoor(Vector3Int dimensionsMM, string name, Vector3 position,
            DoorSashType sashType = DoorSashType.Glass)
        {
            var go = ElementRoot.NewEmpty(name, "Door", position);

            var door = go.AddComponent<DoorElement>();
            door.PartName = go.name;
            door.DimensionsMM = dimensionsMM;
            door.SashType = sashType;
            MaterialManager.ApplyById(door, MaterialCatalog.DefaultId);

            return ElementRoot.Publish(go, door);
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

            var element = go.GetComponent<KitchenElement>();
            if (element == null)
            {
                _partPool.Release(go);
                return;
            }

            switch (element.Disposal)
            {
                case ElementDisposal.PartPool:
                    _partPool.Release(go);
                    return;
                case ElementDisposal.FacadePool:
                    _facadePool.Release(go);
                    return;
            }

            element.PrepareForDestruction();
            PartRegistry.Unregister(element);
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        public void ClearPools()
        {
            _partPool.Clear();
            _facadePool.Clear();
        }
    }
}
