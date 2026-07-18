using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
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
    }
}
