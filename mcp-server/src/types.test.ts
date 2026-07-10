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
  UnityMethod,
} from "./types.js";

// ── Validate that every method name maps correctly ───────────────────────

type _CheckGetElementInfo = ParamsOf<"get_element_info"> extends NameParams ? true : never;
type _CheckMoveElement = ParamsOf<"move_element"> extends MoveElementParams ? true : never;
type _CheckCreateModule = ParamsOf<"create_module"> extends CreateModuleParams ? true : never;
type _CheckPing = ParamsOf<"ping"> extends Record<string, never> ? true : never;

// ── Validate required fields ─────────────────────────────────────────────

// Move/MoveSimulate require name + x/y/z
const _move: MoveElementParams = { name: "Board1", x: 1.5, y: 0, z: 0 };
const _simMove: SimulateMoveParams = { name: "Board1", x: 1.5, y: 0, z: 0 };

// Resize requires name + width/height/depth (int)
const _resize: ResizeElementParams = { name: "Board1", width: 600, height: 400, depth: 18 };
const _simResize: SimulateResizeParams = { name: "Board1", width: 600, height: 400, depth: 18 };

// Create has required + optional fields
const _create: CreateElementParams = {
  template_name: "Board1", name: "Board1", x: 0, y: 0, z: 0,
  width: 800, is_wall: true,
};

// Rotate
const _rotate: RotateElementParams = { name: "Board1", x: 0, y: 90, z: 0 };

// Floor resize (no name)
const _resizeFloor: ResizeFloorParams = { width: 3000, height: 2000, depth: 100 };

// Lock
const _lock: LockElementParams = { name: "Board1", locked: true };

// Simple name-only queries
const _nameOnly: NameParams = { name: "Board1" };
const _modOnly: ModuleParams = { module: "М1" };

// Object path
const _objPath: ObjectPathParams = { object_path: "Parent/Child" };
const _setActive: SetActiveParams = { object_path: "Board1", active: true };
const _setTransform: SetTransformParams = { object_path: "Board1", x: 0, y: 1, z: 2 };

// Settings
const _setSetting: SetSettingParams = { name: "snap_enabled", value: true };
const _snapVerbose: SnapVerboseParams = { enabled: true };

// Console logs
const _consoleLogs: ConsoleLogsParams = {};
const _consoleLogsWithCount: ConsoleLogsParams = { count: 50 };

// Other
const _find: FindObjectsParams = { name_filter: "Board" };
const _export: ExportCsvParams = { path: "/tmp/spec.csv" };
const _menu: MenuItemParams = { menu_path: "Edit/Undo" };

// Snap diagnose (optional x/y/z)
const _snapDiag: SnapDiagnoseParams = { name: "Board1" };
const _snapDiagFull: SnapDiagnoseParams = { name: "Board1", x: 1, y: 2, z: 3 };

// Modules
const _createMod: CreateModuleParams = { name: "М1", members: ["A", "B"] };
const _addToMod: AddToModuleParams = { module: "М1", name: "A" };

// ── Validate no-param methods ────────────────────────────────────────────

const _empty: Record<string, never> = {};
const _noParams1: ParamsOf<"ping"> = _empty;
const _noParams2: ParamsOf<"get_status"> = _empty;
const _noParams3: ParamsOf<"undo"> = _empty;
const _noParams4: ParamsOf<"redo"> = _empty;
const _noParams5: ParamsOf<"get_all_elements"> = _empty;
const _noParams6: ParamsOf<"take_screenshot"> = _empty;
const _noParams7: ParamsOf<"enter_play_mode"> = _empty;
const _noParams8: ParamsOf<"exit_play_mode"> = _empty;
