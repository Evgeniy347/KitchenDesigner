// ============================================================================
//  Typed parameter interfaces for each MCP method.
//  One-to-one with C# ParamsXxx classes in McpModels.cs.
// ============================================================================

/* ── Common param shapes ───────────────────────────────────────────────── */

export interface NameParams {
  name: string;
}

export interface ModuleParams {
  module: string;
}

export interface ObjectPathParams {
  object_path: string;
}

/* ── Read: elements ────────────────────────────────────────────────────── */

export interface ConsoleLogsParams {
  count?: number;
}

export interface FindObjectsParams {
  name_filter: string;
}

export interface ExportCsvParams {
  path: string;
}

export interface SnapDiagnoseParams {
  name: string;
  x?: number;
  y?: number;
  z?: number;
}

/* ── Simulation (dry-run) ──────────────────────────────────────────────── */

export interface SimulateMoveParams {
  name: string;
  x?: number;
  y?: number;
  z?: number;
}

export interface SimulateResizeParams {
  name: string;
  width: number;
  height: number;
  depth: number;
}

/* ── Edit: elements ────────────────────────────────────────────────────── */

export interface MoveElementParams {
  name: string;
  x?: number;
  y?: number;
  z?: number;
}

export interface RotateElementParams {
  name: string;
  x: number;
  y: number;
  z: number;
}

export interface ResizeElementParams {
  name: string;
  width: number;
  height: number;
  depth: number;
}

export interface ResizeFloorParams {
  width: number;
  height: number;
  depth: number;
}

export interface CreateElementParams {
  template_name: string;
  name: string;
  x: number;
  y: number;
  z: number;
  width?: number;
  height?: number;
  depth?: number;
  is_wall?: boolean;
  is_facade?: boolean;
  is_assembled?: boolean;
  is_floor?: boolean;
  fill?: string;
  gapLeft?: number;
  gapRight?: number;
  gapTop?: number;
  gapBottom?: number;
}

export interface ConvertElementParams {
  name: string;
  target: string;
  fill?: string;
}

export interface LockElementParams {
  name: string;
  locked: boolean;
}

export interface FacadeModeParams {
  name: string;
  mode: string;
}

/* ── Advanced: raw objects ─────────────────────────────────────────────── */

export interface SetActiveParams {
  object_path: string;
  active: boolean;
}

export interface SetTransformParams {
  object_path: string;
  x?: number;
  y?: number;
  z?: number;
}

export interface MenuItemParams {
  menu_path: string;
}

/* ── Modules ───────────────────────────────────────────────────────────── */

export interface CreateModuleParams {
  name: string;
  members: string[];
}

export interface AddToModuleParams {
  module: string;
  name: string;
}

/* ── Settings ──────────────────────────────────────────────────────────── */

export interface SetSettingParams {
  name: string;
  value: boolean;
}

export interface SnapVerboseParams {
  enabled: boolean;
}

/* ── Map method name to its params type ────────────────────────────────── */

export interface ParamMap {
  readonly ping: Record<string, never>;
  readonly get_status: Record<string, never>;
  readonly get_scene_hierarchy: Record<string, never>;
  readonly get_all_elements: Record<string, never>;
  readonly get_specification: Record<string, never>;
  readonly get_violations: Record<string, never>;
  readonly get_floor_info: Record<string, never>;
  readonly get_settings: Record<string, never>;
  readonly get_undo_stack_info: Record<string, never>;
  readonly get_modules: Record<string, never>;
  readonly undo: Record<string, never>;
  readonly redo: Record<string, never>;
  readonly exit_module_edit: Record<string, never>;
  readonly take_screenshot: Record<string, never>;
  readonly enter_play_mode: Record<string, never>;
  readonly exit_play_mode: Record<string, never>;

  readonly get_element_info: NameParams;
  readonly get_element_gaps: NameParams;
  readonly get_element_debug: NameParams;
  readonly select_element: NameParams;
  readonly delete_element: NameParams;
  readonly add_wall_component: NameParams;
  readonly remove_from_module: NameParams;

  readonly find_objects: FindObjectsParams;
  readonly get_object_info: ObjectPathParams;
  readonly delete_object: ObjectPathParams;
  readonly set_object_active: SetActiveParams;
  readonly set_position: SetTransformParams;
  readonly set_rotation: SetTransformParams;
  readonly set_scale: SetTransformParams;

  readonly snap_diagnose: SnapDiagnoseParams;
  readonly simulate_move: SimulateMoveParams;
  readonly simulate_resize: SimulateResizeParams;

  readonly move_element: MoveElementParams;
  readonly resize_element: ResizeElementParams;
  readonly resize_floor: ResizeFloorParams;
  readonly rotate_element: RotateElementParams;
  readonly create_element: CreateElementParams;
  readonly convert_element: ConvertElementParams;
  readonly set_element_lock: LockElementParams;
  readonly set_facade_mode: FacadeModeParams;

  readonly module_info: ModuleParams;
  readonly dissolve_module: ModuleParams;
  readonly enter_module_edit: ModuleParams;
  readonly create_module: CreateModuleParams;
  readonly add_to_module: AddToModuleParams;

  readonly set_setting: SetSettingParams;
  readonly set_snap_verbose: SnapVerboseParams;
  readonly get_console_logs: ConsoleLogsParams;
  readonly export_specification_csv: ExportCsvParams;
  readonly execute_menu_item: MenuItemParams;
}

export type UnityMethod = keyof ParamMap;
export type ParamsOf<M extends UnityMethod> = ParamMap[M];
