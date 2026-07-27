using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using KitchenDesigner.Core.UI;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SaveLoadManagerInstance : ISaveLoadManager
    {
        public string SavesDirectory =>
            Path.Combine(Application.persistentDataPath, "saves");

        private const string LastPathKey = "KitchenLastSavePath";

        public string LastPath
        {
            get => PlayerPrefs.GetString(LastPathKey, "");
            set
            {
                PlayerPrefs.SetString(LastPathKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        public bool HasLastPath => !string.IsNullOrEmpty(LastPath);

        public string LastDirectory
        {
            get
            {
                var p = LastPath;
                if (!string.IsNullOrEmpty(p))
                {
                    var dir = Path.GetDirectoryName(p);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
                }
                if (!Directory.Exists(SavesDirectory)) Directory.CreateDirectory(SavesDirectory);
                return SavesDirectory;
            }
        }

        public bool SaveToPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var data = CaptureScene(FindAllElements());
            bool ok = SaveToFile(path, data);
            if (ok)
                LastPath = path;
            return ok;
        }

        public bool SaveToLastPath()
        {
            return HasLastPath && SaveToPath(LastPath);
        }

        public bool LoadFromPath(string path)
        {
            var data = LoadFromFile(path);
            if (data == null) return false;

            if (!IsVersionCompatible(data))
                Debug.LogWarning($"[SaveLoad] Version mismatch: file={data.version}, app={AppConstants.SAVE_FORMAT_VERSION}");

            ClearBoardsImmediate(FindAllElements());
            RestoreScene(data);
            LastPath = path;

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
            return true;
        }

        public bool LoadLastSession()
        {
            if (HasLastPath && File.Exists(LastPath))
                return LoadFromPath(LastPath);

            if (File.Exists(PathForName(AutoSaveManager.AutoSaveName)))
                return LoadProject(AutoSaveManager.AutoSaveName);

            return false;
        }

        public ProjectData CaptureScene(IEnumerable<KitchenElement> elements)
        {
            var items = new List<ElementData>();
            var ordered = new List<KitchenElement>();
            ElementData? basePlateData = null;
            foreach (var e in elements)
            {
                if (e == null) continue;
                if (e.GetComponent<BasePlate>() != null)
                {
                    if (basePlateData == null)
                        basePlateData = ElementData.FromElement(e);
                    continue;
                }
                items.Add(ElementData.FromElement(e));
                ordered.Add(e);
            }
            if (basePlateData == null)
            {
                var floorGo = GameObject.FindWithTag("Floor");
                if (floorGo != null)
                {
                    var bp = floorGo.GetComponent<BasePlate>();
                    if (bp != null && bp.Element != null)
                        basePlateData = ElementData.FromElement(bp.Element);
                }
            }

            var data = new ProjectData(items);
            var groups = new List<GroupData>();
            foreach (var g in GroupManager.AllGroups())
                groups.Add(new GroupData { id = g.id, name = g.name, movable = g.movable, widthAxis = g.widthAxis });
            data.groups = groups.ToArray();
            data.rooms = new List<RoomData>(ProjectRooms.Items).ToArray();
            data.floorplans = new List<FloorplanScopeData>(ProjectFloorplans.Items).ToArray();

            if (CameraController.Instance != null)
                data.camera = CameraController.Instance.GetState();

            data.handleMode = ResizeHandleManager.Mode.ToString();

            data.projectInstructions = ProjectInstructions.Text;

            if (basePlateData != null)
            {
                data.basePlate = basePlateData;
                data.basePlateValid = true;
            }

            data.settings = KitchenSettings.Instance.ToData();

            // Рабочее место пользователя: тумблеры вида из тулбара и окна проекта
            // (где стоят и какие открыты). Контекстные меню сюда не входят.
            data.tintEnabled = ElementHighlighter.TintEnabled;
            data.lightsOn = LightSourceElement.GlobalOn;
            data.windows = ProjectWindows.Capture();

            var indexOf = new Dictionary<KitchenElement, int>();
            for (int i = 0; i < ordered.Count; i++) indexOf[ordered[i]] = i;
            int IndexOf(KitchenElement el) =>
                el != null && indexOf.TryGetValue(el, out var i) ? i : -1;
            data.undoHistory = CommandStack.Instance.ExportUndo(IndexOf).ToArray();
            data.redoHistory = CommandStack.Instance.ExportRedo(IndexOf).ToArray();

            return data;
        }

        public string Serialize(ProjectData data) => JsonUtility.ToJson(data, true);

        public ProjectData? Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            // composite-история пишется плоско (CompositeCommand.ToRecord), поэтому
            // глубоко вложенных записей, на которых падал JsonUtility, больше нет —
            // защитная чистка истории при загрузке не нужна.
            return JsonUtility.FromJson<ProjectData>(json);
        }

        public bool IsVersionCompatible(ProjectData data) =>
            data != null && data.version == AppConstants.SAVE_FORMAT_VERSION;

        public void ClearBoards(IEnumerable<KitchenElement> elements)
        {
            foreach (var e in elements)
            {
                if (e == null) continue;
                if (e.GetComponent<BasePlate>() != null) continue;
                DestroyElement(e.gameObject);
            }
        }

        public List<GameObject> RestoreScene(ProjectData data)
        {
            var created = new List<GameObject>();
            if (data == null || data.elements == null) return created;

            // Старые проекты содержат кириллические имена, пробелы и прямые дубли.
            // Чиним ДО создания элементов и одним проходом, чтобы связи по именам
            // (пара ящика, фасад ящика, стена окна/двери) уехали на новые имена.
            NormalizeElementNames(data.elements);

            GroupManager.Clear();
            if (data.groups != null)
                foreach (var gd in data.groups)
                    if (gd != null) GroupManager.Register(gd.id, gd.name, gd.movable,
                        string.IsNullOrEmpty(gd.widthAxis) ? "x" : gd.widthAxis);

            var resolved = new List<KitchenElement?>();
            foreach (var ed in data.elements)
            {
                if (ed == null) { resolved.Add(null); continue; }
				var go = ed.isDrawer
					? ElementFactory.Instance.CreateDrawer((DrawerType)ed.drawerType, ed.drawerNominalLength,
						(DrawerColor)ed.drawerColor, ed.drawerInternalWidth, ed.name, ed.Position, (DrawerSystem)ed.drawerSystem)
					: ed.isWindow
						? ElementFactory.Instance.CreateWindow(ed.Dimensions, ed.name, ed.Position,
							(GlassTint)ed.windowTint, ed.windowSillProtrusionMM)
						: ed.isDoor
						? ElementFactory.Instance.CreateDoor(ed.Dimensions, ed.name, ed.Position,
                            (DoorSashType)ed.doorSashType)
						: ed.isPanel
						? ElementFactory.Instance.CreatePanel(ed.Dimensions, ed.name, ed.Position,
							ed.gapLeft, ed.gapRight, ed.gapTop, ed.gapBottom)
						: ed.isWall
						? ElementFactory.Instance.CreateWall(ed.Dimensions, ed.name, ed.Position)
						: ed.isFloor
						? ElementFactory.Instance.CreateFloor(ed.Dimensions, ed.name, ed.Position)
						: ed.isLightSource
						? ElementFactory.Instance.CreateLightSource(ed.name, ed.Position)
						: ed.isSink
						? ElementFactory.Instance.CreateSink(ed.name, ed.Position)
						: ed.isPillar
							? ElementFactory.Instance.CreatePillar(ed.midHeightMM, ed.name, ed.Position)
							: ed.isTable
								? ElementFactory.Instance.CreateTable(ed.Dimensions, ed.name, ed.Position)
								: ed.isRadiusTable
									? ElementFactory.Instance.CreateRadiusTable(ed.Dimensions, ed.name, ed.Position)
                            : ed.isRadialShelf
                                ? ElementFactory.Instance.CreateRadialShelf(ed.Dimensions.x, ed.Dimensions.z,
                                    ed.Dimensions.y, ed.cornerRadius, ed.name, ed.Position)
                                : ed.assembled
                                    ? ElementFactory.Instance.CreateAssembledFacade(ed.Dimensions, ed.name,
                                        ed.Position, (AssembledFill)ed.assembledFill)
                                    : ed.isFacade
                                        ? ElementFactory.Instance.CreateFacade(ed.Dimensions, ed.name, ed.Position,
                                            ed.gapLeft, ed.gapRight, ed.gapTop, ed.gapBottom)
                                        : ElementFactory.Instance.CreatePart(ed.Dimensions, ed.name, ed.Position);
                go.transform.rotation = ed.Rotation;
                var el = go.GetComponent<KitchenElement>();
                if (el != null)
                {
                    el.Movable = ed.movable;
                    el.GroupId = ed.groupId;
                    el.Transparent = ed.transparent;
                    MaterialManager.ApplyById(el, ed.materialId);

                    if (el is TableElement tableEl2 && !string.IsNullOrEmpty(ed.legsMaterialId))
                        MaterialManager.ApplyLegs(tableEl2, MaterialCatalog.Get(ed.legsMaterialId));
                    if (el is RadiusTableElement rTableEl2 && !string.IsNullOrEmpty(ed.legsMaterialId))
                        MaterialManager.ApplyLegs(rTableEl2, MaterialCatalog.Get(ed.legsMaterialId));

					if (el is TableElement tEl)
						tEl.LegInsetMM = ed.legInsetMM;
					if (el is RadiusTableElement rtEl)
						rtEl.LegInsetMM = ed.legInsetMM;

                    if (el is AssembledFacadeElement assembled)
                        assembled.GrooveCount = ed.grooveCount;

                    // Пазы принимает только базовая «Деталь»; SetGrooves сам
                    // отсеет лишнее и пересоберёт меш.
                    if (el.SupportsGrooves)
                        el.SetGrooves(ed.GrooveSpecs());

					var wall = el.GetComponent<Wall>();
					if (wall != null)
					{
						wall.Kind = ed.wallKind ?? "";
						if (ed.wallEndShape != null && ed.wallEndShape.Length >= 4)
							wall.SetEndShape(new WallMeshBuilder.EndShape
							{
								startFront = ed.wallEndShape[0], startBack = ed.wallEndShape[1],
								endFront = ed.wallEndShape[2], endBack = ed.wallEndShape[3]
							});
					}

					if (el is FloorElement floorEl && ed.floorPolygonXZ != null && ed.floorPolygonXZ.Length >= 6)
						floorEl.SetPolygonLocalMm(ed.FloorPolygon());

                    if (ed.isFacade && el is FacadeElement facade)
                    {
                        // Зазоры проставляем ЯВНО для любого фасада: обычный получает их
                        // через параметры CreateFacade, но сборный создаётся фабрикой
                        // CreateAssembledFacade БЕЗ параметров зазоров — без этой строки
                        // сохранённые зазоры сборного фасада терялись при загрузке (0/0/0/0).
                        facade.GapLeft = ed.gapLeft;
                        facade.GapRight = ed.gapRight;
                        facade.GapTop = ed.gapTop;
                        facade.GapBottom = ed.gapBottom;
                        facade.Mode = (DoorMode)ed.doorMode;
                        if (ed.doorOpen)
                            facade.SetOpen(true);
                    }

                    if (ed.isDrawer && el is DrawerElement drawerEl)
                    {
                        drawerEl.IsDouble = ed.drawerIsDouble;
                        drawerEl.IsUpperDrawer = ed.drawerIsUpper;
                        drawerEl.PairedDrawerName = ed.drawerPairedName;
                        drawerEl.AttachedFacadeName = ed.drawerAttachedFacadeName;
                        drawerEl.DoubleState = (DoubleDrawerState)ed.doubleDrawerState;
                        // Одиночный ящик открыт по doorOpen (DoubleState его не описывает).
                        if (ed.doorOpen && !drawerEl.IsOpen)
                            drawerEl.SetOpen(true);
                    }

                    if (ed.isWindow && el is WindowElement winEl)
                    {
                        winEl.Mode = (DoorMode)ed.windowDoorMode;
                        if (ed.windowIsOpen) winEl.SetOpen(true);
                        winEl.AttachedWallName = ed.windowAttachedWallName;
                    }

                    if (ed.isDoor && el is DoorElement doorEl)
                    {
                        doorEl.Mode = (DoorMode)ed.doorDoorMode;
                        if (ed.doorIsOpen) doorEl.SetOpen(true);
                        doorEl.AttachedWallName = ed.doorAttachedWallName;
                    }

                    // Мойка: имя детали и смещение восстанавливаем ДО первого
                    // SnapToPart, иначе она врежется в столешницу по позиции
                    // курсора-заглушки, а не туда, где стояла.
                    if (ed.isSink && el is SinkElement sinkEl)
                    {
                        sinkEl.AttachedPartName = ed.sinkAttachedPartName;
                        sinkEl.OffsetXMM = ed.sinkOffsetXMM;
                        sinkEl.OffsetYMM = ed.sinkOffsetYMM;
                    }

                    if (ed.isLightSource && el is LightSourceElement lightEl)
                    {
                        lightEl.TemperatureK = ed.lightTemperatureK;
                        lightEl.PowerW = ed.lightPowerW;
                        lightEl.DiffusionPct = ed.lightDiffusionPct;
                        lightEl.UpLightPct = ed.lightUpPct;
                        lightEl.BeamAngleDeg = ed.lightBeamDeg;
                    }
                }
                created.Add(go);
                resolved.Add(el);
            }

            if (data.camera.valid && CameraController.Instance != null)
                CameraController.Instance.SetState(data.camera);

            if (!string.IsNullOrEmpty(data.handleMode) &&
                System.Enum.TryParse<ResizeHandleManager.HandleMode>(data.handleMode, out var mode))
                ResizeHandleManager.SetMode(mode);

            if (data.basePlateValid && data.basePlate != null)
                RestoreBasePlate(data.basePlate);

            if (data.settings != null)
                KitchenSettings.Instance.ApplyFrom(data.settings);

            // Тумблеры вида и окна проекта. Тонировку ставим ДО RefreshHighlights
            // ниже, иначе подсветка перерисуется по старому значению.
            ElementHighlighter.TintEnabled = data.tintEnabled;
            LightSourceElement.SetGlobalOn(data.lightsOn);
            ProjectWindows.Apply(data.windows);

            ProjectInstructions.Text = data.projectInstructions ?? "";
            ProjectRooms.Set(data.rooms);
            ProjectFloorplans.Set(data.floorplans);

            CommandStack.Instance.Import(data.undoHistory, data.redoHistory,
                i => (i >= 0 && i < resolved.Count) ? resolved[i]! : null!);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return created;
        }

        /// <summary>
        /// Привести имена элементов проекта к допустимому алфавиту и сделать их
        /// уникальными, перенеся на новые имена все связи-по-имени.
        ///
        /// Порядок важен: сначала полностью строится карта старое→новое (по ВСЕМ
        /// элементам), и только потом переписываются ссылки. Если переименовывать
        /// и чинить ссылки по ходу, ссылка на ещё не обработанный элемент указала
        /// бы на его старое имя и осталась битой.
        ///
        /// Карта ключуется без учёта регистра: два элемента «Facade»/«facade» —
        /// это коллизия, второй получит «facade_1», и ссылки на них должны
        /// разойтись так же, как разошлись сами имена.
        /// </summary>
        private static void NormalizeElementNames(ElementData[] elements)
        {
            // Занятыми считаем и то, что уже стоит в сцене (подложка переживает
            // очистку) — иначе загруженный элемент мог бы забрать её имя.
            var used = ElementNaming.ReservedFromScene();
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ed in elements)
            {
                if (ed == null) continue;
                var oldName = ed.name ?? string.Empty;

                // Одинаковые исходные имена — это РАЗНЫЕ элементы (коллизия в файле),
                // каждый обязан получить своё имя. В карту попадает только первый:
                // ссылки на неоднозначное имя всё равно неразрешимы, и первый —
                // единственный разумный кандидат.
                var newName = ElementNaming.Normalize(oldName, null, used);
                used.Add(newName);
                ed.name = newName;

                if (!string.IsNullOrEmpty(oldName) && !map.ContainsKey(oldName))
                    map[oldName] = newName;
            }

            foreach (var ed in elements)
            {
                if (ed == null) continue;
                ed.drawerPairedName = Remap(map, ed.drawerPairedName);
                ed.drawerAttachedFacadeName = Remap(map, ed.drawerAttachedFacadeName);
                ed.windowAttachedWallName = Remap(map, ed.windowAttachedWallName);
                ed.doorAttachedWallName = Remap(map, ed.doorAttachedWallName);
            }
        }

        /// <summary>Ссылка на элемент по имени → новое имя. Ссылку, для которой в
        /// файле нет элемента, оставляем КАК ЕСТЬ: RestoreScene вызывают и на
        /// неполном наборе (частичное восстановление, тесты), и обнуление такой
        /// ссылки потеряло бы связь с элементом, которого просто нет в этой
        /// пачке. Несопоставленное имя всё равно ни к чему не приведёт — связи
        /// ищутся точным совпадением.</summary>
        private static string Remap(Dictionary<string, string> map, string? link)
        {
            if (string.IsNullOrEmpty(link)) return "";
            return map.TryGetValue(link!, out var renamed) ? renamed : link!;
        }

        private void RestoreBasePlate(ElementData data)
        {
            if (data == null) return;
            var floorGo = GameObject.FindWithTag("Floor");
            KitchenElement? element;
            if (floorGo != null)
            {
                var bp = floorGo.GetComponent<BasePlate>();
                element = bp != null ? (bp.Element ?? bp.GetComponent<KitchenElement>()) : null;
                if (element == null)
                    element = floorGo.GetComponent<KitchenElement>();
            }
            else
            {
                element = BasePlate.Create().Element;
            }
            if (element == null) return;
            element.DimensionsMM = data.Dimensions;
            element.transform.position = data.Position;
            element.transform.rotation = data.Rotation;
            element.Movable = data.movable;
            element.Transparent = data.transparent;
            MaterialManager.ApplyById(element, data.materialId);
        }

        public bool SaveToFile(string path, ProjectData data)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, Serialize(data));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoad] Save failed: {ex.Message}");
                return false;
            }
        }

        public ProjectData? LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[SaveLoad] File not found: {path}");
                return null;
            }
            try
            {
                return Deserialize(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoad] Load failed: {ex.Message}");
                return null;
            }
        }

        public string PathForName(string name) =>
            Path.Combine(SavesDirectory, name + ".json");

        public bool SaveProject(string name, bool backup = true)
        {
            var data = CaptureScene(FindAllElements());
            string path = PathForName(name);

            if (backup && File.Exists(path))
                BackupExisting(path);

            return SaveToFile(path, data);
        }

        public string CaptureCurrentJson()
        {
            return Serialize(CaptureScene(FindAllElements()));
        }

        private void BackupExisting(string path)
        {
            try
            {
                string dir = Path.Combine(SavesDirectory, "backups");
                Directory.CreateDirectory(dir);

                string baseName = Path.GetFileNameWithoutExtension(path);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string zipPath = Path.Combine(dir, $"{baseName}_{stamp}.zip");

                using (var fs = new FileStream(zipPath, FileMode.Create))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry($"{baseName}_{stamp}.json", System.IO.Compression.CompressionLevel.Optimal);
                    using (var es = entry.Open())
                    using (var src = File.OpenRead(path))
                        src.CopyTo(es);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoad] Backup failed: {ex.Message}");
            }
        }

        public bool LoadProject(string name)
        {
            var data = LoadFromFile(PathForName(name));
            if (data == null) return false;

            if (!IsVersionCompatible(data))
                Debug.LogWarning($"[SaveLoad] Version mismatch: file={data.version}, app={AppConstants.SAVE_FORMAT_VERSION}");

            ClearBoardsImmediate(FindAllElements());
            RestoreScene(data);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
            return true;
        }

        private void ClearBoardsImmediate(IEnumerable<KitchenElement> elements)
        {
            foreach (var e in elements)
            {
                if (e == null) continue;
                if (e.GetComponent<BasePlate>() != null) continue;
                DestroyElement(e.gameObject);
            }
        }

        public string[] GetSaveFiles()
        {
            if (!Directory.Exists(SavesDirectory)) return new string[0];
            var files = Directory.GetFiles(SavesDirectory, "*.json");
            var names = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
                names[i] = Path.GetFileNameWithoutExtension(files[i]);
            return names;
        }

        private IEnumerable<KitchenElement> FindAllElements()
        {
            return PartRegistry.Instance.GetAll();
        }

        private static void DestroyElement(GameObject go)
        {
            if (go == null) return;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
