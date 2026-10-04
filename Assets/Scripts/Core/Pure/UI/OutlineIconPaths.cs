using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public static class OutlineIconPaths
    {
        public const float GridSize = 20f;
        public const float StrokeWidth = 1.6f;
        public const int TexelsPerGridUnit = 4;

        public const string New = "New";
        public const string Open = "Open";
        public const string Save = "Save";
        public const string SaveAs = "SaveAs";
        public const string Undo = "Undo";
        public const string Redo = "Redo";
        public const string ChevronUp = "ChevronUp";
        public const string ChevronDown = "ChevronDown";
        public const string Levels = "Levels";
        public const string ResizeHandles = "ResizeHandles";
        public const string MoveHandles = "MoveHandles";
        public const string Measure = "Measure";
        public const string Eyedropper = "Eyedropper";
        public const string Bulb = "Bulb";
        public const string Scene = "Scene";
        public const string Warning = "Warning";
        public const string FindIssue = "FindIssue";
        public const string Specification = "Specification";
        public const string Instructions = "Instructions";
        public const string Sun = "Sun";
        public const string Music = "Music";
        public const string Settings = "Settings";
        public const string Pin = "Pin";
        public const string DockExpanded = "DockExpanded";
        public const string DockRail = "DockRail";
        public const string CategoryBoard = "CategoryBoard";
        public const string CategoryFacade = "CategoryFacade";
        public const string CategoryDrawer = "CategoryDrawer";
        public const string CategoryFurniture = "CategoryFurniture";
        public const string CategoryAppliance = "CategoryAppliance";
        public const string CategorySanitary = "CategorySanitary";
        public const string CategoryRoom = "CategoryRoom";
        public const string CategoryConstruction = "CategoryConstruction";

        public static readonly IReadOnlyDictionary<string, string> Table = new Dictionary<string, string>
        {
            [New] = "M5 2h7l3 3v13H5z M12 2v3h3",
            [Open] = "M2 5h6l2 2h8v9H2z",
            [Save] = "M3 3h11l3 3v11H3z M6 3v5h7V3 M6 17v-5h8v5",
            [SaveAs] = "M3 3h11l3 3v11H3z M6 3v5h7V3 M14 13h5 M16.5 10.5v5",
            [Undo] = "M7 5L3 9l4 4 M3 9h9a5 5 0 010 10H9",
            [Redo] = "M13 5l4 4-4 4 M17 9H8a5 5 0 000 10h3",
            [ChevronUp] = "M4 13l6-6 6 6",
            [ChevronDown] = "M4 7l6 6 6-6",
            [Levels] = "M3 5h14 M3 10h14 M3 15h14",
            [ResizeHandles] = "M4 16L16 4 M16 4h-5 M16 4v5 M4 16h5 M4 16v-5",
            [MoveHandles] = "M10 2v16 M2 10h16 M10 2l-3 3 M10 2l3 3 M10 18l-3-3 M10 18l3-3 M2 10l3-3 M2 10l3 3 M18 10l-3-3 M18 10l-3 3",
            [Measure] = "M2 7h16v6H2z M5 7v3 M8 7v2 M11 7v3 M14 7v2",
            [Eyedropper] = "M3.5 16.5l1-3.2 7.2-7.2 2.2 2.2-7.2 7.2z O14.8 5.2 2.4",
            [Bulb] = "M7.5 11A4.6 4.6 0 1 1 12.5 11V14H7.5z M8.5 17h3",
            [Scene] = "M3 4h5v4H3z M12 12h5v4h-5z M5.5 8v6h6.5",
            [Warning] = "M10 3l8 14H2z M10 8v4 M10 14.5v.5",
            [FindIssue] = "O8.5 8.5 5.5 M12.6 12.6l4.9 4.9 M8.5 5.8v3.2 M8.5 10.8v.4",
            [Specification] = "M4 2h12v16H4z M7 6h6 M7 10h6 M7 14h4",
            [Instructions] = "M3 4h5a2 2 0 012 2v11a2 2 0 00-2-2H3z M17 4h-5a2 2 0 00-2 2v11a2 2 0 012-2h5z",
            [Sun] = "O10 10 4 M10 1v2 M10 17v2 M1 10h2 M17 10h2 M3.5 3.5l1.5 1.5 M15 15l1.5 1.5 M3.5 16.5L5 15 M15 5l1.5-1.5",
            [Music] = "M8 15V4l9-2v11 O6 15 2 O15 13 2",
            [Settings] = "O10 10 2.5 O10 10 6 M10 1.4V4 M10 16v2.6 M1.4 10H4 M16 10h2.6 M14.24 5.76L16.08 3.92 M5.76 5.76L3.92 3.92 M14.24 14.24l1.84 1.84 M5.76 14.24l-1.84 1.84",
            [Pin] = "M7 2h6l-1 5 3 3H5l3-3z M10 10v8",
            [DockExpanded] = "M2 3h16v14H2z M9 3v14",
            [DockRail] = "M2 3h16v14H2z M5 3v14",
            [CategoryBoard] = "M2 12l8-4 8 4-8 4z M2 12v2l8 4 8-4v-2",
            [CategoryFacade] = "M5 2h10v16H5z M8 5h4v10H8z M11.5 9v2",
            [CategoryDrawer] = "M3 4h14v6H3z M3 10h14v6H3z M8.5 7h3 M8.5 13h3",
            [CategoryFurniture] = "M1.5 6h17v2h-17z M3.5 8v9 M16.5 8v9",
            [CategoryAppliance] = "M3 2h14v16H3z M3 7h14 O10 12.5 3",
            [CategorySanitary] = "M10 2l5 7a5.5 5.5 0 11-10 0z",
            [CategoryRoom] = "M2 9l8-6 8 6 M4 8v9h12V8 M8 17v-5h4v5",
            [CategoryConstruction] = "M2 4h16v12H2z M2 8h16 M2 12h16 M8 4v4 M13 8v4 M8 12v4",
        };

        public static IReadOnlyList<string> ToolbarNames { get; } = new[]
        {
            New, Open, Save, SaveAs, Undo, Redo, ChevronUp, ChevronDown, Levels, ResizeHandles, MoveHandles,
            Measure, Eyedropper, Bulb, Scene, Warning, FindIssue, Specification, Instructions, Sun, Music,
            Settings,
        };
    }
}
