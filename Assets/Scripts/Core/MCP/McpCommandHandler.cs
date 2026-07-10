using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KitchenDesigner.Core.MCP
{
    public class McpCommandHandler
    {
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
                    case "take_screenshot": return HandleTakeScreenshot(request);
                    case "execute_menu_item": return HandleExecuteMenuItem(request);
                    case "enter_play_mode": return HandleEnterPlayMode(request);
                    case "exit_play_mode": return HandleExitPlayMode(request);
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
            var p = JsonConvert.DeserializeObject<ParamsFindObjects>(req.parameters);
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
            var p = JsonConvert.DeserializeObject<ParamsWithName>(req.parameters);
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
            var p = JsonConvert.DeserializeObject<ParamsSetActive>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            go.SetActive(p.active);
            return McpResponse.Result(req.id, new { ok = true, active = p.active });
        }

        private McpResponse HandleDeleteObject(McpRequest req)
        {
            var p = JsonConvert.DeserializeObject<ParamsWithName>(req.parameters);
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
            var p = JsonConvert.DeserializeObject<ParamsSetTransform>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            go.transform.position = new Vector3(p.x, p.y, p.z);
            return McpResponse.Result(req.id, new { ok = true, position = new { p.x, p.y, p.z } });
        }

        private McpResponse HandleSetRotation(McpRequest req)
        {
            var p = JsonConvert.DeserializeObject<ParamsSetTransform>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            go.transform.eulerAngles = new Vector3(p.x, p.y, p.z);
            return McpResponse.Result(req.id, new { ok = true, rotation = new { p.x, p.y, p.z } });
        }

        private McpResponse HandleSetScale(McpRequest req)
        {
            var p = JsonConvert.DeserializeObject<ParamsSetTransform>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.object_path))
                return McpResponse.Error(req.id, -32602, "object_path required");
            var go = FindGameObject(p.object_path);
            if (go == null) return McpResponse.Error(req.id, -1, $"Object not found: {p.object_path}");
            go.transform.localScale = new Vector3(p.x, p.y, p.z);
            return McpResponse.Result(req.id, new { ok = true, scale = new { p.x, p.y, p.z } });
        }

        private McpResponse HandleGetAllElements(McpRequest req)
        {
            var elements = BoardRegistry.GetAll();
            var list = new List<ElementInfo>();
            foreach (var el in elements)
            {
                if (el == null) continue;
                var t = el.transform;
                list.Add(new ElementInfo
                {
                    name = el.BoardName, type = el.GetType().Name,
                    dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                    posX = t.position.x, posY = t.position.y, posZ = t.position.z,
                    rotX = t.eulerAngles.x, rotY = t.eulerAngles.y, rotZ = t.eulerAngles.z,
                    active = el.gameObject.activeInHierarchy
                });
            }
            return McpResponse.Result(req.id, list);
        }

        private McpResponse HandleGetElementInfo(McpRequest req)
        {
            var p = JsonConvert.DeserializeObject<ParamsWithName>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
            var t = element.transform;
            return McpResponse.Result(req.id, new ElementInfo
            {
                name = element.BoardName, type = element.GetType().Name,
                dimX = element.DimensionsMM.x, dimY = element.DimensionsMM.y, dimZ = element.DimensionsMM.z,
                posX = t.position.x, posY = t.position.y, posZ = t.position.z,
                rotX = t.eulerAngles.x, rotY = t.eulerAngles.y, rotZ = t.eulerAngles.z,
                active = element.gameObject.activeInHierarchy
            });
        }

        private McpResponse HandleMoveElement(McpRequest req)
        {
            var p = JsonConvert.DeserializeObject<ParamsMoveElement>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var before = element.transform.position;
            var rotBefore = element.transform.rotation;
            var after = new Vector3(p.x, p.y, p.z);
            CommandStack.Execute(new MoveCommand(element, before, after, rotBefore, element.transform.rotation));
            Debug.Log($"[MCP] Moved {element.BoardName} to ({p.x}, {p.y}, {p.z})");
            return McpResponse.Result(req.id, new { ok = true, element = p.name, position = new { p.x, p.y, p.z } });
        }

        private McpResponse HandleResizeElement(McpRequest req)
        {
            var p = JsonConvert.DeserializeObject<ParamsResizeElement>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var dimsBefore = element.DimensionsMM;
            var posBefore = element.transform.position;
            var rotBefore = element.transform.rotation;
            var dimsAfter = new Vector3Int(Mathf.Max(1, p.width), Mathf.Max(1, p.height), Mathf.Max(1, p.depth));

            CommandStack.Execute(new ResizeCommand(element, dimsBefore, dimsAfter,
                posBefore, posBefore, rotBefore, rotBefore));
            Debug.Log($"[MCP] Resized {element.BoardName} to ({p.width}, {p.height}, {p.depth})mm");
            return McpResponse.Result(req.id, new { ok = true, element = p.name, dimensions = new { p.width, p.height, p.depth } });
        }

        private McpResponse HandleRotateElement(McpRequest req)
        {
            var p = JsonConvert.DeserializeObject<ParamsRotateElement>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");

            var before = element.transform.position;
            var rotBefore = element.transform.rotation;
            var rotAfter = Quaternion.Euler(p.x, p.y, p.z);
            CommandStack.Execute(new MoveCommand(element, before, before, rotBefore, rotAfter));
            Debug.Log($"[MCP] Rotated {element.BoardName} to ({p.x}, {p.y}, {p.z})");
            return McpResponse.Result(req.id, new { ok = true, element = p.name, rotation = new { p.x, p.y, p.z } });
        }

        private McpResponse HandleCreateElement(McpRequest req)
        {
            var p = JsonConvert.DeserializeObject<ParamsCreateElement>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.template_name))
                return McpResponse.Error(req.id, -32602, "template_name required");

            var pos = new Vector3(p.x, p.y, p.z);
            var dims = new Vector3Int(
                p.width > 0 ? p.width : 800,
                p.height > 0 ? p.height : 400,
                p.depth > 0 ? p.depth : 18);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = p.template_name;
            go.transform.position = pos;
            var element = go.AddComponent<KitchenElement>();
            element.BoardName = p.template_name;
            element.DimensionsMM = dims;

            CommandStack.Execute(new CreateCommand(go));
            Debug.Log($"[MCP] Created {go.name} at ({p.x}, {p.y}, {p.z})");
            return McpResponse.Result(req.id, new { ok = true, name = go.name, path = GetGameObjectPath(go) });
        }

        private McpResponse HandleDeleteElement(McpRequest req)
        {
            var p = JsonConvert.DeserializeObject<ParamsWithName>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name required");
            var element = FindElementByName(p.name);
            if (element == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.name}");
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
            var p = JsonConvert.DeserializeObject<ParamsExportCsv>(req.parameters);
            if (p == null || string.IsNullOrEmpty(p.path))
                return McpResponse.Error(req.id, -32602, "path required");
            var spec = SpecificationManager.Build(BoardRegistry.GetAll());
            SpecificationExport.SaveToFile(spec, p.path);
            return McpResponse.Result(req.id, new { ok = true, path = p.path });
        }

        private McpResponse HandleSelectElement(McpRequest req)
        {
            var p = JsonConvert.DeserializeObject<ParamsWithName>(req.parameters);
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
            var p = JsonConvert.DeserializeObject<ParamsLogCount>(req.parameters);
            int count = (p != null && p.count > 0) ? Mathf.Min(p.count, 200) : 50;
            var entries = ConsoleLogCapture.GetRecent(count);
            return McpResponse.Result(req.id, entries);
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

        private McpResponse HandleExecuteMenuItem(McpRequest req)
        {
#if UNITY_EDITOR
            var p = JsonConvert.DeserializeObject<ParamsMenuPath>(req.parameters);
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
