using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core.MCP
{
    public static class McpWireEnums
    {
        public static bool TryParseDoorMode(string s, out DoorMode mode)
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

        public static bool TryParseFace(string s, out int axis, out bool maxSide)
        {
            axis = 0; maxSide = false;
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "left": axis = 0; maxSide = false; return true;
                case "right": axis = 0; maxSide = true; return true;
                case "bottom": axis = 1; maxSide = false; return true;
                case "top": axis = 1; maxSide = true; return true;
                case "back": axis = 2; maxSide = false; return true;
                case "front": axis = 2; maxSide = true; return true;
                default: return false;
            }
        }

        public static bool TryParseConvertTarget(string s, out ElementConverter.TargetType target)
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

        public static AssembledFill ParseFill(string s)
        {
            if (string.IsNullOrEmpty(s)) return AssembledFill.Blind;
            switch (s.Trim().ToLowerInvariant())
            {
                case "glass": case "стекло": return AssembledFill.Glass;
                case "open": case "empty": case "витрина": return AssembledFill.Open;
                default: return AssembledFill.Blind;
            }
        }

        public static DrawerType ParseDrawerType(string s)
        {
            switch ((s ?? "").Trim().ToUpperInvariant())
            {
                case "B": return DrawerType.B;
                case "C": return DrawerType.C;
                case "D": return DrawerType.D;
                default: return DrawerType.A;
            }
        }

        public static DrawerColor ParseDrawerColor(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "white": case "белый": return DrawerColor.White;
                case "black": case "чёрный": case "черный": return DrawerColor.Black;
                default: return DrawerColor.Anthracite;
            }
        }

        public static DrawerSystem ParseDrawerSystem(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "movento": return DrawerSystem.Movento;
                default: return DrawerSystem.Gtv;
            }
        }

        public static GlassTint ParseGlassTint(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "tinted": case "тонированное": return GlassTint.Tinted;
                default: return GlassTint.Clear;
            }
        }

        public static DoorSashType ParseDoorSashType(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "blind": case "глухое": case "глухая": return DoorSashType.Blind;
                default: return DoorSashType.Glass;
            }
        }

        public static MasonryTechnology ParseMasonry(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "brick_thickened": return MasonryTechnology.BrickThickened;
                case "aerated_block": return MasonryTechnology.AeratedBlock;
                case "timber": return MasonryTechnology.Timber;
                case "frame": return MasonryTechnology.Frame;
                default: return MasonryTechnology.BrickSingle;
            }
        }

        public static string Name(MasonryTechnology t) => t switch
        {
            MasonryTechnology.BrickSingle => "brick_single",
            MasonryTechnology.BrickThickened => "brick_thickened",
            MasonryTechnology.AeratedBlock => "aerated_block",
            MasonryTechnology.Timber => "timber",
            MasonryTechnology.Frame => "frame",
            _ => "brick_single",
        };

        public static string Name(DrawerSystem s) => s == DrawerSystem.Movento ? "movento" : "gtv";

        public static string Name(DrawerColor c) => c switch
        {
            DrawerColor.Anthracite => "anthracite",
            DrawerColor.White => "white",
            DrawerColor.Black => "black",
            _ => "anthracite",
        };

        public static string Name(DoubleDrawerState s) => s switch
        {
            DoubleDrawerState.Closed => "closed",
            DoubleDrawerState.BothOpen => "bothopen",
            DoubleDrawerState.LowerOnly => "loweronly",
            _ => "closed",
        };

        public static string Name(GlassTint t) => t switch
        {
            GlassTint.Clear => "clear",
            GlassTint.Tinted => "tinted",
            _ => "clear",
        };

        public static string Name(DoorSashType t) => t switch
        {
            DoorSashType.Glass => "glass",
            DoorSashType.Blind => "blind",
            _ => "glass",
        };
    }
}
