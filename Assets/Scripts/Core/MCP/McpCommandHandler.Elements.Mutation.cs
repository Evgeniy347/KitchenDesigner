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
        // ── Edit helpers ─────────────────────────────────────────────────

        private static MaterialDef? ResolveMaterial(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var d in MaterialCatalog.All)
                if (string.Equals(d.id, key, StringComparison.OrdinalIgnoreCase)) return d;
            foreach (var d in MaterialCatalog.All)
                if (string.Equals(d.displayName, key, StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }

        private static List<string> ValidateEditOpFields(EditOp op, KitchenElement el)
        {
            var e = new List<string>();
            bool IsNot<T>() => !(el is T);
            bool IsFacadeLike() => el is FacadeElement || el is WindowElement || el is DoorElement;
            if (IsNot<FacadeElement>() && IsNot<AssembledFacadeElement>()) { if (op.gap_left.HasValue) e.Add("gap_left"); if (op.gap_right.HasValue) e.Add("gap_right"); if (op.gap_top.HasValue) e.Add("gap_top"); if (op.gap_bottom.HasValue) e.Add("gap_bottom"); if (op.fill != null) e.Add("fill"); }
            if (!IsFacadeLike()) { if (op.mode != null) e.Add("mode"); }
            if (IsNot<FacadeElement>() && IsNot<WindowElement>() && IsNot<DoorElement>()) { if (op.is_open.HasValue) e.Add("is_open"); }
            if (IsNot<RadialShelfElement>()) { if (op.corner_radius.HasValue) e.Add("corner_radius"); }
            if (IsNot<DrawerElement>()) { if (op.drawer_type != null) e.Add("drawer_type"); if (op.drawer_length.HasValue) e.Add("drawer_length"); if (op.drawer_color != null) e.Add("drawer_color"); if (op.internal_width.HasValue) e.Add("internal_width"); if (op.is_double.HasValue) e.Add("is_double"); if (op.is_upper.HasValue) e.Add("is_upper"); if (op.paired_drawer_name != null) e.Add("paired_drawer_name"); if (op.attached_facade_name != null) e.Add("attached_facade_name"); }
            if (IsNot<TableElement>() && IsNot<RadiusTableElement>()) { if (op.leg_inset_mm.HasValue) e.Add("leg_inset_mm"); if (op.tabletop_material != null) e.Add("tabletop_material"); if (op.legs_material != null) e.Add("legs_material"); }
            if (IsNot<PillarElement>()) { if (op.mid_height_mm.HasValue) e.Add("mid_height_mm"); }
            if (IsNot<WindowElement>()) { if (op.tint != null) e.Add("tint"); if (op.sill_protrusion_mm.HasValue) e.Add("sill_protrusion_mm"); }
            if (IsNot<DoorElement>()) { if (op.sash_type != null) e.Add("sash_type"); }
            if (el is DrawerElement && (op.width.HasValue || op.height.HasValue || op.depth.HasValue)) e.Add("width/height/depth not settable on drawers (size is parametric)");

            // Пазы принимает только базовая «деталь»: у фасада/полки/ящика своя
            // процедурная геометрия, врезка в неё не определена.
            if (op.grooves != null)
            {
                if (!el.SupportsGrooves)
                    e.Add("grooves (plain boards only)");
                else if (!TryParseGrooves(op.grooves, out _, out string grooveError))
                    e.Add($"grooves: {grooveError}");
            }

            // Накладки текстур принимают только стена и пол — у остальных типов
            // декор задаётся на весь элемент полем material.
            if (op.texture_overlays != null)
            {
                if (!el.SupportsTextureOverlays)
                    e.Add("texture_overlays (walls and floors only)");
                else if (!TryParseTextureOverlays(op.texture_overlays, out _, out string overlayError))
                    e.Add($"texture_overlays: {overlayError}");
            }

            // Кромкование — свойство той же базовой «детали», что и пазы.
            // «Лист ли она» проверять здесь рано: размеры могут меняться этой же
            // операцией, и не-лист просто не отдаёт кромок при чтении.
            if (!el.SupportsGrooves)
            {
                if (op.edge_banding.HasValue) e.Add("edge_banding (plain boards only)");
                if (op.edge_thickness_mm.HasValue) e.Add("edge_thickness_mm (plain boards only)");
                if (op.edge_skip_validation.HasValue) e.Add("edge_skip_validation (plain boards only)");
            }
            else if (op.edge_thickness_mm.HasValue
                && (op.edge_thickness_mm.Value < AppConstants.EDGE_THICKNESS_MIN_MM
                    || op.edge_thickness_mm.Value > AppConstants.EDGE_THICKNESS_MAX_MM))
            {
                e.Add($"edge_thickness_mm: out of range " +
                      $"({AppConstants.EDGE_THICKNESS_MIN_MM}..{AppConstants.EDGE_THICKNESS_MAX_MM} mm)");
            }
            return e;
        }

        /// <summary>Разбор списка пазов вида "through:top, blind:left". Пустая
        /// строка — пустой набор (снять все пазы). Ошибки описательные: инструмент
        /// вызывается вслепую, и «invalid» без деталей бесполезно.</summary>
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

        /// <summary>Разбор накладок текстур вида
        /// "a:oak; b:white@100,200+800x600". Разделитель элементов — точка с
        /// запятой, потому что внутри области запятая уже занята координатами.
        /// Область необязательна: без неё накладка занимает грань целиком.
        /// Пустая строка — снять все накладки.</summary>
        public static bool TryParseTextureOverlays(string spec,
            out List<TextureOverlaySpec> result, out string error)
        {
            result = new List<TextureOverlaySpec>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(spec)) return true;

            foreach (var rawItem in spec.Split(';'))
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

                string sideText = parts[0].Trim().ToLowerInvariant();
                OverlaySide side;
                switch (sideText)
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

        /// <summary>Область накладки: "u,v+WxH" — левый нижний угол и размер, мм.</summary>
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

        /// <summary>Обратное представление для get_elements.</summary>
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
            return string.Join("; ", parts);
        }

        /// <summary>Открытые торцы детали для get_elements: "L1,W1,W2". Пустая
        /// строка — все торцы упираются в соседей, кромки нет ни на одном.
        /// Наличие кромки вычисляется, а не хранится, поэтому и отдаётся
        /// вычисленным по текущей сцене.</summary>
        public static string FormatEdges(KitchenElement el, List<KitchenElement> all)
        {
            if (!el.EdgeBandingEnabled) return string.Empty;

            var coverage = EdgeBanding.Coverage(el, all);
            var sides = new List<string>(4);
            foreach (EdgeSide side in System.Enum.GetValues(typeof(EdgeSide)))
                if (coverage.HasEdge(side)) sides.Add(side.ToString());
            return string.Join(",", sides);
        }

        /// <summary>Обратное представление для get_elements: "through:top, blind:left".</summary>
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

        private static bool TryParseDoorMode(string s, out DoorMode mode)
        {
            mode = DoorMode.HingeFrontLeft;
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "front_left": mode = DoorMode.HingeFrontLeft; return true;
                case "front_right": mode = DoorMode.HingeFrontRight; return true;
                case "front_top": mode = DoorMode.HingeFrontTop; return true;
                case "front_bottom": mode = DoorMode.HingeFrontBottom; return true;
                case "back_left": mode = DoorMode.HingeBackLeft; return true;
                case "back_right": mode = DoorMode.HingeBackRight; return true;
                case "back_top": mode = DoorMode.HingeBackTop; return true;
                case "back_bottom": mode = DoorMode.HingeBackBottom; return true;
                case "edge_top_left": mode = DoorMode.HingeEdgeTopLeft; return true;
                case "edge_top_right": mode = DoorMode.HingeEdgeTopRight; return true;
                case "edge_bottom_left": mode = DoorMode.HingeEdgeBottomLeft; return true;
                case "edge_bottom_right": mode = DoorMode.HingeEdgeBottomRight; return true;
                case "drawer_out": mode = DoorMode.DrawerOut; return true;
                case "drawer_in": mode = DoorMode.DrawerIn; return true;
                case "drawer_right": mode = DoorMode.DrawerRight; return true;
                case "drawer_left": mode = DoorMode.DrawerLeft; return true;
                case "drawer_up": mode = DoorMode.DrawerUp; return true;
                case "drawer_down": mode = DoorMode.DrawerDown; return true;
                default: return false;
            }
        }

        private static string WireName(DrawerColor c) => c switch { DrawerColor.Anthracite => "anthracite", DrawerColor.White => "white", DrawerColor.Black => "black", _ => "anthracite" };
        private static string WireName(DoubleDrawerState s) => s switch { DoubleDrawerState.Closed => "closed", DoubleDrawerState.BothOpen => "bothopen", DoubleDrawerState.LowerOnly => "loweronly", _ => "closed" };
        private static string WireName(GlassTint t) => t switch { GlassTint.Clear => "clear", GlassTint.Tinted => "tinted", _ => "clear" };
        private static string WireName(DoorSashType t) => t switch { DoorSashType.Glass => "glass", DoorSashType.Blind => "blind", _ => "glass" };

        private static AssembledFill ParseFill(string s)
        {
            if (string.IsNullOrEmpty(s)) return AssembledFill.Blind;
            switch (s.Trim().ToLowerInvariant())
            {
                case "glass": case "стекло": return AssembledFill.Glass;
                case "open": case "empty": case "витрина": return AssembledFill.Open;
                default: return AssembledFill.Blind;
            }
        }

        private static DrawerType ParseDrawerType(string s)
        {
            switch ((s ?? "").Trim().ToUpperInvariant())
            {
                case "B": return DrawerType.B;
                case "C": return DrawerType.C;
                case "D": return DrawerType.D;
                default: return DrawerType.A;
            }
        }

        private static DrawerColor ParseDrawerColor(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "white": case "белый": return DrawerColor.White;
                case "black": case "чёрный": case "черный": return DrawerColor.Black;
                default: return DrawerColor.Anthracite;
            }
        }

        private static DrawerSystem ParseDrawerSystem(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "movento": return DrawerSystem.Movento;
                default: return DrawerSystem.Gtv;
            }
        }

        private static GlassTint ParseGlassTint(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "tinted": case "тонированное": return GlassTint.Tinted;
                default: return GlassTint.Clear;
            }
        }

        private static DoorSashType ParseDoorSashType(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "blind": case "глухое": case "глухая": return DoorSashType.Blind;
                default: return DoorSashType.Glass;
            }
        }

        private static bool TryParseTarget(string s, out ElementConverter.TargetType target)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "part": case "board": case "деталь":
                    target = ElementConverter.TargetType.Part; return true;
                case "facade": case "дверца": case "фасад":
                    target = ElementConverter.TargetType.Facade; return true;
                case "assembled_facade": case "assembled": case "assembledfacade": case "сборный":
                    target = ElementConverter.TargetType.AssembledFacade; return true;
                case "radial_shelf": case "radial": case "radialshelf": case "радиусная": case "полка":
                    target = ElementConverter.TargetType.RadialShelf; return true;
                default:
                    target = ElementConverter.TargetType.Part; return false;
            }
        }

        private static void ApplyFacadeEdits(EditOp op, FacadeElement facade)
        {
            if (op.gap_left.HasValue) facade.GapLeft = op.gap_left.Value;
            if (op.gap_right.HasValue) facade.GapRight = op.gap_right.Value;
            if (op.gap_top.HasValue) facade.GapTop = op.gap_top.Value;
            if (op.gap_bottom.HasValue) facade.GapBottom = op.gap_bottom.Value;
            if (op.mode != null && TryParseDoorMode(op.mode, out var m)) facade.Mode = m;
            if (op.is_open.HasValue) facade.SetOpen(op.is_open.Value);
        }
        private static void ApplyAssembledEdits(EditOp op, AssembledFacadeElement asm) { ApplyFacadeEdits(op, asm); if (op.fill != null) asm.Fill = ParseFill(op.fill); }
        private static void ApplyRadialShelfEdits(EditOp op, RadialShelfElement shelf) { if (op.corner_radius.HasValue) shelf.CornerRadius = op.corner_radius.Value; }
        private static void ApplyDrawerEdits(EditOp op, DrawerElement drawer)
        {
            if (op.drawer_system != null) drawer.System = ParseDrawerSystem(op.drawer_system);
            if (op.drawer_type != null) drawer.Type = ParseDrawerType(op.drawer_type);
            if (op.drawer_length.HasValue) drawer.NominalLength = op.drawer_length.Value;
            if (op.drawer_color != null) drawer.Color = ParseDrawerColor(op.drawer_color);
            if (op.internal_width.HasValue) drawer.InternalWidth = op.internal_width.Value;
            if (op.is_double.HasValue) drawer.IsDouble = op.is_double.Value;
            if (op.is_upper.HasValue) drawer.IsUpperDrawer = op.is_upper.Value;
            if (op.paired_drawer_name != null) drawer.PairedDrawerName = op.paired_drawer_name == "" ? "" : op.paired_drawer_name;
            if (op.attached_facade_name != null) drawer.AttachedFacadeName = op.attached_facade_name == "" ? "" : op.attached_facade_name;
        }
        private static void ApplyTableEdits(EditOp op, TableElement table)
        {
            if (op.leg_inset_mm.HasValue) table.LegInsetMM = op.leg_inset_mm.Value;
            if (op.tabletop_material != null) { var d = ResolveMaterial(op.tabletop_material); if (d != null) MaterialManager.ApplyTabletop(table, d); }
            if (op.legs_material != null) { var d = ResolveMaterial(op.legs_material); if (d != null) MaterialManager.ApplyLegs(table, d); }
        }
        private static void ApplyRadiusTableEdits(EditOp op, RadiusTableElement rt)
        {
            if (op.leg_inset_mm.HasValue) rt.LegInsetMM = op.leg_inset_mm.Value;
            if (op.tabletop_material != null) { var d = ResolveMaterial(op.tabletop_material); if (d != null) MaterialManager.ApplyTabletop(rt, d); }
            if (op.legs_material != null) { var d = ResolveMaterial(op.legs_material); if (d != null) MaterialManager.ApplyLegs(rt, d); }
        }
        private static void ApplyPillarEdits(EditOp op, PillarElement pillar) { if (op.mid_height_mm.HasValue) pillar.MidHeightMM = op.mid_height_mm.Value; }
        private static void ApplyWindowEdits(EditOp op, WindowElement window)
        {
            if (op.tint != null) window.Tint = ParseGlassTint(op.tint);
            if (op.sill_protrusion_mm.HasValue) window.SillProtrusionMM = op.sill_protrusion_mm.Value;
            if (op.mode != null && TryParseDoorMode(op.mode, out var m)) window.Mode = m;
            if (op.is_open.HasValue) window.SetOpen(op.is_open.Value);
        }
        private static void ApplyDoorEdits(EditOp op, DoorElement door)
        {
            if (op.sash_type != null) door.SashType = ParseDoorSashType(op.sash_type);
            if (op.mode != null && TryParseDoorMode(op.mode, out var m)) door.Mode = m;
            if (op.is_open.HasValue) door.SetOpen(op.is_open.Value);
        }

        private void ApplyNonGeometryEdits(
            List<(EditOp op, KitchenElement el, MaterialDef? material, List<string> warnings)> resolved)
        {
            foreach (var (op, el, mat, _) in resolved)
            {
                if (op.locked.HasValue) el.Movable = !op.locked.Value;
                if (mat != null) MaterialManager.Apply(el, mat);
                if (el is FacadeElement facade && !(el is AssembledFacadeElement)) ApplyFacadeEdits(op, facade);
                if (el is AssembledFacadeElement asmFacade) ApplyAssembledEdits(op, asmFacade);
                if (el is RadialShelfElement shelf) ApplyRadialShelfEdits(op, shelf);
                // Набор пазов задаётся целиком; строка уже проверена в ValidateEditOpFields.
                if (op.grooves != null && el.SupportsGrooves
                    && TryParseGrooves(op.grooves, out var parsedGrooves, out _))
                    el.SetGrooves(parsedGrooves);
                // Набор накладок тоже задаётся целиком; строка проверена там же.
                if (op.texture_overlays != null && el.SupportsTextureOverlays
                    && TryParseTextureOverlays(op.texture_overlays, out var parsedOverlays, out _))
                    el.SetTextureOverlays(parsedOverlays);
                if (el.SupportsGrooves)
                {
                    if (op.edge_banding.HasValue) el.EdgeBandingEnabled = op.edge_banding.Value;
                    if (op.edge_thickness_mm.HasValue) el.EdgeThicknessMM = op.edge_thickness_mm.Value;
                    // Кромки правятся по сторонам (EdgeManualMask), но в контракте
                    // MCP осталось прежнее поле на всю деталь: оно означает
                    // «все четыре стороны ручные».
                    if (op.edge_skip_validation.HasValue)
                        el.EdgeManualMask = op.edge_skip_validation.Value ? EdgeManual.AllMask : 0;
                }
                if (el is DrawerElement drawer) ApplyDrawerEdits(op, drawer);
                if (el is TableElement table) ApplyTableEdits(op, table);
                if (el is RadiusTableElement rt) ApplyRadiusTableEdits(op, rt);
                if (el is PillarElement pillar) ApplyPillarEdits(op, pillar);
                if (el is WindowElement window) ApplyWindowEdits(op, window);
                if (el is DoorElement door) ApplyDoorEdits(op, door);
                // Через DrawerLinks: переименование обязано увести за собой связи
                // по имени (пара ящика, фасад ящика), иначе они станут битыми.
                if (op.new_name != null && op.new_name != el.PartName)
                { DrawerLinks.Rename(el, op.new_name); el.gameObject.name = el.PartName; }
            }
        }

        /// <summary>Состояние элементов батча после (или в dry-run — «как если бы»)
        /// применения: позиция/размер/нарушения на каждый op + счётчик по сцене.</summary>
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

        // ── Mutation handlers ────────────────────────────────────────────

        /// <summary>Транзакционный батч изменений: либо применяются ВСЕ операции
        /// (одной CompositeCommand = один шаг undo), либо ни одна. dry_run —
        /// применить, посчитать нарушения по каждому op и откатить.</summary>
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
                foreach (var err in ValidateEditOpFields(op, el))
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
                    mat = ResolveMaterial(op.material!);
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

        /// <summary>Batch clone: atomically clone one or MANY elements. Whole batch is ONE undo step.</summary>
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
                    // Имя клона выдаёт ElementNaming (суффикс «_1», «_2», …) — оно
                    // уже проставлено фабрикой в Duplicate, здесь только синхронизируем
                    // имя GameObject.
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
            foreach (var el in allClones) { created.Add(el.PartName); elements.Add(BuildElementInfo(el, all, false, vr)); }
            Debug.Log($"[MCP] Clone batch: {p.ops.Length} sources → {allClones.Count} clones");
            return McpResponse.Result(req.id, new { ok = true, created, elements, sceneViolationCount = vr != null ? vr.violations.Count : 0 });
        }

        /// <summary>Batch align: move boards face-to-face against targets. Applied IN ORDER. Whole batch is ONE undo step.</summary>
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
                if (!TryParseFace(op.face, out int axis, out bool maxSide))
                { errors.Add($"Unknown face '{op.face}' for '{op.name}'"); continue; }
                if (!TryParseFace(op.target_face, out int tAxis, out bool tMaxSide))
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

                var elAabb = ComputeAABB(element.GetVertices());
                var tAabb = ComputeAABB(target.GetVertices());
                float myCoord = AabbSide(elAabb, axis, maxSide);
                float targetCoord = AabbSide(tAabb, axis, tMaxSide);
                float gapUnits = op.gap_mm * AppConstants.MM_TO_UNITS;
                float desired = maxSide ? targetCoord - gapUnits : targetCoord + gapUnits;
                float delta = desired - myCoord;
                var before = element.transform.position;
                var after = before;
                // gap_mm — float, да и полугабариты цели могут быть нечётными:
                // грань выравниваемой детали ставим на целый миллиметр (MmGrid).
                after[axis] += delta;
                after = MmGrid.SnapPosition(element, after);
                commands.Add(new MoveCommand(element, before, after, element.transform.rotation, element.transform.rotation));
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

        /// <summary>Равномерно распределить 3+ детали по оси: крайние стоят,
        /// середина двигается. Один шаг undo.</summary>
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
                // Пролёт делится нацело далеко не всегда: три детали на нечётном
                // пролёте дают ровно 0.5 мм, семь — бесконечную дробь. Ставим
                // грань на целый миллиметр (MmGrid).
                after[axis] = first + spacing * i;
                after = MmGrid.SnapPosition(el, after);
                var rot = el.transform.rotation;
                commands.Add(new MoveCommand(el, before, after, rot, rot));
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

        /// <summary>Batch create: atomically create one or MANY elements. Whole batch is ONE undo step.</summary>
        private McpResponse HandleCreateElements(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsCreateElements>();
            if (p == null || p.items == null || p.items.Length == 0)
                return McpResponse.Error(req.id, -32602, "items required (non-empty array)");

            var commands = new List<IUndoCommand>();
            var created = new List<KitchenElement>();
            var errors = new List<string>();
            var namesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in p.items)
            {
                if (string.IsNullOrEmpty(item.name)) { errors.Add("an item is missing 'name'"); continue; }
                if (!ElementNaming.IsValid(item.name))
                { errors.Add($"Invalid name '{item.name}': {ElementNaming.Rule}"); continue; }
                if (namesSeen.Contains(item.name)) { errors.Add($"duplicate name '{item.name}' in this batch"); continue; }
                namesSeen.Add(item.name);
                var existing = FindElementByName(item.name);
                if (existing != null) { errors.Add($"Element '{item.name}' already exists"); continue; }

                var elementType = (item.type ?? "board").Trim().ToLowerInvariant();
                var pos = new Vector3(item.x, item.y, item.z);
                GameObject go = null!;

                switch (elementType)
                {
                    case "floor":
                        // Настоящий пол нужного размера/позиции (FloorElement), а НЕ
                        // BasePlate-синглтон: планировке нужны отдельные полы комнат,
                        // которые можно двигать/растягивать (баг v1: floor→доска/якорь).
                        go = ElementFactory.CreateFloor(new Vector3Int(
                            item.width ?? FloorElement.DEFAULT_SIZE_MM,
                            item.height ?? FloorElement.DEFAULT_THICKNESS_MM,
                            item.depth ?? FloorElement.DEFAULT_SIZE_MM), item.name, pos);
                        break;
                    case "assembled_facade":
                        go = ElementFactory.CreateAssembledFacade(new Vector3Int(item.width ?? 450, item.height ?? 700, item.depth ?? 18), item.name, pos, AssembledFill.Blind);
                        break;
                    case "radial_shelf":
                        int rw = item.width ?? 600, rd = item.depth ?? 400;
                        go = ElementFactory.CreateRadialShelf(rw, rd, item.height ?? AppConstants.BOARD_THICKNESS_DEFAULT,
                            AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, item.name, pos);
                        break;
                    case "panel":
                        // ДВП/ХДФ: тонкая вкладная панель, зазоры входят в габарит.
                        go = ElementFactory.Instance.CreatePanel(
                            new Vector3Int(item.width ?? 600, item.height ?? 400, item.depth ?? 3),
                            item.name, pos);
                        break;
                    case "drawer":
                        go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, item.name, pos, DrawerSystem.Gtv);
                        break;
                    case "movento_drawer":
                        go = ElementFactory.CreateDrawer(DrawerType.A, 500, DrawerColor.Anthracite, item.width ?? 568, item.name, pos, DrawerSystem.Movento);
                        break;
                    case "table":
                        go = ElementFactory.CreateTable(new Vector3Int(item.width ?? 2000, item.height ?? 750, item.depth ?? 1000), item.name, pos);
                        break;
                    case "radius_table":
                        go = ElementFactory.CreateRadiusTable(new Vector3Int(item.width ?? 2000, item.height ?? 750, item.depth ?? 1000), item.name, pos);
                        break;
                    case "pillar":
                        go = ElementFactory.CreatePillar(item.height ?? PillarElement.MidHeightMM_Default, item.name, pos);
                        break;
                    case "window":
                        go = ElementFactory.CreateWindow(new Vector3Int(item.width ?? 900, item.height ?? 1200, item.depth ?? 100),
                            item.name, pos, GlassTint.Clear, 50);
                        break;
                    case "door":
                        go = ElementFactory.CreateDoor(new Vector3Int(item.width ?? 900, item.height ?? 2000, item.depth ?? 100),
                            item.name, pos, DoorSashType.Glass);
                        break;
                    default:
                    {
                        var dims = new Vector3Int(item.width ?? 800, item.height ?? 400, item.depth ?? 18);
                        go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.name = item.name;
                        if (elementType == "facade")
                        {
                            var facade = go.AddComponent<FacadeElement>();
                            facade.PartName = item.name; facade.DimensionsMM = dims;
                        }
                        else { var el = go.AddComponent<KitchenElement>(); el.PartName = item.name; el.DimensionsMM = dims; }
                        if (elementType == "wall") go.AddComponent<Wall>();
                        go.transform.position = pos;
                        break;
                    }
                }
                if (go == null) { errors.Add($"Failed to create '{item.name}'"); continue; }
                commands.Add(new CreateCommand(go));
                created.Add(go.GetComponent<KitchenElement>());
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1, "create_elements rejected: " + string.Join(" | ", errors));

            if (commands.Count > 0)
                CommandStack.Execute(new CompositeCommand($"MCP create_elements x{commands.Count}", commands));

            // Окна/двери должны прилипнуть к ближайшей стене и прорезать проём —
            // как при перетаскивании мышью. При MCP-создании это раньше не
            // вызывалось, поэтому проём не резался (баг v1). Делаем ПОСЛЕ Execute,
            // когда все стены батча уже зарегистрированы.
            foreach (var el in created)
            {
                if (el is WindowElement w) w.SnapToWall();
                else if (el is DoorElement d) d.SnapToWall();
            }

            RefreshElementHighlights();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var elements = new List<ElementInfo>();
            var createdNames = new List<string>();
            foreach (var el in created) { createdNames.Add(el.PartName); elements.Add(BuildElementInfo(el, all, false, vr)); }
            Debug.Log($"[MCP] Created {created.Count} elements: {string.Join(", ", createdNames)}");
            return McpResponse.Result(req.id, new { ok = true, created = createdNames, elements, sceneViolationCount = vr != null ? vr.violations.Count : 0 });
        }

        /// <summary>Batch convert: change type of one or MANY elements. Atomic. NOT undoable.</summary>
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
                if (!TryParseTarget(op.target, out var target))
                { errors.Add($"Unknown target '{op.target}' for '{op.name}'"); continue; }
                var element = FindElementByName(op.name);
                if (element == null) { errors.Add($"Element not found: {op.name}"); continue; }
                var lockErr = RequireMovable(element, op.name, req.id);
                if (lockErr != null) { errors.Add($"'{op.name}' is LOCKED"); continue; }
                var converted = ElementConverter.Convert(element, target);
                if (converted is AssembledFacadeElement assembled && !string.IsNullOrEmpty(op.fill))
                    assembled.Fill = ParseFill(op.fill);
                results.Add(converted);
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1, "convert_elements rejected: " + string.Join(" | ", errors));

            RefreshElementHighlights();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            var elements = new List<ElementInfo>();
            var names = new List<string>();
            foreach (var el in results) { names.Add(el.PartName); elements.Add(BuildElementInfo(el, all, false, vr)); }
            Debug.Log($"[MCP] Converted {results.Count} elements");
            return McpResponse.Result(req.id, new { ok = true, converted = names, elements, sceneViolationCount = vr != null ? vr.violations.Count : 0 });
        }

        /// <summary>Batch delete: atomically delete one or MANY elements. Whole batch is ONE undo step.</summary>
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

        /// <summary>Batch select: highlight one or MANY elements (visual only).</summary>
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
