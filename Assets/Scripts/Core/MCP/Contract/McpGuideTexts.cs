using System.Collections.Generic;

// ============================================================================
//  Мини-справочник для агента (инструмент guide, тема по выбору).
//  Живёт в Contract: кодогенератор переносит тексты в tools.generated.ts,
//  локальный мост отвечает ими без обращения к Unity.
//  ЖЁСТКОЕ ОГРАНИЧЕНИЕ ПАПКИ: только System / System.Collections.Generic.
// ============================================================================

namespace KitchenDesigner.Core.MCP.Contract
{
    public static class McpGuideTexts
    {
        public const string DefaultTopic = "workflow";

        public static readonly Dictionary<string, string> Topics = new Dictionary<string, string>
        {
            ["workflow"] =
@"KITCHEN DESIGNER — WORKFLOW CHEAT-SHEET
Other guide topics: guide {topic:""elements"" | ""fields"" | ""drawers"" | ""violations""}

UNITS (the #1 mistake)
  position x/y/z  = METERS      (1.5 -> 1.5 m)
  size w/h/d      = MILLIMETERS (600 -> 600 mm)
  1 m = 1000 mm.  dimZ = board thickness (smallest side, usually 18 mm).

READING THE SCENE (prefer ONE batch call over many single calls)
  get_elements {filter:""B4_*"", summary:true}  -> compact list of one cabinet
  get_elements {names:[""A"",""B""]}                -> full info for exactly these
  get_all_elements                              -> everything (large!)
  get_free_space {between:[""Side_L"",""Side_R""]} -> the empty box between two panels

EDITING (every mutation returns: element info + ITS violations + sceneViolationCount)
  batch_edit {ops:[{name:""P1"", x:1.2}, {name:""P2"", width:600, rot_y:90}]}
     - MANY changes in ONE transactional call, single undo step.
     - dry_run:true = simulate first, nothing is kept.
  align_element {name:""Shelf"", face:""left"", target:""Side_L"", target_face:""right""}
     - face-to-face placement WITHOUT coordinate math (gap_mm optional).
  clone_element {name:""Shelf"", count:2, offset_y:0.3}  -> Shelf_2, Shelf_3
  distribute_evenly {names:[...3+...], axis:""y""}
  Single-element tools also exist: move_element / resize_element / rotate_element.

CHECKING
  Look at ""violations"" in EVERY mutation response: [] means this element is clean.
  If sceneViolationCount grew after your change, call get_violations (optionally
  with names:[...]) to see what else broke.
  severity in overlaps: touching < minor_overlap < overlap < deep_penetration.
  deep_penetration means the board is INSIDE another one - that is never OK.

STEP-BY-STEP EXAMPLE: three shelves between two panels
  1. get_free_space {between:[""Side_L"",""Side_R""]}   -> inner width/position
  2. create_element {name:""Shelf1"", x:.., y:.., z:.., width:.., height:.., depth:18}
  3. align_element  {name:""Shelf1"", face:""left"", target:""Side_L"", target_face:""right""}
  4. clone_element  {name:""Shelf1"", count:2, offset_y:0.3}
  5. get_violations {names:[""Shelf1"",""Shelf1_2"",""Shelf1_3""]}  -> expect []

SAFETY
  LOCKED elements (locked:true in element info) reject changes. Unlock with
  set_element_lock {locked:false} ONLY if the user explicitly allowed it.
  delete_element / batch_edit / clone_element are undoable with undo.",

            ["elements"] =
@"ELEMENT TYPES (field ""type"" in responses)

KitchenElement        Plain board. The default of create_element.
                      Size = resize_element / batch_edit (width/height/depth, MM).
Wall (component)      A board that is a structural ANCHOR (create_element {is_wall:true}
                      or add_wall_component). Other boards must connect to a wall/floor.
BasePlate (floor)     The floor plate: create_element {is_floor:true}, resize_floor.
FacadeElement         Door/front with gaps (gap_left/right/top/bottom, MM). It FLOATS in
                      its opening: a facade with gap > 0 is exempt from connectivity.
                      Opening mode: set_facade_mode (18 modes, see facadeMode field).
AssembledFacadeElement Framed (assembled) facade with real frame geometry.
                      create_element {is_assembled:true, fill:""blind|glass|open""}.
RadialShelfElement    Board with ONE rounded corner: create_element {is_radial_shelf:true,
                      corner_radius:..}. Radius via set_radial_shelf_properties.
DrawerElement         GTV drawer (sliding box). SIZE COMES FROM ITS PARAMETERS -
                      resize is REJECTED; use set_drawer_properties (type A/B/C/D,
                      drawer_length, internal_width). See guide {topic:""drawers""}.
TableElement          Table (tabletop + 4 legs): create_element {is_table:true}.
                      leg_inset_mm and materials via set_table_properties.
RadiusTableElement    Capsule-shaped table: create_element {is_radius_table:true}.

CONVERSIONS: convert_element switches board <-> facade <-> assembled_facade <->
radial_shelf in place, keeping name/size/position/material.

MODULES: named groups that move together (create_module, add_to_module, ...).
An element's module is in moduleId/moduleName of its info.",

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
effectiveDim*         dim + facade gaps (facades only). NOT rotation-aware -
                      prefer worldDim* for world-space reasoning.
locked                true = move/resize/delete will be rejected (set_element_lock).
hasViolations         true = this element overlaps something or is disconnected.
faceGaps              Per-axis nearest OPPOSITE neighbour: {axis, neighbor, gapMM,
                      touching, isOverlap}. touching=true means flush contact
                      (|gap| < 0.5 mm) - that is GOOD, not a violation.
                      Axes with no facing neighbour are omitted.
moduleId/moduleName   Group membership (0/absent = not grouped).
materialId            Decor id (list_materials).
facadeMode            Facade opening mode (""front_left"", ""drawer_out"", ...).
drawer / table / radiusTable   Type-specific sub-objects, absent otherwise.

MUTATION RESPONSES (move/resize/rotate/create/align/...) always return:
  { ok, element: <full info above>, violations: [<THIS element's problems>],
    sceneViolationCount: <structural violations in the WHOLE scene> }
violations kinds: overlap (with severity + penetrationMm), disconnected,
facade_facing_inward, face_obstruction, opening_collision, drawer_invalid.",

            ["drawers"] =
@"GTV DRAWERS (DrawerElement)

A drawer is a parametric sliding box. Its geometry is DERIVED from parameters -
resize_element is rejected; use set_drawer_properties instead.

PARAMETERS
  drawer_type      Side height: A=86, B=120, C=168, D=200 mm.
  drawer_length    Nominal slide length MM: 250/300/350/400/450/500/550/600.
  internal_width   Internal box width in MM (min 100).
  drawer_color     anthracite | white | black.

CREATE:  create_element {name, x, y, z, is_drawer:true, drawer_type:""B"",
                         drawer_length:450, drawer_internal_width:400}

FRONTS:  attach a facade with set_drawer_properties {attached_facade_name:""F1""} -
         the facade then slides together with the drawer. Empty string detaches.

DOUBLE DRAWERS: two stacked boxes moving as one system:
  set_drawer_properties {is_double:true, paired_drawer_name:""OtherDrawer""}
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
