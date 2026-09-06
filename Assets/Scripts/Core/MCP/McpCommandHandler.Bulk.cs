using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Bulk;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        private McpResponse HandleSetAttr(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsSetAttr>();
            if (p == null || string.IsNullOrWhiteSpace(p.selector))
                return McpResponse.Error(req.id, -32602, "selector required");

            var matched = ElementSelector.Match(p.selector);
            var commands = new List<IUndoCommand>();
            int updated = 0;
            if (!p.thickness.HasValue && !p.width.HasValue && !p.height.HasValue &&
                !p.depth.HasValue && string.IsNullOrEmpty(p.material) && !p.locked.HasValue)
                return McpResponse.Error(req.id, -32602, "at least one attribute required");
            MaterialDef? material = null;
            if (!string.IsNullOrEmpty(p.material))
            {
                material = MaterialCatalog.Find(p.material!);
                if (material == null)
                    return McpResponse.Error(req.id, -32602, $"Unknown material '{p.material}'");
            }

            foreach (var e in matched)
            {
                var before = e.DimensionsMM;
                var after = before;
                if (p.width.HasValue) after.x = Mathf.Max(1, p.width.Value);
                if (p.height.HasValue) after.y = Mathf.Max(1, p.height.Value);
                if (p.depth.HasValue) after.z = Mathf.Max(1, p.depth.Value);
                if (p.thickness.HasValue) after.z = Mathf.Max(1, p.thickness.Value);

                string afterMaterial = material != null ? material.id : e.MaterialId;
                bool afterMovable = p.locked.HasValue ? !p.locked.Value : e.Movable;
                if (after == before && afterMaterial == e.MaterialId && afterMovable == e.Movable) continue;
                commands.Add(new SetElementAttributesCommand(e, after, afterMaterial, afterMovable));
                updated++;
            }

            if (commands.Count > 0)
                CommandStack.Execute(new CompositeCommand($"MCP set_attr x{commands.Count}", commands));
            RefreshElementHighlights();
            return TerseResult(req, matched.Count, updated);
        }

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
                AttachMove.AppendFollowers(commands, e, before, rot, before + delta, rot, matched);
            }

            if (commands.Count > 0)
                CommandStack.Execute(new CompositeCommand($"MCP move x{commands.Count}", commands));
            RefreshElementHighlights();
            return TerseResult(req, matched.Count, commands.Count);
        }

        private McpResponse HandleResizeModule(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsResizeModule>();
            if (p == null || string.IsNullOrWhiteSpace(p.module))
                return McpResponse.Error(req.id, -32602, "module (group name) required");

            var group = ResolveGroup(p.module);
            var members = group != null ? GroupManager.MembersOf(group) : new List<KitchenElement>();
            if (members.Count == 0)
                return McpResponse.Error(req.id, -1, $"module '{p.module}' not found or empty");

            string axisName = string.IsNullOrEmpty(p.axis) ? group!.widthAxis : p.axis;
            char axis = string.IsNullOrEmpty(axisName) ? 'x' : axisName[0];
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

        private McpResponse HandleGroupV2(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsGroupV2>();
            if (p == null || string.IsNullOrWhiteSpace(p.id) || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "id and names (non-empty array) required");
            string axis = string.IsNullOrEmpty(p.width_axis) ? "x" : p.width_axis.ToLowerInvariant();
            if (axis != "x" && axis != "y" && axis != "z")
                return McpResponse.Error(req.id, -32602, "width_axis must be x, y, or z");
            var members = new List<KitchenElement>();
            var seen = new HashSet<KitchenElement>();
            var errors = new List<string>();
            foreach (var name in p.names)
            {
                var e = FindElementByName(name);
                if (e == null) errors.Add($"Element not found: {name}");
                else if (seen.Add(e)) members.Add(e);
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1, "group rejected, NOTHING changed: " + string.Join(" | ", errors));
            var group = ResolveGroup(p.id);
            bool created = group == null;
            if (group == null) group = GroupManager.Create(p.id);
            var command = new SetGroupCommand(group, created, p.id, axis, members);
            CommandStack.Execute(command);
            return McpResponse.Result(req.id, new
            { ok = true, id = group.id, name = group.name, widthAxis = group.widthAxis, memberCount = members.Count });
        }

        private McpResponse HandleAlignSelection(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsAlignSelection>();
            if (p == null || string.IsNullOrWhiteSpace(p.selector) || string.IsNullOrWhiteSpace(p.target))
                return McpResponse.Error(req.id, -32602, "selector and target required");
            if (!McpWireEnums.TryParseFace(p.face, out int axis, out bool maxSide))
                return McpResponse.Error(req.id, -32602, $"Unknown face '{p.face}'");
            string targetFace = string.IsNullOrEmpty(p.target_face) ? OppositeFace(p.face) : p.target_face;
            if (!McpWireEnums.TryParseFace(targetFace, out int targetAxis, out bool targetMax) || targetAxis != axis)
                return McpResponse.Error(req.id, -32602, "face and target_face must be opposite/same-axis faces");
            var target = FindElementByName(p.target);
            if (target == null) return McpResponse.Error(req.id, -32602, $"Target not found: {p.target}");
            var matched = ElementSelector.Match(p.selector);
            if (matched.Count == 0) return McpResponse.Error(req.id, -1, "selector matched no elements");

            var units = new List<List<KitchenElement>>();
            var groupsSeen = new HashSet<int>();
            var looseSeen = new HashSet<KitchenElement>();
            foreach (var e in matched)
            {
                if (e == target) continue;
                var g = GroupManager.GroupOf(e);
                if (g != null)
                {
                    if (!groupsSeen.Add(g.id)) continue;
                    var members = GroupManager.MembersOf(g);
                    if (members.Contains(target))
                        return McpResponse.Error(req.id, -1, $"Target '{p.target}' belongs to selected module '{g.name}'");
                    units.Add(members);
                }
                else if (looseSeen.Add(e)) units.Add(new List<KitchenElement> { e });
            }
            if (units.Count == 0) return McpResponse.Error(req.id, -1, "selector only matched the target");
            foreach (var unit in units)
                foreach (var e in unit)
                    if (!e.Movable) return McpResponse.Error(req.id, -1,
                        $"align rejected, NOTHING moved: element '{e.PartName}' is LOCKED");

            var targetAabb = McpAabb.Of(target.GetVertices());
            float targetCoord = McpAabb.Side(targetAabb, targetAxis, targetMax);
            float gap = p.gap_mm * AppConstants.MM_TO_UNITS;
            var commands = new List<IUndoCommand>();
            foreach (var unit in units)
            {
                var vertices = new List<Vector3>();
                foreach (var e in unit) vertices.AddRange(e.GetVertices());
                var bounds = McpAabb.Of(vertices.ToArray());
                float movingCoord = McpAabb.Side(bounds, axis, maxSide);
                float desired = maxSide ? targetCoord - gap : targetCoord + gap;
                float delta = desired - movingCoord;
                foreach (var e in unit)
                {
                    var before = e.transform.position; var after = before; after[axis] += delta;
                    var rot = e.transform.rotation;
                    commands.Add(new MoveCommand(e, before, after, rot, rot));
                    AttachMove.AppendFollowers(commands, e, before, rot, after, rot, unit);
                }
            }
            CommandStack.Execute(new CompositeCommand($"MCP align x{units.Count} units", commands));
            return TerseResult(req, matched.Count, commands.Count);
        }

        private static string OppositeFace(string face) => face switch
        {
            "left" => "right", "right" => "left", "bottom" => "top",
            "top" => "bottom", "back" => "front", "front" => "back", _ => ""
        };

        private McpResponse HandleGetSceneTree(McpRequest req)
        {
            var all = PartRegistry.GetAll() ?? new List<KitchenElement>();
            var inModule = new HashSet<KitchenElement>();
            var modules = new List<object>();
            var moduleCenters = new Dictionary<string, Vector3>();

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
                    widthAxis = g.widthAxis,
                    memberCount = names.Count,
                    members = names,
                    bboxMinMm = b.HasValue ? V3Mm(b.Value.min) : null,
                    bboxMaxMm = b.HasValue ? V3Mm(b.Value.max) : null,
                });
                if (b.HasValue) moduleCenters[g.name] = b.Value.center;
            }

            var typeCounts = new Dictionary<string, int>();
            var loose = new List<string>();
            var looseElements = new List<KitchenElement>();
            int elementCount = 0;
            foreach (var e in all)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                if (e.GetComponent<BasePlate>() != null) continue;
                elementCount++;
                var t = ElementSelector.TypeOf(e);
                typeCounts[t] = typeCounts.TryGetValue(t, out var n) ? n + 1 : 1;
                if (!inModule.Contains(e)) { loose.Add(e.PartName); looseElements.Add(e); }
            }

            var rooms = new List<object>();
            foreach (var room in ProjectRooms.Items)
            {
                var roomModules = new List<string>();
                foreach (var pair in moduleCenters) if (RoomContains(room, pair.Value)) roomModules.Add(pair.Key);
                var furniture = new List<string>();
                foreach (var e in looseElements)
                {
                    string type = ElementSelector.TypeOf(e);
                    if (type == "wall" || type == "floor" || type == "window" || type == "door") continue;
                    if (RoomContains(room, e.transform.position)) furniture.Add(e.PartName);
                }
                rooms.Add(new
                {
                    id = room.id, floor = room.floor, walls = room.walls, openings = room.openings,
                    modules = roomModules, furniture
                });
            }

            return McpResponse.Result(req.id, new
            {
                roomCount = rooms.Count,
                moduleCount = modules.Count,
                elementCount,
                typeCounts,
                modules,
                loose,
                rooms,
            });
        }

        private static bool RoomContains(RoomData room, Vector3 world)
        {
            var poly = room.polygonXZ;
            if (poly == null || poly.Length < 6 || poly.Length % 2 != 0) return false;
            float x = world.x / AppConstants.MM_TO_UNITS, z = world.z / AppConstants.MM_TO_UNITS;
            bool inside = false;
            int count = poly.Length / 2;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float xi = poly[i * 2], zi = poly[i * 2 + 1];
                float xj = poly[j * 2], zj = poly[j * 2 + 1];
                bool crosses = (zi > z) != (zj > z) &&
                    x < (xj - xi) * (z - zi) / (zj - zi) + xi;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private McpResponse HandleGetCompact(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsGetCompact>();
            if (p == null || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "names required (non-empty array)");
            var allowed = new HashSet<string>(new[]
                { "name", "kind", "anchorMm", "sizeMm", "rotYDeg", "hasViolations", "module", "wallKind" },
                System.StringComparer.OrdinalIgnoreCase);
            var fields = p.fields != null && p.fields.Length > 0
                ? new HashSet<string>(p.fields, System.StringComparer.OrdinalIgnoreCase) : allowed;
            foreach (var f in fields) if (!allowed.Contains(f))
                return McpResponse.Error(req.id, -32602, $"Unknown compact field '{f}'");
            var all = PartRegistry.GetAll();
            var validation = all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var elements = new List<Dictionary<string, object?>>();
            var missing = new List<string>();
            foreach (var name in p.names)
            {
                var e = FindElementByName(name);
                if (e == null) { missing.Add(name); continue; }
                var row = new Dictionary<string, object?>();
                var wall = e.GetComponent<Wall>();
                var aabb = McpAabb.Of(e.GetVertices());
                Vector3 anchor = new Vector3(aabb.minX, aabb.minY, aabb.minZ);
                if (fields.Contains("name")) row["name"] = e.PartName;
                if (fields.Contains("kind")) row["kind"] = ElementSelector.TypeOf(e);
                if (fields.Contains("anchorMm")) row["anchorMm"] = new[]
                    { Mathf.RoundToInt(anchor.x / AppConstants.MM_TO_UNITS), Mathf.RoundToInt(anchor.z / AppConstants.MM_TO_UNITS) };
                if (fields.Contains("sizeMm")) row["sizeMm"] = new[] { e.DimensionsMM.x, e.DimensionsMM.z, e.DimensionsMM.y };
                if (fields.Contains("rotYDeg")) row["rotYDeg"] = Mathf.Round(e.transform.eulerAngles.y * 10f) / 10f;
                if (fields.Contains("hasViolations")) row["hasViolations"] = validation != null && validation.violations.Contains(e);
                if (fields.Contains("module")) row["module"] = GroupManager.GroupOf(e)?.name;
                if (fields.Contains("wallKind")) row["wallKind"] = wall?.Kind;
                elements.Add(row);
            }
            return McpResponse.Result(req.id, new { count = elements.Count, elements, missing = missing.Count > 0 ? missing : null });
        }

        private McpResponse HandlePreviewFloorplan(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsFloorplanDeclaration>();
            var compiled = FloorplanCompiler.Compile(p);
            if (!compiled.IsValid)
                return McpResponse.Error(req.id, -32602,
                    "floorplan invalid: " + string.Join(" | ", compiled.errors));
            return McpResponse.Result(req.id, new
            {
                ok = true,
                svg = FloorplanCompiler.ToSvg(compiled),
                points = compiled.points.Count,
                walls = compiled.walls.Count,
                floors = compiled.floors.Count,
                openings = compiled.openings.Count,
                rooms = compiled.rooms.Count
            });
        }

        private static void Encapsulate(ref Bounds? b, KitchenElement e)
        {
            var r = e.GetComponent<Renderer>();
            var bb = r != null ? r.bounds : new Bounds(e.transform.position, Vector3.zero);
            if (b.HasValue) { var v = b.Value; v.Encapsulate(bb); b = v; }
            else b = bb;
        }

        private static int[] V3Mm(Vector3 v) => new[]
            { Mathf.RoundToInt(v.x / AppConstants.MM_TO_UNITS), Mathf.RoundToInt(v.y / AppConstants.MM_TO_UNITS), Mathf.RoundToInt(v.z / AppConstants.MM_TO_UNITS) };

        private static LinkGroup? ResolveGroup(string groupName)
        {
            foreach (var g in GroupManager.AllGroups())
                if (string.Equals(g.name, groupName, System.StringComparison.OrdinalIgnoreCase))
                    return g;
            return null;
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
