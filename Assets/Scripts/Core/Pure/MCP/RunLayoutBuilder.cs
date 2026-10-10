using System;
using System.Collections.Generic;
using System.Globalization;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class RunLayoutBuilder
    {
        private const string NamingRule = "only latin letters, digits, '-' and '_' are allowed; no spaces, no cyrillic";

        private readonly RunDeclaration _run;
        private readonly RunWall _wall;
        private readonly RunStart _start;
        private readonly BoxMm? _startPart;
        private readonly float _liftOffsetMm;
        private readonly Func<string, bool> _nameIsValid;
        private readonly Func<string, bool> _exists;
        private readonly RunLayout _layout = new RunLayout();

        private RunLayoutBuilder(RunDeclaration run, RunWall wall, BoxMm? startPart, float liftOffsetMm,
            Func<string, bool> nameIsValid, Func<string, bool> exists)
        {
            _run = run;
            _wall = wall;
            _start = RunStart.Parse(run.From);
            _startPart = startPart;
            _liftOffsetMm = liftOffsetMm;
            _nameIsValid = nameIsValid;
            _exists = exists;
        }

        public static RunLayout Build(RunDeclaration run, RunWall wall, BoxMm? startPart, float liftOffsetMm,
            Func<string, bool> nameIsValid, Func<string, bool> exists)
        {
            var builder = new RunLayoutBuilder(run, wall, startPart, liftOffsetMm, nameIsValid, exists);
            builder.Resolve();
            if (builder._layout.Ok) builder.CheckTheRunFitsTheWall();
            if (builder._layout.Ok) builder.WriteSteps();
            return builder._layout;
        }

        private void Resolve()
        {
            if (!_nameIsValid(_run.Id)) _layout.Problems.Add($"id '{_run.Id}': {NamingRule}");
            if (_run.StartMm < 0f) _layout.Problems.Add($"start_mm {Fmt(_run.StartMm)} is negative - use 0 to start at the very end");
            if (_run.GapMm < 0f) _layout.Problems.Add($"gap_mm {Fmt(_run.GapMm)} is negative - use 0 for flush cabinets");
            if (_run.Modules.Count == 0) _layout.Problems.Add("modules is empty: list at least one cabinet");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _run.Modules.Count; i++) ResolveModule(i, _run.Modules[i], seen);
        }

        private void ResolveModule(int index, RunModuleDecl? module, HashSet<string> seen)
        {
            if (module == null)
            {
                _layout.Problems.Add($"module {index + 1} is empty");
                return;
            }
            string tag = $"module {index + 1} '{module.Name}'";
            if (!_nameIsValid(module.Name)) _layout.Problems.Add($"{tag}: {NamingRule}");
            else if (!seen.Add(module.Name)) _layout.Problems.Add($"{tag}: the name is used twice in this run - every cabinet needs its own");
            if (!RunKindDefaults.TryParse(module.Kind, out var kind))
            {
                _layout.Problems.Add($"{tag}: kind '{module.Kind}' is not one of {RunKindDefaults.Words}");
                return;
            }
            if (module.WidthMm < 1) { _layout.Problems.Add($"{tag}: width_mm must be at least 1"); return; }
            if (module.HeightMm.HasValue && module.HeightMm.Value < 1) { _layout.Problems.Add($"{tag}: height_mm must be at least 1"); return; }
            if (module.DepthMm.HasValue && module.DepthMm.Value < 1) { _layout.Problems.Add($"{tag}: depth_mm must be at least 1"); return; }
            _layout.Modules.Add(new RunModuleSize(module.Name, kind, module.WidthMm,
                module.HeightMm ?? RunKindDefaults.HeightMm(kind), module.DepthMm ?? RunKindDefaults.DepthMm(kind)));
        }

        private void CheckTheRunFitsTheWall()
        {
            float needed = 0f;
            foreach (var module in _layout.Modules) needed += module.WidthMm;
            float gaps = _run.GapMm * (_layout.Modules.Count - 1);
            float total = needed + gaps + _run.StartMm;
            float freeFrom = _wall.LowEnd, freeTo = _wall.HighEnd;
            string origin = $"the {(_start.FromRight ? "right" : "left")} end";
            if (_start.IsPart)
            {
                if (_startPart.HasValue) freeFrom = _startPart.Value.Max[_wall.RunAxis];
                origin = $"the far side of '{_start.PartName}'";
            }
            float available = freeTo - freeFrom;
            if (total <= available + Tolerance.ContactMm) return;
            string axis = McpFace.AxisLetter(_wall.RunAxis);
            _layout.Problems.Add($"the run is {Fmt(total - available)} mm longer than the room along wall '{_wall.Name}': "
                + $"cabinets {Fmt(needed)} + gaps {Fmt(gaps)} + start_mm {Fmt(_run.StartMm)} = {Fmt(total)} mm, "
                + $"but only {Fmt(available)} mm are free from {origin} ({axis} {Fmt(freeFrom)}..{Fmt(freeTo)} mm). "
                + "Narrow or drop cabinets, or lower start_mm / gap_mm");
        }

        private void WriteSteps()
        {
            for (int i = 0; i < _layout.Modules.Count; i++)
            {
                var size = _layout.Modules[i];
                var previous = i > 0 ? _layout.Modules[i - 1].Name : null;
                _layout.Steps.Add(new RunStep(size, SpecOf(size, previous), _wall.RotYDeg, !_exists(size.Name)));
            }
        }

        private PlaceSpec SpecOf(RunModuleSize module, string? previous)
        {
            var spec = new PlaceSpec();
            spec.Name = module.Name;
            spec.On = PlaceSolver.FloorWord;
            spec.LiftMm = RunKindDefaults.HangsAboveFloorMm(module.Kind) + _liftOffsetMm;
            spec.Against.Add(new PlaceAgainstSpec(_wall.Name, _wall.RoomFace, 0f));
            if (previous != null)
                spec.Against.Add(new PlaceAgainstSpec(previous, McpFace.NameOf(_wall.RunAxis, _start.GrowsTowardMax), _run.GapMm));
            else if (_start.IsPart)
                spec.Against.Add(new PlaceAgainstSpec(_start.PartName!, McpFace.NameOf(_wall.RunAxis, true), _run.StartMm));
            else
                spec.Align.Add(StartAlign());
            return spec;
        }

        private PlaceAlignSpec StartAlign() => new PlaceAlignSpec(
            _wall.Name,
            McpFace.AxisLetter(_wall.RunAxis),
            _start.FromRight ? "max" : "min",
            _start.FromRight ? -_run.StartMm : _run.StartMm);

        private static string Fmt(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
