using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

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
        /// - dimZ всегда толщина доски (Board convention в AGENTS.md).
        /// </summary>
        public McpResponse Handle(McpRequest request)
        {
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
            var p = req.Params?.ToObject<ParamsWithName>();
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
            var p = req.Params?.ToObject<ParamsWithName>();
            var path = p?.object_path ?? p?.name;
            if (string.IsNullOrEmpty(path))
                return McpResponse.Error(req.id, -32602, "name or object_path required");
            var go = FindGameObject(path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {path}");
            Object.Destroy(go);
            return McpResponse.Result(req.id, new { ok = true, deleted = go.name });
        }

        private McpResponse HandleSetPosition(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetTransform>();
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            go.transform.position = new Vector3(p.x, p.y, p.z);
            return McpResponse.Result(req.id, new { ok = true, position = new { p.x, p.y, p.z } });
        }

        private McpResponse HandleSetRotation(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetTransform>();
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            go.transform.eulerAngles = new Vector3(p.x, p.y, p.z);
            return McpResponse.Result(req.id, new { ok = true, rotation = new { p.x, p.y, p.z } });
        }

        private McpResponse HandleSetScale(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsSetTransform>();
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            go.transform.localScale = new Vector3(p.x, p.y, p.z);
            return McpResponse.Result(req.id, new { ok = true, scale = new { p.x, p.y, p.z } });
        }

        /// <summary>Инфо об элементе, включая принадлежность модулю (группе) —
        /// чтобы через MCP была видна конфигурация сцены.</summary>
        private static ElementInfo BuildElementInfo(KitchenElement el)
        {
            return BuildElementInfo(el, null);
        }

        private static ElementInfo BuildElementInfo(KitchenElement el, List<KitchenElement> allElements)
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

            return new ElementInfo
            {
                name = el.BoardName, type = el.GetType().Name,
                dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                posX = pos.x, posY = pos.y, posZ = pos.z,
                rotX = t.eulerAngles.x, rotY = t.eulerAngles.y, rotZ = t.eulerAngles.z,
                active = el.gameObject.activeInHierarchy,
                moduleId = group != null ? group.id : 0,
                moduleName = group != null ? group.name : null,
                hasViolations = hasViolations,
                aabbMinX = aabb.minX, aabbMinY = aabb.minY, aabbMinZ = aabb.minZ,
                aabbMaxX = aabb.maxX, aabbMaxY = aabb.maxY, aabbMaxZ = aabb.maxZ,
                effectiveDimX = effDim.x, effectiveDimY = effDim.y, effectiveDimZ = effDim.z
            };
        }

        private McpResponse HandleGetAllElements(McpRequest req)
        {
            var elements = BoardRegistry.GetAll();
            var list = new List<ElementInfo>();
            foreach (var el in elements)
            {
                if (el == null) continue;
                list.Add(BuildElementInfo(el, elements));
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
            var p = req.Params?.ToObject<ParamsWithName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            return McpResponse.Result(req.id, BuildElementInfo(element, BoardRegistry.GetAll()));
        }

        // ── Модули (именованные группы досок) ───────────────────────────

        /// <summary>Модуль по id или имени (без учёта регистра).</summary>
        private static LinkGroup FindModule(string module)
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

        private static ModuleInfo BuildModuleInfo(LinkGroup g, List<KitchenElement> allElements)
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
            var allElements = BoardRegistry.GetAll();
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
            return McpResponse.Result(req.id, BuildModuleInfo(g, BoardRegistry.GetAll()));
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
            return McpResponse.Result(req.id, BuildModuleInfo(g, BoardRegistry.GetAll()));
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
            return McpResponse.Result(req.id, BuildModuleInfo(g, BoardRegistry.GetAll()));
        }

        private McpResponse HandleRemoveFromModule(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsWithName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            if (el.GroupId == 0)
                return McpResponse.Error(req.id, -1, $"Element '{p.name}' не входит в модуль");

            var g = GroupManager.GroupOf(el);
            el.GroupId = 0;
            return McpResponse.Result(req.id, g != null
                ? (object)BuildModuleInfo(g, BoardRegistry.GetAll())
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
            var after = new Vector3(p.x, p.y, p.z);
            CommandStack.Execute(new MoveCommand(element, before, after, rotBefore, element.transform.rotation));
            Debug.Log($"[MCP] Moved {element.BoardName} to ({p.x}, {p.y}, {p.z})");
            var (hasViol, aabb, gaps) = DescribeAfterMutation(element);
            return McpResponse.Result(req.id, new {
                ok = true, element = p.name,
                position = new { p.x, p.y, p.z },
                hasViolations = hasViol,
                aabb = aabb,
                faceGaps = gaps
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

            int w = p.width > 0 ? p.width : p.dimX;
            int h = p.height > 0 ? p.height : p.dimY;
            int d = p.depth > 0 ? p.depth : p.dimZ;
            w = Mathf.Max(1, w);
            h = Mathf.Max(1, h);
            d = Mathf.Max(1, d);

            var dimsBefore = element.DimensionsMM;
            var posBefore = element.transform.position;
            var rotBefore = element.transform.rotation;
            var dimsAfter = new Vector3Int(w, h, d);

            CommandStack.Execute(new ResizeCommand(element, dimsBefore, dimsAfter,
                posBefore, posBefore, rotBefore, rotBefore));
            Debug.Log($"[MCP] Resized {element.BoardName} to ({w}, {h}, {d})mm");
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
            var rotAfter = Quaternion.Euler(p.x, p.y, p.z);
            CommandStack.Execute(new MoveCommand(element, before, before, rotBefore, rotAfter));
            Debug.Log($"[MCP] Rotated {element.BoardName} to ({p.x}, {p.y}, {p.z})");
            return McpResponse.Result(req.id, new { ok = true, element = p.name, rotation = new { p.x, p.y, p.z }, hasViolations = HasViolations(element) });
        }

        private McpResponse HandleCreateElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsCreateElement>();
            if (p == null || string.IsNullOrEmpty(p.template_name))
                return McpResponse.Error(req.id, -32602, "template_name required");

            string elementName = string.IsNullOrEmpty(p.name) ? p.template_name : p.name;

            if (p.is_floor)
            {
                var plate = BasePlate.Create();
                plate.Element.BoardName = elementName;
                CommandStack.Execute(new CreateCommand(plate.gameObject));
                Debug.Log($"[MCP] Created floor '{elementName}'");
                return McpResponse.Result(req.id, new { ok = true, name = plate.name, is_floor = true, path = GetGameObjectPath(plate.gameObject) });
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
                facade.BoardName = elementName;
                facade.DimensionsMM = dims;
                facade.GapLeft = p.gapLeft;
                facade.GapRight = p.gapRight;
                facade.GapTop = p.gapTop;
                facade.GapBottom = p.gapBottom;
                element = facade;
            }
            else
            {
                element = go.AddComponent<KitchenElement>();
                element.BoardName = elementName;
                element.DimensionsMM = dims;
                MaterialManager.ApplyById(element, MaterialCatalog.DefaultId);
            }

            if (p.is_wall)
                go.AddComponent<Wall>();

            go.transform.position = pos;
            CommandStack.Execute(new CreateCommand(go));
            Debug.Log($"[MCP] Created {go.name} at ({p.x}, {p.y}, {p.z})");
            return McpResponse.Result(req.id, new { ok = true, name = go.name, is_wall = p.is_wall, is_facade = p.is_facade, path = GetGameObjectPath(go), posX = pos.x, posY = pos.y, posZ = pos.z, hasViolations = HasViolations(element) });
        }

        private McpResponse HandleDeleteElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsWithName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            var lockErr = RequireMovable(element, p.name, req.id);
            if (lockErr != null) return lockErr;
            CommandStack.Execute(new DeleteCommand(element.gameObject));
            Debug.Log($"[MCP] Deleted {p.name}");
            return McpResponse.Result(req.id, new { ok = true, name = p.name });
        }

        private McpResponse HandleUndo(McpRequest req)
        {
            if (!CommandStack.CanUndo)
                return McpResponse.Result(req.id, new { ok = false, reason = "Nothing to undo" });
            var desc = CommandStack.PeekUndoDescription();
            CommandStack.Undo();
            Debug.Log($"[MCP] Undo: {desc}");
            return McpResponse.Result(req.id, new { ok = true, action = "undo", description = desc });
        }

        private McpResponse HandleRedo(McpRequest req)
        {
            if (!CommandStack.CanRedo)
                return McpResponse.Result(req.id, new { ok = false, reason = "Nothing to redo" });
            CommandStack.Redo();
            Debug.Log($"[MCP] Redo");
            return McpResponse.Result(req.id, new { ok = true, action = "redo" });
        }

        private McpResponse HandleGetSpecification(McpRequest req)
        {
            var spec = SpecificationManager.Build(BoardRegistry.GetAll());
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
            var spec = SpecificationManager.Build(BoardRegistry.GetAll());
            SpecificationExport.SaveToFile(spec, p.path);
            return McpResponse.Result(req.id, new { ok = true, path = p.path });
        }

        private McpResponse HandleSelectElement(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsWithName>();
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

        /// <summary>Разбор прилипания: почему доска (не) прилипает из текущей или
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

            var diagnosis = SnapSystem.Diagnose(element, BoardRegistry.GetAll(), pos);
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
            var all = BoardRegistry.GetAll();
            if (all == null || all.Count == 0)
                return McpResponse.Result(req.id, new { violations = new string[0], count = 0 });

            var result = ConstraintValidator.Validate(all);
            var list = new List<object>();
            foreach (var el in result.violations)
                list.Add(new { name = el.BoardName, type = el.GetType().Name });
            return McpResponse.Result(req.id, new { violations = list, count = list.Count });
        }

        private static BasePlate FindFloor()
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
                name = el.BoardName,
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

            var p = req.Params?.ToObject<ParamsResizeElement>();
            if (p == null)
                return McpResponse.Error(req.id, -32602, "invalid parameters");

            var el = plate.Element;
            var dimsBefore = el.DimensionsMM;
            var posBefore = el.transform.position;
            var rotBefore = el.transform.rotation;

            int w = p.width > 0 ? p.width : p.dimX;
            int h = p.height > 0 ? p.height : p.dimY;
            int d = p.depth > 0 ? p.depth : p.dimZ;
            w = Mathf.Max(1, w);
            h = Mathf.Max(1, h);
            d = Mathf.Max(1, d);
            var dimsAfter = new Vector3Int(w, h, d);

            CommandStack.Execute(new ResizeCommand(el,
                dimsBefore, dimsAfter,
                posBefore, posBefore,
                rotBefore, rotBefore));

            Debug.Log($"[MCP] Resized floor to ({w}, {h}, {d})mm");
            return McpResponse.Result(req.id, new
            {
                ok = true,
                dimX = w, dimY = h, dimZ = d
            });
        }

        private McpResponse HandleAddWallComponent(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsWithName>();
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
            var p = req.Params?.ToObject<ParamsWithName>();
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
                name = el.BoardName, type = el.GetType().Name,
                aabb = aabb, faces = faces, vertices = vertices,
                dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                effectiveDimX = effDim.x, effectiveDimY = effDim.y, effectiveDimZ = effDim.z
            });
        }

        // ── get_element_gaps ─────────────────────────────────────────────
        private McpResponse HandleGetElementGaps(McpRequest req)
        {
            var p = req.Params?.ToObject<ParamsWithName>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var el = FindElementByName(p.name);
            if (el == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var all = BoardRegistry.GetAll();
            var gaps = all != null ? ComputeAxisGaps(el, all) : new List<AxisGapInfo>();
            return McpResponse.Result(req.id, new ElementGapsResult { name = el.BoardName, gaps = gaps });
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

            var all = BoardRegistry.GetAll();
            var result = SimulateMoveAt(el, new Vector3(p.x, p.y, p.z), all);
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

            int w = p.width > 0 ? p.width : p.dimX;
            int h = p.height > 0 ? p.height : p.dimY;
            int d = p.depth > 0 ? p.depth : p.dimZ;
            w = Mathf.Max(1, w);
            h = Mathf.Max(1, h);
            d = Mathf.Max(1, d);

            var all = BoardRegistry.GetAll();
            var result = SimulateResizeTo(el, new Vector3Int(w, h, d), all);
            return McpResponse.Result(req.id, result);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>Единая проверка блокировки для мутирующих команд: null — можно
        /// менять; иначе готовый Error с единым текстом (один источник сообщения для
        /// модели во всех move/resize/rotate/delete).</summary>
        private static McpResponse RequireMovable(KitchenElement element, string name, string reqId)
        {
            if (element.Movable) return null;
            return McpResponse.Error(reqId, -1,
                $"Элемент '{name}' заблокирован. Снимите блокировку через set_element_lock (locked:false) — это требует явного разрешения пользователя.");
        }

        /// <summary>Есть ли у элемента нарушения (пересечение / нет связности) в текущей сцене.</summary>
        private static bool HasViolations(KitchenElement element)
        {
            var all = BoardRegistry.GetAll();
            if (all == null || all.Count == 0) return false;
            var vr = ConstraintValidator.Validate(all);
            return vr != null && vr.violations.Contains(element);
        }

        /// <summary>Состояние элемента после мутации: нарушения + AABB + зазоры к соседям.
        /// Единый источник формы ответа для move_element / resize_element.</summary>
        private static (bool hasViolations, AabbInfo aabb, List<AxisGapInfo> gaps) DescribeAfterMutation(KitchenElement element)
        {
            var all = BoardRegistry.GetAll();
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
                string bestNeighbor = null;
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
                        bestNeighbor = other.BoardName;
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
                            overlaps.Add(other.BoardName);
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
                    name = element.BoardName,
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
                            overlaps.Add(other.BoardName);
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
                    name = element.BoardName,
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

        private static GameObject FindGameObject(string path)
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

        private static GameObject FindDescendant(Transform parent, string[] parts, int index)
        {
            if (index >= parts.Length) return parent.gameObject;
            foreach (Transform child in parent)
                if (child.name == parts[index])
                    return FindDescendant(child, parts, index + 1);
            return null;
        }

        private static KitchenElement FindElementByName(string name)
        {
            foreach (var el in BoardRegistry.All)
                if (el != null && (el.BoardName == name || el.name == name))
                    return el;
            return null;
        }
    }
}
