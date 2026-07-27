using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using KitchenDesigner.Core.MCP.Contract;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
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
                platform = Application.platform.ToString(),
                projectInstructions = ProjectInstructions.Text
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

        private McpResponse HandleFindObjects(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsFindObjects>();
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
            var p = req.Params?.ToObjectStrict<ParamsObjectPaths>();
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
            var p = req.Params?.ToObjectStrict<ParamsSetActiveOps>();
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
            var p = req.Params?.ToObjectStrict<ParamsObjectPaths>();
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

        private McpResponse HandleSetPosition(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsTransformOps>();
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
            var p = req.Params?.ToObjectStrict<ParamsTransformOps>();
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
            var p = req.Params?.ToObjectStrict<ParamsTransformOps>();
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

        private McpResponse HandleExecuteMenuItem(McpRequest req)
        {
#if UNITY_EDITOR
            var p = req.Params?.ToObjectStrict<ParamsMenuPath>();
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
    }
}
