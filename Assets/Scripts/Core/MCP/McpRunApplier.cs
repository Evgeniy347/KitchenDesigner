using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class McpRunApplier
    {
        private const float SameRotationDeg = 0.01f;

        private readonly Func<string, KitchenElement?> _find;
        private readonly Action _settle;
        private readonly string _requestId;
        private readonly ParamsApplyRun _run;
        private readonly McpReference _reference;
        private readonly bool _full;

        private McpRunScene _scene = null!;
        private RunWall _wall;
        private RunLayout _layout = null!;
        private HashSet<string> _owned = null!;
        private List<KitchenElement> _obsolete = new List<KitchenElement>();
        private readonly Dictionary<string, (Vector3 position, Quaternion rotation)> _before =
            new Dictionary<string, (Vector3, Quaternion)>(StringComparer.OrdinalIgnoreCase);

        public McpRunApplier(string requestId, ParamsApplyRun run, McpReference reference, bool full,
            Func<string, KitchenElement?> find, Action settle)
        {
            _requestId = requestId;
            _run = run;
            _reference = reference;
            _full = full;
            _find = find;
            _settle = settle;
        }

        public McpResponse Apply()
        {
            var refusal = Prepare();
            if (refusal != null) return Refuse(refusal);
            var report = McpMutationReport.Begin();
            bool commit = false;
            McpResponse? response = null;
            CommandStack.BeginCapture();
            try
            {
                response = RunTransaction(report, out commit);
            }
            finally
            {
                CommandStack.EndCapture($"MCP apply_run '{_run.id}'", commit);
            }
            _settle();
            return response!;
        }

        private List<string>? Prepare()
        {
            if (string.IsNullOrWhiteSpace(_run.id) || !ElementNaming.IsValid(_run.id))
                return new List<string> { $"id '{_run.id}': {ElementNaming.Rule}" };
            if (!McpRunScene.TryRead(_find(_run.wall), _run.wall, out _scene, out var wallProblem))
                return new List<string> { wallProblem };
            if (!RunRoomSide.TryResolve(_run.wall, _scene.WallBox, _scene.RunAxis, _run.room_side,
                    _scene.Floors, out bool roomOnMaxSide, out var sideProblem))
                return new List<string> { sideProblem };
            _wall = new RunWall(_run.wall, _scene.WallBox, _scene.RunAxis, roomOnMaxSide);
            _owned = McpRunScopes.Owned(_run.id);

            var problems = new List<string>();
            var declaration = Declaration();
            var start = RunStart.Parse(_run.from);
            BoxMm? startPart = null;
            if (start.IsPart) startPart = FindStartPart(start.PartName!, problems);
            problems.AddRange(OwnershipProblems(declaration));
            _layout = RunLayoutBuilder.Build(declaration, _wall, startPart, LiftOffsetMm(),
                ElementNaming.IsValid, name => _find(name) != null);
            problems.AddRange(_layout.Problems);
            if (problems.Count > 0) return problems;
            return LockedProblems();
        }

        private McpResponse Refuse(List<string> problems) =>
            McpResponse.Error(_requestId, -1, "apply_run rejected, NOTHING was applied: "
                + string.Join(" | ", problems) + ". " + McpNameHints.ResendAll);

        private RunDeclaration Declaration()
        {
            var declaration = new RunDeclaration();
            declaration.Id = _run.id;
            declaration.From = _run.from;
            declaration.StartMm = _run.start_mm;
            declaration.GapMm = _run.gap_mm;
            foreach (var module in _run.modules ?? Array.Empty<RunModule>())
            {
                if (module == null) { declaration.Modules.Add(null); continue; }
                var decl = new RunModuleDecl();
                decl.Name = module.name;
                decl.Kind = module.kind;
                decl.WidthMm = module.width_mm;
                decl.HeightMm = module.height_mm;
                decl.DepthMm = module.depth_mm;
                declaration.Modules.Add(decl);
            }
            return declaration;
        }

        private BoxMm? FindStartPart(string name, List<string> problems)
        {
            var part = _find(name);
            if (part == null)
            {
                problems.Add("from: " + McpNameHints.NotFound(name, PartNames()));
                return null;
            }
            if (part == _scene.WallElement)
            {
                problems.Add($"from '{name}' is the wall itself: use from:'left' or from:'right' for a wall end");
                return null;
            }
            return McpRunScene.BoxOf(part);
        }

        private IEnumerable<string> OwnershipProblems(RunDeclaration declaration)
        {
            foreach (var module in declaration.Modules)
            {
                if (module == null || string.IsNullOrEmpty(module.Name)) continue;
                var existing = _find(module.Name);
                if (existing == null || _owned.Contains(module.Name)) continue;
                var owner = McpRunScopes.OwnerOf(module.Name, _run.id);
                yield return owner != null
                    ? $"module '{module.Name}' belongs to run '{owner}': pick another name or change that run"
                    : McpNameHints.AlreadyExists(module.Name, PartNames())
                        + " (a part that is not part of this run cannot be taken over)";
            }
        }

        private float LiftOffsetMm() =>
            _run.base_y_mm.HasValue ? _run.base_y_mm.Value - LevelRegistry.CurrentFloorElevationMm : 0f;

        private List<string>? LockedProblems()
        {
            var problems = new List<string>();
            var declared = new HashSet<string>(_layout.Modules.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
            _obsolete = new List<KitchenElement>();
            foreach (var name in _owned)
            {
                var element = _find(name);
                if (element == null || declared.Contains(name)) continue;
                if (!element.Movable) problems.Add(McpNameHints.Locked(name));
                else _obsolete.Add(element);
            }
            foreach (var module in _layout.Modules)
            {
                var element = _find(module.Name);
                if (element != null && !element.Movable) problems.Add(McpNameHints.Locked(module.Name));
            }
            return problems.Count > 0 ? problems : null;
        }

        private McpResponse RunTransaction(McpMutationReport report, out bool commit)
        {
            commit = false;
            SnapshotExisting();
            DeleteObsolete();
            int resized = ResizeExisting();
            var placer = new McpPlacer(_find);
            placer.IgnoreUntilPlaced(_layout.Modules.Select(m => m.Name));
            for (int i = 0; i < _layout.Steps.Count; i++)
                placer.Place(i, ItemOf(_layout.Steps[i]), _layout.Steps[i].Spec);
            if (placer.Failed)
                return Abort(placer, placer.Errors.ToList());
            var conflicts = RunOpeningConflicts.Find(_wall, PlacedCabinets(placer), _scene.Openings);
            if (conflicts.Count > 0)
                return Abort(placer, conflicts);

            var changed = new List<KitchenElement>(placer.Changed);
            if (_run.dry_run) return Preview(report, placer, changed);
            if (NothingChanges(resized)) return Unchanged(report, placer, changed);

            CommandStack.Execute(new CompositeCommand($"MCP apply_run '{_run.id}' place x{placer.Commands.Count}",
                placer.Commands.ToList()));
            CommandStack.Execute(McpRunScopes.Record(_run.id, _layout.Modules.Select(m => m.Name).ToArray()));
            _settle();
            commit = true;
            var reply = report.Finish(changed, _reference, _full);
            reply.id = _run.id;
            reply.deleted = _obsolete.Count > 0 ? _obsolete.Select(e => e.PartName).ToList() : null;
            reply.dryRun = false;
            reply.applied = true;
            return McpResponse.Result(_requestId, reply);
        }

        private void SnapshotExisting()
        {
            foreach (var module in _layout.Modules)
            {
                var element = _find(module.Name);
                if (element != null) _before[module.Name] = (element.transform.position, element.transform.rotation);
            }
        }

        private void DeleteObsolete()
        {
            if (_obsolete.Count == 0) return;
            var commands = _obsolete.Select(e => (IUndoCommand)new DeleteCommand(e.gameObject)).ToList();
            CommandStack.Execute(new CompositeCommand($"MCP apply_run '{_run.id}' delete x{commands.Count}", commands));
        }

        private int ResizeExisting()
        {
            int resized = 0;
            foreach (var module in _layout.Modules)
            {
                var element = _find(module.Name);
                if (element == null) continue;
                var wanted = new Vector3Int(module.WidthMm, module.HeightMm, module.DepthMm);
                if (element.DimensionsMM == wanted) continue;
                var position = element.transform.position;
                var rotation = element.transform.rotation;
                CommandStack.Execute(new ResizeCommand(element, element.DimensionsMM, wanted,
                    position, position, rotation, rotation));
                resized++;
            }
            return resized;
        }

        private static PlaceItem ItemOf(RunStep step)
        {
            var item = new PlaceItem();
            item.name = step.Spec.Name;
            item.rot_y = step.RotYDeg;
            if (!step.IsNew) return item;
            item.type = "board";
            item.width = step.Size.WidthMm;
            item.height = step.Size.HeightMm;
            item.depth = step.Size.DepthMm;
            return item;
        }

        private List<RunPlaced> PlacedCabinets(McpPlacer placer)
        {
            var placed = new List<RunPlaced>();
            for (int i = 0; i < placer.Changed.Count; i++)
                placed.Add(new RunPlaced(_layout.Steps[i].Size.Name, _layout.Steps[i].Size.Kind,
                    McpRunScene.BoxOf(placer.Changed[i])));
            return placed;
        }

        private bool NothingChanges(int resized)
        {
            if (resized > 0 || _obsolete.Count > 0) return false;
            var declared = _layout.Modules.Select(m => m.Name).ToList();
            if (_layout.Steps.Any(s => s.IsNew) || !_owned.SetEquals(declared)) return false;
            foreach (var module in _layout.Modules)
            {
                var element = _find(module.Name)!;
                var before = _before[module.Name];
                if (Vector3.Distance(element.transform.position, before.position) > Tolerance.ContactUnits) return false;
                if (Quaternion.Angle(element.transform.rotation, before.rotation) > SameRotationDeg) return false;
            }
            return true;
        }

        private McpResponse Unchanged(McpMutationReport report, McpPlacer placer, List<KitchenElement> changed)
        {
            placer.RollBack();
            var reply = report.Finish(changed, _reference, _full);
            reply.id = _run.id;
            reply.unchanged = true;
            reply.dryRun = false;
            reply.applied = false;
            return McpResponse.Result(_requestId, reply);
        }

        private McpResponse Preview(McpMutationReport report, McpPlacer placer, List<KitchenElement> changed)
        {
            _settle();
            var reply = report.Finish(changed, _reference, _full);
            reply.id = _run.id;
            reply.deleted = _obsolete.Count > 0 ? _obsolete.Select(e => e.PartName).ToList() : null;
            reply.dryRun = true;
            reply.applied = false;
            placer.RollBack();
            return McpResponse.Result(_requestId, reply);
        }

        private McpResponse Abort(McpPlacer placer, List<string> problems)
        {
            placer.RollBack();
            return Refuse(problems);
        }

        private static List<string> PartNames()
        {
            var names = new List<string>();
            foreach (var element in PartRegistry.GetAll())
                if (element != null) names.Add(element.PartName);
            return names;
        }
    }
}
