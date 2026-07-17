using UnityEngine;
using UnityEngine.Pool;

namespace KitchenDesigner.Core
{
    public class ElementFactoryInstance : IElementFactory
    {
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
            var window = go.GetComponent<WindowElement>();
            if (window != null) Object.DestroyImmediate(window);
            var door = go.GetComponent<DoorElement>();
            if (door != null) Object.DestroyImmediate(door);
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

        public GameObject CreateRadialShelf(int widthMM, int depthMM, int thicknessMM, int cornerRadiusMM, string name, Vector3 position)
        {
            widthMM = Mathf.Max(1, widthMM);
            depthMM = Mathf.Max(1, depthMM);
            thicknessMM = Mathf.Max(1, thicknessMM);

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
            shelf.DimensionsMM = new Vector3Int(widthMM, thicknessMM, depthMM);
            shelf.CornerRadius = cornerRadiusMM; // клампится к 1..min(ширина, глубина)

            MaterialManager.ApplyById(shelf, MaterialCatalog.DefaultId);
            PartRegistry.Register(shelf);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return go;
        }

        public GameObject Duplicate(KitchenElement source)
        {
            if (source == null) return null!;

            var dims = source.DimensionsMM;
            var offset = source.transform.position + new Vector3(0.1f, 0, 0);

            if (source is DrawerElement srcDrawer)
            {
                var go = CreateDrawer(srcDrawer.Type, srcDrawer.NominalLength, srcDrawer.Color, srcDrawer.InternalWidth, source.PartName + " (copy)", offset);
                go.transform.rotation = source.transform.rotation;
                var copy = go.GetComponent<DrawerElement>();
                if (copy != null)
                {
                    copy.IsDouble = srcDrawer.IsDouble;
                    copy.IsUpperDrawer = srcDrawer.IsUpperDrawer;
                    // Связи по именам НЕ копируем: копия «украла» бы пару/фасад
                    // оригинала (цикл копии двигал бы чужой парный ящик).
                }
                return go;
            }

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
                var go = CreateRadialShelf(dims.x, dims.z, dims.y, radial.CornerRadius, source.PartName + " (copy)", offset);
                go.transform.rotation = source.transform.rotation;
                MaterialManager.ApplyById(go.GetComponent<KitchenElement>(), source.MaterialId);
                return go;
            }

			if (source is FloorElement)
			{
				var go = CreateFloor(dims, source.PartName + " (copy)", offset);
				go.transform.rotation = source.transform.rotation;
				MaterialManager.ApplyById(go.GetComponent<KitchenElement>(), source.MaterialId);
				return go;
			}

			if (source is LightSourceElement)
			{
				var go = CreateLightSource(source.PartName + " (copy)", offset);
				go.transform.rotation = source.transform.rotation;
				return go;
			}

			if (source is PillarElement srcPillar)
			{
				var go = CreatePillar(srcPillar.MidHeightMM, source.PartName + " (copy)", offset);
				go.transform.rotation = source.transform.rotation;
				MaterialManager.ApplyById(go.GetComponent<KitchenElement>(), source.MaterialId);
				return go;
			}

			if (source is TableElement tbl)
			{
				var go = CreateTable(dims, source.PartName + " (copy)", offset);
				go.transform.rotation = source.transform.rotation;
				MaterialManager.ApplyById(go.GetComponent<KitchenElement>(), source.MaterialId);
				return go;
			}

			if (source is RadiusTableElement rtSrc)
			{
				var go = CreateRadiusTable(dims, source.PartName + " (copy)", offset);
				go.transform.rotation = source.transform.rotation;
				var copy = go.GetComponent<RadiusTableElement>();
				if (copy != null)
				{
					copy.LegInsetMM = rtSrc.LegInsetMM;
					copy.TabletopMaterialId = rtSrc.TabletopMaterialId;
					copy.LegsMaterialId = rtSrc.LegsMaterialId;
				}
				MaterialManager.ApplyById(go.GetComponent<KitchenElement>(), source.MaterialId);
				return go;
			}

			if (source is WindowElement srcWin)
            {
                var go = CreateWindow(dims, source.PartName + " (copy)", offset, srcWin.Tint, srcWin.SillProtrusionMM);
                go.transform.rotation = source.transform.rotation;
                var copy = go.GetComponent<WindowElement>();
                if (copy != null) { copy.Mode = srcWin.Mode; }
                MaterialManager.ApplyById(go.GetComponent<KitchenElement>(), source.MaterialId);
                return go;
            }

			if (source is DoorElement srcDoor)
            {
                var go = CreateDoor(dims, source.PartName + " (copy)", offset);
                go.transform.rotation = source.transform.rotation;
                var copy = go.GetComponent<DoorElement>();
                if (copy != null) { copy.Mode = srcDoor.Mode; }
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
            var go = new GameObject(name);
            go.tag = "KitchenElement";
            go.transform.position = position;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = WallMeshBuilder.Build(new System.Collections.Generic.List<WallMeshBuilder.WindowCutout>());

            var el = go.AddComponent<KitchenElement>();
            el.PartName = name;
            el.DimensionsMM = dimensionsMM;
            MaterialManager.ApplyById(el, MaterialCatalog.DefaultId);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mf.sharedMesh;
            collider.convex = false;

            go.AddComponent<Wall>();

            PartRegistry.Register(el);
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
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

        public GameObject CreateDrawer(DrawerType type, int nominalLength, DrawerColor color, int internalWidth, string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.IsNullOrEmpty(name) ? "Ящик GTV" : name;
            go.tag = "KitchenElement";
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var drawer = go.AddComponent<DrawerElement>();
            drawer.PartName = go.name;
            drawer.Type = type;
            drawer.NominalLength = nominalLength;
            drawer.InternalWidth = internalWidth;
            drawer.Color = color;

            MaterialManager.ApplyById(drawer, DrawerConstants.GetColorMaterialId(color));
            PartRegistry.Register(drawer);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return go;
        }

        public GameObject CreateTable(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.IsNullOrEmpty(name) ? "Прямоугольный стол" : name;
            go.tag = "KitchenElement";
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var boxCollider = go.GetComponent<BoxCollider>();
            if (boxCollider != null) Object.DestroyImmediate(boxCollider);

            var meshCollider = go.AddComponent<MeshCollider>();
            meshCollider.convex = false;

            var table = go.AddComponent<TableElement>();
            table.PartName = go.name;
            table.DimensionsMM = dimensionsMM;

            if (DefaultMaterial != null)
                table.SetMaterial(DefaultMaterial);

            PartRegistry.Register(table);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return go;
        }

		public GameObject CreateRadiusTable(Vector3Int dimensionsMM, string name, Vector3 position)
		{
			var go = new GameObject(string.IsNullOrEmpty(name) ? "Радиусный стол" : name);
			go.tag = "KitchenElement";
			go.transform.position = position;

			var rb = go.AddComponent<Rigidbody>();
			rb.isKinematic = true;
			rb.useGravity = false;

			var radiusTable = go.AddComponent<RadiusTableElement>();
			radiusTable.PartName = go.name;
			radiusTable.DimensionsMM = dimensionsMM;

			if (DefaultMaterial != null)
				radiusTable.SetMaterial(DefaultMaterial);

			PartRegistry.Register(radiusTable);

			if (ElementHighlighter.Instance != null)
				ElementHighlighter.Instance.RefreshHighlights();

			return go;
		}

		public GameObject CreatePillar(int midHeightMM, string name, Vector3 position)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = string.IsNullOrEmpty(name) ? "Опора" : name;
			go.tag = "KitchenElement";
			go.transform.position = position;

			var rb = go.AddComponent<Rigidbody>();
			rb.isKinematic = true;
			rb.useGravity = false;

			var boxCollider = go.GetComponent<BoxCollider>();
			if (boxCollider != null) Object.DestroyImmediate(boxCollider);

			var meshCollider = go.AddComponent<MeshCollider>();
			meshCollider.convex = false;

			var pillar = go.AddComponent<PillarElement>();
			pillar.PartName = go.name;
			pillar.MidHeightMM = midHeightMM;
			pillar.DimensionsMM = new Vector3Int(PillarElement.TopDiameterMM, pillar.TotalHeightMM, PillarElement.TopDiameterMM);

			if (DefaultMaterial != null)
				MaterialManager.ApplyById(pillar, MaterialCatalog.DefaultId);

			PartRegistry.Register(pillar);

			if (ElementHighlighter.Instance != null)
				ElementHighlighter.Instance.RefreshHighlights();

			return go;
		}

		public GameObject CreateFloor(Vector3Int dimensionsMM, string name, Vector3 position)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = string.IsNullOrEmpty(name) ? "Пол" : name;
			go.tag = "KitchenElement";
			go.transform.position = position;

			var rb = go.AddComponent<Rigidbody>();
			rb.isKinematic = true;
			rb.useGravity = false;

			var floor = go.AddComponent<FloorElement>();
			floor.PartName = go.name;
			floor.DimensionsMM = dimensionsMM;
			floor.Movable = true;

			MaterialManager.ApplyById(floor, MaterialCatalog.DefaultId);

			FloorElement.RefreshBasePlateVisibility();

			PartRegistry.Register(floor);

			if (ElementHighlighter.Instance != null)
				ElementHighlighter.Instance.RefreshHighlights();

			return go;
		}

		public GameObject CreateLightSource(string name, Vector3 position)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			go.name = string.IsNullOrEmpty(name) ? "Источник света" : name;
			go.tag = "KitchenElement";
			go.transform.position = position;

			var rb = go.AddComponent<Rigidbody>();
			rb.isKinematic = true;
			rb.useGravity = false;

			var lamp = go.AddComponent<LightSourceElement>();
			lamp.PartName = go.name;
			lamp.DimensionsMM = new Vector3Int(
				LightSourceElement.DEFAULT_SIZE_MM,
				LightSourceElement.DEFAULT_SIZE_MM,
				LightSourceElement.DEFAULT_SIZE_MM);
			lamp.Movable = true;
			lamp.EnsureLight();
			lamp.SyncLightState();

			// Светящийся «плафон»: эмиссия видна и при выключенной тонировке.
			var shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader != null)
			{
				var mat = new Material(shader);
				mat.EnableKeyword("_EMISSION");
				mat.SetColor("_BaseColor", new Color(1f, 0.97f, 0.85f, 1f));
				mat.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.7f) * 1.2f);
				go.GetComponent<MeshRenderer>().material = mat;
			}

			PartRegistry.Register(lamp);

			if (ElementHighlighter.Instance != null)
				ElementHighlighter.Instance.RefreshHighlights();

			return go;
		}

		public GameObject CreateWindow(Vector3Int dimensionsMM, string name, Vector3 position,
            GlassTint tint = GlassTint.Clear, int sillProtrusionMM = 50)
        {
            var go = new GameObject(name);
            go.tag = "KitchenElement";
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Коллайдер (BoxCollider по габаритам) создаёт сам WindowElement в
            // ApplyDimensions — корневой масштаб окна единичный.
            var window = go.AddComponent<WindowElement>();
            window.PartName = name;
            window.DimensionsMM = dimensionsMM;
            window.Tint = tint;
            window.SillProtrusionMM = sillProtrusionMM;
            MaterialManager.ApplyById(window, MaterialCatalog.DefaultId);

            PartRegistry.Register(window);
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
            return go;
        }

        public GameObject CreateDoor(Vector3Int dimensionsMM, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.tag = "KitchenElement";
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var door = go.AddComponent<DoorElement>();
            door.PartName = name;
            door.DimensionsMM = dimensionsMM;
            MaterialManager.ApplyById(door, MaterialCatalog.DefaultId);

            PartRegistry.Register(door);
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
			var pillar = go.GetComponent<PillarElement>();
			if (pillar != null)
			{
				PartRegistry.Unregister(pillar);
				if (Application.isPlaying)
					Object.Destroy(go);
				else
					Object.DestroyImmediate(go);
				return;
			}
			var table = go.GetComponent<TableElement>();
			if (table != null)
			{
				table.DestroyChildren();
				PartRegistry.Unregister(table);
				if (Application.isPlaying)
					Object.Destroy(go);
				else
					Object.DestroyImmediate(go);
				return;
			}
			var rTable = go.GetComponent<RadiusTableElement>();
			if (rTable != null)
			{
				rTable.DestroyChildren();
				PartRegistry.Unregister(rTable);
				if (Application.isPlaying)
					Object.Destroy(go);
				else
					Object.DestroyImmediate(go);
				return;
			}
			var window = go.GetComponent<WindowElement>();
            if (window != null)
            {
                window.DestroyChildren();
                PartRegistry.Unregister(window);
                if (Application.isPlaying)
                    Object.Destroy(go);
                else
                    Object.DestroyImmediate(go);
                return;
            }
			var door = go.GetComponent<DoorElement>();
            if (door != null)
            {
                door.DestroyChildren();
                PartRegistry.Unregister(door);
                if (Application.isPlaying)
                    Object.Destroy(go);
                else
                    Object.DestroyImmediate(go);
                return;
            }
            if (go.GetComponent<AssembledFacadeElement>() != null
                || go.GetComponent<RadialShelfElement>() != null
                || go.GetComponent<DrawerElement>() != null)
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
