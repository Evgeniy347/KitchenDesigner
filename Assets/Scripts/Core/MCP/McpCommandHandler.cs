using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using KitchenDesigner.Core.MCP.Contract;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KitchenDesigner.Core.MCP
{
    public class McpCommandHandler
    {
        /// <summary>
        /// Диспетчер MCP-команд. Каждый метод обрабатывается в отдельном handler'е.
        ///
        /// Протокольные соглашения:
        /// - ВСЕ адресные операции — батчи (names[]/ops[]/items[]); одноэлементных нет.
        /// - Батчи атомарны: любая невалидная операция отклоняет весь батч.
        /// - edit_elements — единственный редактор свойств; dry_run:true — симуляция.
        /// - После каждой мутации смотри violations/sceneViolationCount в ответе.
        /// - dimZ всегда толщина детали (Board convention в AGENTS.md).
        /// </summary>
        public McpResponse Handle(McpRequest request)
        {
            // Команда агента — считаем это активностью, чтобы сцена не «спала»
            // на 1 FPS и агент не ждал кадр при каждом вызове.
            FrameRateManager.KeepAwake(1f);
            try
            {
                switch (request.method)
                {
                    case "ping": return HandlePing(request);
                    case "get_status": return HandleGetStatus(request);
                    case "get_scene_hierarchy": return HandleGetSceneHierarchy(request);
                    case "find_objects": return HandleFindObjects(request);
                    case "get_object_info": return HandleGetObjectInfo(request);
                    case "set_object_active": return HandleSetActive(request);
                    case "delete_object": return HandleDeleteObject(request);
                    case "set_position": return HandleSetPosition(request);
                    case "set_rotation": return HandleSetRotation(request);
                    case "set_scale": return HandleSetScale(request);
                    case "get_all_elements": return HandleGetAllElements(request);
                    case "get_elements": return HandleGetElements(request);
                    case "edit_elements": return HandleEditElements(request);
                    case "clone_elements": return HandleCloneElements(request);
                    case "align_elements": return HandleAlignElements(request);
                    case "distribute_evenly": return HandleDistributeEvenly(request);
                    case "get_free_space": return HandleGetFreeSpace(request);
                    case "create_elements": return HandleCreateElements(request);
                    case "convert_elements": return HandleConvertElements(request);
                    case "delete_elements": return HandleDeleteElements(request);
                    case "undo": return HandleUndo(request);
                    case "redo": return HandleRedo(request);
                    case "get_specification": return HandleGetSpecification(request);
                    case "export_specification_csv": return HandleExportCsv(request);
                    case "select_elements": return HandleSelectElements(request);
                    case "get_undo_stack_info": return HandleUndoStackInfo(request);
                    case "get_console_logs": return HandleConsoleLogs(request);
                    case "get_settings": return HandleGetSettings(request);
                    case "set_snap_verbose": return HandleSetSnapVerbose(request);
                    case "snap_diagnose": return HandleSnapDiagnose(request);
                    case "get_modules": return HandleGetModules(request);
                    case "module_info": return HandleModuleInfo(request);
                    case "create_module": return HandleCreateModule(request);
                    case "dissolve_module": return HandleDissolveModule(request);
                    case "add_to_module": return HandleAddToModule(request);
                    case "remove_from_module": return HandleRemoveFromModule(request);
                    case "enter_module_edit": return HandleEnterModuleEdit(request);
                    case "exit_module_edit": return HandleExitModuleEdit(request);
                    case "take_screenshot": return HandleTakeScreenshot(request);
                    case "get_floor_info": return HandleGetFloorInfo(request);
                    case "get_violations": return HandleGetViolations(request);
                    case "resize_floor": return HandleResizeFloor(request);
                    case "add_wall_component": return HandleAddWallComponent(request);
                    case "set_setting": return HandleSetSetting(request);
                    case "execute_menu_item": return HandleExecuteMenuItem(request);
                    case "enter_play_mode": return HandleEnterPlayMode(request);
                    case "exit_play_mode": return HandleExitPlayMode(request);
                    case "get_element_debug": return HandleGetElementDebug(request);
                    case "get_element_gaps": return HandleGetElementGaps(request);
                    case "rename_elements": return HandleRenameElements(request);
                    case "cycle_drawer_animation": return HandleCycleDrawerAnimation(request);
                    case "list_materials": return HandleListMaterials(request);
                    case "reload_textures": return HandleReloadTextures(request);
                    default:
                        return McpResponse.Error(request.id, -32601, $"Unknown method: {request.method}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Error handling '{request.method}': {ex.Message}\n{ex.StackTrace}");
                return McpResponse.Error(request.id, -1, $"Internal error: {ex.Message}");
            }
        }

        private McpResponse HandlePing(McpRequest req)
        {
            return McpResponse.Result(req.id, new { status = "ok", unity = Application.unityVersion });
        }

        private McpResponse HandleGetStatus(McpRequest req)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return McpResponse.Result(req.id, new StatusInfo
            {
                sceneName = activeScene.name,
                objectCount = activeScene.isLoaded ? activeScene.rootCount : 0,
                isPlaying = Application.isPlaying,
                platform = Application.platform.ToString()
            });
        }

        private McpResponse HandleGetSceneHierarchy(McpRequest req)
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var nodes = new List<HierarchyNode>();
            foreach (var root in roots)
                nodes.Add(BuildHierarchyNode(root));
            return McpResponse.Result(req.id, nodes);
        }

        private HierarchyNode BuildHierarchyNode(GameObject go)
        {
            var node = new HierarchyNode
            {
                name = go.name,
                path = GetGameObjectPath(go),
                active = go.activeSelf,
                children = new List<HierarchyNode>()
            };
            foreach (Transform child in go.transform)
                node.children.Add(BuildHierarchyNode(child.gameObject));
            return node;
        }

        private static string GetGameObjectPath(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            var t = go.transform.parent;
            while (t != null)
            {
                sb.Insert(0, "/");
                sb.Insert(0, t.name);
                t = t.parent;
            }
            return sb.ToString();
        }

        private McpResponse HandleFindObjects(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsFindObjects>();
            if (p == null || string.IsNullOrEmpty(p.name_filter))
                return McpResponse.Error(req.id, -32602, "name_filter required");

            var filter = p.name_filter.ToLowerInvariant();
            var results = new List<object>();
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allObjects)
            {
                if (go.scene.name == null) continue;
                if (go.name.ToLowerInvariant().Contains(filter))
                    results.Add(new { name = go.name, path = GetGameObjectPath(go), active = go.activeInHierarchy });
            }
            return McpResponse.Result(req.id, results);
        }

        private McpResponse HandleGetObjectInfo(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsObjectPath>();
            if (p == null || (string.IsNullOrEmpty(p.name) && string.IsNullOrEmpty(p.object_path)))
                return McpResponse.Error(req.id, -32602, "name or object_path required");

            var target = FindGameObject(p.object_path ?? p.name);
            if (target == null)
                return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path ?? p.name}");

            var components = new List<string>();
            foreach (var c in target.GetComponents<Component>())
                if (c != null) components.Add(c.GetType().Name);

            var children = new List<string>();
            foreach (Transform child in target.transform)
                children.Add(child.name);

            return McpResponse.Result(req.id, new ObjectInfo
            {
                name = target.name, path = GetGameObjectPath(target),
                posX = target.transform.position.x, posY = target.transform.position.y, posZ = target.transform.position.z,
                rotX = target.transform.eulerAngles.x, rotY = target.transform.eulerAngles.y, rotZ = target.transform.eulerAngles.z,
                scaleX = target.transform.localScale.x, scaleY = target.transform.localScale.y, scaleZ = target.transform.localScale.z,
                active = target.activeInHierarchy, components = components, children = children
            });
        }

        private McpResponse HandleSetActive(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetActive>();
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            go.SetActive(p.active);
            return McpResponse.Result(req.id, new { ok = true, active = p.active });
        }

        private McpResponse HandleDeleteObject(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsObjectPath>();
            var path = p?.object_path ?? p?.name;
            if (string.IsNullOrEmpty(path))
                return McpResponse.Error(req.id, -32602, "name or object_path required");
            var go = FindGameObject(path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {path}");
            Object.Destroy(go);
            return McpResponse.Result(req.id, new { ok = true, deleted = go.name });
        }

        /// <summary>Собрать вектор из nullable-полей, беря текущее значение для
        /// отсутствующих осей (омитить = «не менять эту ось»).</summary>
        private static Vector3 ResolveVec(float? x, float? y, float? z, Vector3 current)
            => new Vector3(x ?? current.x, y ?? current.y, z ?? current.z);

        /// <summary>Собрать размеры (мм) из nullable-полей. Приоритет: width/height/depth,
        /// затем алиасы dimX/dimY/dimZ, затем текущий размер. Отсутствующее измерение =
        /// «не менять». Каждое измерение не меньше 1 мм.</summary>
        private static Vector3Int ResolveDims(int? width, int? height, int? depth,
            int? dimX, int? dimY, int? dimZ, Vector3Int current)
        {
            int w = width ?? dimX ?? current.x;
            int h = height ?? dimY ?? current.y;
            int d = depth ?? dimZ ?? current.z;
            return new Vector3Int(Mathf.Max(1, w), Mathf.Max(1, h), Mathf.Max(1, d));
        }

        private McpResponse HandleSetPosition(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetTransform>();
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            var v = ResolveVec(p.x, p.y, p.z, go.transform.position);
            go.transform.position = v;
            return McpResponse.Result(req.id, new { ok = true, position = new { x = v.x, y = v.y, z = v.z } });
        }

        private McpResponse HandleSetRotation(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetTransform>();
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            var v = ResolveVec(p.x, p.y, p.z, go.transform.eulerAngles);
            go.transform.eulerAngles = v;
            return McpResponse.Result(req.id, new { ok = true, rotation = new { x = v.x, y = v.y, z = v.z } });
        }

        private McpResponse HandleSetScale(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetTransform>();
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            var v = ResolveVec(p.x, p.y, p.z, go.transform.localScale);
            go.transform.localScale = v;
            return McpResponse.Result(req.id, new { ok = true, scale = new { x = v.x, y = v.y, z = v.z } });
        }

        /// <summary>Инфо об элементе, включая принадлежность модулю (группе) —
        /// чтобы через MCP была видна конфигурация сцены.</summary>
        private static ElementInfo BuildElementInfo(KitchenElement el)
        {
            return BuildElementInfo(el, null, false);
        }

        private static ElementInfo BuildElementInfo(KitchenElement el, List<KitchenElement>? allElements,
            bool includeFacadeValidation = false, ValidationResult? validation = null)
        {
            var t = el.transform;
            var group = GroupManager.GroupOf(el);
            var wall = el.GetComponent<Wall>();
            Vector3 pos = wall != null ? wall.FullPosition : t.position;

            bool hasViolations = false;
            if (allElements != null && allElements.Count > 0)
            {
                // validation прокидывается вызывающим, когда сцена уже провалидирована
                // (get_all_elements, модули) — иначе Validate на каждый элемент = O(n²).
                var vr = validation ?? ConstraintValidator.Validate(allElements);
                hasViolations = vr.violations.Contains(el);
            }

            var aabb = ComputeAABB(el.GetVertices());
            var effDim = GetEffectiveDimMM(el);
            var gaps = allElements != null ? ComputeAxisGaps(el, allElements) : null;
            var radial = el as RadialShelfElement;
            var drawer = el as DrawerElement;
			var table = el as TableElement;
			var radiusTable = el as RadiusTableElement;
			var window = el as WindowElement;
			var door = el as DoorElement;
			var pillar = el as PillarElement;
            FacadeValidationData? facadeValidation = includeFacadeValidation && el is FacadeElement fe && allElements != null
                ? ComputeFacadeValidation(fe, allElements)
                : (FacadeValidationData?)null;

            return new ElementInfo
            {
                name = el.PartName, type = el.GetType().Name,
                dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                posX = pos.x, posY = pos.y, posZ = pos.z,
                rotX = t.eulerAngles.x, rotY = t.eulerAngles.y, rotZ = t.eulerAngles.z,
                active = el.gameObject.activeInHierarchy,
                locked = !el.Movable,
                moduleId = group != null ? group.id : 0,
                moduleName = group != null ? group.name : null,
                materialId = el.MaterialId,
                hasViolations = hasViolations,
                aabbMinX = aabb.minX, aabbMinY = aabb.minY, aabbMinZ = aabb.minZ,
                aabbMaxX = aabb.maxX, aabbMaxY = aabb.maxY, aabbMaxZ = aabb.maxZ,
                worldDimX = Mathf.RoundToInt((aabb.maxX - aabb.minX) / AppConstants.MM_TO_UNITS),
                worldDimY = Mathf.RoundToInt((aabb.maxY - aabb.minY) / AppConstants.MM_TO_UNITS),
                worldDimZ = Mathf.RoundToInt((aabb.maxZ - aabb.minZ) / AppConstants.MM_TO_UNITS),
                effectiveDimX = effDim.x, effectiveDimY = effDim.y, effectiveDimZ = effDim.z,
                faceGaps = gaps,
                cornerRadius = radial != null ? radial.CornerRadius : 0,
                facadeMode = el is FacadeElement feMode ? FacadeDoor.WireName(feMode.Mode) : null,
                faceNormalX = facadeValidation?.normal.x,
                faceNormalY = facadeValidation?.normal.y,
                faceNormalZ = facadeValidation?.normal.z,
                faceInward = facadeValidation != null ? facadeValidation.Value.faceInward : (bool?)null,
                faceObstructions = facadeValidation?.obstructions,
                openingViolations = facadeValidation?.openingViolations,
                drawer = drawer != null ? new DrawerInfo
                {
                    drawerType = drawer.Type.ToString(),
                    drawerLength = drawer.NominalLength,
                    drawerColor = drawer.Color.ToString(),
                    internalWidth = drawer.InternalWidth,
                    isDouble = drawer.IsDouble,
                    isUpper = drawer.IsUpperDrawer,
                    pairedDrawerName = drawer.PairedDrawerName,
                    attachedFacadeName = drawer.AttachedFacadeName,
                    doubleState = drawer.DoubleState.ToString(),
                    isOpen = drawer.IsOpen
                } : null,
                table = table != null ? new TableInfo
                {
                    legInsetMM = table.LegInsetMM,
                    tabletopMaterialId = table.TabletopMaterialId,
                    legsMaterialId = table.LegsMaterialId
                } : null,
				radiusTable = radiusTable != null ? new RadiusTableInfo
				{
					legInsetMM = radiusTable.LegInsetMM,
					shape = "capsule",
					tabletopMaterialId = radiusTable.TabletopMaterialId,
					legsMaterialId = radiusTable.LegsMaterialId
				} : null,
				pillar = pillar != null ? new PillarInfo
				{
					midHeightMM = pillar.MidHeightMM
				} : null,
				window = window != null ? new WindowInfo
                {
                    tint = window.Tint.ToString(),
                    sillProtrusionMM = window.SillProtrusionMM,
                    mode = FacadeDoor.WireName(window.Mode),
                    isOpen = window.IsOpen,
                    attachedWallName = window.AttachedWallName
                } : null,
                door = door != null ? new DoorInfo
                {
                    sashType = door.SashType.ToString(),
                    mode = FacadeDoor.WireName(door.Mode),
                    isOpen = door.IsOpen,
                    attachedWallName = door.AttachedWallName
                } : null
            };
        }

        private readonly struct FacadeValidationData
        {
            public readonly Vector3 normal;
            public readonly bool faceInward;
            public readonly List<FaceObstructionInfo> obstructions;
            public readonly List<OpeningViolationInfo> openingViolations;

            public FacadeValidationData(Vector3 normal, bool faceInward,
                List<FaceObstructionInfo> obstructions,
                List<OpeningViolationInfo> openingViolations)
            {
                this.normal = normal;
                this.faceInward = faceInward;
                this.obstructions = obstructions;
                this.openingViolations = openingViolations;
            }
        }

        private static FacadeValidationData ComputeFacadeValidation(FacadeElement facade, List<KitchenElement> allElements)
        {
            var normal = FacadeValidator.GetFaceNormal(facade);
            bool faceInward = FacadeValidator.IsFacingInward(facade);

            var rawObstructions = FacadeValidator.FindFaceObstructions(facade, allElements);
            var obstructions = new List<FaceObstructionInfo>(rawObstructions.Count);
            foreach (var o in rawObstructions)
            {
                obstructions.Add(new FaceObstructionInfo
                {
                    neighbor = o.neighbor,
                    distanceFromFaceMm = o.distanceFromFaceMm,
                    overlapWidthMm = o.overlapWidthMm,
                    overlapHeightMm = o.overlapHeightMm
                });
            }

            var rawOpening = FacadeValidator.FindOpeningViolations(facade, allElements);
            var opening = new List<OpeningViolationInfo>(rawOpening.Count);
            foreach (var v in rawOpening)
            {
                opening.Add(new OpeningViolationInfo
                {
                    neighbor = v.neighbor,
                    openingMode = v.openingMode,
                    collisionAtProgress = v.collisionAtProgress,
                    collisionOverlapMm = v.collisionOverlapMm
                });
            }

            return new FacadeValidationData(normal, faceInward, obstructions, opening);
        }

        private static (object? faceNormal, bool faceInward, object? faceObstructions, object? openingViolations)
            BuildFacadeResponseFields(KitchenElement element)
        {
            if (!(element is FacadeElement facade))
                return (null, false, null, null);

            var data = ComputeFacadeValidation(facade, PartRegistry.GetAll());
            var normal = new { x = data.normal.x, y = data.normal.y, z = data.normal.z };
            return (normal, data.faceInward, data.obstructions, data.openingViolations);
        }

        private McpResponse HandleGetAllElements(McpRequest req)
        {
            var elements = PartRegistry.GetAll();
            // Валидируем сцену ОДИН раз на весь список (иначе Validate звался бы
            // на каждый элемент — O(n²) и заметная пауза кадра на больших сценах).
            var vr = elements != null && elements.Count > 0 ? ConstraintValidator.Validate(elements) : null;
            var list = new List<ElementInfo>();
            if (elements == null) return McpResponse.Error(req.id, -1, "PartRegistry is not initialized");
            foreach (var el in elements)
            {
                if (el == null) continue;
                list.Add(BuildElementInfo(el, elements, false, vr));
            }

            var etag = ComputeEtag(list);

            // Если клиент прислал If-None-Match и ETag совпадает — данные не изменились
            if (req.Headers != null
                && req.Headers.TryGetValue("If-None-Match", out var clientEtag)
                && clientEtag == etag)
            {
                return McpResponse.NotModified(req.id, etag);
            }

            var result = McpResponse.Result(req.id, list);
            result.etag = etag;
            return result;
        }

        /// <summary>SHA256 хеш от JSON-представления списка для ETag. Использует
        /// McpJson (округление до 0.1 мм) — хеш не меняется от суб-миллиметрового дрейфа.</summary>
        private static string ComputeEtag(List<ElementInfo> list)
        {
            var json = McpJson.Serialize(list);
            var bytes = Encoding.UTF8.GetBytes(json);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private McpResponse HandleGetElementInfo(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            return McpResponse.Result(req.id, BuildElementInfo(element, PartRegistry.GetAll(), true));
        }

        /// <summary>Совпадение имени с фильтром: подстрока или wildcard '*', без учёта регистра.</summary>
        private static bool NameMatchesFilter(string name, string filter)
        {
            if (filter.IndexOf('*') < 0)
                return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(filter).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(name, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        /// <summary>Батч-чтение: несколько элементов по именам и/или фильтру одним вызовом.</summary>
        private McpResponse HandleGetElements(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsGetElements>() ?? new ParamsGetElements();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;

            var matched = new List<KitchenElement>();
            var missing = new List<string>();

            if (p.names != null && p.names.Length > 0)
            {
                foreach (var name in p.names)
                {
                    var el = FindElementByName(name);
                    if (el == null) missing.Add(name);
                    else matched.Add(el);
                }
            }

            if (!string.IsNullOrEmpty(p.filter) && all != null)
            {
                foreach (var el in all)
                    if (el != null && !matched.Contains(el) && NameMatchesFilter(el.PartName, p.filter!))
                        matched.Add(el);
            }

            // Ни names, ни filter — весь список (эквивалент get_all_elements).
            if ((p.names == null || p.names.Length == 0) && string.IsNullOrEmpty(p.filter) && all != null)
            {
                foreach (var el in all)
                    if (el != null) matched.Add(el);
            }

            object elements;
            if (p.summary)
            {
                var list = new List<object>();
                foreach (var el in matched)
                {
                    var pos = el.GetComponent<Wall>() is Wall w ? w.FullPosition : el.transform.position;
                    list.Add(new
                    {
                        name = el.PartName,
                        type = el.GetType().Name,
                        posX = pos.x, posY = pos.y, posZ = pos.z,
                        dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                        rotY = el.transform.eulerAngles.y,
                        locked = !el.Movable,
                        hasViolations = vr != null && vr.violations.Contains(el)
                    });
                }
                elements = list;
            }
            else
            {
                var list = new List<ElementInfo>();
                foreach (var el in matched)
                    list.Add(BuildElementInfo(el, all, false, vr));
                elements = list;
            }

            return McpResponse.Result(req.id, new
            {
                count = matched.Count,
                elements,
                missing = missing.Count > 0 ? missing : null
            });
        }

        /// <summary>Состояние элементов батча после (или в dry-run — «как если бы»)
        /// применения: позиция/размер/нарушения на каждый op + счётчик по сцене.</summary>
        private static (List<object> results, int sceneViolationCount) DescribeBatch(
            List<(BatchOp op, KitchenElement el, MaterialDef? material)> resolved)
        {
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var results = new List<object>();
            foreach (var item in resolved)
            {
                var el = item.el;
                var pos = el.transform.position;
                results.Add(new
                {
                    name = el.PartName,
                    posX = pos.x, posY = pos.y, posZ = pos.z,
                    dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                    rotY = el.transform.eulerAngles.y,
                    locked = !el.Movable,
                    violations = BuildElementViolations(el, all, vr)
                });
            }
            return (results, vr != null ? vr.violations.Count : 0);
        }

        /// <summary>Транзакционный батч изменений: либо применяются ВСЕ операции
        /// (одной CompositeCommand = один шаг undo), либо ни одна. dry_run —
        /// применить, посчитать нарушения по каждому op и откатить.</summary>
        private McpResponse HandleBatchEdit(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsBatchEdit>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            // ── Фаза 1: резолв и проверка ВСЕХ операций до каких-либо изменений ──
            var errors = new List<string>();
            var resolved = new List<(BatchOp op, KitchenElement el, MaterialDef? material)>();
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.name)) { errors.Add("an op is missing 'name'"); continue; }
                var el = FindElementByName(op.name);
                if (el == null) { errors.Add($"Element not found: {op.name}"); continue; }

                bool geometry = op.x.HasValue || op.y.HasValue || op.z.HasValue
                    || op.width.HasValue || op.height.HasValue || op.depth.HasValue
                    || op.rot_x.HasValue || op.rot_y.HasValue || op.rot_z.HasValue;

                if (geometry && !el.Movable && op.locked != false)
                {
                    errors.Add($"Element '{op.name}' is LOCKED (unlock requires the user's permission)");
                    continue;
                }
                if (el is DrawerElement && (op.width.HasValue || op.height.HasValue || op.depth.HasValue))
                {
                    errors.Add($"'{op.name}' is a GTV drawer: use set_drawer_properties instead of resizing");
                    continue;
                }

                MaterialDef? mat = null;
                if (!string.IsNullOrEmpty(op.material))
                {
                    mat = ResolveMaterial(op.material!);
                    if (mat == null)
                    {
                        errors.Add($"Unknown material '{op.material}' for '{op.name}' (see list_materials)");
                        continue;
                    }
                }
                resolved.Add((op, el, mat));
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "batch_edit rejected, NOTHING was applied. Fix these ops and retry: " + string.Join(" | ", errors));

            // ── Фаза 2: собрать команды (before-значения — до любых изменений) ──
            var commands = new List<IUndoCommand>();
            foreach (var (op, el, _) in resolved)
            {
                bool hasPos = op.x.HasValue || op.y.HasValue || op.z.HasValue;
                bool hasRot = op.rot_x.HasValue || op.rot_y.HasValue || op.rot_z.HasValue;
                bool hasDims = op.width.HasValue || op.height.HasValue || op.depth.HasValue;
                if (!hasPos && !hasRot && !hasDims) continue;

                var posBefore = el.transform.position;
                var rotBefore = el.transform.rotation;
                var posAfter = ResolveVec(op.x, op.y, op.z, posBefore);
                var rotAfter = hasRot
                    ? Quaternion.Euler(ResolveVec(op.rot_x, op.rot_y, op.rot_z, el.transform.eulerAngles))
                    : rotBefore;

                if (hasDims)
                {
                    var dimsAfter = ResolveDims(op.width, op.height, op.depth, null, null, null, el.DimensionsMM);
                    commands.Add(new ResizeCommand(el, el.DimensionsMM, dimsAfter, posBefore, posAfter, rotBefore, rotAfter));
                }
                else
                {
                    commands.Add(new MoveCommand(el, posBefore, posAfter, rotBefore, rotAfter));
                }
            }

            var composite = new CompositeCommand($"MCP batch edit ({resolved.Count} ops)", commands);

            if (p.dry_run)
            {
                composite.Execute();
                var (dryResults, drySceneCount) = DescribeBatch(resolved);
                composite.Undo();
                return McpResponse.Result(req.id, new
                {
                    ok = true, dryRun = true, applied = false,
                    results = dryResults,
                    sceneViolationCount = drySceneCount
                });
            }

            if (commands.Count > 0)
                CommandStack.Execute(composite);

            // Негеометрические изменения (вне undo-стека, как и set_element_lock/set_material).
            foreach (var (op, el, mat) in resolved)
            {
                if (op.locked.HasValue) el.Movable = !op.locked.Value;
                if (mat != null) MaterialManager.Apply(el, mat);
            }
            RefreshElementHighlights();

            var (results, sceneCount) = DescribeBatch(resolved);
            Debug.Log($"[MCP] Batch edit applied: {resolved.Count} ops, {commands.Count} geometry commands");
            return McpResponse.Result(req.id, new
            {
                ok = true, dryRun = false, applied = true,
                results,
                sceneViolationCount = sceneCount
            });
        }

        /// <summary>Клонирование: count копий со сдвигом offset*N, имена name_2, name_3…
        /// Всё клонирование — один шаг undo (CompositeCommand).</summary>
        private McpResponse HandleCloneElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsCloneElement>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var source = FindElementByName(p.name);
            if (source == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            int count = Mathf.Clamp(p.count <= 0 ? 1 : p.count, 1, 50);
            var offset = new Vector3(p.offset_x, p.offset_y, p.offset_z);
            var basePos = source.transform.position;

            var commands = new List<IUndoCommand>();
            var clones = new List<KitchenElement>();
            int suffix = 2;
            for (int i = 1; i <= count; i++)
            {
                var go = ElementFactory.Duplicate(source);
                if (go == null) return McpResponse.Error(req.id, -1, $"Failed to duplicate '{p.name}'");
                var el = go.GetComponent<KitchenElement>();

                string cloneName;
                do { cloneName = p.name + "_" + suffix; suffix++; }
                while (FindElementByName(cloneName) != null);
                el.PartName = cloneName;
                go.name = cloneName;
                go.transform.position = basePos + offset * i;

                commands.Add(new CreateCommand(go));
                clones.Add(el);
            }
            CommandStack.Execute(new CompositeCommand($"MCP clone {p.name} x{count}", commands));
            RefreshElementHighlights();

            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var created = new List<string>();
            var elements = new List<ElementInfo>();
            foreach (var el in clones)
            {
                created.Add(el.PartName);
                elements.Add(BuildElementInfo(el, all, false, vr));
            }

            Debug.Log($"[MCP] Cloned '{p.name}' x{count}: {string.Join(", ", created)}");
            return McpResponse.Result(req.id, new
            {
                ok = true,
                created,
                elements,
                sceneViolationCount = vr != null ? vr.violations.Count : 0
            });
        }

        // ── Модули (именованные группы деталей) ───────────────────────────

        /// <summary>Модуль по id или имени (без учёта регистра).</summary>
        private static LinkGroup? FindModule(string module)
        {
            if (string.IsNullOrEmpty(module)) return null;
            if (int.TryParse(module, out int id))
            {
                foreach (var g in GroupManager.AllGroups())
                    if (g.id == id) return g;
            }
            foreach (var g in GroupManager.AllGroups())
                if (string.Equals(g.name, module, StringComparison.OrdinalIgnoreCase)) return g;
            return null;
        }

        private static ModuleInfo BuildModuleInfo(LinkGroup g)
        {
            return BuildModuleInfo(g, null);
        }

        private static ModuleInfo BuildModuleInfo(LinkGroup g, List<KitchenElement>? allElements,
            ValidationResult? validation = null)
        {
            var members = GroupManager.MembersOf(g);
            var info = new ModuleInfo
            {
                id = g.id,
                name = g.name,
                movable = g.movable,
                editing = ModuleEditMode.Active == g,
                elementCount = members.Count,
                elements = new List<ElementInfo>()
            };

            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var el in members)
            {
                if (el == null) continue;
                info.elements.Add(BuildElementInfo(el, allElements, false, validation));
                foreach (var v in el.GetVertices())
                {
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                }
            }
            if (info.elements.Count > 0)
            {
                Vector3 c = (min + max) * 0.5f;
                Vector3 s = (max - min) / AppConstants.MM_TO_UNITS;
                info.boundsCenter = new[] { c.x, c.y, c.z };
                info.boundsSizeMM = new[]
                    { Mathf.RoundToInt(s.x), Mathf.RoundToInt(s.y), Mathf.RoundToInt(s.z) };
            }
            return info;
        }

        private McpResponse HandleGetModules(McpRequest req)
        {
            var allElements = PartRegistry.GetAll();
            var vr = allElements != null && allElements.Count > 0 ? ConstraintValidator.Validate(allElements) : null;
            var list = new List<ModuleInfo>();
            foreach (var g in GroupManager.AllGroups())
                list.Add(BuildModuleInfo(g, allElements, vr));
            return McpResponse.Result(req.id, list);
        }

        private McpResponse HandleModuleInfo(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsModule>();
            if (p == null || string.IsNullOrEmpty(p.module))
                return McpResponse.Error(req.id, -32602, "module (id or name) required");
            var g = FindModule(p.module);
            if (g == null) return McpResponse.Error(req.id, -1, $"Module not found: {p.module}");
            return McpResponse.Result(req.id, BuildModuleInfo(g, PartRegistry.GetAll()));
        }

        private McpResponse HandleCreateModule(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsCreateModule>();
            if (p == null || p.members == null || p.members.Length < 2)
                return McpResponse.Error(req.id, -32602, "members: at least 2 board names required");

            var resolved = new List<KitchenElement>();
            var missing = new List<string>();
            foreach (var name in p.members)
            {
                var el = FindElementByName(name);
                if (el == null) missing.Add(name);
                else resolved.Add(el);
            }
            if (missing.Count > 0)
                return McpResponse.Error(req.id, -1, $"Elements not found: {string.Join(", ", missing)}");

            var g = GroupManager.Link(resolved);
            if (g == null) return McpResponse.Error(req.id, -1, "Failed to create module");
            if (!string.IsNullOrEmpty(p.name)) GroupManager.Rename(g, p.name);

            Debug.Log($"[MCP] Module '{g.name}' (id {g.id}) created from {resolved.Count} elements");
            return McpResponse.Result(req.id, BuildModuleInfo(g, PartRegistry.GetAll()));
        }

        private McpResponse HandleDissolveModule(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsModule>();
            if (p == null || string.IsNullOrEmpty(p.module))
                return McpResponse.Error(req.id, -32602, "module required");
            var g = FindModule(p.module);
            if (g == null) return McpResponse.Error(req.id, -1, $"Module not found: {p.module}");
            GroupManager.Unlink(g);
            Debug.Log($"[MCP] Module '{g.name}' dissolved");
            return McpResponse.Result(req.id, new { ok = true, name = g.name });
        }

        private McpResponse HandleAddToModule(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsModuleElement>();
            if (p == null || string.IsNullOrEmpty(p.module) || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "module and name required");
            var g = FindModule(p.module);
            if (g == null) return McpResponse.Error(req.id, -1, $"Module not found: {p.module}");
            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            GroupManager.AddTo(g, el); // через сервис: событие Changed + подвижность группы
            return McpResponse.Result(req.id, BuildModuleInfo(g, PartRegistry.GetAll()));
        }

        private McpResponse HandleRemoveFromModule(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            if (el.GroupId == 0)
                return McpResponse.Error(req.id, -1, $"Element '{p.name}' is not in any module");

            var g = GroupManager.GroupOf(el);
            GroupManager.RemoveFrom(el); // через сервис: событие Changed
            return McpResponse.Result(req.id, g != null
                ? (object)BuildModuleInfo(g, PartRegistry.GetAll())
                : new { ok = true });
        }

        private McpResponse HandleEnterModuleEdit(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsModule>();
            if (p == null || string.IsNullOrEmpty(p.module))
                return McpResponse.Error(req.id, -32602, "module required");
            var g = FindModule(p.module);
            if (g == null) return McpResponse.Error(req.id, -1, $"Module not found: {p.module}");

            ModuleEditMode.Enter(g);
            Debug.Log($"[MCP] Module edit: '{g.name}'");
            return McpResponse.Result(req.id, new { ok = true, editing = g.name });
        }

        private McpResponse HandleExitModuleEdit(McpRequest req)
        {
            bool was = ModuleEditMode.IsActive;
            ModuleEditMode.Exit();
            return McpResponse.Result(req.id, new { ok = true, wasEditing = was });
        }

        /// <summary>"left"/"right"/"bottom"/"top"/"back"/"front" → ось (0/1/2) и сторона.</summary>
        private static bool TryParseFace(string s, out int axis, out bool maxSide)
        {
            axis = 0; maxSide = false;
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "left":   axis = 0; maxSide = false; return true;
                case "right":  axis = 0; maxSide = true;  return true;
                case "bottom": axis = 1; maxSide = false; return true;
                case "top":    axis = 1; maxSide = true;  return true;
                case "back":   axis = 2; maxSide = false; return true;
                case "front":  axis = 2; maxSide = true;  return true;
                default: return false;
            }
        }

        private static float AabbSide(AabbInfo aabb, int axis, bool maxSide)
        {
            if (axis == 0) return maxSide ? aabb.maxX : aabb.minX;
            if (axis == 1) return maxSide ? aabb.maxY : aabb.minY;
            return maxSide ? aabb.maxZ : aabb.minZ;
        }

        /// <summary>Придвинуть грань элемента к грани цели (с зазором gap_mm) —
        /// вся арифметика координат на стороне сервера.</summary>
        private McpResponse HandleAlignElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsAlignElement>();
            if (p == null || string.IsNullOrEmpty(p.name) || string.IsNullOrEmpty(p.target))
                return McpResponse.Error(req.id, -32602, "name and target required");

            if (!TryParseFace(p.face, out int axis, out bool maxSide))
                return McpResponse.Error(req.id, -32602,
                    $"Unknown face '{p.face}'. Valid: left | right | bottom | top | back | front");
            if (!TryParseFace(p.target_face, out int tAxis, out bool tMaxSide))
                return McpResponse.Error(req.id, -32602,
                    $"Unknown target_face '{p.target_face}'. Valid: left | right | bottom | top | back | front");
            if (axis != tAxis)
                return McpResponse.Error(req.id, -32602,
                    $"face '{p.face}' and target_face '{p.target_face}' are on different axes; both must be left/right (X), bottom/top (Y) or back/front (Z)");

            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            var target = FindElementByName(p.target);
            if (target == null) return McpResponse.Error(req.id, -1, $"Target element not found: {p.target}");
            if (element == target) return McpResponse.Error(req.id, -32602, "name and target must differ");
            var lockErr = RequireMovable(element, p.name, req.id);
            if (lockErr != null) return lockErr;

            var elAabb = ComputeAABB(element.GetVertices());
            var tAabb = ComputeAABB(target.GetVertices());
            float myCoord = AabbSide(elAabb, axis, maxSide);
            float targetCoord = AabbSide(tAabb, axis, tMaxSide);
            float gapUnits = p.gap_mm * AppConstants.MM_TO_UNITS;
            // Наша min-грань встаёт НА gap правее целевой координаты, max-грань — левее:
            // left→right = примыкание справа от цели, right→left = слева, и т.д.
            float desired = maxSide ? targetCoord - gapUnits : targetCoord + gapUnits;
            float delta = desired - myCoord;

            var before = element.transform.position;
            var after = before;
            after[axis] += delta;
            var rot = element.transform.rotation;
            CommandStack.Execute(new MoveCommand(element, before, after, rot, rot));
            RefreshElementHighlights();
            Debug.Log($"[MCP] Aligned {p.name}.{p.face} to {p.target}.{p.target_face} gap={p.gap_mm}mm (delta {delta:F4} on axis {axis})");
            return McpResponse.Result(req.id, BuildMutationResult(element));
        }

        /// <summary>Равномерно распределить 3+ детали по оси: крайние стоят,
        /// середина двигается. Один шаг undo.</summary>
        private McpResponse HandleDistributeEvenly(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsDistributeEvenly>();
            if (p == null || p.names == null || p.names.Length < 3)
                return McpResponse.Error(req.id, -32602, "names: at least 3 board names required");
            int axis = p.axis == "x" ? 0 : p.axis == "y" ? 1 : p.axis == "z" ? 2 : -1;
            if (axis < 0)
                return McpResponse.Error(req.id, -32602, $"Unknown axis '{p.axis}'. Valid: x | y | z");

            var resolved = new List<KitchenElement>();
            var errors = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { errors.Add($"Element not found: {name}"); continue; }
                if (!el.Movable) { errors.Add($"Element '{name}' is LOCKED"); continue; }
                resolved.Add(el);
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "distribute_evenly rejected, NOTHING was moved: " + string.Join(" | ", errors));

            resolved.Sort((a, b) => a.transform.position[axis].CompareTo(b.transform.position[axis]));
            float first = resolved[0].transform.position[axis];
            float last = resolved[resolved.Count - 1].transform.position[axis];
            float spacing = (last - first) / (resolved.Count - 1);

            var commands = new List<IUndoCommand>();
            for (int i = 1; i < resolved.Count - 1; i++)
            {
                var el = resolved[i];
                var before = el.transform.position;
                var after = before;
                after[axis] = first + spacing * i;
                var rot = el.transform.rotation;
                commands.Add(new MoveCommand(el, before, after, rot, rot));
            }
            CommandStack.Execute(new CompositeCommand($"MCP distribute {resolved.Count} elements", commands));
            RefreshElementHighlights();

            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var results = new List<object>();
            foreach (var el in resolved)
            {
                var pos = el.transform.position;
                results.Add(new
                {
                    name = el.PartName,
                    posX = pos.x, posY = pos.y, posZ = pos.z,
                    violations = BuildElementViolations(el, all, vr)
                });
            }
            Debug.Log($"[MCP] Distributed {resolved.Count} elements along {p.axis}, spacing {spacing:F4} m");
            return McpResponse.Result(req.id, new
            {
                ok = true,
                axis = p.axis,
                spacingMm = spacing / AppConstants.MM_TO_UNITS,
                results,
                sceneViolationCount = vr != null ? vr.violations.Count : 0
            });
        }

        /// <summary>Свободный параллелепипед между двумя деталями + кто в него уже влез.</summary>
        private McpResponse HandleGetFreeSpace(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsGetFreeSpace>();
            if (p == null || p.between == null || p.between.Length != 2)
                return McpResponse.Error(req.id, -32602, "between: exactly 2 board names required");

            var elA = FindElementByName(p.between[0]);
            if (elA == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.between[0]}");
            var elB = FindElementByName(p.between[1]);
            if (elB == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.between[1]}");

            var a = ComputeAABB(elA.GetVertices());
            var b = ComputeAABB(elB.GetVertices());
            float[] aMin = { a.minX, a.minY, a.minZ }, aMax = { a.maxX, a.maxY, a.maxZ };
            float[] bMin = { b.minX, b.minY, b.minZ }, bMax = { b.maxX, b.maxY, b.maxZ };

            // Ось разделения — наибольший положительный зазор между AABB.
            int sepAxis = -1;
            float bestGap = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            for (int axis = 0; axis < 3; axis++)
            {
                float gap = Mathf.Max(bMin[axis] - aMax[axis], aMin[axis] - bMax[axis]);
                if (gap > bestGap) { bestGap = gap; sepAxis = axis; }
            }
            if (sepAxis < 0)
                return McpResponse.Result(req.id, new
                {
                    free = false,
                    message = "The two boards overlap or touch — there is no free box between them."
                });

            var lo = new float[3];
            var hi = new float[3];
            for (int axis = 0; axis < 3; axis++)
            {
                if (axis == sepAxis)
                {
                    // Между ближними гранями по оси разделения.
                    if (aMax[axis] <= bMin[axis]) { lo[axis] = aMax[axis]; hi[axis] = bMin[axis]; }
                    else { lo[axis] = bMax[axis]; hi[axis] = aMin[axis]; }
                }
                else
                {
                    // Общее «окно» — пересечение проекций.
                    lo[axis] = Mathf.Max(aMin[axis], bMin[axis]);
                    hi[axis] = Mathf.Min(aMax[axis], bMax[axis]);
                    if (hi[axis] <= lo[axis])
                        return McpResponse.Result(req.id, new
                        {
                            free = false,
                            message = $"The boards do not face each other: their projections do not overlap on the {(axis == 0 ? "X" : axis == 1 ? "Y" : "Z")} axis."
                        });
                }
            }

            // Кто уже занимает этот объём.
            var box = new AabbInfo { minX = lo[0], minY = lo[1], minZ = lo[2], maxX = hi[0], maxY = hi[1], maxZ = hi[2] };
            var blockers = new List<object>();
            foreach (var other in PartRegistry.GetAll())
            {
                if (other == null || other == elA || other == elB) continue;
                var o = ComputeAABB(other.GetVertices());
                bool intersects =
                    Tolerance.IntervalsOverlap(o.minX, o.maxX, box.minX, box.maxX) &&
                    Tolerance.IntervalsOverlap(o.minY, o.maxY, box.minY, box.maxY) &&
                    Tolerance.IntervalsOverlap(o.minZ, o.maxZ, box.minZ, box.maxZ);
                if (intersects) blockers.Add(new { name = other.PartName, type = other.GetType().Name });
            }

            float toMm = 1f / AppConstants.MM_TO_UNITS;
            return McpResponse.Result(req.id, new
            {
                free = true,
                separationAxis = sepAxis == 0 ? "x" : sepAxis == 1 ? "y" : "z",
                sizeMmX = Mathf.RoundToInt((hi[0] - lo[0]) * toMm),
                sizeMmY = Mathf.RoundToInt((hi[1] - lo[1]) * toMm),
                sizeMmZ = Mathf.RoundToInt((hi[2] - lo[2]) * toMm),
                minX = lo[0], minY = lo[1], minZ = lo[2],
                maxX = hi[0], maxY = hi[1], maxZ = hi[2],
                centerX = (lo[0] + hi[0]) * 0.5f,
                centerY = (lo[1] + hi[1]) * 0.5f,
                centerZ = (lo[2] + hi[2]) * 0.5f,
                blockers = blockers.Count > 0 ? blockers : null
            });
        }

        private McpResponse HandleMoveElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsMoveElement>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            var lockErr = RequireMovable(element, p.name, req.id);
            if (lockErr != null) return lockErr;

            var before = element.transform.position;
            var rotBefore = element.transform.rotation;
            var after = ResolveVec(p.x, p.y, p.z, before);
            CommandStack.Execute(new MoveCommand(element, before, after, rotBefore, element.transform.rotation));
            RefreshElementHighlights();
            Debug.Log($"[MCP] Moved {element.PartName} to ({after.x}, {after.y}, {after.z})");
            return McpResponse.Result(req.id, BuildMutationResult(element));
        }

        private McpResponse HandleResizeElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsResizeElement>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            var lockErr = RequireMovable(element, p.name, req.id);
            if (lockErr != null) return lockErr;

            // Размеры ящика — производные от его параметров (тип/длина/ширина);
            // прямой resize молча откатился бы в ApplyDimensions.
            if (element is DrawerElement)
                return McpResponse.Error(req.id, -1,
                    $"'{p.name}' is a GTV drawer: use set_drawer_properties (drawer_type/drawer_length/internal_width) instead of resize_element");

            var dimsBefore = element.DimensionsMM;
            var dimsAfter = ResolveDims(p.width, p.height, p.depth, p.dimX, p.dimY, p.dimZ, dimsBefore);
            int w = dimsAfter.x, h = dimsAfter.y, d = dimsAfter.z;

            var posBefore = element.transform.position;
            var rotBefore = element.transform.rotation;

            CommandStack.Execute(new ResizeCommand(element, dimsBefore, dimsAfter,
                posBefore, posBefore, rotBefore, rotBefore));
            RefreshElementHighlights();
            Debug.Log($"[MCP] Resized {element.PartName} to ({w}, {h}, {d})mm");
            return McpResponse.Result(req.id, BuildMutationResult(element));
        }

        private McpResponse HandleRotateElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsRotateElement>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            var lockErr = RequireMovable(element, p.name, req.id);
            if (lockErr != null) return lockErr;

            var before = element.transform.position;
            var rotBefore = element.transform.rotation;
            var euler = ResolveVec(p.x, p.y, p.z, element.transform.eulerAngles);
            var rotAfter = Quaternion.Euler(euler.x, euler.y, euler.z);
            CommandStack.Execute(new MoveCommand(element, before, before, rotBefore, rotAfter));
            RefreshElementHighlights();
            Debug.Log($"[MCP] Rotated {element.PartName} to ({euler.x}, {euler.y}, {euler.z})");
            return McpResponse.Result(req.id, BuildMutationResult(element));
        }

        private McpResponse HandleCreateElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsCreateElement>();
            if (p == null || (string.IsNullOrEmpty(p.name) && string.IsNullOrEmpty(p.template_name)))
                return McpResponse.Error(req.id, -32602, "name required");

            string elementName = string.IsNullOrEmpty(p.name) ? p.template_name : p.name;

            if (p.is_floor)
            {
                var plate = BasePlate.Create();
                plate.Element.PartName = elementName;
                CommandStack.Execute(new CreateCommand(plate.gameObject));
                RefreshElementHighlights();
                Debug.Log($"[MCP] Created floor '{elementName}'");
                return McpResponse.Result(req.id, BuildMutationResult(plate.Element));
            }

            if (p.is_assembled)
            {
                // Сборный (рамочный) фасад строится процедурно через фабрику
                // (тот же путь, что save/load и сайдбар), а не из примитива-куба.
                var dimsA = new Vector3Int(
                    p.width > 0 ? p.width : 450,
                    p.height > 0 ? p.height : 700,
                    p.depth > 0 ? p.depth : 18);
                var posA = new Vector3(p.x, p.y, p.z);
                var fillA = ParseFill(p.fill);
                var goA = ElementFactory.CreateAssembledFacade(dimsA, elementName, posA, fillA);
                CommandStack.Execute(new CreateCommand(goA));
                RefreshElementHighlights();
                var elA = goA.GetComponent<KitchenElement>();
                Debug.Log($"[MCP] Created assembled facade '{elementName}' fill={fillA}");
                return McpResponse.Result(req.id, BuildMutationResult(elA));
            }

            if (p.is_radial_shelf)
            {
                int width = p.width > 0 ? p.width : 600;
                int depthZ = p.depth > 0 ? p.depth : 400;
                int thickness = p.height > 0 ? p.height : AppConstants.BOARD_THICKNESS_DEFAULT;
                int cornerRadius = p.corner_radius > 0 ? p.corner_radius : AppConstants.RADIAL_CORNER_RADIUS_DEFAULT;
                cornerRadius = Mathf.Clamp(cornerRadius, 1, Mathf.Min(width, depthZ));

                var posR = new Vector3(p.x, p.y, p.z);
                var goR = ElementFactory.CreateRadialShelf(width, depthZ, thickness, cornerRadius, elementName, posR);
                CommandStack.Execute(new CreateCommand(goR));
                RefreshElementHighlights();
                var elR = goR.GetComponent<KitchenElement>();
                Debug.Log($"[MCP] Created radial shelf '{elementName}' {width}x{thickness}x{depthZ} cornerRadius={cornerRadius}");
                return McpResponse.Result(req.id, BuildMutationResult(elR));
            }

            if (p.is_drawer)
            {
                var drawerType = ParseDrawerType(p.drawer_type);
                var drawerColor = ParseDrawerColor(p.drawer_color);
                int length = p.drawer_length > 0 ? p.drawer_length : 350;
                int intWidth = p.drawer_internal_width > 0 ? p.drawer_internal_width : 400;
                var posD = new Vector3(p.x, p.y, p.z);
                var goD = ElementFactory.CreateDrawer(drawerType, length, drawerColor, intWidth, elementName, posD);
                CommandStack.Execute(new CreateCommand(goD));
                RefreshElementHighlights();
                var elD = goD.GetComponent<KitchenElement>();
                Debug.Log($"[MCP] Created drawer '{elementName}' type={drawerType} length={length} color={drawerColor}");
                return McpResponse.Result(req.id, BuildMutationResult(elD));
            }

            if (p.is_table)
            {
                var dimsT = new Vector3Int(
                    p.width > 0 ? p.width : 2000,
                    p.height > 0 ? p.height : 750,
                    p.depth > 0 ? p.depth : 1000);
                var posT = new Vector3(p.x, p.y, p.z);
                var goT = ElementFactory.CreateTable(dimsT, elementName, posT);
                var tableEl = goT.GetComponent<TableElement>();
                if (tableEl != null && p.leg_inset_mm > 0)
                    tableEl.LegInsetMM = p.leg_inset_mm;
                CommandStack.Execute(new CreateCommand(goT));
                RefreshElementHighlights();
                var elT = goT.GetComponent<KitchenElement>();
                Debug.Log($"[MCP] Created table '{elementName}' {dimsT.x}x{dimsT.y}x{dimsT.z} legInset={p.leg_inset_mm}");
                return McpResponse.Result(req.id, BuildMutationResult(elT));
            }

			if (p.is_radius_table)
			{
				var dimsRT = new Vector3Int(
					p.width > 0 ? p.width : 2000,
					p.height > 0 ? p.height : 750,
					p.depth > 0 ? p.depth : 1000);
				var posRT = new Vector3(p.x, p.y, p.z);
				var goRT = ElementFactory.CreateRadiusTable(dimsRT, elementName, posRT);
				var radiusTableEl = goRT.GetComponent<RadiusTableElement>();
				if (radiusTableEl != null && p.leg_inset_mm > 0)
					radiusTableEl.LegInsetMM = p.leg_inset_mm;
				CommandStack.Execute(new CreateCommand(goRT));
				RefreshElementHighlights();
				var elRT = goRT.GetComponent<KitchenElement>();
				Debug.Log($"[MCP] Created radius table '{elementName}' {dimsRT.x}x{dimsRT.y}x{dimsRT.z} legInset={p.leg_inset_mm}");
				return McpResponse.Result(req.id, BuildMutationResult(elRT));
			}

			if (p.is_pillar)
			{
				int midH = p.height > 0 ? p.height : PillarElement.MidHeightMM_Default;
				var posP = new Vector3(p.x, p.y, p.z);
				var goP = ElementFactory.CreatePillar(midH, elementName, posP);
				var pillarEl = goP.GetComponent<PillarElement>();
				CommandStack.Execute(new CreateCommand(goP));
				RefreshElementHighlights();
				var elP = goP.GetComponent<KitchenElement>();
				Debug.Log($"[MCP] Created pillar '{elementName}' midHeight={(pillarEl != null ? pillarEl.MidHeightMM : midH)}");
				return McpResponse.Result(req.id, BuildMutationResult(elP));
			}

			if (p.is_window)
            {
                var dimsW = new Vector3Int(
                    p.width > 0 ? p.width : 900,
                    p.height > 0 ? p.height : 1200,
                    p.depth > 0 ? p.depth : 100);
                var posW = new Vector3(p.x, p.y, p.z);
                var tintValue = ParseGlassTint(p.window_tint);
                int sill = p.window_sill_protrusion_mm >= 0 ? p.window_sill_protrusion_mm : 50;
                var goW = ElementFactory.CreateWindow(dimsW, elementName, posW, tintValue, sill);
                CommandStack.Execute(new CreateCommand(goW));
                RefreshElementHighlights();
                var elW = goW.GetComponent<KitchenElement>();
                Debug.Log($"[MCP] Created window '{elementName}' {dimsW.x}x{dimsW.y}x{dimsW.z} tint={tintValue} sill={sill}");
                return McpResponse.Result(req.id, BuildMutationResult(elW));
            }

			if (p.is_door)
            {
                var dimsD = new Vector3Int(
                    p.width > 0 ? p.width : 900,
                    p.height > 0 ? p.height : 2000,
                    p.depth > 0 ? p.depth : 100);
                var posD = new Vector3(p.x, p.y, p.z);
                var sashType = ParseDoorSashType(p.door_sash_type);
                var goD = ElementFactory.CreateDoor(dimsD, elementName, posD, sashType);
                CommandStack.Execute(new CreateCommand(goD));
                RefreshElementHighlights();
                var elD = goD.GetComponent<KitchenElement>();
                Debug.Log($"[MCP] Created door '{elementName}' {dimsD.x}x{dimsD.y}x{dimsD.z} sashType={sashType}");
                return McpResponse.Result(req.id, BuildMutationResult(elD));
            }

            var pos = new Vector3(p.x, p.y, p.z);
            var dims = new Vector3Int(
                p.width > 0 ? p.width : 800,
                p.height > 0 ? p.height : 400,
                p.depth > 0 ? p.depth : 18);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = elementName;
            KitchenElement element;

            if (p.is_facade)
            {
                var facade = go.AddComponent<FacadeElement>();
                facade.PartName = elementName;
                facade.DimensionsMM = dims;
                facade.GapLeft = p.gapLeft;
                facade.GapRight = p.gapRight;
                facade.GapTop = p.gapTop;
                facade.GapBottom = p.gapBottom;
                MaterialManager.ApplyById(facade, MaterialCatalog.DefaultId);
                element = facade;
            }
            else
            {
                element = go.AddComponent<KitchenElement>();
                element.PartName = elementName;
                element.DimensionsMM = dims;
                MaterialManager.ApplyById(element, MaterialCatalog.DefaultId);
            }

            if (p.is_wall)
                go.AddComponent<Wall>();

            go.transform.position = pos;
            CommandStack.Execute(new CreateCommand(go));
            RefreshElementHighlights();
            Debug.Log($"[MCP] Created {go.name} at ({p.x}, {p.y}, {p.z})");
            return McpResponse.Result(req.id, BuildMutationResult(element));
        }

        /// <summary>Строка → тип заполнения сборного фасада. По умолчанию Blind.</summary>
        private static AssembledFill ParseFill(string s)
        {
            if (string.IsNullOrEmpty(s)) return AssembledFill.Blind;
            switch (s.Trim().ToLowerInvariant())
            {
                case "glass": case "стекло": return AssembledFill.Glass;
                case "open": case "empty": case "витрина": return AssembledFill.Open;
                default: return AssembledFill.Blind; // blind / панель / глухой
            }
        }

        /// <summary>Строка → DrawerType. По умолчанию A.</summary>
        private static DrawerType ParseDrawerType(string s)
        {
            switch ((s ?? "").Trim().ToUpperInvariant())
            {
                case "B": return DrawerType.B;
                case "C": return DrawerType.C;
                case "D": return DrawerType.D;
                default: return DrawerType.A;
            }
        }

        /// <summary>Строка → DrawerColor. По умолчанию Anthracite.</summary>
        private static DrawerColor ParseDrawerColor(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "white": case "белый": return DrawerColor.White;
                case "black": case "чёрный": case "черный": return DrawerColor.Black;
                default: return DrawerColor.Anthracite;
            }
        }

        /// <summary>Строка → GlassTint. По умолчанию Clear.</summary>
        private static GlassTint ParseGlassTint(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "tinted": case "тонированное": return GlassTint.Tinted;
                default: return GlassTint.Clear;
            }
        }

        /// <summary>Строка → DoorSashType. По умолчанию Glass.</summary>
        private static DoorSashType ParseDoorSashType(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "blind": case "глухое": case "глухая": return DoorSashType.Blind;
                default: return DoorSashType.Glass;
            }
        }

        /// <summary>Строка → целевой тип для конвертации элемента.</summary>
        private static bool TryParseTarget(string s, out ElementConverter.TargetType target)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "part": case "board": case "деталь":
                    target = ElementConverter.TargetType.Part; return true;
                case "facade": case "дверца": case "фасад":
                    target = ElementConverter.TargetType.Facade; return true;
                case "assembled_facade": case "assembled": case "assembledfacade": case "сборный":
                    target = ElementConverter.TargetType.AssembledFacade; return true;
                case "radial_shelf": case "radial": case "radialshelf": case "радиусная": case "полка":
                    target = ElementConverter.TargetType.RadialShelf; return true;
                default:
                    target = ElementConverter.TargetType.Part; return false;
            }
        }

        /// <summary>Сменить ТИП существующего элемента: деталь ↔ фасад ↔ сборный
        /// фасад, сохранив имя/размеры/позицию/материал (см. <see cref="ElementConverter"/>).
        /// Для target=assembled_facade опциональный fill (blind|glass|open).</summary>
        private McpResponse HandleConvertElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsConvertElement>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            if (!TryParseTarget(p.target, out var target))
                return McpResponse.Error(req.id, -32602,
                    $"Unknown target '{p.target}'. Valid: part | facade | assembled_facade | radial_shelf");

            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            var lockErr = RequireMovable(element, p.name, req.id);
            if (lockErr != null) return lockErr;

            var converted = ElementConverter.Convert(element, target);
            if (converted is AssembledFacadeElement assembled && !string.IsNullOrEmpty(p.fill))
                assembled.Fill = ParseFill(p.fill);
            RefreshElementHighlights();
            Debug.Log($"[MCP] Converted '{p.name}' → {target}");
            return McpResponse.Result(req.id, BuildMutationResult(converted));
        }

        private McpResponse HandleDeleteElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            var lockErr = RequireMovable(element, p.name, req.id);
            if (lockErr != null) return lockErr;
            CommandStack.Execute(new DeleteCommand(element.gameObject));
            RefreshElementHighlights();
            Debug.Log($"[MCP] Deleted {p.name}");
            var allAfterDelete = PartRegistry.GetAll();
            var vrAfterDelete = allAfterDelete != null && allAfterDelete.Count > 0
                ? ConstraintValidator.Validate(allAfterDelete) : null;
            return McpResponse.Result(req.id, new
            {
                ok = true,
                name = p.name,
                sceneViolationCount = vrAfterDelete != null ? vrAfterDelete.violations.Count : 0
            });
        }

        private McpResponse HandleUndo(McpRequest req)
        {
            if (!CommandStack.CanUndo)
                return McpResponse.Result(req.id, new { ok = false, reason = "Nothing to undo" });
            var desc = CommandStack.PeekUndoDescription();
            CommandStack.Undo();
            RefreshElementHighlights();
            Debug.Log($"[MCP] Undo: {desc}");
            return McpResponse.Result(req.id, new { ok = true, action = "undo", description = desc });
        }

        private McpResponse HandleRedo(McpRequest req)
        {
            if (!CommandStack.CanRedo)
                return McpResponse.Result(req.id, new { ok = false, reason = "Nothing to redo" });
            CommandStack.Redo();
            RefreshElementHighlights();
            Debug.Log($"[MCP] Redo");
            return McpResponse.Result(req.id, new { ok = true, action = "redo" });
        }

        private McpResponse HandleGetSpecification(McpRequest req)
        {
            var spec = SpecificationManager.Build(PartRegistry.GetAll());
            var lines = spec.lines.Select(l => new SpecLineInfo
            {
                name = l.name,
                dimX = l.dimensionsMM.x, dimY = l.dimensionsMM.y, dimZ = l.dimensionsMM.z,
                count = l.count, areaPerBoardM2 = l.areaPerBoardM2, totalAreaM2 = l.totalAreaM2
            }).ToList();

            return McpResponse.Result(req.id, new SpecInfo
            {
                lines = lines, totalCount = spec.totalCount, totalAreaM2 = spec.totalAreaM2
            });
        }

        private McpResponse HandleExportCsv(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsExportCsv>();
            if (p == null || string.IsNullOrEmpty(p.path))
                return McpResponse.Error(req.id, -32602, "path required");
            var spec = SpecificationManager.Build(PartRegistry.GetAll());
            SpecificationExport.SaveToFile(spec, p.path);
            return McpResponse.Result(req.id, new { ok = true, path = p.path });
        }

        private McpResponse HandleSelectElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

#if UNITY_EDITOR
            Selection.activeGameObject = element.gameObject;
#endif
            var sel = Object.FindAnyObjectByType<SelectionManager>();
            if (sel != null) sel.Select(element);
            return McpResponse.Result(req.id, new { ok = true, name = p.name });
        }

        private McpResponse HandleUndoStackInfo(McpRequest req)
        {
            return McpResponse.Result(req.id, new UndoStackInfo
            {
                canUndo = CommandStack.CanUndo,
                canRedo = CommandStack.CanRedo,
                undoDescription = CommandStack.PeekUndoDescription()
            });
        }

        private McpResponse HandleConsoleLogs(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsLogCount>();
            int count = (p != null && p.count > 0) ? Mathf.Min(p.count, 200) : 50;
            var entries = ConsoleLogCapture.GetRecent(count);
            return McpResponse.Result(req.id, entries);
        }

        private McpResponse HandleGetSettings(McpRequest req)
        {
            var s = KitchenSettings.Instance;
            if (s == null) return McpResponse.Error(req.id, -1, "KitchenSettings not loaded");
            return McpResponse.Result(req.id, new
            {
                snapEnabled = s.SnapEnabled,
                snapThresholdMM = s.SnapThreshold,
                gridEnabled = s.GridEnabled,
                gridStepMM = s.GridStep,
                blockOnViolation = s.BlockOnViolation,
                autoSave = s.AutoSave,
                autoSaveIntervalSec = s.AutoSaveInterval,
                snapVerboseLog = SnapSystem.VerboseLog,
                cameraPanFree = s.CameraPanFree
            });
        }

        private McpResponse HandleSetSetting(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetSetting>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name and value required");

            var s = KitchenSettings.Instance;
            if (s == null) return McpResponse.Error(req.id, -1, "KitchenSettings not loaded");

            switch (p.name.ToLowerInvariant())
            {
                case "lower_near_walls": s.LowerNearWalls = p.value; break;
                case "snap_enabled": s.SnapEnabled = p.value; break;
                case "grid_enabled": s.GridEnabled = p.value; break;
                case "walls_enabled": s.WallsEnabled = p.value; break;
                case "camera_pan_free": s.CameraPanFree = p.value; break;
                default:
                    return McpResponse.Error(req.id, -32602, $"Unknown setting: {p.name}");
            }

            Debug.Log($"[MCP] Setting '{p.name}' = {p.value}");
            return McpResponse.Result(req.id, new { ok = true, name = p.name, value = p.value });
        }

        private McpResponse HandleSetSnapVerbose(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetEnabled>();
            if (p == null) return McpResponse.Error(req.id, -32602, "enabled required");
            SnapSystem.VerboseLog = p.enabled;
            Debug.Log($"[MCP] Snap verbose log: {p.enabled}");
            return McpResponse.Result(req.id, new { ok = true, enabled = p.enabled });
        }

        /// <summary>Разбор прилипания: почему деталь (не) прилипает из текущей или
        /// заданной позиции — по каждому соседу лучшая пара граней и причина.</summary>
        private McpResponse HandleSnapDiagnose(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSnapDiagnose>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var pos = element.transform.position;
            if (p.x.HasValue) pos.x = p.x.Value;
            if (p.y.HasValue) pos.y = p.y.Value;
            if (p.z.HasValue) pos.z = p.z.Value;

            var diagnosis = SnapSystem.Diagnose(element, PartRegistry.GetAll(), pos);
            return McpResponse.Result(req.id, diagnosis);
        }

        private McpResponse HandleTakeScreenshot(McpRequest req)
        {
            var path = System.IO.Path.Combine(Application.temporaryCachePath, "mcp_screenshot.png");
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            var bytes = ImageConversion.EncodeToPNG(tex);
            System.IO.File.WriteAllBytes(path, bytes);
            Object.Destroy(tex);
            return McpResponse.Result(req.id, new { ok = true, path });
        }

        private McpResponse HandleGetViolations(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsGetViolations>();
            HashSet<string>? nameFilter = null;
            if (p?.names != null && p.names.Length > 0)
                nameFilter = new HashSet<string>(p.names, StringComparer.OrdinalIgnoreCase);
            bool Wanted(KitchenElement e) => nameFilter == null || nameFilter.Contains(e.PartName);

            var all = PartRegistry.GetAll();
            if (all == null || all.Count == 0)
                return McpResponse.Result(req.id, new { violations = new string[0], count = 0 });

            var result = ConstraintValidator.Validate(all);
            var facadeIssues = ComputeFacadeViolations(all);
            var drawerIssues = ComputeDrawerViolations(all);

            var list = new List<object>();
            var seen = new HashSet<KitchenElement>();

            foreach (var el in result.violations)
            {
                seen.Add(el);
                if (!Wanted(el)) continue;
                var overlaps = ComputeViolationOverlaps(el, all);
                var facadeFields = BuildFacadeViolationFields(el, all);
                list.Add(new {
                    name = el.PartName,
                    type = el.GetType().Name,
                    overlapsWith = overlaps,
                    disconnected = overlaps.Count == 0,
                    facadeFields.faceNormal,
                    facadeFields.faceInward,
                    facadeFields.faceObstructions,
                    facadeFields.openingViolations
                });
            }

            foreach (var kvp in facadeIssues)
            {
                var el = kvp.Key;
                if (seen.Contains(el)) continue;
                seen.Add(el);
                if (!Wanted(el)) continue;
                list.Add(new {
                    name = el.PartName,
                    type = el.GetType().Name,
                    overlapsWith = new List<object>(),
                    disconnected = false,
                    kvp.Value.faceNormal,
                    kvp.Value.faceInward,
                    kvp.Value.faceObstructions,
                    kvp.Value.openingViolations
                });
            }

            foreach (var kvp in drawerIssues)
            {
                var el = kvp.Key;
                if (seen.Contains(el)) continue;
                seen.Add(el);
                if (!Wanted(el)) continue;
                list.Add(new {
                    name = el.PartName,
                    type = el.GetType().Name,
                    overlapsWith = new List<object>(),
                    disconnected = false,
                    faceNormal = (object?)null,
                    faceInward = false,
                    faceObstructions = (object?)null,
                    openingViolations = (object?)null,
                    drawerValidationErrors = kvp.Value
                });
            }

            return McpResponse.Result(req.id, new { violations = list, count = list.Count });
        }

        private static Dictionary<KitchenElement, (object? faceNormal, bool faceInward, object? faceObstructions, object? openingViolations)>
            ComputeFacadeViolations(List<KitchenElement> all)
        {
            var result = new Dictionary<KitchenElement, (object?, bool, object?, object?)>();
            foreach (var el in all)
            {
                if (!(el is FacadeElement facade)) continue;
                var fields = BuildFacadeResponseFields(facade);
                bool hasIssue = fields.faceInward ||
                    (fields.faceObstructions != null && ((List<FaceObstructionInfo>)fields.faceObstructions).Count > 0) ||
                    (fields.openingViolations != null && ((List<OpeningViolationInfo>)fields.openingViolations).Count > 0);
                if (hasIssue)
                    result[el] = fields;
            }
            return result;
        }

        private static Dictionary<KitchenElement, List<string>>
            ComputeDrawerViolations(List<KitchenElement> all)
        {
            var result = new Dictionary<KitchenElement, List<string>>();
            foreach (var el in all)
            {
                if (!(el is DrawerElement drawer)) continue;
                var validation = DrawerValidator.ValidateAll(drawer, all);
                if (!validation.IsValid)
                    result[el] = validation.Errors;
            }
            return result;
        }

        private static (object? faceNormal, bool faceInward, object? faceObstructions, object? openingViolations)
            BuildFacadeViolationFields(KitchenElement el, List<KitchenElement> all)
        {
            if (!(el is FacadeElement facade)) return (null, false, null, null);
            return BuildFacadeResponseFields(facade);
        }

        private static List<object> ComputeViolationOverlaps(KitchenElement el, List<KitchenElement> all)
        {
            var elAabb = ComputeAABB(el.GetVertices());
            var results = new List<object>();
            foreach (var other in all)
            {
                if (other == el || other == null) continue;
                var otherAabb = ComputeAABB(other.GetVertices());
                if (!AABBsOverlap(elAabb, otherAabb)) continue;

                float overlapX = Mathf.Min(elAabb.maxX, otherAabb.maxX) - Mathf.Max(elAabb.minX, otherAabb.minX);
                float overlapY = Mathf.Min(elAabb.maxY, otherAabb.maxY) - Mathf.Max(elAabb.minY, otherAabb.minY);
                float overlapZ = Mathf.Min(elAabb.maxZ, otherAabb.maxZ) - Mathf.Max(elAabb.minZ, otherAabb.minZ);
                float toMm = 1f / AppConstants.MM_TO_UNITS;
                // Глубина проникновения = минимальная из трёх протяжённостей
                // пересечения; по ней агент отличает «касание» от «вдавлено на 18 мм».
                float depthMm = Mathf.Min(overlapX, Mathf.Min(overlapY, overlapZ)) * toMm;
                if (Tolerance.IsNoiseMm(depthMm)) continue; // float-шум вплотную стоящих деталей

                results.Add(new {
                    kind = "overlap",
                    neighbor = other.PartName,
                    severity = ClassifyOverlapMm(depthMm),
                    penetrationMm = Mathf.Round(depthMm * 10f) / 10f,
                    overlapXmm = Mathf.Round(overlapX * toMm * 10f) / 10f,
                    overlapYmm = Mathf.Round(overlapY * toMm * 10f) / 10f,
                    overlapZmm = Mathf.Round(overlapZ * toMm * 10f) / 10f
                });
            }
            return results;
        }

        private static BasePlate? FindFloor()
        {
            var go = GameObject.FindWithTag("Floor");
            if (go == null) return null;
            return go.GetComponent<BasePlate>();
        }

        private McpResponse HandleGetFloorInfo(McpRequest req)
        {
            var plate = FindFloor();
            if (plate == null || plate.Element == null)
                return McpResponse.Error(req.id, -1, "Floor not found");

            var el = plate.Element;
            var dims = el.DimensionsMM;
            var pos = el.transform.position;
            return McpResponse.Result(req.id, new
            {
                name = el.PartName,
                dimX = dims.x, dimY = dims.y, dimZ = dims.z,
                posX = pos.x, posY = pos.y, posZ = pos.z,
                hasViolations = HasViolations(el)
            });
        }

        private McpResponse HandleResizeFloor(McpRequest req)
        {
            var plate = FindFloor();
            if (plate == null || plate.Element == null)
                return McpResponse.Error(req.id, -1, "Floor not found");

            var p = req.Params?.ToObject<ParamsResizeFloor>();
            if (p == null)
                return McpResponse.Error(req.id, -32602, "invalid parameters");

            var el = plate.Element;
            var dimsBefore = el.DimensionsMM;
            var posBefore = el.transform.position;
            var rotBefore = el.transform.rotation;

            var dimsAfter = ResolveDims(p.width, p.height, p.depth, null, null, null, dimsBefore);
            int w = dimsAfter.x, h = dimsAfter.y, d = dimsAfter.z;

            CommandStack.Execute(new ResizeCommand(el,
                dimsBefore, dimsAfter,
                posBefore, posBefore,
                rotBefore, rotBefore));
            RefreshElementHighlights();

            Debug.Log($"[MCP] Resized floor to ({w}, {h}, {d})mm");
            return McpResponse.Result(req.id, BuildMutationResult(el));
        }

        private McpResponse HandleAddWallComponent(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            if (element.GetComponent<Wall>() != null)
                return McpResponse.Result(req.id, new { ok = true, name = p.name, was_already_wall = true });

            element.gameObject.AddComponent<Wall>();
            Debug.Log($"[MCP] Added Wall component to {p.name}");
            // Стена — якорь связности: превращение может «вылечить» соседей,
            // поэтому возвращаем полный конверт с пересчитанными нарушениями.
            return McpResponse.Result(req.id, BuildMutationResult(element));
        }

        private McpResponse HandleExecuteMenuItem(McpRequest req)
        {
#if UNITY_EDITOR
            var p = req.Params?.ToObject<ParamsMenuPath>();
            if (p == null || string.IsNullOrEmpty(p.menu_path))
                return McpResponse.Error(req.id, -32602, "menu_path required");
            var result = EditorApplication.ExecuteMenuItem(p.menu_path);
            return McpResponse.Result(req.id, new { ok = result, menu_path = p.menu_path });
#else
            return McpResponse.Error(req.id, -1, "Not available in standalone build");
#endif
        }

        private McpResponse HandleEnterPlayMode(McpRequest req)
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = true;
            return McpResponse.Result(req.id, new { ok = true, isPlaying = true });
#else
            return McpResponse.Error(req.id, -1, "Not available in standalone build");
#endif
        }

        private McpResponse HandleExitPlayMode(McpRequest req)
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
            return McpResponse.Result(req.id, new { ok = true, isPlaying = false });
#else
            return McpResponse.Error(req.id, -1, "Not available in standalone build");
#endif
        }

        // ── get_element_debug ───────────────────────────────────────────
        private McpResponse HandleGetElementDebug(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var verts = el.GetVertices();
            var aabb = ComputeAABB(verts);

            var srcFaces = el.GetFaces();
            var faces = new FaceInfo[srcFaces.Length];
            for (int i = 0; i < srcFaces.Length; i++)
            {
                faces[i] = new FaceInfo
                {
                    centerX = srcFaces[i].center.x, centerY = srcFaces[i].center.y, centerZ = srcFaces[i].center.z,
                    normalX = srcFaces[i].normal.x, normalY = srcFaces[i].normal.y, normalZ = srcFaces[i].normal.z,
                    sizeX = srcFaces[i].size.x, sizeY = srcFaces[i].size.y
                };
            }

            var vertices = new VertexInfo[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                vertices[i] = new VertexInfo { x = verts[i].x, y = verts[i].y, z = verts[i].z };

            var effDim = GetEffectiveDimMM(el);
            return McpResponse.Result(req.id, new ElementDebugInfo
            {
                name = el.PartName, type = el.GetType().Name,
                aabb = aabb, faces = faces, vertices = vertices,
                dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                effectiveDimX = effDim.x, effectiveDimY = effDim.y, effectiveDimZ = effDim.z
            });
        }

        // ── get_element_gaps ─────────────────────────────────────────────
        private McpResponse HandleGetElementGaps(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var all = PartRegistry.GetAll();
            var gaps = all != null ? ComputeAxisGaps(el, all) : new List<AxisGapInfo>();
            return McpResponse.Result(req.id, new ElementGapsResult { name = el.PartName, gaps = gaps });
        }

        // ── simulate_move ───────────────────────────────────────────────
        /// <summary>
        /// Dry-run move: не меняет позицию, возвращает simulatedAABB,
        /// overlapsWith, faceGaps и wouldHaveViolations.
        /// Вызывай ПЕРЕД move_element для проверки.
        /// </summary>
        private McpResponse HandleSimulateMove(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSimulateMove>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var all = PartRegistry.GetAll();
            var target = ResolveVec(p.x, p.y, p.z, el.transform.position);
            var result = SimulateMoveAt(el, target, all);
            return McpResponse.Result(req.id, result);
        }

        // ── simulate_resize ──────────────────────────────────────────────
        /// <summary>
        /// Dry-run resize: не меняет размеры, возвращает simulatedAABB,
        /// overlapsWith, faceGaps и wouldHaveViolations.
        /// Вызывай ПЕРЕД resize_element для проверки.
        /// </summary>
        private McpResponse HandleSimulateResize(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSimulateResize>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var dims = ResolveDims(p.width, p.height, p.depth, p.dimX, p.dimY, p.dimZ, el.DimensionsMM);

            var all = PartRegistry.GetAll();
            var result = SimulateResizeTo(el, dims, all);
            return McpResponse.Result(req.id, result);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>Единая проверка блокировки для мутирующих команд: null — можно
        /// менять; иначе готовый Error с единым текстом (один источник сообщения для
        /// модели во всех move/resize/rotate/delete).</summary>
        private static McpResponse? RequireMovable(KitchenElement element, string name, string reqId)
        {
            if (element.Movable) return null;
            return McpResponse.Error(reqId, -1,
                $"Element '{name}' is LOCKED, so move/resize/delete are rejected. " +
                "Unlock it with set_element_lock {locked:false} — but ONLY if the user explicitly allowed editing this element.");
        }

        /// <summary>Обновить подсветку (зелёная/красная) после мутации. Все пути мутации
        /// (MCP, UI, drag, undo/redo) должны вызывать это, иначе визуал устаревает.</summary>
        private static void RefreshElementHighlights()
        {
            var hl = Object.FindAnyObjectByType<ElementHighlighter>();
            if (hl != null)
                hl.RefreshHighlights();
        }

        /// <summary>Есть ли у элемента нарушения (пересечение / нет связности) в текущей сцене.</summary>
        private static bool HasViolations(KitchenElement element)
        {
            var all = PartRegistry.GetAll();
            if (all == null || all.Count == 0) return false;
            var vr = ConstraintValidator.Validate(all);
            return vr != null && vr.violations.Contains(element);
        }

        /// <summary>Нарушения ИМЕННО этого элемента: пересечения (kind=overlap, с severity),
        /// оторванность от структуры, фасадные проблемы, ошибки ящика.
        /// Пустой список = элемент чист.</summary>
        private static List<object> BuildElementViolations(KitchenElement el, List<KitchenElement>? all, ValidationResult? vr)
        {
            var list = new List<object>();
            if (all == null || all.Count == 0) return list;

            var overlaps = ComputeViolationOverlaps(el, all);
            foreach (var o in overlaps) list.Add(o);

            if (vr != null && vr.violations.Contains(el) && overlaps.Count == 0)
                list.Add(new
                {
                    kind = "disconnected",
                    message = "Element is not face-to-face connected to the wall/floor structure."
                });

            if (el is FacadeElement facade)
            {
                var data = ComputeFacadeValidation(facade, all);
                if (data.faceInward)
                    list.Add(new
                    {
                        kind = "facade_facing_inward",
                        message = "The facade's front face points INTO the cabinet. Rotate it 180 degrees."
                    });
                foreach (var o in data.obstructions)
                    list.Add(new
                    {
                        kind = "face_obstruction",
                        neighbor = o.neighbor,
                        distanceFromFaceMm = o.distanceFromFaceMm,
                        overlapWidthMm = o.overlapWidthMm,
                        overlapHeightMm = o.overlapHeightMm
                    });
                foreach (var v in data.openingViolations)
                    list.Add(new
                    {
                        kind = "opening_collision",
                        neighbor = v.neighbor,
                        openingMode = v.openingMode,
                        collisionAtProgress = v.collisionAtProgress,
                        collisionOverlapMm = v.collisionOverlapMm
                    });
            }

            if (el is DrawerElement drawer)
            {
                var validation = DrawerValidator.ValidateAll(drawer, all);
                if (!validation.IsValid)
                    foreach (var err in validation.Errors)
                        list.Add(new { kind = "drawer_invalid", message = err });
            }

            return list;
        }

        /// <summary>Единый конверт ответа ВСЕХ мутаций: ok + полный ElementInfo +
        /// нарушения этого элемента + счётчик структурных нарушений по сцене.
        /// Модель видит результат и проблемы сразу, без второго запроса.</summary>
        private static object BuildMutationResult(KitchenElement el)
        {
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            return new
            {
                ok = true,
                element = BuildElementInfo(el, all, includeFacadeValidation: false, validation: vr),
                violations = BuildElementViolations(el, all, vr),
                // Структурные (пересечение/оторванность) нарушения по ВСЕЙ сцене.
                // Если счётчик вырос после мутации — задета чужая деталь: get_violations.
                sceneViolationCount = vr != null ? vr.violations.Count : 0
            };
        }

        private static AabbInfo ComputeAABB(Vector3[] vertices)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var v in vertices)
            {
                if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
                if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
            }
            return new AabbInfo { minX = minX, minY = minY, minZ = minZ, maxX = maxX, maxY = maxY, maxZ = maxZ };
        }

        private static bool AABBsOverlap(AabbInfo a, AabbInfo b)
        {
            return a.minX < b.maxX && a.maxX > b.minX &&
                   a.minY < b.maxY && a.maxY > b.minY &&
                   a.minZ < b.maxZ && a.maxZ > b.minZ;
        }

        private static Vector3Int GetEffectiveDimMM(KitchenElement el)
        {
            var facade = el as FacadeElement;
            if (facade != null)
            {
                return new Vector3Int(
                    el.DimensionsMM.x + facade.GapLeft + facade.GapRight,
                    el.DimensionsMM.y + facade.GapTop + facade.GapBottom,
                    el.DimensionsMM.z);
            }
            return el.DimensionsMM;
        }

        /// <summary>Глубина пересечения (мм) → категория серьёзности для агента.</summary>
        private static string ClassifyOverlapMm(float mm) =>
            Tolerance.IsNoiseMm(mm) ? "touching"
            : mm < 2f ? "minor_overlap"
            : mm < 10f ? "overlap"
            : "deep_penetration";

        /// <summary>Проекции AABB на две оси, КРОМЕ указанной, пересекаются (с допуском).
        /// Без этого «ближайшим по Y» может оказаться деталь из другого угла сцены.</summary>
        private static bool ProjectionsOverlapExceptAxis(AabbInfo a, AabbInfo b, int axis)
        {
            if (axis != 0 && !Tolerance.IntervalsOverlap(a.minX, a.maxX, b.minX, b.maxX)) return false;
            if (axis != 1 && !Tolerance.IntervalsOverlap(a.minY, a.maxY, b.minY, b.maxY)) return false;
            if (axis != 2 && !Tolerance.IntervalsOverlap(a.minZ, a.maxZ, b.minZ, b.maxZ)) return false;
            return true;
        }

        private static List<AxisGapInfo> ComputeAxisGaps(KitchenElement element, List<KitchenElement> allElements)
        {
            var elAabb = ComputeAABB(element.GetVertices());
            var gaps = new List<AxisGapInfo>();
            string[] axisNames = { "x", "y", "z" };
            float[] aMin = { elAabb.minX, elAabb.minY, elAabb.minZ };
            float[] aMax = { elAabb.maxX, elAabb.maxY, elAabb.maxZ };

            // AABB соседей считаем один раз, а не по разу на каждую ось.
            var others = new List<(KitchenElement el, AabbInfo aabb)>(allElements.Count);
            foreach (var other in allElements)
                if (other != null && other != element)
                    others.Add((other, ComputeAABB(other.GetVertices())));

            for (int axis = 0; axis < 3; axis++)
            {
                float bestGapUnits = float.MaxValue;
                string? bestNeighbor = null;
                float am = aMin[axis], ax = aMax[axis];

                foreach (var (other, oAabb) in others)
                {
                    // Сосед по оси осмыслен только при пересечении проекций
                    // на две другие оси (реально «напротив», а не где-то в сцене).
                    if (!ProjectionsOverlapExceptAxis(elAabb, oAabb, axis)) continue;

                    float bMin = 0, bMax = 0;
                    if (axis == 0) { bMin = oAabb.minX; bMax = oAabb.maxX; }
                    else if (axis == 1) { bMin = oAabb.minY; bMax = oAabb.maxY; }
                    else { bMin = oAabb.minZ; bMax = oAabb.maxZ; }

                    float gap;
                    if (ax <= bMin) gap = bMin - ax;
                    else if (bMax <= am) gap = am - bMax;
                    else gap = -(Mathf.Min(ax, bMax) - Mathf.Max(am, bMin));

                    if (Mathf.Abs(gap) < Mathf.Abs(bestGapUnits))
                    {
                        bestGapUnits = gap;
                        bestNeighbor = other.PartName;
                    }
                }

                // Нет соседа напротив по этой оси — запись не пишем вовсе
                // (раньше писался фиктивный gapMM: 0 с пустым neighbor).
                if (bestNeighbor == null) continue;

                float gapMM = bestGapUnits / AppConstants.MM_TO_UNITS;
                bool touching = Tolerance.IsNoiseMm(gapMM);
                gaps.Add(new AxisGapInfo
                {
                    axis = axisNames[axis],
                    neighbor = bestNeighbor,
                    gapMM = touching ? 0f : gapMM,
                    touching = touching,
                    isOverlap = !touching && gapMM < 0
                });
            }
            return gaps;
        }

        private static SimulateResult SimulateMoveAt(KitchenElement element, Vector3 testPos, List<KitchenElement> allElements)
        {
            var oldPos = element.transform.position;
            var oldRot = element.transform.rotation;
            var currentAabb = ComputeAABB(element.GetVertices());

            try
            {
                element.transform.position = testPos;
                var simAabb = ComputeAABB(element.GetVertices());

                var overlaps = new List<string>();
                if (allElements != null)
                {
                    foreach (var other in allElements)
                    {
                        if (other == element || other == null) continue;
                        if (AABBsOverlap(simAabb, ComputeAABB(other.GetVertices())))
                            overlaps.Add(other.PartName);
                    }
                }

                var gaps = allElements != null ? ComputeAxisGaps(element, allElements) : new List<AxisGapInfo>();

                bool wouldViolate = false;
                if (allElements != null && allElements.Count > 0)
                {
                    var vr = ConstraintValidator.Validate(allElements);
                    wouldViolate = vr != null && vr.violations.Contains(element);
                }

                return new SimulateResult
                {
                    name = element.PartName,
                    currentAABB = currentAabb,
                    simulatedAABB = simAabb,
                    overlapsWith = overlaps,
                    faceGaps = gaps,
                    wouldHaveViolations = wouldViolate
                };
            }
            finally
            {
                element.transform.position = oldPos;
                element.transform.rotation = oldRot;
            }
        }

        private static SimulateResult SimulateResizeTo(KitchenElement element, Vector3Int testDims, List<KitchenElement> allElements)
        {
            var oldDims = element.DimensionsMM;
            var oldPos = element.transform.position;
            var oldRot = element.transform.rotation;
            var currentAabb = ComputeAABB(element.GetVertices());

            try
            {
                element.DimensionsMM = testDims;
                var simAabb = ComputeAABB(element.GetVertices());

                var overlaps = new List<string>();
                if (allElements != null)
                {
                    foreach (var other in allElements)
                    {
                        if (other == element || other == null) continue;
                        if (AABBsOverlap(simAabb, ComputeAABB(other.GetVertices())))
                            overlaps.Add(other.PartName);
                    }
                }

                var gaps = allElements != null ? ComputeAxisGaps(element, allElements) : new List<AxisGapInfo>();

                bool wouldViolate = false;
                if (allElements != null && allElements.Count > 0)
                {
                    var vr = ConstraintValidator.Validate(allElements);
                    wouldViolate = vr != null && vr.violations.Contains(element);
                }

                return new SimulateResult
                {
                    name = element.PartName,
                    currentAABB = currentAabb,
                    simulatedAABB = simAabb,
                    overlapsWith = overlaps,
                    faceGaps = gaps,
                    wouldHaveViolations = wouldViolate
                };
            }
            finally
            {
                element.DimensionsMM = oldDims;
                element.transform.position = oldPos;
                element.transform.rotation = oldRot;
            }
        }

        // ── set_element_lock ────────────────────────────────────────────
        private McpResponse HandleSetElementLock(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsElementLock>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            el.Movable = !p.locked;
            Debug.Log($"[MCP] Element '{p.name}' lock set to {p.locked} (Movable={!p.locked})");
            return McpResponse.Result(req.id, BuildMutationResult(el));
        }

        private McpResponse HandleSetFacadeMode(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetFacadeMode>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            if (string.IsNullOrEmpty(p.mode))
                return McpResponse.Error(req.id, -32602, "mode required");

            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var facade = el as FacadeElement;
            if (facade == null) return McpResponse.Error(req.id, -1, $"Element '{p.name}' is not a facade");

            switch (p.mode.ToLowerInvariant())
            {
                case "front_left":     facade.Mode = DoorMode.HingeFrontLeft; break;
                case "front_right":    facade.Mode = DoorMode.HingeFrontRight; break;
                case "front_top":      facade.Mode = DoorMode.HingeFrontTop; break;
                case "front_bottom":   facade.Mode = DoorMode.HingeFrontBottom; break;
                case "back_left":      facade.Mode = DoorMode.HingeBackLeft; break;
                case "back_right":     facade.Mode = DoorMode.HingeBackRight; break;
                case "back_top":       facade.Mode = DoorMode.HingeBackTop; break;
                case "back_bottom":    facade.Mode = DoorMode.HingeBackBottom; break;
                case "edge_top_left":  facade.Mode = DoorMode.HingeEdgeTopLeft; break;
                case "edge_top_right": facade.Mode = DoorMode.HingeEdgeTopRight; break;
                case "edge_bottom_left":  facade.Mode = DoorMode.HingeEdgeBottomLeft; break;
                case "edge_bottom_right": facade.Mode = DoorMode.HingeEdgeBottomRight; break;
                case "drawer_out":     facade.Mode = DoorMode.DrawerOut; break;
                case "drawer_in":      facade.Mode = DoorMode.DrawerIn; break;
                case "drawer_right":   facade.Mode = DoorMode.DrawerRight; break;
                case "drawer_left":    facade.Mode = DoorMode.DrawerLeft; break;
                case "drawer_up":      facade.Mode = DoorMode.DrawerUp; break;
                case "drawer_down":    facade.Mode = DoorMode.DrawerDown; break;
                default:
                    return McpResponse.Error(req.id, -32602, $"Unknown mode: '{p.mode}'. Valid: front_left, front_right, front_top, front_bottom, back_left, back_right, back_top, back_bottom, edge_top_left, edge_top_right, edge_bottom_left, edge_bottom_right, drawer_out, drawer_in, drawer_right, drawer_left, drawer_up, drawer_down");
            }

            Debug.Log($"[MCP] Facade '{p.name}' mode set to {p.mode}");
            return McpResponse.Result(req.id, BuildMutationResult(facade));
        }

        // ── Drawer operations ─────────────────────────────────────────────

        private McpResponse HandleSetDrawerProperties(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetDrawerProperties>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");

            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var drawer = el as DrawerElement;
            if (drawer == null) return McpResponse.Error(req.id, -1, $"Element '{p.name}' is not a drawer");

            if (!string.IsNullOrEmpty(p.drawer_type))
                drawer.Type = ParseDrawerType(p.drawer_type);
            if (p.drawer_length.HasValue)
                drawer.NominalLength = p.drawer_length.Value;
            if (!string.IsNullOrEmpty(p.drawer_color))
                drawer.Color = ParseDrawerColor(p.drawer_color);
            if (p.internal_width.HasValue)
                drawer.InternalWidth = p.internal_width.Value;
            if (p.is_double.HasValue)
                drawer.IsDouble = p.is_double.Value;
            if (p.is_upper.HasValue)
                drawer.IsUpperDrawer = p.is_upper.Value;
            if (p.paired_drawer_name != null)
            {
                // Пустая строка отвязывает; непустая обязана указывать на живой ящик.
                if (p.paired_drawer_name != "" && !(FindElementByName(p.paired_drawer_name) is DrawerElement))
                    return McpResponse.Error(req.id, -1, $"Paired drawer not found: {p.paired_drawer_name}");
                drawer.PairedDrawerName = p.paired_drawer_name;
            }
            if (p.attached_facade_name != null)
            {
                if (p.attached_facade_name != "" && !(FindElementByName(p.attached_facade_name) is FacadeElement))
                    return McpResponse.Error(req.id, -1, $"Facade not found: {p.attached_facade_name}");
                drawer.AttachedFacadeName = p.attached_facade_name;
            }

            Debug.Log($"[MCP] Drawer '{p.name}' properties updated");
            return McpResponse.Result(req.id, BuildMutationResult(drawer));
        }

        private McpResponse HandleSetRadialShelfProperties(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetRadialShelfProperties>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");

            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var shelf = el as RadialShelfElement;
            if (shelf == null)
                return McpResponse.Error(req.id, -1, $"Element '{p.name}' is not a radial shelf");

            if (p.corner_radius.HasValue)
                shelf.CornerRadius = p.corner_radius.Value;

            Debug.Log($"[MCP] Radial shelf '{p.name}' cornerRadius={shelf.CornerRadius}");
            return McpResponse.Result(req.id, BuildMutationResult(el));
        }

		private McpResponse HandleSetTableProperties(McpRequest req)
		{
			var p = req.Params?.ToObject<ParamsSetTableProperties>();
			if (p == null || string.IsNullOrEmpty(p.name))
				return McpResponse.Error(req.id, -32602, "name required");

			var el = FindElementByName(p.name);
			if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

			var table = el as TableElement;
			var rt = el as RadiusTableElement;
			if (table == null && rt == null)
				return McpResponse.Error(req.id, -1, $"Element '{p.name}' is not a table");

			if (p.leg_inset_mm.HasValue)
			{
				if (table != null) table.LegInsetMM = p.leg_inset_mm.Value;
				else rt!.LegInsetMM = p.leg_inset_mm.Value;
			}

			if (p.tabletop_material_id != null)
			{
				var def = MaterialCatalog.Get(p.tabletop_material_id);
				if (def != null)
				{
					if (table != null) MaterialManager.ApplyTabletop(table, def);
					else MaterialManager.ApplyTabletop(rt!, def);
				}
			}

			if (p.legs_material_id != null)
			{
				var def = MaterialCatalog.Get(p.legs_material_id);
				if (def != null)
				{
					if (table != null) MaterialManager.ApplyLegs(table, def);
					else MaterialManager.ApplyLegs(rt!, def);
				}
			}

			Debug.Log($"[MCP] Table '{p.name}' properties updated");
			return McpResponse.Result(req.id, BuildMutationResult(el));
		}

		private McpResponse HandleSetPillarProperties(McpRequest req)
		{
			var p = req.Params?.ToObject<ParamsSetPillarProperties>();
			if (p == null || string.IsNullOrEmpty(p.name))
				return McpResponse.Error(req.id, -32602, "name required");

			var el = FindElementByName(p.name);
			if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

			PillarElement? pillar = el as PillarElement;
			if (pillar == null)
				return McpResponse.Error(req.id, -1, $"Element '{p.name}' is not a pillar");

			if (p.mid_height_mm.HasValue)
				pillar.MidHeightMM = p.mid_height_mm.Value;

			Debug.Log($"[MCP] Pillar '{p.name}' midHeight={pillar.MidHeightMM}");
			return McpResponse.Result(req.id, BuildMutationResult(el));
		}

		private McpResponse HandleSetWindowProperties(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetWindowProperties>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");

            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var window = el as WindowElement;
            if (window == null)
                return McpResponse.Error(req.id, -1, $"Element '{p.name}' is not a window");

            if (!string.IsNullOrEmpty(p.tint))
                window.Tint = ParseGlassTint(p.tint);
            if (p.sill_protrusion_mm.HasValue)
                window.SillProtrusionMM = p.sill_protrusion_mm.Value;
            if (!string.IsNullOrEmpty(p.mode))
            {
                switch (p.mode.ToLowerInvariant())
                {
                    case "front_left": window.Mode = DoorMode.HingeFrontLeft; break;
                    case "front_right": window.Mode = DoorMode.HingeFrontRight; break;
                    case "front_top": window.Mode = DoorMode.HingeFrontTop; break;
                    case "front_bottom": window.Mode = DoorMode.HingeFrontBottom; break;
                }
            }
            if (p.is_open.HasValue)
                window.SetOpen(p.is_open.Value);

            Debug.Log($"[MCP] Window '{p.name}' properties updated");
            return McpResponse.Result(req.id, BuildMutationResult(window));
        }

		private McpResponse HandleSetDoorProperties(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetDoorProperties>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");

            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var door = el as DoorElement;
            if (door == null)
                return McpResponse.Error(req.id, -1, $"Element '{p.name}' is not a door");

            if (!string.IsNullOrEmpty(p.sash_type))
                door.SashType = ParseDoorSashType(p.sash_type);
            if (!string.IsNullOrEmpty(p.mode))
            {
                switch (p.mode.ToLowerInvariant())
                {
                    case "front_left": door.Mode = DoorMode.HingeFrontLeft; break;
                    case "front_right": door.Mode = DoorMode.HingeFrontRight; break;
                    case "front_top": door.Mode = DoorMode.HingeFrontTop; break;
                    case "front_bottom": door.Mode = DoorMode.HingeFrontBottom; break;
                }
            }
            if (p.is_open.HasValue)
                door.SetOpen(p.is_open.Value);

            Debug.Log($"[MCP] Door '{p.name}' properties updated");
            return McpResponse.Result(req.id, BuildMutationResult(door));
        }

        private McpResponse HandleCycleDrawerAnimation(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");

            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var drawer = el as DrawerElement;
            if (drawer == null) return McpResponse.Error(req.id, -1, $"Element '{p.name}' is not a drawer");

            // Одиночный ящик — обычный toggle; трёхфазный цикл имеет смысл
            // только для двойного (Closed → BothOpen → LowerOnly → Closed).
            if (drawer.IsDouble)
                drawer.CycleDoubleState();
            else
                drawer.ToggleOpen();
            Debug.Log($"[MCP] Drawer '{p.name}' cycled: isOpen={drawer.IsOpen} doubleState={drawer.DoubleState}");
            // Анимация не меняет геометрию сцены — конверт мутаций не нужен,
            // текущее состояние (isOpen/doubleState) есть в element.drawer.
            return McpResponse.Result(req.id, new { ok = true, name = p.name, isDouble = drawer.IsDouble,
                isOpen = drawer.IsOpen, doubleState = drawer.DoubleState.ToString() });
        }

        // ── Материалы / текстуры ────────────────────────────────────────

        /// <summary>Декор по id ИЛИ отображаемому имени (без учёта регистра).
        /// Возвращает null для неизвестного — чтобы явно ошибиться, а не молча
        /// подставить дефолт (в отличие от MaterialCatalog.Get).</summary>
        private static MaterialDef? ResolveMaterial(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var d in MaterialCatalog.All)
                if (string.Equals(d.id, key, StringComparison.OrdinalIgnoreCase)) return d;
            foreach (var d in MaterialCatalog.All)
                if (string.Equals(d.displayName, key, StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }

        private McpResponse HandleSetMaterial(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetMaterial>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            if (string.IsNullOrEmpty(p.material))
                return McpResponse.Error(req.id, -32602, "material required (id or display name from list_materials)");

            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var def = ResolveMaterial(p.material);
            if (def == null)
                return McpResponse.Error(req.id, -1,
                    $"Unknown material '{p.material}'. Call list_materials for the valid ids/names.");

            MaterialManager.Apply(el, def);
            RefreshElementHighlights();
            // Если элемент выделен — обновить подсветку выделения поверх нового декора.
            var sel = Object.FindAnyObjectByType<SelectionManager>();
            if (sel != null) sel.RefreshHighlight(el);

            Debug.Log($"[MCP] Material of '{p.name}' set to {def.id} ({def.displayName})");
            // Материал не меняет геометрию — лёгкий ответ вместо полного конверта.
            return McpResponse.Result(req.id, new
            {
                ok = true, element = p.name,
                materialId = def.id, material = def.displayName,
                hasTexture = !string.IsNullOrEmpty(def.baseMapResource)
            });
        }

        private McpResponse HandleListMaterials(McpRequest req)
        {
            var list = new List<object>();
            foreach (var m in MaterialCatalog.All)
                list.Add(new
                {
                    id = m.id,
                    name = m.displayName,
                    kind = m.kind,
                    hasTexture = m.texture != null || !string.IsNullOrEmpty(m.baseMapResource),
                    tileWidthMM = m.tileSizeMM,
                    tileHeightMM = m.TileHeightMM
                });
            return McpResponse.Result(req.id, new { materials = list, defaultId = MaterialCatalog.DefaultId });
        }

        /// <summary>Пере-сканировать внешнюю папку текстур (добавили файлы — обновить
        /// без перезапуска). Пере-применяет декоры на всех элементах + подсветку.</summary>
        private McpResponse HandleReloadTextures(McpRequest req)
        {
            int n = ExternalTextureCatalog.LoadAll();

            // Кэш материалов сброшен внутри LoadAll — пере-вешаем декор каждого элемента
            // по его текущему materialId, затем обновляем валидационную подсветку.
            var all = PartRegistry.GetAll();
            if (all != null)
                foreach (var el in all)
                    if (el != null) MaterialManager.ApplyById(el, el.MaterialId);
            RefreshElementHighlights();

            Debug.Log($"[MCP] reload_textures: {n} decors from {ExternalTextureCatalog.DirectoryPath}");
            return McpResponse.Result(req.id, new
            {
                ok = true,
                loaded = n,
                directory = ExternalTextureCatalog.DirectoryPath,
                totalMaterials = MaterialCatalog.All.Count
            });
        }

        private static GameObject? FindGameObject(string path)
        {
            if (path.Contains("/"))
            {
                var parts = path.Split('/');
                var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in roots)
                {
                    if (root.name == parts[0])
                    {
                        var found = FindDescendant(root.transform, parts, 1);
                        if (found != null) return found;
                    }
                }
            }
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene.name == null) continue;
                if (go.name == path) return go;
            }
            return null;
        }

        private static GameObject? FindDescendant(Transform parent, string[] parts, int index)
        {
            if (index >= parts.Length) return parent.gameObject;
            foreach (Transform child in parent)
                if (child.name == parts[index])
                    return FindDescendant(child, parts, index + 1);
            return null;
        }

        // ── rename_element ────────────────────────────────────────────
        private McpResponse HandleRenameElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsRenameElement>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            if (string.IsNullOrEmpty(p.new_name))
                return McpResponse.Error(req.id, -32602, "new_name required");

            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            if (p.new_name == p.name)
                return McpResponse.Result(req.id, new { ok = true, name = p.name, message = "Name unchanged (same as current)" });

            var conflict = PartRegistry.All.FirstOrDefault(x => x != null && x != el && x.PartName == p.new_name);
            if (conflict != null)
                return McpResponse.Error(req.id, -1, $"Name '{p.new_name}' is already taken by another element");

            var oldName = el.PartName;
            el.PartName = p.new_name;
            el.gameObject.name = p.new_name;

            Debug.Log($"[MCP] Element renamed: '{oldName}' -> '{p.new_name}'");
            return McpResponse.Result(req.id, new { ok = true, old_name = oldName, new_name = p.new_name });
        }

        private static KitchenElement? FindElementByName(string name)
        {
            foreach (var el in PartRegistry.All)
                if (el != null && (el.PartName == name || el.name == name))
                    return el;
            return null;
        }
    }
}
