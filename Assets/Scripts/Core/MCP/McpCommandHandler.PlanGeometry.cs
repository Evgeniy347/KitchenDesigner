using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        private McpResponse HandleCreateWalls(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsCreateWalls>();
            if (p == null || p.segments == null || p.segments.Length == 0)
                return McpResponse.Error(req.id, -32602, "segments required (non-empty array)");

            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prepared = new List<(WallSegmentMm item, int thickness, string material,
                Vector3Int dims, Vector3 pos, Quaternion rot, KitchenElement? existing)>();
            foreach (var s in p.segments)
            {
                if (s == null || !ElementNaming.IsValid(s.name))
                { errors.Add($"Invalid wall name '{s?.name}': {ElementNaming.Rule}"); continue; }
                if (!seen.Add(s.name)) { errors.Add($"duplicate wall name '{s.name}'"); continue; }
                string kind = (s.kind ?? "").Trim().ToLowerInvariant();
                if (kind != "bearing" && kind != "partition")
                { errors.Add($"Wall '{s.name}': kind must be bearing or partition"); continue; }
                if (s.height <= 0) { errors.Add($"Wall '{s.name}': height must be positive"); continue; }
                string thicknessKey = kind + "_wall_thickness_mm";
                if (!ProjectInstructions.TryGetPositiveMm(thicknessKey, out int thickness))
                { errors.Add($"Wall '{s.name}': project instruction '{thicknessKey}: <positive mm>' is required"); continue; }

                long dx = (long)s.to_x - s.from_x, dz = (long)s.to_z - s.from_z;
                int length = Mathf.RoundToInt(Mathf.Sqrt((float)(dx * dx + dz * dz)));
                if (length <= 0) { errors.Add($"Wall '{s.name}': endpoints must differ"); continue; }
                var existing = FindElementByName(s.name);
                if (existing != null && existing.GetComponent<Wall>() == null)
                { errors.Add($"Element '{s.name}' exists and is not a wall"); continue; }

                float cxMm = p.origin_x_mm + (s.from_x + s.to_x) * 0.5f;
                float czMm = p.origin_z_mm + (s.from_z + s.to_z) * 0.5f;
                var pos = new Vector3(cxMm, p.base_y_mm + s.height * 0.5f, czMm) * AppConstants.MM_TO_UNITS;
                float angle = -Mathf.Atan2((float)dz, (float)dx) * Mathf.Rad2Deg;
                var rot = Quaternion.Euler(0f, angle, 0f);
                string materialKey = kind + "_wall_material";
                string material = ProjectInstructions.TryGetValue(materialKey, out var configured)
                    ? configured : MaterialCatalog.DefaultId;
                prepared.Add((s, thickness, material, new Vector3Int(length, s.height, thickness), pos, rot, existing));
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -32602, "create_walls rejected, NOTHING changed: " + string.Join(" | ", errors));

            var shapes = ComputeWallEndShapes(prepared);
            var commands = new List<IUndoCommand>();
            var affected = new List<KitchenElement>();
            var created = new List<string>();
            int updated = 0;
            for (int i = 0; i < prepared.Count; i++)
            {
                var x = prepared[i];
                if (x.existing == null)
                {
                    var go = ElementFactory.CreateWall(x.dims, x.item.name, x.pos);
                    go.transform.rotation = x.rot;
                    var el = go.GetComponent<KitchenElement>();
                    var wall = go.GetComponent<Wall>(); wall.Kind = x.item.kind.ToLowerInvariant(); wall.SetEndShape(shapes[i]);
                    MaterialManager.ApplyById(el, x.material);
                    commands.Add(new CreateCommand(go)); affected.Add(el); created.Add(el.PartName);
                }
                else
                {
                    commands.Add(new SetWallGeometryCommand(x.existing, x.dims, x.pos, x.rot,
                        x.item.kind.ToLowerInvariant(), x.material, shapes[i]));
                    affected.Add(x.existing); updated++;
                }
            }
            CommandStack.Execute(new CompositeCommand($"MCP create_walls x{commands.Count}", commands));
            return PlanMutationResult(req, created, updated, affected);
        }

        private static List<WallMeshBuilder.EndShape> ComputeWallEndShapes(
            List<(WallSegmentMm item, int thickness, string material, Vector3Int dims,
                Vector3 pos, Quaternion rot, KitchenElement? existing)> walls)
        {
            var result = new List<WallMeshBuilder.EndShape>(walls.Count);
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                float len = w.dims.x;
                result.Add(new WallMeshBuilder.EndShape
                {
                    startFront = -0.5f + JointShift(walls, i, true, 1) / len,
                    startBack = -0.5f + JointShift(walls, i, true, -1) / len,
                    endFront = 0.5f + JointShift(walls, i, false, 1) / len,
                    endBack = 0.5f + JointShift(walls, i, false, -1) / len,
                });
            }
            return result;
        }

        private static float JointShift(
            List<(WallSegmentMm item, int thickness, string material, Vector3Int dims,
                Vector3 pos, Quaternion rot, KitchenElement? existing)> walls,
            int index, bool atStart, int side)
        {
            var a = walls[index];
            var ai = a.item;
            var joint = new Vector2(atStart ? ai.from_x : ai.to_x, atStart ? ai.from_z : ai.to_z);
            var direction = new Vector2(ai.to_x - ai.from_x, ai.to_z - ai.from_z).normalized;
            var normal = new Vector2(-direction.y, direction.x);
            var linePoint = joint + normal * (a.thickness * 0.5f * side);
            bool found = false; float best = 0f;
            for (int j = 0; j < walls.Count; j++)
            {
                if (j == index) continue;
                var bi = walls[j].item;
                bool bStart = bi.from_x == joint.x && bi.from_z == joint.y;
                bool bEnd = bi.to_x == joint.x && bi.to_z == joint.y;
                if (!bStart && !bEnd) continue;
                var bd = new Vector2(bi.to_x - bi.from_x, bi.to_z - bi.from_z).normalized;
                float cross = direction.x * bd.y - direction.y * bd.x;
                if (Mathf.Abs(cross) <= Tolerance.EpsilonUnits) continue;
                int otherSide = atStart == bStart ? -side : side;
                var bn = new Vector2(-bd.y, bd.x);
                var otherPoint = joint + bn * (walls[j].thickness * 0.5f * otherSide);
                var delta = otherPoint - linePoint;
                float shift = (delta.x * bd.y - delta.y * bd.x) / cross;
                if (!found || Mathf.Abs(shift) < Mathf.Abs(best)) { best = shift; found = true; }
            }
            return found ? best : 0f;
        }

        private McpResponse HandleCreateFloorV2(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsCreateFloorV2>();
            if (p == null || !ElementNaming.IsValid(p.name))
                return McpResponse.Error(req.id, -32602, $"Valid name required: {ElementNaming.Rule}");
            if (p.poly == null || p.poly.Length < 3)
                return McpResponse.Error(req.id, -32602, "poly requires at least 3 points");
            int thickness;
            if (p.thickness_mm.HasValue) thickness = p.thickness_mm.Value;
            else if (!ProjectInstructions.TryGetPositiveMm("floor_thickness_mm", out thickness))
                return McpResponse.Error(req.id, -32602,
                    "thickness_mm or project instruction 'floor_thickness_mm: <positive mm>' is required");
            if (thickness <= 0) return McpResponse.Error(req.id, -32602, "thickness_mm must be positive");

            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            foreach (var v in p.poly)
            { minX = Math.Min(minX, v.x); maxX = Math.Max(maxX, v.x); minZ = Math.Min(minZ, v.z); maxZ = Math.Max(maxZ, v.z); }
            if (maxX <= minX || maxZ <= minZ)
                return McpResponse.Error(req.id, -32602, "floor polygon bounds must have positive width and depth");
            int cx = minX + (maxX - minX) / 2, cz = minZ + (maxZ - minZ) / 2;
            var local = new List<Vector2Int>(p.poly.Length);
            foreach (var v in p.poly) local.Add(new Vector2Int(v.x - cx, v.z - cz));
            try
            {
                var probe = FloorPolygonMesh.Build(local, new Vector2Int(maxX - minX, maxZ - minZ));
                if (Application.isPlaying) UnityEngine.Object.Destroy(probe); else UnityEngine.Object.DestroyImmediate(probe);
            }
            catch (ArgumentException ex) { return McpResponse.Error(req.id, -32602, ex.Message); }

            var existing = FindElementByName(p.name);
            if (existing != null && !(existing is FloorElement))
                return McpResponse.Error(req.id, -32602, $"Element '{p.name}' exists and is not a floor");
            var dims = new Vector3Int(maxX - minX, thickness, maxZ - minZ);
            var pos = new Vector3(p.origin_x_mm + cx, p.top_y_mm - thickness * 0.5f,
                p.origin_z_mm + cz) * AppConstants.MM_TO_UNITS;
            var created = new List<string>();
            var affected = new List<KitchenElement>();
            int updated = 0;
            IUndoCommand command;
            if (existing == null)
            {
                var go = ElementFactory.CreateFloor(dims, p.name, pos);
                var floor = go.GetComponent<FloorElement>(); floor.SetPolygonLocalMm(local);
                command = new CreateCommand(go); created.Add(floor.PartName); affected.Add(floor);
            }
            else
            {
                var floor = (FloorElement)existing;
                command = new SetFloorGeometryCommand(floor, dims, pos, local);
                affected.Add(floor); updated = 1;
            }
            CommandStack.Execute(new CompositeCommand("MCP create_floor", new List<IUndoCommand> { command }));
            return PlanMutationResult(req, created, updated, affected);
        }

        private McpResponse PlanMutationResult(McpRequest req, List<string> created, int updated,
            List<KitchenElement> affected)
        {
            RefreshElementHighlights();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var violations = new List<object>();
            foreach (var el in affected) violations.AddRange(BuildElementViolations(el, all, vr));
            return McpResponse.Result(req.id, new { ok = true, created, updated, deleted = 0, violations });
        }
    }
}
