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
                    case "get_specification": return HandleGetSpecification(request);
                    case "export_specification_csv": return HandleExportCsv(request);
                    case "select_elements": return HandleSelectElements(request);
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
                    case "set_setting": return HandleSetSetting(request);
                    case "execute_menu_item": return HandleExecuteMenuItem(request);
                    case "enter_play_mode": return HandleEnterPlayMode(request);
                    case "exit_play_mode": return HandleExitPlayMode(request);
                    case "get_element_debug": return HandleGetElementDebug(request);
                    case "get_element_gaps": return HandleGetElementGaps(request);
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
            var p = req.Params?.ToObject<ParamsObjectPaths>();
            if (p == null || p.object_paths == null || p.object_paths.Length == 0)
                return McpResponse.Error(req.id, -32602, "object_paths required (non-empty array)");

            var results = new List<object>();
            var missing = new List<string>();
            foreach (var path in p.object_paths)
            {
                var target = FindGameObject(path);
                if (target == null) { missing.Add(path); continue; }

                var components = new List<string>();
                foreach (var c in target.GetComponents<Component>())
                    if (c != null) components.Add(c.GetType().Name);

                var children = new List<string>();
                foreach (Transform child in target.transform)
                    children.Add(child.name);

                results.Add(new ObjectInfo
                {
                    name = target.name, path = GetGameObjectPath(target),
                    posX = target.transform.position.x, posY = target.transform.position.y, posZ = target.transform.position.z,
                    rotX = target.transform.eulerAngles.x, rotY = target.transform.eulerAngles.y, rotZ = target.transform.eulerAngles.z,
                    scaleX = target.transform.localScale.x, scaleY = target.transform.localScale.y, scaleZ = target.transform.localScale.z,
                    active = target.activeInHierarchy, components = components, children = children
                });
            }
            return McpResponse.Result(req.id, new { count = results.Count, results, missing = missing.Count > 0 ? missing : null });
        }

        private McpResponse HandleSetActive(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetActiveOps>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var results = new List<object>();
            var errors = new List<string>();
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.object_path)) { errors.Add("op missing object_path"); continue; }
                var go = FindGameObject(op.object_path);
                if (go == null) { errors.Add($"Object not found: {op.object_path}"); continue; }
                go.SetActive(op.active);
                results.Add(new { object_path = op.object_path, ok = true, active = op.active });
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "set_active rejected: " + string.Join(" | ", errors));

            return McpResponse.Result(req.id, new { ok = true, results });
        }

        private McpResponse HandleDeleteObject(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsObjectPaths>();
            if (p == null || p.object_paths == null || p.object_paths.Length == 0)
                return McpResponse.Error(req.id, -32602, "object_paths required (non-empty array)");

            var resolved = new List<(string path, string name, GameObject go)>();
            var errors = new List<string>();
            foreach (var path in p.object_paths)
            {
                var go = FindGameObject(path);
                if (go == null) { errors.Add($"Object not found: {path}"); continue; }
                resolved.Add((path, go.name, go));
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "delete_object rejected, NOTHING was deleted: " + string.Join(" | ", errors));

            var deleted = new List<object>();
            foreach (var item in resolved)
            {
                Object.Destroy(item.go);
                deleted.Add(new { object_path = item.path, name = item.name });
            }
            return McpResponse.Result(req.id, new { ok = true, deleted });
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
            var p = req.Params?.ToObject<ParamsTransformOps>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var results = new List<object>();
            var errors = new List<string>();
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.object_path)) { errors.Add("op missing object_path"); continue; }
                var go = FindGameObject(op.object_path);
                if (go == null) { errors.Add($"Object not found: {op.object_path}"); continue; }
                var v = ResolveVec(op.x, op.y, op.z, go.transform.position);
                go.transform.position = v;
                results.Add(new { object_path = op.object_path, ok = true, position = new { x = v.x, y = v.y, z = v.z } });
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "set_position rejected: " + string.Join(" | ", errors));

            return McpResponse.Result(req.id, new { ok = true, results });
        }

        private McpResponse HandleSetRotation(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsTransformOps>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var results = new List<object>();
            var errors = new List<string>();
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.object_path)) { errors.Add("op missing object_path"); continue; }
                var go = FindGameObject(op.object_path);
                if (go == null) { errors.Add($"Object not found: {op.object_path}"); continue; }
                var v = ResolveVec(op.x, op.y, op.z, go.transform.eulerAngles);
                go.transform.eulerAngles = v;
                results.Add(new { object_path = op.object_path, ok = true, rotation = new { x = v.x, y = v.y, z = v.z } });
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "set_rotation rejected: " + string.Join(" | ", errors));

            return McpResponse.Result(req.id, new { ok = true, results });
        }

        private McpResponse HandleSetScale(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsTransformOps>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var results = new List<object>();
            var errors = new List<string>();
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.object_path)) { errors.Add("op missing object_path"); continue; }
                var go = FindGameObject(op.object_path);
                if (go == null) { errors.Add($"Object not found: {op.object_path}"); continue; }
                var v = ResolveVec(op.x, op.y, op.z, go.transform.localScale);
                go.transform.localScale = v;
                results.Add(new { object_path = op.object_path, ok = true, scale = new { x = v.x, y = v.y, z = v.z } });
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "set_scale rejected: " + string.Join(" | ", errors));

            return McpResponse.Result(req.id, new { ok = true, results });
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
                    drawerColor = WireName(drawer.Color),
                    internalWidth = drawer.InternalWidth,
                    isDouble = drawer.IsDouble,
                    isUpper = drawer.IsUpperDrawer,
                    pairedDrawerName = drawer.PairedDrawerName,
                    attachedFacadeName = drawer.AttachedFacadeName,
                    doubleState = WireName(drawer.DoubleState),
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
                    tint = WireName(window.Tint),
                    sillProtrusionMM = window.SillProtrusionMM,
                    mode = FacadeDoor.WireName(window.Mode),
                    isOpen = window.IsOpen,
                    attachedWallName = window.AttachedWallName
                } : null,
                door = door != null ? new DoorInfo
                {
                    sashType = WireName(door.SashType),
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
            List<(EditOp op, KitchenElement el, MaterialDef? material, List<string> warnings)> resolved)
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
        /// <summary>THE universal editor: change ANY user-editable properties of one or MANY
        /// elements in ONE transactional call. dry_run simulates (apply → violations → revert).</summary>
        private McpResponse HandleEditElements(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsEditElements>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var errors = new List<string>();
            var resolved = new List<(EditOp op, KitchenElement el, MaterialDef? material, List<string> warnings)>();
            var newNamesBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.name)) { errors.Add("an op is missing 'name'"); continue; }
                var el = FindElementByName(op.name);
                if (el == null) { errors.Add($"Element not found: {op.name}"); continue; }
                bool geometry = op.x.HasValue || op.y.HasValue || op.z.HasValue
                    || op.width.HasValue || op.height.HasValue || op.depth.HasValue
                    || op.rot_x.HasValue || op.rot_y.HasValue || op.rot_z.HasValue;
                if (geometry && !el.Movable && op.locked != false)
                { errors.Add($"Element '{op.name}' is LOCKED"); continue; }
                foreach (var err in ValidateEditOpFields(op, el))
                    errors.Add($"Invalid field for '{op.name}': {err}");
                // Validate new_name
                if (op.new_name != null && op.new_name != op.name)
                {
                    var conflict = PartRegistry.All.FirstOrDefault(x => x != null && x != el && x.PartName == op.new_name);
                    if (conflict != null) errors.Add($"new_name '{op.new_name}' already taken by another element (for '{op.name}')");
                    else if (!newNamesBatch.Add(op.new_name)) errors.Add($"Duplicate new_name '{op.new_name}' in this batch (for '{op.name}')");
                }
                MaterialDef? mat = null;
                if (!string.IsNullOrEmpty(op.material))
                {
                    mat = ResolveMaterial(op.material!);
                    if (mat == null) errors.Add($"Unknown material '{op.material}' for '{op.name}' (see list_materials)");
                }
                resolved.Add((op, el, mat, new List<string>()));
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "edit_elements rejected, NOTHING was applied: " + string.Join(" | ", errors));

            var commands = new List<IUndoCommand>();
            foreach (var (op, el, _, _) in resolved)
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
                else commands.Add(new MoveCommand(el, posBefore, posAfter, rotBefore, rotAfter));
            }
            var composite = new CompositeCommand($"MCP edit_elements ({resolved.Count} ops)", commands);
            if (p.dry_run)
            {
                composite.Execute();
                ApplyNonGeometryEdits(resolved);
                var (dryResults, drySceneCount) = DescribeBatch(resolved);
                composite.Undo();
                return McpResponse.Result(req.id, new { ok = true, dryRun = true, applied = false, results = dryResults, sceneViolationCount = drySceneCount });
            }
            if (commands.Count > 0) CommandStack.Execute(composite);
            ApplyNonGeometryEdits(resolved);
            RefreshElementHighlights();
            var (results, sceneCount) = DescribeBatch(resolved);
            Debug.Log($"[MCP] edit_elements: {resolved.Count} ops, {commands.Count} geometry");
            return McpResponse.Result(req.id, new { ok = true, dryRun = false, applied = true, results, sceneViolationCount = sceneCount });
        }

        private void ApplyNonGeometryEdits(
            List<(EditOp op, KitchenElement el, MaterialDef? material, List<string> warnings)> resolved)
        {
            foreach (var (op, el, mat, _) in resolved)
            {
                if (op.locked.HasValue) el.Movable = !op.locked.Value;
                if (mat != null) MaterialManager.Apply(el, mat);
                if (el is FacadeElement facade && !(el is AssembledFacadeElement)) ApplyFacadeEdits(op, facade);
                if (el is AssembledFacadeElement asmFacade) ApplyAssembledEdits(op, asmFacade);
                if (el is RadialShelfElement shelf) ApplyRadialShelfEdits(op, shelf);
                if (el is DrawerElement drawer) ApplyDrawerEdits(op, drawer);
                if (el is TableElement table) ApplyTableEdits(op, table);
                if (el is RadiusTableElement rt) ApplyRadiusTableEdits(op, rt);
                if (el is PillarElement pillar) ApplyPillarEdits(op, pillar);
                if (el is WindowElement window) ApplyWindowEdits(op, window);
                if (el is DoorElement door) ApplyDoorEdits(op, door);
                if (op.new_name != null && op.new_name != el.PartName) { el.PartName = op.new_name; el.gameObject.name = op.new_name; }
            }
        }

        private static List<string> ValidateEditOpFields(EditOp op, KitchenElement el)
        {
            var e = new List<string>();
            bool IsNot<T>() => !(el is T);
            bool IsFacadeLike() => el is FacadeElement || el is WindowElement || el is DoorElement;
            if (IsNot<FacadeElement>() && IsNot<AssembledFacadeElement>()) { if (op.gap_left.HasValue) e.Add("gap_left"); if (op.gap_right.HasValue) e.Add("gap_right"); if (op.gap_top.HasValue) e.Add("gap_top"); if (op.gap_bottom.HasValue) e.Add("gap_bottom"); if (op.fill != null) e.Add("fill"); }
            if (!IsFacadeLike()) { if (op.mode != null) e.Add("mode"); }
            if (IsNot<FacadeElement>() && IsNot<WindowElement>() && IsNot<DoorElement>()) { if (op.is_open.HasValue) e.Add("is_open"); }
            if (IsNot<RadialShelfElement>()) { if (op.corner_radius.HasValue) e.Add("corner_radius"); }
            if (IsNot<DrawerElement>()) { if (op.drawer_type != null) e.Add("drawer_type"); if (op.drawer_length.HasValue) e.Add("drawer_length"); if (op.drawer_color != null) e.Add("drawer_color"); if (op.internal_width.HasValue) e.Add("internal_width"); if (op.is_double.HasValue) e.Add("is_double"); if (op.is_upper.HasValue) e.Add("is_upper"); if (op.paired_drawer_name != null) e.Add("paired_drawer_name"); if (op.attached_facade_name != null) e.Add("attached_facade_name"); }
            if (IsNot<TableElement>() && IsNot<RadiusTableElement>()) { if (op.leg_inset_mm.HasValue) e.Add("leg_inset_mm"); if (op.tabletop_material != null) e.Add("tabletop_material"); if (op.legs_material != null) e.Add("legs_material"); }
            if (IsNot<PillarElement>()) { if (op.mid_height_mm.HasValue) e.Add("mid_height_mm"); }
            if (IsNot<WindowElement>()) { if (op.tint != null) e.Add("tint"); if (op.sill_protrusion_mm.HasValue) e.Add("sill_protrusion_mm"); }
            if (IsNot<DoorElement>()) { if (op.sash_type != null) e.Add("sash_type"); }
            if (el is DrawerElement && (op.width.HasValue || op.height.HasValue || op.depth.HasValue)) e.Add("width/height/depth not settable on drawers (size is parametric)");
            return e;
        }

        private static void ApplyFacadeEdits(EditOp op, FacadeElement facade)
        {
            if (op.gap_left.HasValue) facade.GapLeft = op.gap_left.Value;
            if (op.gap_right.HasValue) facade.GapRight = op.gap_right.Value;
            if (op.gap_top.HasValue) facade.GapTop = op.gap_top.Value;
            if (op.gap_bottom.HasValue) facade.GapBottom = op.gap_bottom.Value;
            if (op.mode != null && TryParseDoorMode(op.mode, out var m)) facade.Mode = m;
            if (op.is_open.HasValue) facade.SetOpen(op.is_open.Value);
        }
        private static void ApplyAssembledEdits(EditOp op, AssembledFacadeElement asm) { ApplyFacadeEdits(op, asm); if (op.fill != null) asm.Fill = ParseFill(op.fill); }
        private static void ApplyRadialShelfEdits(EditOp op, RadialShelfElement shelf) { if (op.corner_radius.HasValue) shelf.CornerRadius = op.corner_radius.Value; }
        private static void ApplyDrawerEdits(EditOp op, DrawerElement drawer)
        {
            if (op.drawer_type != null) drawer.Type = ParseDrawerType(op.drawer_type);
            if (op.drawer_length.HasValue) drawer.NominalLength = op.drawer_length.Value;
            if (op.drawer_color != null) drawer.Color = ParseDrawerColor(op.drawer_color);
            if (op.internal_width.HasValue) drawer.InternalWidth = op.internal_width.Value;
            if (op.is_double.HasValue) drawer.IsDouble = op.is_double.Value;
            if (op.is_upper.HasValue) drawer.IsUpperDrawer = op.is_upper.Value;
            if (op.paired_drawer_name != null) drawer.PairedDrawerName = op.paired_drawer_name == "" ? "" : op.paired_drawer_name;
            if (op.attached_facade_name != null) drawer.AttachedFacadeName = op.attached_facade_name == "" ? "" : op.attached_facade_name;
        }
        private static void ApplyTableEdits(EditOp op, TableElement table)
        {
            if (op.leg_inset_mm.HasValue) table.LegInsetMM = op.leg_inset_mm.Value;
            if (op.tabletop_material != null) { var d = ResolveMaterial(op.tabletop_material); if (d != null) MaterialManager.ApplyTabletop(table, d); }
            if (op.legs_material != null) { var d = ResolveMaterial(op.legs_material); if (d != null) MaterialManager.ApplyLegs(table, d); }
        }
        private static void ApplyRadiusTableEdits(EditOp op, RadiusTableElement rt)
        {
            if (op.leg_inset_mm.HasValue) rt.LegInsetMM = op.leg_inset_mm.Value;
            if (op.tabletop_material != null) { var d = ResolveMaterial(op.tabletop_material); if (d != null) MaterialManager.ApplyTabletop(rt, d); }
            if (op.legs_material != null) { var d = ResolveMaterial(op.legs_material); if (d != null) MaterialManager.ApplyLegs(rt, d); }
        }
        private static void ApplyPillarEdits(EditOp op, PillarElement pillar) { if (op.mid_height_mm.HasValue) pillar.MidHeightMM = op.mid_height_mm.Value; }
        private static void ApplyWindowEdits(EditOp op, WindowElement window)
        {
            if (op.tint != null) window.Tint = ParseGlassTint(op.tint);
            if (op.sill_protrusion_mm.HasValue) window.SillProtrusionMM = op.sill_protrusion_mm.Value;
            if (op.mode != null && TryParseDoorMode(op.mode, out var m)) window.Mode = m;
            if (op.is_open.HasValue) window.SetOpen(op.is_open.Value);
        }
        private static void ApplyDoorEdits(EditOp op, DoorElement door)
        {
            if (op.sash_type != null) door.SashType = ParseDoorSashType(op.sash_type);
            if (op.mode != null && TryParseDoorMode(op.mode, out var m)) door.Mode = m;
            if (op.is_open.HasValue) door.SetOpen(op.is_open.Value);
        }

        private static bool TryParseDoorMode(string s, out DoorMode mode)
        {
            mode = DoorMode.HingeFrontLeft;
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "front_left": mode = DoorMode.HingeFrontLeft; return true;
                case "front_right": mode = DoorMode.HingeFrontRight; return true;
                case "front_top": mode = DoorMode.HingeFrontTop; return true;
                case "front_bottom": mode = DoorMode.HingeFrontBottom; return true;
                case "back_left": mode = DoorMode.HingeBackLeft; return true;
                case "back_right": mode = DoorMode.HingeBackRight; return true;
                case "back_top": mode = DoorMode.HingeBackTop; return true;
                case "back_bottom": mode = DoorMode.HingeBackBottom; return true;
                case "edge_top_left": mode = DoorMode.HingeEdgeTopLeft; return true;
                case "edge_top_right": mode = DoorMode.HingeEdgeTopRight; return true;
                case "edge_bottom_left": mode = DoorMode.HingeEdgeBottomLeft; return true;
                case "edge_bottom_right": mode = DoorMode.HingeEdgeBottomRight; return true;
                case "drawer_out": mode = DoorMode.DrawerOut; return true;
                case "drawer_in": mode = DoorMode.DrawerIn; return true;
                case "drawer_right": mode = DoorMode.DrawerRight; return true;
                case "drawer_left": mode = DoorMode.DrawerLeft; return true;
                case "drawer_up": mode = DoorMode.DrawerUp; return true;
                case "drawer_down": mode = DoorMode.DrawerDown; return true;
                default: return false;
            }
        }

        private static string WireName(DrawerColor c) => c switch { DrawerColor.Anthracite => "anthracite", DrawerColor.White => "white", DrawerColor.Black => "black", _ => "anthracite" };
        private static string WireName(DoubleDrawerState s) => s switch { DoubleDrawerState.Closed => "closed", DoubleDrawerState.BothOpen => "bothopen", DoubleDrawerState.LowerOnly => "loweronly", _ => "closed" };
        private static string WireName(GlassTint t) => t switch { GlassTint.Clear => "clear", GlassTint.Tinted => "tinted", _ => "clear" };
        private static string WireName(DoorSashType t) => t switch { DoorSashType.Glass => "glass", DoorSashType.Blind => "blind", _ => "glass" };

        /// <summary>Batch clone: atomically clone one or MANY elements. Whole batch is ONE undo step.</summary>
        private McpResponse HandleCloneElements(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsCloneElements>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var commands = new List<IUndoCommand>();
            var allClones = new List<KitchenElement>();
            var errors = new List<string>();
            int globalSuffix = 2;
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.name)) { errors.Add("an op is missing 'name'"); continue; }
                var source = FindElementByName(op.name);
                if (source == null) { errors.Add($"Element not found: {op.name}"); continue; }
                int count = Mathf.Clamp(op.count <= 0 ? 1 : op.count, 1, 50);
                var offset = new Vector3(op.offset_x, op.offset_y, op.offset_z);
                var basePos = source.transform.position;
                for (int i = 1; i <= count; i++)
                {
                    var go = ElementFactory.Duplicate(source);
                    if (go == null) { errors.Add($"Failed to duplicate '{op.name}'"); break; }
                    var el = go.GetComponent<KitchenElement>();
                    string cloneName;
                    do { cloneName = op.name + "_" + globalSuffix; globalSuffix++; }
                    while (FindElementByName(cloneName) != null);
                    el.PartName = cloneName;
                    go.name = cloneName;
                    go.transform.position = basePos + offset * i;
                    commands.Add(new CreateCommand(go));
                    allClones.Add(el);
                }
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1, "clone_elements rejected: " + string.Join(" | ", errors));

            CommandStack.Execute(new CompositeCommand($"MCP clone_elements x{allClones.Count}", commands));
            RefreshElementHighlights();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var created = new List<string>();
            var elements = new List<ElementInfo>();
            foreach (var el in allClones) { created.Add(el.PartName); elements.Add(BuildElementInfo(el, all, false, vr)); }
            Debug.Log($"[MCP] Clone batch: {p.ops.Length} sources → {allClones.Count} clones");
            return McpResponse.Result(req.id, new { ok = true, created, elements, sceneViolationCount = vr != null ? vr.violations.Count : 0 });
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
            var p = req.Params?.ToObject<ParamsModules>();
            if (p == null || p.modules == null || p.modules.Length == 0)
                return McpResponse.Error(req.id, -32602, "modules required (non-empty array)");

            var allElements = PartRegistry.GetAll();
            var results = new List<object>();
            var missing = new List<string>();
            foreach (var module in p.modules)
            {
                var g = FindModule(module);
                if (g == null) { missing.Add(module); continue; }
                results.Add(BuildModuleInfo(g, allElements));
            }
            return McpResponse.Result(req.id, new { count = results.Count, results, missing = missing.Count > 0 ? missing : null });
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
            var p = req.Params?.ToObject<ParamsModules>();
            if (p == null || p.modules == null || p.modules.Length == 0)
                return McpResponse.Error(req.id, -32602, "modules required (non-empty array)");

            var resolved = new List<LinkGroup>();
            var errors = new List<string>();
            foreach (var module in p.modules)
            {
                var g = FindModule(module);
                if (g == null) { errors.Add($"Module not found: {module}"); continue; }
                resolved.Add(g);
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "dissolve_module rejected: " + string.Join(" | ", errors));

            var names = new List<string>();
            foreach (var g in resolved)
            {
                GroupManager.Unlink(g);
                names.Add(g.name);
                Debug.Log($"[MCP] Module '{g.name}' dissolved");
            }
            return McpResponse.Result(req.id, new { ok = true, names, count = resolved.Count });
        }

        private McpResponse HandleAddToModule(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsModuleElements>();
            if (p == null || string.IsNullOrEmpty(p.module) || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "module and names (non-empty array) required");
            var g = FindModule(p.module);
            if (g == null) return McpResponse.Error(req.id, -1, $"Module not found: {p.module}");

            var resolved = new List<KitchenElement>();
            var missing = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { missing.Add(name); continue; }
                resolved.Add(el);
            }
            if (missing.Count > 0)
                return McpResponse.Error(req.id, -1, $"Elements not found: {string.Join(", ", missing)}");

            foreach (var el in resolved)
                GroupManager.AddTo(g, el);

            Debug.Log($"[MCP] Added {resolved.Count} elements to module '{g.name}'");
            return McpResponse.Result(req.id, BuildModuleInfo(g, PartRegistry.GetAll()));
        }

        private McpResponse HandleRemoveFromModule(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsNames>();
            if (p == null || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "names required (non-empty array)");

            var resolved = new List<KitchenElement>();
            var errors = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { errors.Add($"Element not found: {name}"); continue; }
                if (el.GroupId == 0) { errors.Add($"Element '{name}' is not in any module"); continue; }
                resolved.Add(el);
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "remove_from_module rejected: " + string.Join(" | ", errors));

            var removedNames = new List<string>();
            foreach (var el in resolved)
            {
                GroupManager.RemoveFrom(el);
                removedNames.Add(el.PartName);
            }
            Debug.Log($"[MCP] Removed {resolved.Count} elements from modules");
            return McpResponse.Result(req.id, new { ok = true, removed = removedNames, count = resolved.Count });
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

        /// <summary>Batch align: move boards face-to-face against targets. Applied IN ORDER. Whole batch is ONE undo step.</summary>
        private McpResponse HandleAlignElements(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsAlignElements>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var commands = new List<IUndoCommand>();
            var errors = new List<string>();
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.name) || string.IsNullOrEmpty(op.target))
                { errors.Add("op missing name or target"); continue; }
                if (!TryParseFace(op.face, out int axis, out bool maxSide))
                { errors.Add($"Unknown face '{op.face}' for '{op.name}'"); continue; }
                if (!TryParseFace(op.target_face, out int tAxis, out bool tMaxSide))
                { errors.Add($"Unknown target_face '{op.target_face}' for '{op.target}'"); continue; }
                if (axis != tAxis)
                { errors.Add($"face '{op.face}' and target_face '{op.target_face}' on different axes"); continue; }
                var element = FindElementByName(op.name);
                if (element == null) { errors.Add($"Element not found: {op.name}"); continue; }
                var target = FindElementByName(op.target);
                if (target == null) { errors.Add($"Target not found: {op.target}"); continue; }
                if (element == target) { errors.Add($"'{op.name}' and '{op.target}' must differ"); continue; }
                var lockErr = RequireMovable(element, op.name, req.id);
                if (lockErr != null) { errors.Add($"'{op.name}' is LOCKED"); continue; }

                var elAabb = ComputeAABB(element.GetVertices());
                var tAabb = ComputeAABB(target.GetVertices());
                float myCoord = AabbSide(elAabb, axis, maxSide);
                float targetCoord = AabbSide(tAabb, axis, tMaxSide);
                float gapUnits = op.gap_mm * AppConstants.MM_TO_UNITS;
                float desired = maxSide ? targetCoord - gapUnits : targetCoord + gapUnits;
                float delta = desired - myCoord;
                var before = element.transform.position;
                var after = before;
                after[axis] += delta;
                commands.Add(new MoveCommand(element, before, after, element.transform.rotation, element.transform.rotation));
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1, "align_elements rejected: " + string.Join(" | ", errors));

            CommandStack.Execute(new CompositeCommand($"MCP align_elements ({commands.Count} moves)", commands));
            RefreshElementHighlights();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var results = new List<object>();
            foreach (var op in p.ops)
            {
                var el = FindElementByName(op.name);
                if (el == null) continue;
                var pos = el.transform.position;
                results.Add(new { name = el.PartName, posX = pos.x, posY = pos.y, posZ = pos.z, violations = BuildElementViolations(el, all, vr) });
            }
            Debug.Log($"[MCP] Aligned {commands.Count} elements");
            return McpResponse.Result(req.id, new { ok = true, aligned = results.Count, results, sceneViolationCount = vr != null ? vr.violations.Count : 0 });
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

        /// <summary>Batch create: atomically create one or MANY elements. Whole batch is ONE undo step.</summary>
        private McpResponse HandleCreateElements(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsCreateElements>();
            if (p == null || p.items == null || p.items.Length == 0)
                return McpResponse.Error(req.id, -32602, "items required (non-empty array)");

            var commands = new List<IUndoCommand>();
            var created = new List<KitchenElement>();
            var errors = new List<string>();
            var namesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in p.items)
            {
                if (string.IsNullOrEmpty(item.name)) { errors.Add("an item is missing 'name'"); continue; }
                if (namesSeen.Contains(item.name)) { errors.Add($"duplicate name '{item.name}' in this batch"); continue; }
                namesSeen.Add(item.name);
                var existing = FindElementByName(item.name);
                if (existing != null) { errors.Add($"Element '{item.name}' already exists"); continue; }

                var elementType = (item.type ?? "board").Trim().ToLowerInvariant();
                var pos = new Vector3(item.x, item.y, item.z);
                GameObject go = null!;

                switch (elementType)
                {
                    case "floor":
                        var plate = BasePlate.Create();
                        plate.Element.PartName = item.name;
                        commands.Add(new CreateCommand(plate.gameObject));
                        created.Add(plate.Element);
                        break;
                    case "assembled_facade":
                        go = ElementFactory.CreateAssembledFacade(new Vector3Int(item.width ?? 450, item.height ?? 700, item.depth ?? 18), item.name, pos, AssembledFill.Blind);
                        break;
                    case "radial_shelf":
                        int rw = item.width ?? 600, rd = item.depth ?? 400;
                        go = ElementFactory.CreateRadialShelf(rw, rd, item.height ?? AppConstants.BOARD_THICKNESS_DEFAULT,
                            AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, item.name, pos);
                        break;
                    case "drawer":
                        go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, item.name, pos);
                        break;
                    case "table":
                        go = ElementFactory.CreateTable(new Vector3Int(item.width ?? 2000, item.height ?? 750, item.depth ?? 1000), item.name, pos);
                        break;
                    case "radius_table":
                        go = ElementFactory.CreateRadiusTable(new Vector3Int(item.width ?? 2000, item.height ?? 750, item.depth ?? 1000), item.name, pos);
                        break;
                    case "pillar":
                        go = ElementFactory.CreatePillar(item.height ?? PillarElement.MidHeightMM_Default, item.name, pos);
                        break;
                    case "window":
                        go = ElementFactory.CreateWindow(new Vector3Int(item.width ?? 900, item.height ?? 1200, item.depth ?? 100),
                            item.name, pos, GlassTint.Clear, 50);
                        break;
                    case "door":
                        go = ElementFactory.CreateDoor(new Vector3Int(item.width ?? 900, item.height ?? 2000, item.depth ?? 100),
                            item.name, pos, DoorSashType.Glass);
                        break;
                    default:
                    {
                        var dims = new Vector3Int(item.width ?? 800, item.height ?? 400, item.depth ?? 18);
                        go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.name = item.name;
                        if (elementType == "facade")
                        {
                            var facade = go.AddComponent<FacadeElement>();
                            facade.PartName = item.name; facade.DimensionsMM = dims;
                        }
                        else { var el = go.AddComponent<KitchenElement>(); el.PartName = item.name; el.DimensionsMM = dims; }
                        if (elementType == "wall") go.AddComponent<Wall>();
                        go.transform.position = pos;
                        break;
                    }
                }
                if (go == null) { errors.Add($"Failed to create '{item.name}'"); continue; }
                commands.Add(new CreateCommand(go));
                created.Add(go.GetComponent<KitchenElement>());
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1, "create_elements rejected: " + string.Join(" | ", errors));

            if (commands.Count > 0)
                CommandStack.Execute(new CompositeCommand($"MCP create_elements x{commands.Count}", commands));
            RefreshElementHighlights();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var elements = new List<ElementInfo>();
            var createdNames = new List<string>();
            foreach (var el in created) { createdNames.Add(el.PartName); elements.Add(BuildElementInfo(el, all, false, vr)); }
            Debug.Log($"[MCP] Created {created.Count} elements: {string.Join(", ", createdNames)}");
            return McpResponse.Result(req.id, new { ok = true, created = createdNames, elements, sceneViolationCount = vr != null ? vr.violations.Count : 0 });
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

        /// <summary>Batch convert: change type of one or MANY elements. Atomic. NOT undoable.</summary>
        private McpResponse HandleConvertElements(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsConvertElements>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var errors = new List<string>();
            var results = new List<KitchenElement>();
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.name)) { errors.Add("op missing name"); continue; }
                if (!TryParseTarget(op.target, out var target))
                { errors.Add($"Unknown target '{op.target}' for '{op.name}'"); continue; }
                var element = FindElementByName(op.name);
                if (element == null) { errors.Add($"Element not found: {op.name}"); continue; }
                var lockErr = RequireMovable(element, op.name, req.id);
                if (lockErr != null) { errors.Add($"'{op.name}' is LOCKED"); continue; }
                var converted = ElementConverter.Convert(element, target);
                if (converted is AssembledFacadeElement assembled && !string.IsNullOrEmpty(op.fill))
                    assembled.Fill = ParseFill(op.fill);
                results.Add(converted);
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1, "convert_elements rejected: " + string.Join(" | ", errors));

            RefreshElementHighlights();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var elements = new List<ElementInfo>();
            var names = new List<string>();
            foreach (var el in results) { names.Add(el.PartName); elements.Add(BuildElementInfo(el, all, false, vr)); }
            Debug.Log($"[MCP] Converted {results.Count} elements");
            return McpResponse.Result(req.id, new { ok = true, converted = names, elements, sceneViolationCount = vr != null ? vr.violations.Count : 0 });
        }

        /// <summary>Batch delete: atomically delete one or MANY elements. Whole batch is ONE undo step.</summary>
        private McpResponse HandleDeleteElements(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsNames>();
            if (p == null || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "names required (non-empty array)");

            var commands = new List<IUndoCommand>();
            var errors = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { errors.Add($"Element not found: {name}"); continue; }
                var lockErr = RequireMovable(el, name, req.id);
                if (lockErr != null) { errors.Add($"'{name}' is LOCKED"); continue; }
                commands.Add(new DeleteCommand(el.gameObject));
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1, "delete_elements rejected: " + string.Join(" | ", errors));

            var deletedNames = p.names.ToList();
            CommandStack.Execute(new CompositeCommand($"MCP delete_elements x{commands.Count}", commands));
            RefreshElementHighlights();
            var allAfter = PartRegistry.GetAll();
            var vrAfter = allAfter != null && allAfter.Count > 0 ? ConstraintValidator.Validate(allAfter) : null;
            Debug.Log($"[MCP] Deleted {commands.Count} elements");
            return McpResponse.Result(req.id, new { ok = true, deleted = deletedNames, sceneViolationCount = vrAfter != null ? vrAfter.violations.Count : 0 });
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

        /// <summary>Batch select: highlight one or MANY elements (visual only).</summary>
        private McpResponse HandleSelectElements(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsNames>();
            if (p == null || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "names required (non-empty array)");

            var selected = new List<string>();
            var missing = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { missing.Add(name); continue; }
#if UNITY_EDITOR
                if (selected.Count == 0) Selection.activeGameObject = el.gameObject;
#endif
                var sel = Object.FindAnyObjectByType<SelectionManager>();
                if (sel != null) sel.Select(el);
                selected.Add(name);
            }
            return McpResponse.Result(req.id, new { ok = true, selected, missing = missing.Count > 0 ? missing : null });
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

        /// <summary>Batch snap diagnose: for EACH given board explain why it does or does not snap.</summary>
        private McpResponse HandleSnapDiagnose(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSnapDiagnose>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var results = new List<object>();
            var missing = new List<string>();
            foreach (var op in p.ops)
            {
                var element = FindElementByName(op.name);
                if (element == null) { missing.Add(op.name); continue; }
                var pos = element.transform.position;
                if (op.x.HasValue) pos.x = op.x.Value;
                if (op.y.HasValue) pos.y = op.y.Value;
                if (op.z.HasValue) pos.z = op.z.Value;
                var diagnosis = SnapSystem.Diagnose(element, PartRegistry.GetAll(), pos);
                results.Add(diagnosis);
            }
            return McpResponse.Result(req.id, new { results, missing = missing.Count > 0 ? missing : null });
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
            var p = req.Params?.ToObject<ParamsNames>();
            if (p == null || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "names required (non-empty array)");

            var results = new List<object>();
            var missing = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { missing.Add(name); continue; }
                results.Add(BuildElementDebugInfo(el));
            }
            return McpResponse.Result(req.id, new { count = results.Count, results, missing = missing.Count > 0 ? missing : null });
        }

        private static ElementDebugInfo BuildElementDebugInfo(KitchenElement el)
        {
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
            return new ElementDebugInfo
            {
                name = el.PartName, type = el.GetType().Name,
                aabb = aabb, faces = faces, vertices = vertices,
                dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                effectiveDimX = effDim.x, effectiveDimY = effDim.y, effectiveDimZ = effDim.z
            };
        }

        // ── get_element_gaps ─────────────────────────────────────────────
        private McpResponse HandleGetElementGaps(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsNames>();
            if (p == null || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "names required (non-empty array)");

            var all = PartRegistry.GetAll();
            var results = new List<object>();
            var missing = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { missing.Add(name); continue; }
                var gaps = all != null ? ComputeAxisGaps(el, all) : new List<AxisGapInfo>();
                results.Add(new ElementGapsResult { name = el.PartName, gaps = gaps });
            }
            return McpResponse.Result(req.id, new { count = results.Count, results, missing = missing.Count > 0 ? missing : null });
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

        private McpResponse HandleCycleDrawerAnimation(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsNames>();
            if (p == null || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "names required (non-empty array)");

            var results = new List<object>();
            var errors = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { errors.Add($"Element not found: {name}"); continue; }
                var drawer = el as DrawerElement;
                if (drawer == null) { errors.Add($"Element '{name}' is not a drawer"); continue; }

                if (drawer.IsDouble)
                    drawer.CycleDoubleState();
                else
                    drawer.ToggleOpen();
                results.Add(new { name, isDouble = drawer.IsDouble, isOpen = drawer.IsOpen, doubleState = drawer.DoubleState.ToString() });
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "cycle_drawer_animation rejected, NOTHING was cycled: " + string.Join(" | ", errors));

            Debug.Log($"[MCP] Cycled {results.Count} drawers");
            return McpResponse.Result(req.id, new { ok = true, results, errors = errors.Count > 0 ? errors : null });
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

        private static KitchenElement? FindElementByName(string name)
        {
            foreach (var el in PartRegistry.All)
                if (el != null && (el.PartName == name || el.name == name))
                    return el;
            return null;
        }
    }
}
