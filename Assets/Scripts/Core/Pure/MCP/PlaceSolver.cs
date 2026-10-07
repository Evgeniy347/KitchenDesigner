using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class PlaceSolver
    {
        public const string FloorWord = "floor";

        private readonly string _name;
        private readonly PlaceItem _item;
        private readonly Vector3 _size;
        private readonly IReadOnlyList<NeighbourBox> _scene;
        private readonly Dictionary<string, BoxMm> _byName;
        private readonly float?[] _pins = new float?[3];
        private readonly string[] _why = new string[3];
        private readonly PlaceOutcome _outcome = new PlaceOutcome();

        private PlaceSolver(string name, PlaceItem item, Vector3 sizeMm, IReadOnlyList<NeighbourBox> scene)
        {
            _name = name;
            _item = item;
            _size = sizeMm;
            _scene = scene;
            _byName = new Dictionary<string, BoxMm>(StringComparer.OrdinalIgnoreCase);
            foreach (var other in scene) _byName[other.Name] = other.Box;
        }

        public static PlaceOutcome Solve(string name, PlaceItem item, Vector3 sizeMm, Vector3? currentMinMm,
            IReadOnlyList<NeighbourBox> sceneWithoutSelf, float groundMm)
        {
            var solver = new PlaceSolver(name, item, sizeMm, sceneWithoutSelf);
            solver.PinEverything(currentMinMm, groundMm);
            solver.CheckNothingOverlaps();
            return solver._outcome;
        }

        public static string FaceToStandBy(BoxMm ours, BoxMm theirs)
        {
            int axis = 0;
            float least = float.MaxValue;
            for (int a = 0; a < 3; a++)
            {
                float overlap = Mathf.Min(ours.Max[a], theirs.Max[a]) - Mathf.Max(ours.Min[a], theirs.Min[a]);
                if (overlap < least) { least = overlap; axis = a; }
            }
            return McpFace.NameOf(axis, ours.Center[axis] >= theirs.Center[axis]);
        }

        private void PinEverything(Vector3? current, float groundMm)
        {
            PinOn(groundMm);
            foreach (var op in _item.against ?? Array.Empty<PlaceAgainstOp>()) PinAgainst(op);
            foreach (var op in _item.align ?? Array.Empty<PlaceAlignOp>()) PinAlign(op);
            if (string.IsNullOrWhiteSpace(_item.on) && !_pins[1].HasValue && !current.HasValue)
                Pin(1, groundMm + _item.lift_mm, "the floor (default on:\"floor\")");
            bool clean = _outcome.Ok;
            for (int axis = 0; axis < 3; axis++)
            {
                if (_pins[axis].HasValue) continue;
                if (current.HasValue) _pins[axis] = current.Value[axis];
                else if (clean) _outcome.Problems.Add(MissingAxis(axis));
            }
            if (_outcome.Ok)
                _outcome.MinMm = new Vector3(_pins[0].GetValueOrDefault(), _pins[1].GetValueOrDefault(), _pins[2].GetValueOrDefault());
        }

        private void PinOn(float groundMm)
        {
            if (string.IsNullOrWhiteSpace(_item.on)) return;
            var on = _item.on!.Trim();
            if (string.Equals(on, FloorWord, StringComparison.OrdinalIgnoreCase))
            {
                Pin(1, groundMm + _item.lift_mm, "on:\"floor\"");
                return;
            }
            if (TryTarget(on, "on", out var box))
                Pin(1, box.Max.y + _item.lift_mm, $"on:'{on}' (its top)");
        }

        private void PinAgainst(PlaceAgainstOp op)
        {
            if (!McpFace.TryParse(op.face, out int axis, out bool maxSide))
            {
                _outcome.Problems.Add($"against '{op.target}': " + McpNameHints.UnknownFace(op.face));
                return;
            }
            if (op.gap_mm < 0f)
            {
                _outcome.Problems.Add($"against '{op.target}': gap_mm {Fmt(op.gap_mm)} is negative - use 0 for flush contact");
                return;
            }
            if (!TryTarget(op.target, "against", out var box)) return;
            float min = maxSide ? box.Max[axis] + op.gap_mm : box.Min[axis] - op.gap_mm - _size[axis];
            Pin(axis, min, $"against '{op.target}' face {McpFace.NameOf(axis, maxSide)}");
        }

        private void PinAlign(PlaceAlignOp op)
        {
            if (!McpFace.TryParseAxis(op.axis, out int axis))
            {
                _outcome.Problems.Add($"align '{op.target}': axis '{op.axis}' is not x, y or z");
                return;
            }
            if (!TryTarget(op.target, "align", out var box)) return;
            string at = (op.at ?? string.Empty).Trim().ToLowerInvariant();
            float? min = at == "min" ? box.Min[axis]
                : at == "center" ? box.Center[axis] - _size[axis] * 0.5f
                : at == "max" ? box.Max[axis] - _size[axis]
                : (float?)null;
            if (!min.HasValue)
            {
                _outcome.Problems.Add($"align '{op.target}': at '{op.at}' is not min, center or max");
                return;
            }
            Pin(axis, min.Value + op.offset_mm, $"align '{op.target}' {McpFace.AxisLetter(axis)} {at}");
        }

        private bool TryTarget(string target, string role, out BoxMm box)
        {
            box = default;
            if (string.IsNullOrWhiteSpace(target))
            {
                _outcome.Problems.Add($"{role}: the target name is empty");
                return false;
            }
            if (string.Equals(target, _name, StringComparison.OrdinalIgnoreCase))
            {
                _outcome.Problems.Add($"{role} '{target}': a part cannot be placed relative to itself");
                return false;
            }
            if (_byName.TryGetValue(target, out box)) return true;
            var names = new List<string>(_byName.Keys);
            _outcome.Problems.Add($"{role}: " + McpNameHints.NotFound(target, names));
            return false;
        }

        private void Pin(int axis, float min, string source)
        {
            float held = _pins[axis].GetValueOrDefault();
            if (!_pins[axis].HasValue)
            {
                _pins[axis] = min;
                _why[axis] = source;
                return;
            }
            if (Mathf.Abs(held - min) <= Tolerance.ContactMm) return;
            _outcome.Problems.Add($"{McpFace.AxisLetter(axis)} is fixed twice with different results: "
                + $"{_why[axis]} puts the part's minimum {McpFace.AxisLetter(axis)} at {Fmt(held)} mm, "
                + $"{source} at {Fmt(min)} mm. Keep ONE of them, or change a gap_mm / offset_mm so they agree");
        }

        private static string MissingAxis(int axis)
        {
            switch (axis)
            {
                case 0:
                    return "x is not determined: add align {target:'<window or wall>', axis:'x', at:'center'|'min'|'max'} "
                        + "or against {target:'<neighbour>', face:'left'|'right'}";
                case 1:
                    return "y is not determined: add on:\"floor\" (or on:'<name of the part it rests on>') "
                        + "or against {target:'<name>', face:'top'|'bottom'}";
                default:
                    return "z is not determined: add against {target:'<wall or neighbour>', face:'front'|'back'} "
                        + "or align {target:'<name>', axis:'z', at:'min'|'center'|'max'}";
            }
        }

        private void CheckNothingOverlaps()
        {
            if (!_outcome.Ok) return;
            var ours = new BoxMm(_outcome.MinMm, _outcome.MinMm + _size);
            foreach (var overlap in PlacementRelations.Of(ours, _scene).Overlaps)
            {
                var theirs = _byName[overlap.n];
                string face = FaceToStandBy(ours, theirs);
                string how = face == "top"
                    ? $"to stand ON it use on:'{overlap.n}'"
                    : $"to stand next to it add against {{target:'{overlap.n}', face:'{face}'}}";
                _outcome.Problems.Add($"the computed spot overlaps '{overlap.n}' by {Fmt(overlap.depthMm)} mm "
                    + $"('{overlap.n}' spans x {Fmt(theirs.Min.x)}..{Fmt(theirs.Max.x)}, y {Fmt(theirs.Min.y)}..{Fmt(theirs.Max.y)}, "
                    + $"z {Fmt(theirs.Min.z)}..{Fmt(theirs.Max.z)} mm); {how}, or fix the constraints that put it here "
                    + $"({Describe()})");
            }
        }

        private string Describe()
        {
            var parts = new List<string>();
            for (int axis = 0; axis < 3; axis++)
                if (!string.IsNullOrEmpty(_why[axis])) parts.Add($"{McpFace.AxisLetter(axis)}: {_why[axis]}");
            return string.Join("; ", parts);
        }

        private static string Fmt(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
