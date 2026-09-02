using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP.Contract
{
    public static class McpGuideTexts
    {
        public const string DefaultTopic = "workflow";

        public static readonly Dictionary<string, string> Topics = new Dictionary<string, string>
        {
            ["workflow"] =
@"KITCHEN DESIGNER — WORKFLOW CHEAT-SHEET
Other topics: guide {topic:""planning"" | ""bulk"" | ""elements"" | ""fields"" |
""drawers"" | ""violations""}

FIRST CALL OF A SESSION
  get_project_instructions -> the project's own conventions (wall thicknesses,
  board thickness, gaps, naming). They OVERRIDE any default assumption here.

UNITS (the #1 mistake)
  position x/y/z  = METERS      (1.5 -> 1.5 m)   [element tools]
  size w/h/d      = MILLIMETERS (600 -> 600 mm)
  Plan/bulk tools (apply_floorplan, create_walls, create_floor, add_opening,
  move, set_attr) are MILLIMETRES ONLY — no meters anywhere in them.
  1 m = 1000 mm.  dimZ = board thickness (smallest side, usually 18 mm).

READING THE SCENE (cheap -> expensive)
  get_scene_tree                                -> modules, bboxes, type counts
  get {names:[""B4_Side_L""]}                     -> compact corner geometry (MM)
  get_elements {filter:""B4_*"", summary:true}     -> one cabinet, compact
  get_elements {names:[""A"",""B""]}                -> full info for exactly these
  get_free_space {between:[""Side_L"",""Side_R""]} -> the empty box between panels
  get_all_elements                              -> LEGACY full dump, very large

DRAWING THE APARTMENT — declarative, not by hand
  apply_floorplan builds rooms/walls/floors/openings in ONE call and ONE undo.
  Never place walls as boards with hand-computed centers. See guide
  {topic:""planning""}.

CHANGING MANY BOARDS AT ONCE — let the server do the arithmetic
  set_attr / move / align / resize_module take a SELECTOR and an intent, so you
  pass one number instead of per-board coordinates. See guide {topic:""bulk""}.

EDITING SINGLE ELEMENTS (every mutation returns element info + ITS violations)
  edit_elements {ops:[{name:""P1"", x:1.2}, {name:""P2"", width:600, rot_y:90}]}
     - MANY changes in ONE transactional call, single undo step.
     - dry_run:true = simulate first, nothing is kept.
  create_elements {items:[{name:""Shelf1"", type:""board"", x:.., width:.., ..}]}
  align_elements {ops:[{name:""Shelf1"", face:""left"", target:""Side_L"",
                       target_face:""right""}]}   - face-to-face, no math
  clone_elements {ops:[{name:""Shelf1"", count:2, offset_y:0.3}]} -> _2, _3
  distribute_evenly {names:[...3+...], axis:""y""}
  convert_elements / delete_elements / select_elements — all batch, all atomic.

CHECKING
  Look at ""violations"" in EVERY mutation response: [] means this element is clean.
  If sceneViolationCount grew after your change, call get_violations (optionally
  with names:[...]) to see what else broke.
  severity in overlaps: touching < minor_overlap < overlap < deep_penetration.
  deep_penetration means the board is INSIDE another one - that is never OK.

STEP-BY-STEP EXAMPLE: three shelves between two panels
  1. get_free_space {between:[""Side_L"",""Side_R""]}   -> inner width/position
  2. create_elements {items:[{name:""Shelf1"", x:.., y:.., z:.., width:..,
                              height:.., depth:18}]}
  3. align_elements  {ops:[{name:""Shelf1"", face:""left"", target:""Side_L"",
                            target_face:""right""}]}
  4. clone_elements  {ops:[{name:""Shelf1"", count:2, offset_y:0.3}]}
  5. get_violations {names:[""Shelf1"",""Shelf1_2"",""Shelf1_3""]}  -> expect []

SAFETY
  LOCKED elements (locked:true in element info) reject changes. Unlock with
  edit_elements {locked:false} ONLY if the user explicitly allowed it.
  create_elements / edit_elements / clone_elements / delete_elements are undoable.
  Prefer them over the ADVANCED raw tools (set_position, set_scale,
  delete_object) — those bypass undo, validation and snapping.",

            ["planning"] =
@"FLOORPLANS — DECLARE, DON'T COMPUTE

The server owns the geometry: you send 2D points in MM and it derives wall
thickness, centers, rotations, corner joints, floor meshes and wall cutouts.
Never emit walls/floors as plain boards with hand-computed centers.

COORDINATES
  X = east(+), Z = north(+), Y = up. Everything in MM.
  origin_x_mm / origin_z_mm place the declaration's (0,0) in the world; all
  points are relative to it. Convention: origin = inner south-west corner.

apply_floorplan {id, origin_x_mm, origin_z_mm, points[], rooms[], walls[],
                 floors[], openings[]}
  points   {id, x, z}                     — named corners
  rooms    {id, poly:[point ids], kind:""bearing""|""partition"", height,
            top_y_mm, thickness_mm}       — makes a floor + 4 walls; an edge
                                            shared with another room REUSES the
                                            same wall (never two walls in one)
  walls    {id, from, to, kind, height, thickness_mm?}  — explicit single walls;
            thickness_mm overrides the kind's project instruction for that one
            wall (a 150 mm facade in a 250/125 project), so a plan can carry
            more than the two default thicknesses
  floors   {id, poly:[3+ point ids], top_y_mm, thickness_mm}
  openings {id, wall, kind:""window""|""door"", offset_mm (from the wall's
            declared start point), width, height, sill_mm}
  Thickness per kind and the default floor thickness come from the project
  instructions (get_project_instructions) — no hardcoded presets. If an
  instruction the plan needs is missing, the call fails and NOTHING changes.

  ONE call = ONE undo step. Idempotent by id: call it again with a changed
  declaration and the scope is reconciled — elements that disappeared from the
  declaration are deleted. Any failure rolls the whole transaction back.

preview_floorplan — same compiler, no scene change: returns validation errors
  and a deterministic top-down SVG. Use it to show the user a plan first.

GRANULAR PLAN TOOLS (same geometry engine, when a full declaration is overkill)
  create_walls {origin_x_mm, origin_z_mm, base_y_mm, segments:[{name, from_x,
                from_z, to_x, to_z, kind, height}]}
  create_floor {name, origin_x_mm, origin_z_mm, top_y_mm, thickness_mm, poly:
                [{x,z}]}
  add_opening  {name, wall, kind, offset_mm, width, height, sill_mm}
  All three are idempotent by name and are ONE undo step each.

EXAMPLE — one room with a window
  apply_floorplan {id:""flat"", origin_x_mm:-1585, origin_z_mm:-3620,
    points:[{id:""sw"",x:0,z:0},{id:""se"",x:3170,z:0},
            {id:""ne"",x:3170,z:7240},{id:""nw"",x:0,z:7240}],
    rooms:[{id:""kitchen"", poly:[""sw"",""se"",""ne"",""nw""], kind:""bearing"",
            height:2700}],
    openings:[{id:""w1"", wall:""kitchen_sw_nw"", kind:""window"", offset_mm:4720,
               width:1200, height:1400, sill_mm:800}]}
  The response is terse: counts + violations. Wall ids generated by a room are
  <room>_<from>_<to>; read them back with get_scene_tree if unsure.",

            ["bulk"] =
@"BULK / RELATIONAL OPERATIONS — PASS INTENT, NOT COORDINATES

The point: ""make every board 16 mm instead of 18"", ""push all modules against
the wall"", ""widen module B4 by 100 mm"" must be ONE call with ONE number. The
server picks the elements and recomputes every board.

SELECTOR (a string; space-separated clauses, ALL must match)
  B4_*                     name mask ('*' = glob; without '*' = substring)
  name:PATTERN             same, explicit
  type:board|wall|floor|window|door|drawer|facade|assembled_facade|
       radial_shelf|panel|table|radius_table|stool|chair|sofa|pouffe|bed|pillar|
       screw_leg|light
  module:NAME / group:NAME by module name (mask allowed)
  thickness==18            compare a dimension in MM; also width/height/depth
                           with == != >= <= > <
  all_boards               plain boards only
  all_modules              anything that belongs to a module
  *  /  all                everything (the floor anchor is never matched)
  Example: ""all_boards thickness==18"" or ""module:B4 type:facade"".

TOOLS
  set_attr {selector, thickness|width|height|depth (MM), material, locked}
      set_attr {selector:""all_boards thickness==18"", thickness:16}
  move {selector, dx, dy, dz}          shift the whole selection in MM
  align {selector, target, face, target_face, gap_mm}
      Moves each matched loose element — and each matched MODULE as one rigid
      unit — until its face meets the target's face. ""all modules to the wall""
      = align {selector:""all_modules"", target:""wall_north"", face:""back""}.
  resize_module {module, axis:""x""|""y""|""z"", delta_mm}
      Grows/shrinks a module by a delta: the near side stays, the far side
      moves, spanning boards (bottom/back/facades) stretch. You pass 3 values,
      not 15 coordinates. axis defaults to the module's stored width_axis.
  group {id, names[], width_axis}      declare/replace a module and annotate
                                       the axis resize_module should use.

  Every one of them is atomic, ONE undo step, and returns
  {matched, updated, sceneViolationCount} — no per-element dumps.

RELATED: get_modules / module_info list modules; add_to_module,
remove_from_module, dissolve_module, create_module manage membership one by one
(group does it declaratively in a single call).",

            ["elements"] =
@"ELEMENT TYPES (field ""type"" in responses)

KitchenElement        Plain board. The default type of create_elements.
                      Size = edit_elements (width/height/depth, MM).
Wall (component)      A board that is a structural ANCHOR. Other boards must
                      connect to a wall/floor. Create walls with create_walls or
                      apply_floorplan — create_elements {type:""wall""} is the
                      low-level fallback.
FloorElement          A floor slab. Create with create_floor / apply_floorplan
                      (polygon, corner-anchored). create_elements {type:""floor""}
                      makes a single rectangular slab.
BasePlate             The scene's floor anchor singleton (resize_floor). Legacy:
                      real rooms use FloorElement.
FacadeElement         Door/front, gaps default to 2 MM per side. It FLOATS in
                      its opening: a facade with gap > 0 is exempt from
                      connectivity. Opening mode: edit_elements {mode:..}
                      (18 modes, see the facadeMode field).
AssembledFacadeElement Framed (assembled) facade with real frame geometry.
                      create_elements {type:""assembled_facade"",
                      fill:""blind|glass|open""}.
RadialShelfElement    Board with ONE rounded corner (type:""radial_shelf"");
                      radius via edit_elements {corner_radius:..}.
PanelElement          ДВП/ХДФ back panel whose gaps count toward its bounding
                      box, so it seats into grooves (type:""panel"").
DrawerElement         GTV drawer (sliding box). SIZE COMES FROM ITS PARAMETERS -
                      width/height/depth are REJECTED; use the drawer fields of
                      edit_elements. See guide {topic:""drawers""}.
TableElement          Table (tabletop + 4 legs), type:""table"". leg_inset_mm and
                      materials via edit_elements.
RadiusTableElement    Capsule-shaped table (type:""radius_table"").
StoolElement          Stool: seat + 4 legs (type:""stool"", 360x450x360 mm by
                      default). ONE type for both shapes — corner_radius 0 is a
                      square stool, min(width, depth)/2 a fully round one.
                      Seat and legs decors via tabletop_material/legs_material.
ChairElement          Chair: a stool with a backrest (type:""chair"",
                      400x900x400 mm by default). corner_radius rounds the SEAT
                      exactly like a stool; seat_height is the seat TOP above the
                      floor (450 mm by default). The backrest is a 20 mm panel at
                      the BACK (-Z), from the seat top to the overall height.
                      Seat and legs decors via tabletop_material/legs_material.
SofaElement           Sofa (type:""sofa"", 2000x800x900 mm by default). NO armrests:
                      four cushions instead — two upright on the back, two lying
                      flat along the sides where armrests would be. corner_radius
                      rounds the solid base BLOCK (120 mm by default); seat_height
                      is that block's top above the floor (360 mm by default), and
                      what is left up to height becomes the back. Body and cushion
                      decors via tabletop_material/legs_material.
PouffeElement         Pouffe (type:""pouffe"", 450x400x450 mm by default). An
                      upholstered box standing on the floor with NO legs, plus a
                      soft seat cushion on top. corner_radius rounds the whole box
                      in plan (120 mm by default; the maximum makes it round);
                      pouffe_seat_thickness is the cushion (50 mm by default,
                      clamped to 20..height/3) and the rest of the height is the
                      box. Body and cushion decors via tabletop_material/
                      legs_material.
BedElement            Bed (type:""bed"", 1800x900x2000 mm by default — width x
                      height x LENGTH: the length runs along Z, because the
                      headboard faces -Z like every other back in this family).
                      Built from 100 mm legs, a 200 mm frame, a 180 mm mattress
                      and pillows on top; the mattress top is 480 mm above the
                      floor. TWO switches, both of which RESET dimensions:
                      bed_double true = 1800 wide with two pillows and six legs,
                      false = 900 wide with one pillow and four legs (switching
                      resets width/height/depth to that type's defaults and
                      discards a manual resize — switching there and back is two
                      resets, not an undo); bed_headboard true/false resets the
                      HEIGHT only (900 with a headboard, 600 without, which is
                      the top of the pillows). Between switches width, length and
                      height stay freely editable; height is clamped to 600
                      without a headboard and 700 with one. Carcass and mattress
                      decors via tabletop_material/legs_material.
PillarElement         Pillar (type:""pillar"", mid_height_mm, diameter_mm).
ScrewLegElement       Screw-in levelling leg with a threaded insert
                      (type:""screw_leg""). A foot (screw_base_diameter_mm x
                      screw_base_height_mm, 25x8 mm by default) plus a threaded
                      rod (screw_thread M6/M8/M10, screw_thread_length_mm 50 by
                      default) that goes screw_insertion_mm (25) INTO the part
                      above it. Drop it under any part: it takes that part as its
                      host, re-derives the thread length so the foot stands on the
                      floor, and shares space with the host legally — a thread
                      poking through a 16 mm board is not a collision. Snapping
                      centres it on the host; off-centre on a side thinner than
                      25 mm is reported as LEG-01.
SinkElement / CooktopElement
                      Recessed appliances (type:""sink"" / ""cooktop""). They sit
                      on a plain board with a horizontal face (the countertop),
                      snap to it and cut a hole in it. The cooktop is TWO boxes:
                      width/depth are the 5 mm plate on top, height is the TOTAL
                      (plate + the box inside the countertop), and the box itself
                      is cutout_width/cutout_depth via edit_elements. Its cutout
                      magnets flush to sides and facades under the countertop;
                      overlapping a carcass board there is a violation.
                      A cooktop created with model:""Bosch PUE611BB5E"" is a fixed
                      appliance: 592x522 glass, 51 total height, 560x490 cutout.
                      Its size and cutout come from the manufacturer — width,
                      height, depth and cutout_* are REJECTED by edit_elements.
OvenElement           Built-in electric oven (type:""oven""), one model only:
                      Bosch HBA514BB3. NOT recessed — it needs no host board and
                      cuts no hole; it just stands in a column like a carcass
                      part. Bounding box 594x595x568: a 594x595x19.5 front
                      (control strip 96 high on top, black door glass 499 below)
                      plus a 560x548x570 hollow body behind it. The handle
                      sticks out 50 mm IN FRONT of that box on purpose.
                      COLLISIONS ARE CHECKED AGAINST THE BODY ONLY (560x548x570,
                      recessed behind the front and 25 mm below its top): the
                      594 wide front is wider than any 600-module opening by
                      design and lies OVER the side panels — counting it would
                      make every normal installation a COL-01.
                      The door drops DOWN around its bottom edge:
                      edit_elements {is_open:true}; state in element.oven.isOpen.
                      Everything else is fixed by the manufacturer —
                      width/height/depth are REJECTED by edit_elements.
DishwasherElement     Fully integrated dishwasher (type:""dishwasher""), one
                      model only: Bosch SMV25EX02E. Bounding box 598x815x550 —
                      the appliance itself, niche 600 wide. It has NO front
                      panel of its own: the furniture facade is a SEPARATE
                      element attached by name via edit_elements
                      attached_facade_name, exactly like a drawer. That facade
                      is 600 wide and 655..725 high (720 nominal); its height
                      sets the plinth (body height minus facade, 90..220).
                      A facade outside that range is reported by get_violations
                      as DWH-03, NOT rejected. That facade hangs on BRACKETS, so
                      it counts as attached while it is up to 5 mm away from the
                      appliance front (dishwasher.facadeMountGapMM) — a drawer
                      front, screwed on flush, still requires real contact.
                      The bottom 90 mm (baseHeightMM) is the appliance's own
                      BASE, recessed 100 mm (baseSetbackMM) so toes fit under
                      the facade; the 725 mm above it is the door. COLLISIONS
                      SKIP that bottom band — plinth boards and module legs
                      belong in the niche in front of the base — so standing on
                      something is checked separately: get_violations reports
                      DWH-05 when nothing is under the sole or the appliance is
                      sunk into the floor.
                      The door drops DOWN around its bottom edge and takes the
                      attached facade with it: edit_elements {is_open:true};
                      state in element.dishwasher.isOpen.
                      Size is fixed by the manufacturer — width/height/depth are
                      REJECTED by edit_elements.
WindowElement / DoorElement
                      Openings. Prefer add_opening / apply_floorplan: they
                      attach the opening to a wall and cut the hole. Creating
                      them with create_elements also snaps to a nearby wall.

CONVERSIONS: convert_elements switches board <-> facade <-> assembled_facade <->
radial_shelf in place, keeping name/size/position/material.

MODULES: named groups that move together (group, create_module, add_to_module,
...). An element's module is in moduleId/moduleName of its info.",

            ["fields"] =
@"RESPONSE FIELD SEMANTICS (element info)

name                  Unique text id. All tools address elements by exact name.
type                  Element class - see guide {topic:""elements""}.
dimX/dimY/dimZ        LOCAL size in MM (dimZ = thickness). Does NOT change when
                      the board is rotated.
worldDimX/Y/Z         WORLD-axis extents in MM (from AABB). USE THESE when the
                      board is rotated: after rot_y=90 a 600x18 board has
                      worldDimX=18, worldDimZ=600.
posX/posY/posZ        Center position in METERS (world).
rotX/rotY/rotZ        Euler angles in DEGREES.
aabbMin*/aabbMax*     World bounding box in METERS.
effectiveDim*         dim + gaps (any element that has them). NOT rotation-aware
                      - prefer worldDim* for world-space reasoning.
locked                true = move/resize/delete will be rejected (edit_elements
                      {locked:false} unlocks).
hasViolations         true = this element overlaps something or is disconnected.
faceGaps              Per-axis nearest OPPOSITE neighbour: {axis, neighbor, gapMM,
                      touching, isOverlap}. touching=true means flush contact
                      (|gap| < 0.5 mm) - that is GOOD, not a violation.
                      Axes with no facing neighbour are omitted.
moduleId/moduleName   Group membership (0/absent = not grouped).
materialId            Decor id (list_materials).
facadeMode            Facade opening mode (""front_left"", ""drawer_out"", ...).
drawer / table / radiusTable / stool / chair / sofa / pouffe / bed   Type-specific
                      sub-objects, absent otherwise.

COMPACT v2 GEOMETRY (get, get_scene_tree) — a different, terser shape:
  {name, kind, anchor:[x,z] MM corner, size:[width,depth,height] MM, rotY,
   hasViolations, module}. Positions are the MIN corner in MM, not the center in
   meters. Use it for reasoning about layout; use get_elements for full detail.

MUTATION RESPONSES (create/edit/align/clone/...) always return:
  { ok, element: <full info above>, violations: [<THIS element's problems>],
    sceneViolationCount: <structural violations in the WHOLE scene> }
Bulk and plan tools return counts instead: {matched/created/updated/deleted,
violations, sceneViolationCount}.
violations kinds: overlap (with severity + penetrationMm), disconnected,
facade_facing_inward, face_obstruction, opening_collision, drawer_invalid.",

            ["drawers"] =
@"GTV DRAWERS (DrawerElement)

A drawer is a parametric sliding box. Its geometry is DERIVED from parameters -
width/height/depth in edit_elements are rejected; use the drawer fields instead.

PARAMETERS (all set through edit_elements)
  drawer_type      Side height: A=86, B=120, C=168, D=200 mm.
  drawer_length    Nominal slide length MM: 250/300/350/400/450/500/550/600.
  internal_width   Internal box width in MM (min 100).
  drawer_color     anthracite | white | black.

CREATE:  create_elements {items:[{name:""D1"", type:""drawer"", x:.., y:.., z:..,
                          drawer_type:""B"", drawer_length:450,
                          drawer_internal_width:400}]}

SYSTEMS (element.drawer.system): ""gtv"" (default, bought metal box - one spec
line) or ""movento"" (wooden box - explodes into separate spec parts: sides,
front, back, bottom; parts are automatic, not selectable). Create a Movento
drawer with create_elements type:""movento_drawer"".

FRONTS:  attach a facade with edit_elements {attached_facade_name:""F1""} -
         the facade then slides together with the drawer. Empty string detaches.

DOUBLE DRAWERS: two stacked boxes moving as one system:
  edit_elements {is_double:true, paired_drawer_name:""OtherDrawer""}
  (link BOTH drawers to each other; mark the upper one with is_upper:true).

ANIMATION: cycle_drawer_animation toggles a single drawer open/closed; a double
drawer cycles Closed -> BothOpen -> LowerOnly -> Closed. State is in
element.drawer: isOpen, doubleState.

VALIDATION: drawer problems (bad length, missing pair, ...) appear as
kind:""drawer_invalid"" entries in the violations list of mutation responses
and in get_violations.",

            ["violations"] =
@"VIOLATIONS - WHAT COUNTS AND WHAT DOES NOT

STRUCTURAL (make hasViolations true):
  overlap        Two boards occupy the same volume. Reported with severity:
                   touching          < 0.5 mm  - NOT a violation (flush contact)
                   minor_overlap     0.5..2 mm - tiny intrusion, usually a mistake
                   overlap           2..10 mm  - real intersection
                   deep_penetration  > 10 mm   - board is INSIDE another; never OK
                 penetrationMm = depth of intrusion; overlapX/Y/Zmm = extent per axis.
  disconnected   The board is not face-to-face connected (within 0.5 mm) to the
                 wall/floor structure. Exempt: facades with gap > 0 (they float in
                 their opening) and drawers (they live inside a cabinet).

FACADE-ONLY (reported per element, do not flip hasViolations):
  facade_facing_inward   Front face points INTO the cabinet - rotate 180 deg.
  face_obstruction       Something sits right in front of the facade (within 100 mm).
  opening_collision      The door/drawer trajectory hits a neighbour
                         (collisionAtProgress: 0..1 of the opening travel).

DRAWER-ONLY: drawer_invalid with a message (bad parameters/pairing).

TOLERANCES: everything below 0.5 mm is float noise and is filtered out server-side.
All numbers arrive rounded to 0.1 mm. Boards standing flush report touching:true,
gapMM:0 - treat that as a GOOD fit.

CHECKING: every mutation response carries the changed element's violations plus
sceneViolationCount. get_violations {names:[...]} checks specific boards;
get_violations {} audits the whole scene."
        };
    }
}
