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
        /// - get_all_elements — единственный источник истины (snapshot) перед изменениями.
        /// - simulate_move / simulate_resize — dry-run; проверяй wouldHaveViolations до apply.
        /// - После каждой мутации вызывай get_violations для проверки регрессий.
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
                    case "get_element_info": return HandleGetElementInfo(request);
                    case "move_element": return HandleMoveElement(request);
                    case "resize_element": return HandleResizeElement(request);
                    case "rotate_element": return HandleRotateElement(request);
                    case "create_element": return HandleCreateElement(request);
                    case "convert_element": return HandleConvertElement(request);
                    case "delete_element": return HandleDeleteElement(request);
                    case "undo": return HandleUndo(request);
                    case "redo": return HandleRedo(request);
                    case "get_specification": return HandleGetSpecification(request);
                    case "export_specification_csv": return HandleExportCsv(request);
                    case "select_element": return HandleSelectElement(request);
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
                    case "simulate_move": return HandleSimulateMove(request);
                    case "simulate_resize": return HandleSimulateResize(request);
                    case "set_element_lock": return HandleSetElementLock(request);
                    case "set_facade_mode": return HandleSetFacadeMode(request);
                    case "set_drawer_properties": return HandleSetDrawerProperties(request);
                    case "cycle_drawer_animation": return HandleCycleDrawerAnimation(request);
                    case "set_material": return HandleSetMaterial(request);
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

        private static ElementInfo BuildElementInfo(KitchenElement el, List<KitchenElement>? allElements, bool includeFacadeValidation = false)
        {
            var t = el.transform;
            var group = GroupManager.GroupOf(el);
            var wall = el.GetComponent<Wall>();
            Vector3 pos = wall != null ? wall.FullPosition : t.position;

            bool hasViolations = false;
            if (allElements != null && allElements.Count > 0)
            {
                var vr = ConstraintValidator.Validate(allElements);
                hasViolations = vr.violations.Contains(el);
            }

            var aabb = ComputeAABB(el.GetVertices());
            var effDim = GetEffectiveDimMM(el);
            var gaps = allElements != null ? ComputeAxisGaps(el, allElements) : null;
            var radial = el as RadialShelfElement;
            var drawer = el as DrawerElement;
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
                moduleId = group != null ? group.id : 0,
                moduleName = group != null ? group.name : null,
                materialId = el.MaterialId,
                hasViolations = hasViolations,
                aabbMinX = aabb.minX, aabbMinY = aabb.minY, aabbMinZ = aabb.minZ,
                aabbMaxX = aabb.maxX, aabbMaxY = aabb.maxY, aabbMaxZ = aabb.maxZ,
                effectiveDimX = effDim.x, effectiveDimY = effDim.y, effectiveDimZ = effDim.z,
                faceGaps = gaps,
                radius = radial != null ? radial.Radius : 0,
                faceNormalX = facadeValidation?.normal.x ?? 0f,
                faceNormalY = facadeValidation?.normal.y ?? 0f,
                faceNormalZ = facadeValidation?.normal.z ?? 0f,
                faceInward = facadeValidation?.faceInward ?? false,
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
            var list = new List<ElementInfo>();
            foreach (var el in elements)
            {
                if (el == null) continue;
                list.Add(BuildElementInfo(el, elements, false));
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

        /// <summary>SHA256 хеш от JSON-представления списка для ETag.</summary>
        private static string ComputeEtag(List<ElementInfo> list)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(list);
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

        private static ModuleInfo BuildModuleInfo(LinkGroup g, List<KitchenElement>? allElements)
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
                info.elements.Add(BuildElementInfo(el, allElements));
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
            var list = new List<ModuleInfo>();
            foreach (var g in GroupManager.AllGroups())
                list.Add(BuildModuleInfo(g, allElements));
            return McpResponse.Result(req.id, list);
        }

        private McpResponse HandleModuleInfo(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsModule>();
            if (p == null || string.IsNullOrEmpty(p.module))
                return McpResponse.Error(req.id, -32602, "module (id или имя) required");
            var g = FindModule(p.module);
            if (g == null) return McpResponse.Error(req.id, -1, $"Module not found: {p.module}");
            return McpResponse.Result(req.id, BuildModuleInfo(g, PartRegistry.GetAll()));
        }

        private McpResponse HandleCreateModule(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsCreateModule>();
            if (p == null || p.members == null || p.members.Length < 2)
                return McpResponse.Error(req.id, -32602, "members: минимум 2 имени деталей");

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
            if (g == null) return McpResponse.Error(req.id, -1, "Не удалось создать модуль");
            if (!string.IsNullOrEmpty(p.name)) g.name = p.name;

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
                return McpResponse.Error(req.id, -32602, "module и name required");
            var g = FindModule(p.module);
            if (g == null) return McpResponse.Error(req.id, -1, $"Module not found: {p.module}");
            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            el.GroupId = g.id;
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
                return McpResponse.Error(req.id, -1, $"Element '{p.name}' не входит в модуль");

            var g = GroupManager.GroupOf(el);
            el.GroupId = 0;
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
            var (hasViol, aabb, gaps) = DescribeAfterMutation(element);
            var (faceNormal, faceInward, faceObstructions, openingViolations) = BuildFacadeResponseFields(element);
            return McpResponse.Result(req.id, new {
                ok = true, element = p.name,
                position = new { x = after.x, y = after.y, z = after.z },
                hasViolations = hasViol,
                aabb = aabb,
                faceGaps = gaps,
                faceNormal = faceNormal,
                faceInward = faceInward,
                faceObstructions = faceObstructions,
                openingViolations = openingViolations
            });
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
            var (hasViol, aabb, gaps) = DescribeAfterMutation(element);
            return McpResponse.Result(req.id, new {
                ok = true, element = p.name,
                dimensions = new { width = w, height = h, depth = d },
                hasViolations = hasViol,
                aabb = aabb,
                faceGaps = gaps
            });
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
            var (faceNormalR, faceInwardR, faceObstructionsR, openingViolationsR) = BuildFacadeResponseFields(element);
            return McpResponse.Result(req.id, new {
                ok = true, element = p.name,
                rotation = new { x = euler.x, y = euler.y, z = euler.z },
                hasViolations = HasViolations(element),
                faceNormal = faceNormalR,
                faceInward = faceInwardR,
                faceObstructions = faceObstructionsR,
                openingViolations = openingViolationsR
            });
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
                return McpResponse.Result(req.id, new { ok = true, name = plate.name, is_floor = true, path = GetGameObjectPath(plate.gameObject) });
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
                return McpResponse.Result(req.id, new {
                    ok = true, name = goA.name, is_assembled = true, fill = fillA.ToString(),
                    path = GetGameObjectPath(goA), posX = posA.x, posY = posA.y, posZ = posA.z,
                    hasViolations = HasViolations(elA) });
            }

            if (p.is_radial_shelf)
            {
                int radius = p.radius > 0 ? p.radius : 300;
                int thickness = p.depth > 0 ? p.depth : AppConstants.BOARD_THICKNESS_DEFAULT;
                var posR = new Vector3(p.x, p.y, p.z);
                var goR = ElementFactory.CreateRadialShelf(radius, thickness, elementName, posR);
                CommandStack.Execute(new CreateCommand(goR));
                RefreshElementHighlights();
                var elR = goR.GetComponent<KitchenElement>();
                Debug.Log($"[MCP] Created radial shelf '{elementName}' radius={radius} thickness={thickness}");
                return McpResponse.Result(req.id, new {
                    ok = true, name = goR.name, is_radial_shelf = true, radius = radius,
                    path = GetGameObjectPath(goR), posX = posR.x, posY = posR.y, posZ = posR.z,
                    hasViolations = HasViolations(elR) });
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
                return McpResponse.Result(req.id, new {
                    ok = true, name = goD.name, is_drawer = true,
                    drawer_type = p.drawer_type, drawer_length = length, drawer_color = drawerColor.ToString(),
                    path = GetGameObjectPath(goD), posX = posD.x, posY = posD.y, posZ = posD.z,
                    hasViolations = HasViolations(elD) });
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
            var (faceNormalC, faceInwardC, faceObstructionsC, openingViolationsC) = BuildFacadeResponseFields(element);
            return McpResponse.Result(req.id, new {
                ok = true, name = go.name, is_wall = p.is_wall, is_facade = p.is_facade,
                path = GetGameObjectPath(go), posX = pos.x, posY = pos.y, posZ = pos.z,
                hasViolations = HasViolations(element),
                faceNormal = faceNormalC,
                faceInward = faceInwardC,
                faceObstructions = faceObstructionsC,
                openingViolations = openingViolationsC
            });
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

        /// <summary>Строка → целевой тип для конвертации элемента.</summary>
        private static bool TryParseTarget(string s, out ElementConverter.TargetType target)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "part": case "board": case "деталь":
                    target = ElementConverter.TargetType.Part; return true;
                case "facade": case "door": case "фасад": case "дверца":
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
            return McpResponse.Result(req.id, new {
                ok = true, name = converted.PartName, type = converted.GetType().Name,
                target = target.ToString(), hasViolations = HasViolations(converted) });
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
            return McpResponse.Result(req.id, new { ok = true, name = p.name });
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
                snapVerboseLog = SnapSystem.VerboseLog
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
                default:
                    return McpResponse.Error(req.id, -32602, $"Unknown setting: {p.name}");
            }

            s.Save();
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
#if UNITY_WEBGL
            ScreenCapture.CaptureScreenshot(path);
#else
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            var bytes = ImageConversion.EncodeToPNG(tex);
            System.IO.File.WriteAllBytes(path, bytes);
            Object.Destroy(tex);
#endif
            return McpResponse.Result(req.id, new { ok = true, path });
        }

        private McpResponse HandleGetViolations(McpRequest req)
        {
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

                results.Add(new {
                    neighbor = other.PartName,
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
            return McpResponse.Result(req.id, new
            {
                ok = true,
                dimX = w, dimY = h, dimZ = d
            });
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
            return McpResponse.Result(req.id, new { ok = true, name = p.name, was_already_wall = false });
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
                $"Элемент '{name}' заблокирован. Снимите блокировку через set_element_lock (locked:false) — это требует явного разрешения пользователя.");
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

        /// <summary>Состояние элемента после мутации: нарушения + AABB + зазоры к соседям.
        /// Единый источник формы ответа для move_element / resize_element.</summary>
        private static (bool hasViolations, AabbInfo aabb, List<AxisGapInfo>? gaps) DescribeAfterMutation(KitchenElement element)
        {
            var all = PartRegistry.GetAll();
            var vr = all != null ? ConstraintValidator.Validate(all) : null;
            var aabb = ComputeAABB(element.GetVertices());
            var gaps = all != null ? ComputeAxisGaps(element, all) : null;
            return (vr != null && vr.violations.Contains(element), aabb, gaps);
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

        private static List<AxisGapInfo> ComputeAxisGaps(KitchenElement element, List<KitchenElement> allElements)
        {
            var elAabb = ComputeAABB(element.GetVertices());
            var gaps = new List<AxisGapInfo>();
            string[] axisNames = { "x", "y", "z" };
            float[] aMin = { elAabb.minX, elAabb.minY, elAabb.minZ };
            float[] aMax = { elAabb.maxX, elAabb.maxY, elAabb.maxZ };

            for (int axis = 0; axis < 3; axis++)
            {
                float bestGapUnits = float.MaxValue;
                string? bestNeighbor = null;
                float am = aMin[axis], ax = aMax[axis];

                foreach (var other in allElements)
                {
                    if (other == element || other == null) continue;
                    var oAabb = ComputeAABB(other.GetVertices());
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

                float gapMM = bestGapUnits == float.MaxValue ? 0f : bestGapUnits / AppConstants.MM_TO_UNITS;
                gaps.Add(new AxisGapInfo
                {
                    axis = axisNames[axis],
                    neighbor = bestNeighbor ?? "",
                    gapMM = gapMM,
                    isOverlap = gapMM < 0
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
            return McpResponse.Result(req.id, new { ok = true, name = p.name, locked = p.locked });
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
            var data = ComputeFacadeValidation(facade, PartRegistry.GetAll());
            var normal = new { x = data.normal.x, y = data.normal.y, z = data.normal.z };
            return McpResponse.Result(req.id, new {
                ok = true, name = p.name, mode = p.mode,
                faceNormal = normal,
                faceInward = data.faceInward,
                faceObstructions = data.obstructions,
                openingViolations = data.openingViolations
            });
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
            return McpResponse.Result(req.id, BuildElementInfo(drawer, PartRegistry.GetAll(), false));
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
                return McpResponse.Error(req.id, -32602, "material required (id или имя из list_materials)");

            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var def = ResolveMaterial(p.material);
            if (def == null)
                return McpResponse.Error(req.id, -1,
                    $"Unknown material '{p.material}'. Вызови list_materials для доступных id/имён.");

            MaterialManager.Apply(el, def);
            RefreshElementHighlights();
            // Если элемент выделен — обновить подсветку выделения поверх нового декора.
            var sel = Object.FindAnyObjectByType<SelectionManager>();
            if (sel != null) sel.RefreshHighlight(el);

            Debug.Log($"[MCP] Material of '{p.name}' set to {def.id} ({def.displayName})");
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

        private static KitchenElement? FindElementByName(string name)
        {
            foreach (var el in PartRegistry.All)
                if (el != null && (el.PartName == name || el.name == name))
                    return el;
            return null;
        }
    }
}
