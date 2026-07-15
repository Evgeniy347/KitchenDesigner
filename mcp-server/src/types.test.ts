// ============================================================================
//  Compile-time type tests for types.ts
//  Run: npx tsc --noEmit
// ============================================================================

import type {
  MoveElementParams,
  SimulateMoveParams,
  ResizeElementParams,
  SimulateResizeParams,
  CreateElementParams,
  RotateElementParams,
  ResizeFloorParams,
  LockElementParams,
  NameParams,
  ModuleParams,
  ObjectPathParams,
  SetActiveParams,
  SetTransformParams,
  SetSettingParams,
  SnapVerboseParams,
  ConsoleLogsParams,
  FindObjectsParams,
  ExportCsvParams,
  MenuItemParams,
  SnapDiagnoseParams,
  CreateModuleParams,
  AddToModuleParams,
  ParamMap,
  ParamsOf,
} from "./types.js";

// ── Validate that every method name maps to the correct interface ──────

type _CheckGetElementInfo = ParamsOf<"get_element_info"> extends NameParams ? true : never;
type _CheckMoveElement = ParamsOf<"move_element"> extends MoveElementParams ? true : never;
type _CheckCreateModule = ParamsOf<"create_module"> extends CreateModuleParams ? true : never;
type _CheckPing = ParamsOf<"ping"> extends Record<string, never> ? true : never;
type _CheckResize = ParamsOf<"resize_element"> extends ResizeElementParams ? true : never;
type _CheckFloor = ParamsOf<"resize_floor"> extends ResizeFloorParams ? true : never;
type _CheckRotate = ParamsOf<"rotate_element"> extends RotateElementParams ? true : never;
type _CheckLock = ParamsOf<"set_element_lock"> extends LockElementParams ? true : never;
type _CheckSnapDiag = ParamsOf<"snap_diagnose"> extends SnapDiagnoseParams ? true : never;
type _CheckAddMod = ParamsOf<"add_to_module"> extends AddToModuleParams ? true : never;

// ── Validate all no-param methods map to Record<string, never> ─────────
type IsEmpty<M extends keyof ParamMap> = ParamMap[M] extends Record<string, never> ? true : never;
type _Empty1 = IsEmpty<"ping">;
type _Empty2 = IsEmpty<"get_status">;
type _Empty3 = IsEmpty<"get_scene_hierarchy">;
type _Empty4 = IsEmpty<"get_all_elements">;
type _Empty5 = IsEmpty<"get_specification">;
type _Empty6 = IsEmpty<"get_violations">;
type _Empty7 = IsEmpty<"get_floor_info">;
type _Empty8 = IsEmpty<"get_settings">;
type _Empty9 = IsEmpty<"get_undo_stack_info">;
type _Empty10 = IsEmpty<"get_modules">;
type _Empty11 = IsEmpty<"undo">;
type _Empty12 = IsEmpty<"redo">;
type _Empty13 = IsEmpty<"exit_module_edit">;
type _Empty14 = IsEmpty<"take_screenshot">;
type _Empty15 = IsEmpty<"enter_play_mode">;
type _Empty16 = IsEmpty<"exit_play_mode">;

// ── Validate required fields — correct shape and types ──────────────────

// Move supports optional x/y/z (omit axis = keep current)
const _move: MoveElementParams = { name: "Board1", x: 1.5, y: 0, z: 0 };
const _moveSingleAxis: MoveElementParams = { name: "Board1", x: 1.5 };
const _moveNameOnly: MoveElementParams = { name: "Board1" };

// Move accepts negative position values (Z < 0 is valid)
const _moveNegative: MoveElementParams = { name: "Board1", x: 0, y: 0, z: -3.62 };

// SimulateMove supports optional x/y/z (omit axis = use current)
const _simMove: SimulateMoveParams = { name: "Board1", x: 1.5, y: 0, z: 0 };
const _simMoveNameOnly: SimulateMoveParams = { name: "Board1" };
const _simMoveNegative: SimulateMoveParams = { name: "Board1", x: 0, y: 0, z: -3.62 };

// Resize requires name + width/height/depth (positive ints)
const _resize: ResizeElementParams = { name: "Board1", width: 600, height: 400, depth: 18 };
const _simResize: SimulateResizeParams = { name: "Board1", width: 600, height: 400, depth: 18 };

// Create has required template_name/name/x/y/z + optional sizes/booleans
const _create: CreateElementParams = {
  template_name: "Board1", name: "Board1", x: 0, y: 0, z: 0,
  width: 800, is_wall: true,
};
// Create with negatives (Z < 0 should be allowed)
const _createNegative: CreateElementParams = {
  template_name: "Board1", name: "Board1", x: 0, y: 0, z: -3.62,
  width: 600, height: 720, depth: 18,
};
// Create with gaps
const _createGap: CreateElementParams = {
  template_name: "F1", name: "F1", x: 0, y: 0, z: 0,
  width: 600, height: 716, depth: 18,
  gapLeft: 2, gapRight: 2, gapTop: 2, gapBottom: 2, is_facade: true,
};

// Create radial shelf (rectangular board with one rounded corner)
const _createRadial: CreateElementParams = {
  template_name: "RS1", name: "RS1", x: 0, y: 0, z: 0,
  is_radial_shelf: true, width: 600, height: 18, depth: 400, corner_radius: 200,
};
// Legacy radial call (radius + depth-as-thickness)
const _createRadialLegacy: CreateElementParams = {
  template_name: "RS2", name: "RS2", x: 0, y: 0, z: 0,
  is_radial_shelf: true, radius: 450, depth: 18,
};

// Rotate: Euler angles in degrees
const _rotate: RotateElementParams = { name: "Board1", x: 0, y: 90, z: 0 };

// Floor resize (no name field)
const _resizeFloor: ResizeFloorParams = { width: 3000, height: 2000, depth: 100 };

// Lock
const _lock: LockElementParams = { name: "Board1", locked: true };

// Simple name-only queries
const _nameOnly: NameParams = { name: "Board1" };
const _modOnly: ModuleParams = { module: "М1" };

// Object path operations
const _objPath: ObjectPathParams = { object_path: "Parent/Child" };
const _setActive: SetActiveParams = { object_path: "Board1", active: true };
const _setTransform: SetTransformParams = { object_path: "Board1", x: 0, y: 1, z: 2 };
const _setTransformNegative: SetTransformParams = { object_path: "Board1", x: -2, y: 0, z: -3.5 };
const _setTransformNameOnly: SetTransformParams = { object_path: "Board1" };

// Settings
const _setSetting: SetSettingParams = { name: "snap_enabled", value: true };
const _snapVerbose: SnapVerboseParams = { enabled: true };

// Console logs (optional count)
const _consoleLogs: ConsoleLogsParams = {};
const _consoleLogsWithCount: ConsoleLogsParams = { count: 50 };

// Other
const _find: FindObjectsParams = { name_filter: "Board" };
const _export: ExportCsvParams = { path: "/tmp/spec.csv" };
const _menu: MenuItemParams = { menu_path: "Edit/Undo" };

// Snap diagnose (optional x/y/z, negative values OK)
const _snapDiag: SnapDiagnoseParams = { name: "Board1" };
const _snapDiagFull: SnapDiagnoseParams = { name: "Board1", x: 1, y: 2, z: 3 };
const _snapDiagNegative: SnapDiagnoseParams = { name: "Board1", z: -3.62 };

// Modules
const _createMod: CreateModuleParams = { name: "М1", members: ["A", "B"] };
const _addToMod: AddToModuleParams = { module: "М1", name: "A" };
