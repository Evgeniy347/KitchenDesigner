// ============================================================================
//  Compile-time type tests for types.ts
//  Run: npx tsc --noEmit
// ============================================================================
// ── Validate required fields — correct shape and types ──────────────────
// Move supports optional x/y/z (omit axis = keep current)
const _move = { name: "Board1", x: 1.5, y: 0, z: 0 };
const _moveSingleAxis = { name: "Board1", x: 1.5 };
const _moveNameOnly = { name: "Board1" };
// Move accepts negative position values (Z < 0 is valid)
const _moveNegative = { name: "Board1", x: 0, y: 0, z: -3.62 };
// SimulateMove supports optional x/y/z (omit axis = use current)
const _simMove = { name: "Board1", x: 1.5, y: 0, z: 0 };
const _simMoveNameOnly = { name: "Board1" };
const _simMoveNegative = { name: "Board1", x: 0, y: 0, z: -3.62 };
// Resize requires name + width/height/depth (positive ints)
const _resize = { name: "Board1", width: 600, height: 400, depth: 18 };
const _simResize = { name: "Board1", width: 600, height: 400, depth: 18 };
// Create has required template_name/name/x/y/z + optional sizes/booleans
const _create = {
    template_name: "Board1", name: "Board1", x: 0, y: 0, z: 0,
    width: 800, is_wall: true,
};
// Create with negatives (Z < 0 should be allowed)
const _createNegative = {
    template_name: "Board1", name: "Board1", x: 0, y: 0, z: -3.62,
    width: 600, height: 720, depth: 18,
};
// Create with gaps
const _createGap = {
    template_name: "F1", name: "F1", x: 0, y: 0, z: 0,
    width: 600, height: 716, depth: 18,
    gapLeft: 2, gapRight: 2, gapTop: 2, gapBottom: 2, is_facade: true,
};
// Create radial shelf (rectangular board with one rounded corner)
const _createRadial = {
    template_name: "RS1", name: "RS1", x: 0, y: 0, z: 0,
    is_radial_shelf: true, width: 600, height: 18, depth: 400, corner_radius: 200,
};
// Legacy radial call (radius + depth-as-thickness)
const _createRadialLegacy = {
    template_name: "RS2", name: "RS2", x: 0, y: 0, z: 0,
    is_radial_shelf: true, radius: 450, depth: 18,
};
// Rotate: Euler angles in degrees
const _rotate = { name: "Board1", x: 0, y: 90, z: 0 };
// Floor resize (no name field)
const _resizeFloor = { width: 3000, height: 2000, depth: 100 };
// Lock
const _lock = { name: "Board1", locked: true };
// Simple name-only queries
const _nameOnly = { name: "Board1" };
const _modOnly = { module: "М1" };
// Object path operations
const _objPath = { object_path: "Parent/Child" };
const _setActive = { object_path: "Board1", active: true };
const _setTransform = { object_path: "Board1", x: 0, y: 1, z: 2 };
const _setTransformNegative = { object_path: "Board1", x: -2, y: 0, z: -3.5 };
const _setTransformNameOnly = { object_path: "Board1" };
// Settings
const _setSetting = { name: "snap_enabled", value: true };
const _snapVerbose = { enabled: true };
// Console logs (optional count)
const _consoleLogs = {};
const _consoleLogsWithCount = { count: 50 };
// Other
const _find = { name_filter: "Board" };
const _export = { path: "/tmp/spec.csv" };
const _menu = { menu_path: "Edit/Undo" };
// Snap diagnose (optional x/y/z, negative values OK)
const _snapDiag = { name: "Board1" };
const _snapDiagFull = { name: "Board1", x: 1, y: 2, z: 3 };
const _snapDiagNegative = { name: "Board1", z: -3.62 };
// Modules
const _createMod = { name: "М1", members: ["A", "B"] };
const _addToMod = { module: "М1", name: "A" };
export {};
