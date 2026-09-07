using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    public static class McpSpecCodec
    {
        public static bool TryParseGrooves(string spec, out List<GrooveSpec> result, out string error)
        {
            result = new List<GrooveSpec>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(spec)) return true;

            foreach (var rawItem in spec.Split(','))
            {
                var item = rawItem.Trim();
                if (item.Length == 0) continue;

                var parts = item.Split(':');
                if (parts.Length != 2)
                {
                    error = $"'{item}' is not \"kind:side\" (e.g. \"through:top\")";
                    return false;
                }

                GrooveKind kind;
                switch (parts[0].Trim().ToLowerInvariant())
                {
                    case "through": kind = GrooveKind.Through; break;
                    case "blind": kind = GrooveKind.Blind; break;
                    default:
                        error = $"unknown kind '{parts[0].Trim()}' in '{item}' (expected through|blind)";
                        return false;
                }

                GrooveSide side;
                switch (parts[1].Trim().ToLowerInvariant())
                {
                    case "top": side = GrooveSide.Top; break;
                    case "bottom": side = GrooveSide.Bottom; break;
                    case "left": side = GrooveSide.Left; break;
                    case "right": side = GrooveSide.Right; break;
                    default:
                        error = $"unknown side '{parts[1].Trim()}' in '{item}' (expected top|bottom|left|right)";
                        return false;
                }

                var groove = new GrooveSpec(kind, side);
                if (result.Contains(groove))
                {
                    error = $"duplicate groove '{item}' — offset is fixed, a second one would land on the first";
                    return false;
                }
                if (result.Count >= AppConstants.GROOVE_MAX_PER_PART)
                {
                    error = $"too many grooves (max {AppConstants.GROOVE_MAX_PER_PART})";
                    return false;
                }
                result.Add(groove);
            }
            return true;
        }

        public static string FormatGrooves(KitchenElement el)
        {
            var grooves = el.Grooves;
            if (grooves.Count == 0) return string.Empty;

            var parts = new List<string>(grooves.Count);
            foreach (var g in grooves)
            {
                string kind = g.kind == GrooveKind.Blind ? "blind" : "through";
                string side = g.side switch
                {
                    GrooveSide.Top => "top",
                    GrooveSide.Bottom => "bottom",
                    GrooveSide.Left => "left",
                    _ => "right",
                };
                parts.Add($"{kind}:{side}");
            }
            return string.Join(", ", parts);
        }

        public const char OVERLAY_ITEM_SEPARATOR = ';';

        public const char EDGE_ITEM_SEPARATOR = ';';

        private static readonly char[] EDGE_ITEM_SEPARATORS = { ';', ',' };

        public static bool TryParseTextureOverlays(string spec,
            out List<TextureOverlaySpec> result, out string error)
        {
            result = new List<TextureOverlaySpec>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(spec)) return true;

            foreach (var rawItem in spec.Split(OVERLAY_ITEM_SEPARATOR))
            {
                var item = rawItem.Trim();
                if (item.Length == 0) continue;

                string body = item;
                int u0 = 0, v0 = 0, w = 0, h = 0;
                int at = body.IndexOf('@');
                if (at >= 0)
                {
                    if (!TryParseOverlayRect(body.Substring(at + 1), item, ref u0, ref v0, ref w, ref h, ref error))
                        return false;
                    body = body.Substring(0, at);
                }

                var parts = body.Split(':');
                if (parts.Length != 2)
                {
                    error = $"'{item}' is not \"side:materialId\" (e.g. \"a:oak\")";
                    return false;
                }

                OverlaySide side;
                switch (parts[0].Trim().ToLowerInvariant())
                {
                    case "a": side = OverlaySide.A; break;
                    case "b": side = OverlaySide.B; break;
                    case "c": side = OverlaySide.C; break;
                    case "d": side = OverlaySide.D; break;
                    case "e": side = OverlaySide.E; break;
                    case "f": side = OverlaySide.F; break;
                    case "all": side = OverlaySide.All; break;
                    default:
                        error = $"unknown side '{parts[0].Trim()}' in '{item}' (expected a|b|c|d|e|f|all)";
                        return false;
                }

                string materialId = parts[1].Trim();
                if (materialId.Length == 0)
                {
                    error = $"'{item}' has no material id";
                    return false;
                }

                if (result.Count >= TextureOverlayGeometry.MAX_PER_ELEMENT)
                {
                    error = $"too many texture overlays (max {TextureOverlayGeometry.MAX_PER_ELEMENT})";
                    return false;
                }
                result.Add(new TextureOverlaySpec(side, materialId, u0, v0, w, h));
            }
            return true;
        }

        private static bool TryParseOverlayRect(string rect, string item,
            ref int u0, ref int v0, ref int w, ref int h, ref string error)
        {
            int plus = rect.IndexOf('+');
            int cross = rect.IndexOf('x');
            int comma = rect.IndexOf(',');
            if (plus < 0 || cross < plus || comma < 0 || comma > plus)
            {
                error = $"'{item}' has a bad area — expected \"@u,v+WxH\" (mm)";
                return false;
            }

            bool ok = int.TryParse(rect.Substring(0, comma).Trim(), out u0)
                & int.TryParse(rect.Substring(comma + 1, plus - comma - 1).Trim(), out v0)
                & int.TryParse(rect.Substring(plus + 1, cross - plus - 1).Trim(), out w)
                & int.TryParse(rect.Substring(cross + 1).Trim(), out h);
            if (!ok)
            {
                error = $"'{item}' has non-integer area numbers (mm are whole)";
                return false;
            }
            if (w < TextureOverlaySpec.MIN_SIZE_MM || h < TextureOverlaySpec.MIN_SIZE_MM)
            {
                error = $"'{item}': area is smaller than {TextureOverlaySpec.MIN_SIZE_MM} mm";
                return false;
            }
            return true;
        }

        public static string FormatTextureOverlays(KitchenElement el)
        {
            var overlays = el.TextureOverlays;
            if (overlays.Count == 0) return string.Empty;

            var parts = new List<string>(overlays.Count);
            foreach (var o in overlays)
            {
                string side = o.side == OverlaySide.All
                    ? "all"
                    : TextureOverlaySpec.SideLabel(o.side).ToLowerInvariant();
                parts.Add(o.IsFullFace
                    ? $"{side}:{o.MaterialId}"
                    : $"{side}:{o.MaterialId}@{o.u0MM},{o.v0MM}+{o.widthMM}x{o.heightMM}");
            }
            return string.Join(OVERLAY_ITEM_SEPARATOR + " ", parts);
        }

        public static bool TryParseEdgeSides(string spec,
            out List<(EdgeSide side, EdgeSideState state)> result, out string error)
        {
            result = new List<(EdgeSide, EdgeSideState)>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(spec)) return true;

            foreach (var rawItem in spec.Split(EDGE_ITEM_SEPARATORS))
            {
                var item = rawItem.Trim();
                if (item.Length == 0) continue;

                var parts = item.Split(':');
                if (parts.Length != 2)
                {
                    error = $"'{item}' is not \"side:state\" (e.g. \"L1:on\")";
                    return false;
                }

                EdgeSide side;
                switch (parts[0].Trim().ToUpperInvariant())
                {
                    case "L1": side = EdgeSide.L1; break;
                    case "L2": side = EdgeSide.L2; break;
                    case "W1": side = EdgeSide.W1; break;
                    case "W2": side = EdgeSide.W2; break;
                    default:
                        error = $"unknown side '{parts[0].Trim()}' in '{item}' (expected L1|L2|W1|W2)";
                        return false;
                }

                EdgeSideState state;
                switch (parts[1].Trim().ToLowerInvariant())
                {
                    case "on": state = EdgeSideState.Forced; break;
                    case "off": state = EdgeSideState.Suppressed; break;
                    case "auto": state = EdgeSideState.Auto; break;
                    default:
                        error = $"unknown state '{parts[1].Trim()}' in '{item}' (expected on|off|auto)";
                        return false;
                }

                result.Add((side, state));
            }
            return true;
        }

        public static string FormatEdgeSides(KitchenElement el)
        {
            var parts = new List<string>(4);
            foreach (EdgeSide side in EdgeStates.All)
            {
                var state = el.EdgeStateOf(side);
                if (state == EdgeSideState.Auto) continue;
                parts.Add($"{side}:{(state == EdgeSideState.Forced ? "on" : "off")}");
            }
            return string.Join(EDGE_ITEM_SEPARATOR + " ", parts);
        }

        public static string FormatBandedEdgesRecomputedFromScene(KitchenElement el, List<KitchenElement> all)
        {
            if (!el.EdgeBandingEnabled) return string.Empty;

            var coverage = EdgeBanding.Coverage(el, all);
            var sides = new List<string>(4);
            foreach (EdgeSide side in EdgeStates.All)
                if (EdgeBanding.HasEdgeEffective(el, coverage, side)) sides.Add(side.ToString());
            return string.Join(",", sides);
        }
    }
}
