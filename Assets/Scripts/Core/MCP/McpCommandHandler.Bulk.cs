using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Bulk;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    /// <summary>
    /// MCP v2 — массовые/реляционные операции. Агент задаёт НАМЕРЕНИЕ над
    /// выборкой (селектор), а сервер сам подбирает элементы и пересчитывает
    /// геометрию. Ответы терсовые: matched/updated/sceneViolationCount.
    /// </summary>
    public partial class McpCommandHandler
    {
        // ── set_attr: массовое изменение атрибутов по селектору ────────────
        private McpResponse HandleSetAttr(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsSetAttr>();
            if (p == null || string.IsNullOrWhiteSpace(p.selector))
                return McpResponse.Error(req.id, -32602, "selector required");

            var matched = ElementSelector.Match(p.selector);
            var commands = new List<IUndoCommand>();
            int updated = 0;

            foreach (var e in matched)
            {
                var before = e.DimensionsMM;
                var after = before;
                if (p.width.HasValue) after.x = Mathf.Max(1, p.width.Value);
                if (p.height.HasValue) after.y = Mathf.Max(1, p.height.Value);
                if (p.depth.HasValue) after.z = Mathf.Max(1, p.depth.Value);
                if (p.thickness.HasValue) after.z = Mathf.Max(1, p.thickness.Value);

                if (after != before)
                {
                    var pos = e.transform.position;
                    var rot = e.transform.rotation;
                    commands.Add(new ResizeCommand(e, before, after, pos, pos, rot, rot));
                    updated++;
                }

                // Материал/блокировка применяем сразу (не входят в undo-модель геометрии).
                if (!string.IsNullOrEmpty(p.material)) { MaterialManager.ApplyById(e, p.material!); updated++; }
                if (p.locked.HasValue) { e.Movable = !p.locked.Value; updated++; }
            }

            if (commands.Count > 0)
                CommandStack.Execute(new CompositeCommand($"MCP set_attr x{commands.Count}", commands));
            RefreshElementHighlights();
            return TerseResult(req, matched.Count, updated);
        }

        // ── move: сдвиг выборки на дельту (мм) ─────────────────────────────
        private McpResponse HandleMove(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsMove>();
            if (p == null || string.IsNullOrWhiteSpace(p.selector))
                return McpResponse.Error(req.id, -32602, "selector required");

            var delta = new Vector3(p.dx, p.dy, p.dz) * AppConstants.MM_TO_UNITS;
            var matched = ElementSelector.Match(p.selector);
            var commands = new List<IUndoCommand>();

            foreach (var e in matched)
            {
                if (!e.Movable) continue;
                var before = e.transform.position;
                var rot = e.transform.rotation;
                commands.Add(new MoveCommand(e, before, before + delta, rot, rot));
            }

            if (commands.Count > 0)
                CommandStack.Execute(new CompositeCommand($"MCP move x{commands.Count}", commands));
            RefreshElementHighlights();
            return TerseResult(req, matched.Count, commands.Count);
        }

        // ── resize_module: «расширь модуль на N мм» ────────────────────────
        private McpResponse HandleResizeModule(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsResizeModule>();
            if (p == null || string.IsNullOrWhiteSpace(p.module))
                return McpResponse.Error(req.id, -32602, "module (group name) required");

            var members = ResolveGroupMembers(p.module);
            if (members.Count == 0)
                return McpResponse.Error(req.id, -1, $"module '{p.module}' not found or empty");

            char axis = string.IsNullOrEmpty(p.axis) ? 'x' : p.axis[0];
            var plan = ModuleResize.Plan(members, axis, p.delta_mm);
            var commands = new List<IUndoCommand>();

            foreach (var c in plan)
            {
                var e = c.element;
                var posB = e.transform.position;
                var rot = e.transform.rotation;
                if (c.newDimensions != e.DimensionsMM)
                    commands.Add(new ResizeCommand(e, e.DimensionsMM, c.newDimensions, posB, c.newPosition, rot, rot));
                else if (c.newPosition != posB)
                    commands.Add(new MoveCommand(e, posB, c.newPosition, rot, rot));
            }

            if (commands.Count > 0)
                CommandStack.Execute(new CompositeCommand($"MCP resize_module '{p.module}' x{commands.Count}", commands));
            RefreshElementHighlights();
            return TerseResult(req, members.Count, commands.Count);
        }

        // ── get_scene_tree: терсовая иерархия (модули → элементы) ──────────
        private McpResponse HandleGetSceneTree(McpRequest req)
        {
            var all = PartRegistry.GetAll() ?? new List<KitchenElement>();
            var inModule = new HashSet<KitchenElement>();
            var modules = new List<object>();

            foreach (var g in GroupManager.AllGroups())
            {
                var members = GroupManager.MembersOf(g);
                var names = new List<string>();
                Bounds? b = null;
                foreach (var m in members)
                {
                    if (m == null) continue;
                    inModule.Add(m);
                    names.Add(m.PartName);
                    Encapsulate(ref b, m);
                }
                modules.Add(new
                {
                    name = g.name,
                    id = g.id,
                    memberCount = names.Count,
                    members = names,
                    bboxMin = b.HasValue ? V3(b.Value.min) : null,
                    bboxMax = b.HasValue ? V3(b.Value.max) : null,
                });
            }

            var typeCounts = new Dictionary<string, int>();
            var loose = new List<string>();
            int elementCount = 0;
            foreach (var e in all)
            {
                if (e == null || e.GetComponent<BasePlate>() != null) continue;
                elementCount++;
                var t = ElementSelector.TypeOf(e);
                typeCounts[t] = typeCounts.TryGetValue(t, out var n) ? n + 1 : 1;
                if (!inModule.Contains(e)) loose.Add(e.PartName);
            }

            return McpResponse.Result(req.id, new
            {
                moduleCount = modules.Count,
                elementCount,
                typeCounts,
                modules,
                loose,
            });
        }

        private static void Encapsulate(ref Bounds? b, KitchenElement e)
        {
            var r = e.GetComponent<Renderer>();
            var bb = r != null ? r.bounds : new Bounds(e.transform.position, Vector3.zero);
            if (b.HasValue) { var v = b.Value; v.Encapsulate(bb); b = v; }
            else b = bb;
        }

        private static float[] V3(Vector3 v) => new[]
        {
            Mathf.Round(v.x * 1000f) / 1000f,
            Mathf.Round(v.y * 1000f) / 1000f,
            Mathf.Round(v.z * 1000f) / 1000f,
        };

        // ── helpers ────────────────────────────────────────────────────────
        private static List<KitchenElement> ResolveGroupMembers(string groupName)
        {
            foreach (var g in GroupManager.AllGroups())
                if (string.Equals(g.name, groupName, System.StringComparison.OrdinalIgnoreCase))
                    return GroupManager.MembersOf(g);
            return new List<KitchenElement>();
        }

        private McpResponse TerseResult(McpRequest req, int matched, int updated)
        {
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            return McpResponse.Result(req.id, new
            {
                ok = true,
                matched,
                updated,
                sceneViolationCount = vr != null ? vr.violations.Count : 0
            });
        }
    }
}
