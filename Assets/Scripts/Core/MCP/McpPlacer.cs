using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class McpPlacer
    {
        private static readonly Dictionary<string, string> RefusedTypes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["window"] = "use add_opening: it attaches the window to a wall and cuts the hole",
                ["door"] = "use add_opening: it attaches the door to a wall and cuts the hole",
                ["wall"] = "use create_walls (walls are declared by their end points)",
                ["floor"] = "use create_floor (a floor is declared by its polygon)",
            };

        private readonly Func<string, KitchenElement?> _find;
        private readonly List<string> _errors = new List<string>();
        private readonly List<Action> _rollbacks = new List<Action>();
        private readonly List<IUndoCommand> _commands = new List<IUndoCommand>();
        private readonly List<KitchenElement> _changed = new List<KitchenElement>();
        private readonly HashSet<string> _failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public McpPlacer(Func<string, KitchenElement?> find)
        {
            _find = find;
        }

        public IReadOnlyList<string> Errors => _errors;

        public IReadOnlyList<IUndoCommand> Commands => _commands;

        public List<KitchenElement> Changed => _changed;

        public bool Failed => _errors.Count > 0;

        public void Place(int index, PlaceItem item)
        {
            string tag = $"item {index + 1} '{item.name}'";
            if (RefersToAFailedItem(item, out var broken))
            {
                Reject(item, $"{tag}: skipped - it refers to '{broken}', which was rejected above");
                return;
            }

            var work = Prepare(tag, item);
            if (work == null)
            {
                _failed.Add(item.name);
                return;
            }

            var outcome = Solve(item, work);
            if (!outcome.Ok)
            {
                work.Undo();
                foreach (var problem in outcome.Problems) _errors.Add($"{tag}: {problem}");
                _failed.Add(item.name);
                return;
            }
            Commit(work, outcome.MinMm);
        }

        public void RollBack()
        {
            for (int i = _rollbacks.Count - 1; i >= 0; i--) _rollbacks[i]();
            _rollbacks.Clear();
            _commands.Clear();
            _changed.Clear();
        }

        private void Reject(PlaceItem item, string error)
        {
            _errors.Add(error);
            _failed.Add(item.name);
        }

        private bool RefersToAFailedItem(PlaceItem item, out string broken)
        {
            broken = string.Empty;
            if (_failed.Count == 0) return false;
            var targets = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.on)) targets.Add(item.on!.Trim());
            foreach (var a in item.against ?? Array.Empty<PlaceAgainstOp>()) targets.Add(a.target);
            foreach (var a in item.align ?? Array.Empty<PlaceAlignOp>()) targets.Add(a.target);
            foreach (var target in targets)
                if (target != null && _failed.Contains(target)) { broken = target; return true; }
            return false;
        }

        private PlaceWork? Prepare(string tag, PlaceItem item)
        {
            if (string.IsNullOrWhiteSpace(item.name))
            {
                _errors.Add($"{tag}: the item has no name");
                return null;
            }
            var existing = _find(item.name);
            return existing != null ? PrepareExisting(tag, item, existing) : Spawn(tag, item);
        }

        private PlaceWork? PrepareExisting(string tag, PlaceItem item, KitchenElement existing)
        {
            if (!existing.Movable)
            {
                _errors.Add($"{tag}: " + McpNameHints.Locked(item.name));
                return null;
            }
            if (item.type != null || item.width.HasValue || item.height.HasValue || item.depth.HasValue)
            {
                _errors.Add($"{tag}: '{item.name}' already exists - type and size apply to NEW parts only. "
                    + "Resize it with edit_elements, or give the new part another name");
                return null;
            }
            var work = new PlaceWork(existing, false);
            if (item.rot_y.HasValue)
            {
                var euler = existing.transform.eulerAngles;
                existing.transform.rotation = Quaternion.Euler(euler.x, item.rot_y.Value, euler.z);
            }
            return work;
        }

        private PlaceWork? Spawn(string tag, PlaceItem item)
        {
            if (!ElementNaming.IsValid(item.name))
            {
                _errors.Add($"{tag}: invalid name. {ElementNaming.Rule}");
                return null;
            }
            string type = (item.type ?? "board").Trim().ToLowerInvariant();
            if (RefusedTypes.TryGetValue(type, out var advice))
            {
                _errors.Add($"{tag}: type '{type}' cannot be placed here - {advice}");
                return null;
            }
            if (!ElementSpawners.CanSpawn(type))
            {
                _errors.Add($"{tag}: unknown type '{type}' (allowed: {string.Join(", ", ElementSpawners.SpawnableTypes)})");
                return null;
            }
            var create = new CreateItem();
            create.name = item.name;
            create.type = type;
            create.width = item.width;
            create.height = item.height;
            create.depth = item.depth;
            var fixedHeight = ElementSpawners.FixedHeightRefusal(type, create);
            if (fixedHeight != null)
            {
                _errors.Add($"{tag}: {fixedHeight}");
                return null;
            }
            var go = ElementSpawners.Spawn(type, create, Vector3.zero);
            var element = go.GetComponent<KitchenElement>();
            PartRegistry.Register(element);
            element.LevelId = LevelRegistry.CurrentId;
            if (item.rot_y.HasValue) go.transform.rotation = Quaternion.Euler(0f, item.rot_y.Value, 0f);
            return new PlaceWork(element, true);
        }

        private PlaceOutcome Solve(PlaceItem item, PlaceWork work)
        {
            var element = work.Element;
            var box = McpAnchor.ToMmBoxStruct(McpAabb.Of(element.GetVertices()));
            var scene = new List<NeighbourBox>();
            foreach (var other in PartRegistry.GetAll())
                if (other != null && other != element)
                    scene.Add(new NeighbourBox(other.PartName,
                        McpAnchor.ToMmBoxStruct(McpAabb.Of(other.GetVertices()))));
            float ground = work.IsNew
                ? LevelRegistry.CurrentFloorElevationMm
                : LevelRegistry.LevelOf(element).floorElevationMm;
            Vector3? current = work.IsNew ? (Vector3?)null : box.Min;
            return PlaceSolver.Solve(item.name, ToSpec(item), box.Size, current, scene, ground);
        }

        private static PlaceSpec ToSpec(PlaceItem item)
        {
            var spec = new PlaceSpec();
            spec.Name = item.name;
            spec.On = item.on;
            spec.LiftMm = item.lift_mm;
            foreach (var a in item.against ?? Array.Empty<PlaceAgainstOp>())
                spec.Against.Add(new PlaceAgainstSpec(a.target, a.face, a.gap_mm));
            foreach (var a in item.align ?? Array.Empty<PlaceAlignOp>())
                spec.Align.Add(new PlaceAlignSpec(a.target, a.axis, a.at, a.offset_mm));
            return spec;
        }

        private void Commit(PlaceWork work, Vector3 minMm)
        {
            var element = work.Element;
            McpAnchor.PlaceRefPointAt(element, McpReference.MinCorner, McpAnchor.FromMm(minMm.x, minMm.y, minMm.z));
            _rollbacks.Add(work.Undo);
            _changed.Add(element);
            if (work.IsNew)
            {
                _commands.Add(new CreateCommand(element.gameObject));
                return;
            }
            var rotAfter = element.transform.rotation;
            var posAfter = element.transform.position;
            _commands.Add(new MoveCommand(element, work.PositionBefore, posAfter, work.RotationBefore, rotAfter));
            AttachMove.AppendFollowers(_commands, element, work.PositionBefore, work.RotationBefore, posAfter, rotAfter);
        }
    }
}
