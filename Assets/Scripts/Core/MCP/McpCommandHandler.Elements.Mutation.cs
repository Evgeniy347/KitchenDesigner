using System;
using System.Collections.Generic;
using System.Linq;
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
        private static void ApplyGapEdits(EditOp op, KitchenElement el)
        {
            if (!el.SupportsGaps) return;
            if (op.gap_left.HasValue) el.GapLeft = op.gap_left.Value;
            if (op.gap_right.HasValue) el.GapRight = op.gap_right.Value;
            if (op.gap_top.HasValue) el.GapTop = op.gap_top.Value;
            if (op.gap_bottom.HasValue) el.GapBottom = op.gap_bottom.Value;
            if (op.gap_front.HasValue) el.GapFront = op.gap_front.Value;
            if (op.gap_back.HasValue) el.GapBack = op.gap_back.Value;
        }

        private static void ApplyWholeSetEdits(EditOp op, KitchenElement el)
        {
            if (op.grooves != null && el.SupportsGrooves
                && McpSpecCodec.TryParseGrooves(op.grooves, out var parsedGrooves, out _))
                el.SetGrooves(parsedGrooves);
            if (op.texture_overlays != null && el.SupportsTextureOverlays
                && McpSpecCodec.TryParseTextureOverlays(op.texture_overlays, out var parsedOverlays, out _))
                el.SetTextureOverlays(parsedOverlays);
        }

        private static void ApplyEdgeEdits(EditOp op, KitchenElement el)
        {
            if (!el.SupportsGrooves) return;
            if (op.edge_banding.HasValue) el.EdgeBandingEnabled = op.edge_banding.Value;
            if (op.edge_thickness_mm.HasValue) el.EdgeThicknessMM = op.edge_thickness_mm.Value;
            if (op.edge_skip_validation.HasValue)
                el.EdgeManualMask = op.edge_skip_validation.Value ? EdgeManual.AllMask : 0;
        }

        private void ApplyNonGeometryEdits(
            List<(EditOp op, KitchenElement el, MaterialDef? material, List<string> warnings)> resolved)
        {
            foreach (var (op, el, mat, _) in resolved)
            {
                if (op.locked.HasValue) el.Movable = !op.locked.Value;
                if (op.transparent.HasValue) el.Transparent = op.transparent.Value;
                if (mat != null)
                {
                    MaterialManager.Apply(el, mat);
                    SelectionManager.Instance?.RefreshHighlight(el);
                }
                ApplyGapEdits(op, el);
                ApplyWholeSetEdits(op, el);
                ApplyEdgeEdits(op, el);
                if (op.attached_to_name != null && AttachLinks.CanBeChild(el))
                    el.AttachedToName = op.attached_to_name;
                ElementEditAppliers.ApplyTypeSpecific(op, el);
                if (op.new_name != null && op.new_name != el.PartName)
                {
                    DrawerLinks.Rename(el, op.new_name);
                    el.gameObject.name = el.PartName;
                }
            }
        }

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

        private McpResponse HandleEditElements(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsEditElements>();
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
                foreach (var err in EditFieldRules.Reject(op, el, FindElementByName))
                    errors.Add($"Invalid field for '{op.name}': {err}");
                if (op.new_name != null && op.new_name != op.name)
                {
                    if (!ElementNaming.IsValid(op.new_name))
                    { errors.Add($"Invalid new_name '{op.new_name}' (for '{op.name}'): {ElementNaming.Rule}"); continue; }
                    var conflict = PartRegistry.All?.FirstOrDefault(x => x != null && x != el && string.Equals(x.PartName, op.new_name, StringComparison.OrdinalIgnoreCase));
                    if (conflict != null) errors.Add($"new_name '{op.new_name}' already taken by another element (for '{op.name}')");
                    else if (!newNamesBatch.Add(op.new_name)) errors.Add($"Duplicate new_name '{op.new_name}' in this batch (for '{op.name}')");
                }
                MaterialDef? mat = null;
                if (!string.IsNullOrEmpty(op.material))
                {
                    mat = MaterialCatalog.Find(op.material!);
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
                    var dimsAfter = ResolveDims(op.width, op.height, op.depth, op.dimX, op.dimY, op.dimZ, el.DimensionsMM);
                    commands.Add(new ResizeCommand(el, el.DimensionsMM, dimsAfter, posBefore, posAfter, rotBefore, rotAfter));
                }
                else commands.Add(new MoveCommand(el, posBefore, posAfter, rotBefore, rotAfter));
                AttachMove.AppendFollowers(commands, el, posBefore, rotBefore, posAfter, rotAfter);
            }
            var composite = new CompositeCommand($"MCP edit_elements ({resolved.Count} ops)", commands);
            if (p.dry_run)
            {
                composite.Execute();
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

        private McpResponse HandleCloneElements(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsCloneElements>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var commands = new List<IUndoCommand>();
            var allClones = new List<KitchenElement>();
            var errors = new List<string>();
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
                    go.name = el.PartName;
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
            foreach (var el in allClones) { created.Add(el.PartName); elements.Add(ElementInfoBuilder.Build(el, all, false, vr)); }
            Debug.Log($"[MCP] Clone batch: {p.ops.Length} sources → {allClones.Count} clones");
            return McpResponse.Result(req.id, new { ok = true, created, elements, sceneViolationCount = vr != null ? vr.violations.Count : 0 });
        }

        private McpResponse HandleAlignElements(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsAlignElements>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var commands = new List<IUndoCommand>();
            var errors = new List<string>();
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.name) || string.IsNullOrEmpty(op.target))
                { errors.Add("op missing name or target"); continue; }
                if (!McpWireEnums.TryParseFace(op.face, out int axis, out bool maxSide))
                { errors.Add($"Unknown face '{op.face}' for '{op.name}'"); continue; }
                if (!McpWireEnums.TryParseFace(op.target_face, out int tAxis, out bool tMaxSide))
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

                var elAabb = McpAabb.Of(element.GetVertices());
                var tAabb = McpAabb.Of(target.GetVertices());
                float myCoord = McpAabb.Side(elAabb, axis, maxSide);
                float targetCoord = McpAabb.Side(tAabb, axis, tMaxSide);
                float gapUnits = op.gap_mm * AppConstants.MM_TO_UNITS;
                float desired = maxSide ? targetCoord - gapUnits : targetCoord + gapUnits;
                float delta = desired - myCoord;
                var before = element.transform.position;
                var after = before;
                after[axis] += delta;
                after = MmGrid.SnapPosition(element, after);
                var alignRot = element.transform.rotation;
                commands.Add(new MoveCommand(element, before, after, alignRot, alignRot));
                AttachMove.AppendFollowers(commands, element, before, alignRot, after, alignRot);
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

        private McpResponse HandleDistributeEvenly(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsDistributeEvenly>();
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
                after = MmGrid.SnapPosition(el, after);
                var rot = el.transform.rotation;
                commands.Add(new MoveCommand(el, before, after, rot, rot));
                AttachMove.AppendFollowers(commands, el, before, rot, after, rot, resolved);
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

        private static void SnapOpeningsOnceEveryWallOfTheBatchIsRegistered(List<KitchenElement> created)
        {
            foreach (var el in created)
            {
                if (el is WindowElement window) window.SnapToWall();
                else if (el is DoorElement door) door.SnapToWall();
            }
        }

        private List<(CreateItem item, string type)> AcceptCreateItems(CreateItem[] items, List<string> errors)
        {
            var accepted = new List<(CreateItem item, string type)>();
            var namesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.name)) { errors.Add("an item is missing 'name'"); continue; }
                if (!ElementNaming.IsValid(item.name))
                { errors.Add($"Invalid name '{item.name}': {ElementNaming.Rule}"); continue; }
                if (namesSeen.Contains(item.name)) { errors.Add($"duplicate name '{item.name}' in this batch"); continue; }
                namesSeen.Add(item.name);
                var existing = FindElementByName(item.name);
                if (existing != null) { errors.Add($"Element '{item.name}' already exists"); continue; }

                var elementType = (item.type ?? "board").Trim().ToLowerInvariant();
                if (!ElementSpawners.CanSpawn(elementType))
                {
                    errors.Add($"Unknown type '{elementType}' for '{item.name}' "
                        + $"(allowed: {string.Join(", ", ElementSpawners.SpawnableTypes)})");
                    continue;
                }
                if (!string.IsNullOrEmpty(item.model))
                {
                    if (!ApplianceModels.IsKnown(item.model))
                    { errors.Add($"Unknown appliance model '{item.model}' for '{item.name}'"); continue; }
                    if (!ElementSpawners.ModelBelongsToType(elementType, item.model!))
                    { errors.Add($"Model '{item.model}' does not belong to type '{elementType}' ('{item.name}')"); continue; }
                }

                accepted.Add((item, elementType));
            }
            return accepted;
        }

        private McpResponse HandleCreateElements(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsCreateElements>();
            if (p == null || p.items == null || p.items.Length == 0)
                return McpResponse.Error(req.id, -32602, "items required (non-empty array)");

            var errors = new List<string>();
            var accepted = AcceptCreateItems(p.items, errors);
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "create_elements rejected, NOTHING was created: " + string.Join(" | ", errors));

            var commands = new List<IUndoCommand>();
            var created = new List<KitchenElement>();
            foreach (var (item, elementType) in accepted)
            {
                var go = ElementSpawners.Spawn(elementType, item, new Vector3(item.x, item.y, item.z));
                commands.Add(new CreateCommand(go));
                created.Add(go.GetComponent<KitchenElement>());
            }

            if (commands.Count > 0)
                CommandStack.Execute(new CompositeCommand($"MCP create_elements x{commands.Count}", commands));

            SnapOpeningsOnceEveryWallOfTheBatchIsRegistered(created);
            RefreshElementHighlights();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var elements = new List<ElementInfo>();
            var createdNames = new List<string>();
            foreach (var el in created) { createdNames.Add(el.PartName); elements.Add(ElementInfoBuilder.Build(el, all, false, vr)); }
            Debug.Log($"[MCP] Created {created.Count} elements: {string.Join(", ", createdNames)}");
            return McpResponse.Result(req.id, new { ok = true, created = createdNames, elements, sceneViolationCount = vr != null ? vr.violations.Count : 0 });
        }

        private McpResponse HandleConvertElements(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsConvertElements>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var errors = new List<string>();
            var results = new List<KitchenElement>();
            foreach (var op in p.ops)
            {
                if (string.IsNullOrEmpty(op.name)) { errors.Add("op missing name"); continue; }
                if (!McpWireEnums.TryParseConvertTarget(op.target, out var target))
                { errors.Add($"Unknown target '{op.target}' for '{op.name}'"); continue; }
                var element = FindElementByName(op.name);
                if (element == null) { errors.Add($"Element not found: {op.name}"); continue; }
                var lockErr = RequireMovable(element, op.name, req.id);
                if (lockErr != null) { errors.Add($"'{op.name}' is LOCKED"); continue; }
                var converted = ElementConverter.Convert(element, target);
                if (converted is AssembledFacadeElement assembled && !string.IsNullOrEmpty(op.fill))
                    assembled.Fill = McpWireEnums.ParseFill(op.fill);
                results.Add(converted);
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1, "convert_elements rejected: " + string.Join(" | ", errors));

            RefreshElementHighlights();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var elements = new List<ElementInfo>();
            var names = new List<string>();
            foreach (var el in results) { names.Add(el.PartName); elements.Add(ElementInfoBuilder.Build(el, all, false, vr)); }
            Debug.Log($"[MCP] Converted {results.Count} elements");
            return McpResponse.Result(req.id, new { ok = true, converted = names, elements, sceneViolationCount = vr != null ? vr.violations.Count : 0 });
        }

        private McpResponse HandleDeleteElements(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsNames>();
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

        private McpResponse HandleSelectElements(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsNames>();
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

        private McpResponse HandleResizeFloor(McpRequest req)
        {
            var plate = FindFloor();
            if (plate == null || plate.Element == null)
                return McpResponse.Error(req.id, -1, "Floor not found");

            var p = req.Params?.ToObjectStrict<ParamsResizeFloor>();
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
    }
}
