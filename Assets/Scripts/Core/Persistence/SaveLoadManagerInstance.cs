using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
                groups.Add(new GroupData { id = g.id, name = g.name, movable = g.movable });
            data.groups = groups.ToArray();

            if (CameraController.Instance != null)
                data.camera = CameraController.Instance.GetState();

            data.handleMode = ResizeHandleManager.Mode.ToString();

            if (basePlateData != null)
            {
                data.basePlate = basePlateData;
                data.basePlateValid = true;
            }

            data.settings = KitchenSettings.Instance.ToData();

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

            GroupManager.Clear();
            if (data.groups != null)
                foreach (var gd in data.groups)
                    if (gd != null) GroupManager.Register(gd.id, gd.name, gd.movable);

            var resolved = new List<KitchenElement?>();
            foreach (var ed in data.elements)
            {
                if (ed == null) { resolved.Add(null); continue; }
                var go = ed.isDrawer
                    ? ElementFactory.Instance.CreateDrawer((DrawerType)ed.drawerType, ed.drawerNominalLength,
                        (DrawerColor)ed.drawerColor, ed.drawerInternalWidth, ed.name, ed.Position)
                    : ed.isWall
                        ? ElementFactory.Instance.CreateWall(ed.Dimensions, ed.name, ed.Position)
                        : ed.isTable
                            ? ElementFactory.Instance.CreateTable(ed.Dimensions, ed.name, ed.Position)
                            : ed.isRadiusTable
                                ? ElementFactory.Instance.CreateRadiusTable(ed.Dimensions, ed.name, ed.Position)
                            : ed.isRadialShelf
                                ? ElementFactory.Instance.CreateRadialShelf(ed.Dimensions.x, ed.Dimensions.z,
                                    ed.Dimensions.y, ed.EffectiveCornerRadius, ed.name, ed.Position)
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

                    if (ed.isFacade && el is FacadeElement facade)
                    {
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

            CommandStack.Instance.Import(data.undoHistory, data.redoHistory,
                i => (i >= 0 && i < resolved.Count) ? resolved[i]! : null!);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return created;
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
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
