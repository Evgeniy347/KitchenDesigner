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
  corner_radius?: number;
  is_wall?: boolean;
  is_facade?: boolean;
  is_assembled?: boolean;
  is_radial_shelf?: boolean;
  is_floor?: boolean;
  is_drawer?: boolean;
  drawer_type?: string;
  drawer_length?: number;
  drawer_color?: string;
  drawer_internal_width?: number;
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

export interface SetDrawerPropertiesParams {
  name: string;
  drawer_type?: string;
  drawer_length?: number;
  drawer_color?: string;
  internal_width?: number;
  is_double?: boolean;
  is_upper?: boolean;
  paired_drawer_name?: string;
  attached_facade_name?: string;
}

export interface NamedElementParams {
  name: string;
}

/* ── Batch tools ───────────────────────────────────────────────────────── */

export interface GetElementsParams {
  names?: string[];
  filter?: string;
  summary?: boolean;
}

export interface GetViolationsParams {
  names?: string[];
}

export interface BatchOp {
  name: string;
  x?: number;
  y?: number;
  z?: number;
  width?: number;
  height?: number;
  depth?: number;
  rot_x?: number;
  rot_y?: number;
  rot_z?: number;
  locked?: boolean;
  material?: string;
}

export interface BatchEditParams {
  ops: BatchOp[];
  dry_run?: boolean;
}

export interface CloneElementParams {
  name: string;
  count?: number;
  offset_x?: number;
  offset_y?: number;
  offset_z?: number;
}

/* ── High-level placement ──────────────────────────────────────────────── */

export type FaceName = "left" | "right" | "bottom" | "top" | "back" | "front";

export interface AlignElementParams {
  name: string;
  face: FaceName;
  target: string;
  target_face: FaceName;
  gap_mm?: number;
}

export interface GetFreeSpaceParams {
  between: string[]; // exactly 2 names
}

export interface DistributeEvenlyParams {
  names: string[]; // 3+
  axis: "x" | "y" | "z";
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
  value?: boolean;
  number?: number;
}

export interface SnapVerboseParams {
  enabled: boolean;
}

/* ── Common response shapes ────────────────────────────────────────────── */

export interface Vector3Json {
  x: number;
  y: number;
  z: number;
}

export interface FaceObstruction {
  neighbor: string;
  distanceFromFaceMm: number;
  overlapWidthMm: number;
  overlapHeightMm: number;
}

export interface OpeningViolation {
  neighbor: string;
  openingMode: string;
  collisionAtProgress: number;
  collisionOverlapMm: number;
}

export interface ElementInfo {
  name: string;
  type: string;
  dimX: number;
  dimY: number;
  dimZ: number;
  posX: number;
  posY: number;
  posZ: number;
  rotX: number;
  rotY: number;
  rotZ: number;
  active: boolean;
  locked: boolean;
  moduleId: number;
  moduleName?: string | null;
  materialId?: string | null;
  hasViolations: boolean;
  aabbMinX: number;
  aabbMinY: number;
  aabbMinZ: number;
  aabbMaxX: number;
  aabbMaxY: number;
  aabbMaxZ: number;
  /** Габариты в МИРОВЫХ осях (мм, из AABB) — учитывают поворот. */
  worldDimX: number;
  worldDimY: number;
  worldDimZ: number;
  effectiveDimX: number;
  effectiveDimY: number;
  effectiveDimZ: number;
  faceGaps?: AxisGapInfo[] | null;
  cornerRadius?: number;
  facadeMode?: string | null;
  faceNormalX?: number | null;
  faceNormalY?: number | null;
  faceNormalZ?: number | null;
  faceInward?: boolean | null;
  faceObstructions?: FaceObstruction[] | null;
  openingViolations?: OpeningViolation[] | null;
}

export interface AxisGapInfo {
  axis: string;
  neighbor?: string | null;
  gapMM: number;
  /** |зазор| < 0.5 мм — детали вплотную (НЕ нарушение). */
  touching: boolean;
  isOverlap: boolean;
}

export type OverlapSeverity = "touching" | "minor_overlap" | "overlap" | "deep_penetration";

/** Одно нарушение элемента в конверте мутаций / get_violations. */
export interface ElementViolation {
  kind: "overlap" | "disconnected" | "facade_facing_inward" | "face_obstruction"
      | "opening_collision" | "drawer_invalid";
  neighbor?: string;
  severity?: OverlapSeverity;
  penetrationMm?: number;
  overlapXmm?: number;
  overlapYmm?: number;
  overlapZmm?: number;
  message?: string;
  openingMode?: string;
  collisionAtProgress?: number;
  collisionOverlapMm?: number;
  distanceFromFaceMm?: number;
  overlapWidthMm?: number;
  overlapHeightMm?: number;
}

/** Единый конверт ответа всех мутаций. */
export interface MutationResult {
  ok: boolean;
  element: ElementInfo;
  violations: ElementViolation[];
  sceneViolationCount: number;
}

export interface ViolationEntry {
  name: string;
  type: string;
  overlapsWith: Array<{
    kind: "overlap";
    neighbor: string;
    severity: OverlapSeverity;
    penetrationMm: number;
    overlapXmm: number;
    overlapYmm: number;
    overlapZmm: number;
  }>;
  disconnected: boolean;
  faceNormal?: Vector3Json | null;
  faceInward?: boolean;
  faceObstructions?: FaceObstruction[] | null;
  openingViolations?: OpeningViolation[] | null;
}

/* ── Map method name to its params type ────────────────────────────────── */

export interface ParamMap {
  readonly ping: Record<string, never>;
  readonly get_status: Record<string, never>;
  readonly get_scene_hierarchy: Record<string, never>;
  readonly get_all_elements: Record<string, never>;
  readonly get_specification: Record<string, never>;
  readonly get_violations: GetViolationsParams;
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

  readonly get_elements: GetElementsParams;
  readonly batch_edit: BatchEditParams;
  readonly clone_element: CloneElementParams;
  readonly align_element: AlignElementParams;
  readonly distribute_evenly: DistributeEvenlyParams;
  readonly get_free_space: GetFreeSpaceParams;

  readonly move_element: MoveElementParams;
  readonly resize_element: ResizeElementParams;
  readonly resize_floor: ResizeFloorParams;
  readonly rotate_element: RotateElementParams;
  readonly create_element: CreateElementParams;
  readonly convert_element: ConvertElementParams;
  readonly set_element_lock: LockElementParams;
  readonly set_facade_mode: FacadeModeParams;
  readonly set_drawer_properties: SetDrawerPropertiesParams;
  readonly cycle_drawer_animation: NamedElementParams;

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
