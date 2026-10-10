using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP.Contract
{
    public static class McpGuideTexts
    {
        public const string DefaultTopic = "workflow";

        public const string Instructions =
@"This server controls a 3D kitchen / furniture BOARD designer running in Unity.

START HERE:
- get_project_instructions — the project's own conventions (wall thicknesses,
  board thickness, gaps, naming). They override any default you might assume.

UNITS:
- MILLIMETRES everywhere, in and out. There is no other unit on this wire.
- Every dimensional field carries its unit in its NAME (anchor_x_mm, offset_x_mm,
  posMm, aabbMinXMm); angles carry Deg. Read the name, not this paragraph.
- A position is ONE POINT of the part's world box, chosen by the request's ref.
  Default ref = left-bottom-back = the MINIMUM corner, never the centre. Every
  response reports positions in the SAME ref the request used and echoes it, so the
  posMm you read goes back into anchor_x_mm / anchor_y_mm / anchor_z_mm unchanged
  (same ref) and the part does not move. ref words, joined with '-': left|right (X),
  bottom|top (Y), back|front (Z), center = middle of the axes not named
  (center-bottom = middle of the footprint at floor level).

IDENTITY:
- Every board has a unique text ""name"". Use describe_scene to look around
  (render_plan draws it as a picture), then get_elements {filter/names} for detail.
- dimZMm (depth) is the board's LOCAL thickness; worldDim*Mm are the world-axis
  sizes (use those when a board is rotated).

LET THE SERVER DO THE GEOMETRY (do NOT compute centers by hand):
- apply_floorplan {id, points, rooms, walls, floors, openings} — a whole plan in
  ONE call and ONE undo step; idempotent by id. preview_floorplan validates the
  same declaration and returns a top-down SVG without touching the scene.
  Granular variants: create_walls / create_floor / add_opening.
- set_attr / move / align / resize_module take a SELECTOR (""all_boards
  thickness==18"", ""module:B4"") plus ONE number, and the server recomputes every
  affected board. Use them instead of per-element coordinate math.

HOW TO EDIT SINGLE ELEMENTS (batch-first):
1. READ:  get_elements {filter:""B4_*"", summary:true} — targeted and compact.
2. WRITE: edit_elements {ops:[...], dry_run:true} to preview, then without dry_run.
   One op = name + any of anchor_x_mm/anchor_y_mm/anchor_z_mm, width/height/depth, rot_* (deg),
   locked, material, facade gaps, mode, fill, drawer/table/pillar/window/door params.
   The whole batch is atomic and is ONE undo step.
3. CREATE: create_elements {items:[...]} — only sets name, type, position, size.
   Use edit_elements to set all other properties afterwards.
4. CHECK: every mutation returns a short ""placement"" per changed part: posMm,
   footprintMm (WORLD size x,y,z after rotation), on (what it stands on), touches
   [{n, face}], gaps [{n, face, gapMm}], room, level, issues ([] = clean) - and
   sceneViolationDelta {added, removed}: the violations THIS call created or
   fixed. Old violations of the scene are not counted.
   verbosity:""full"" on a mutation adds the complete ElementInfo (about 1 KB per part).
5. WRONG RESULT? undo {steps} takes the last mutating calls back (redo returns
   them). Never delete and re-create to fix a mistake.

PLACEMENT WITHOUT MATH:
- place {items:[{name, type/width/height/depth for a NEW part, on, against, align}]} - say WHERE
  in words, the server computes the position. LOOP: look (describe_scene, get) ->
  place -> read the placement in the reply (issues, touches, gaps, sceneViolationDelta)
  -> wrong? undo, or place the same name again. See guide {topic:""place""}.
- apply_run {id, wall, from, start_mm, modules:[{name, kind, width_mm}]} - a whole row of cabinets along a wall in one
  call; send the same id again to change it (idempotent). See guide {topic:""run""}.
- align_elements — press a face flush against (or gap_mm away from) another board's face.
- get_free_space — the empty box between two boards (size, bounds, blockers).
- clone_elements — N copies with a step offset; distribute_evenly — equal spacing.

SAFETY:
- locked:true in element info means move/resize/delete are rejected. Unlock with
  edit_elements {locked:false} ONLY when the user explicitly allowed it.
- Prefer edit_elements over raw set_position / set_scale / delete_object.
- delete_elements, edit_elements and clone_elements are undoable (undo tool).
- get_all_elements is a legacy full dump (~1 KB per element) — prefer
  get_scene_tree + get.

IF A CALL FAILS:
- ""Element not found"" -> get_elements {filter:...} to find the exact name, retry.
- A connection error means the Kitchen Designer app is not running - ask the user to start it.

Call guide {topic:""workflow""|""planning""|""bulk""|""elements""|""fields""|""drawers""|""violations""|""place""|""run""}
any time.";


        public static readonly Dictionary<string, string> Topics = new Dictionary<string, string>
        {
            ["workflow"] =
@"KITCHEN DESIGNER — WORKFLOW CHEAT-SHEET
Other topics: guide {topic:""planning"" | ""bulk"" | ""elements"" | ""fields"" |
""drawers"" | ""violations"" | ""place"" | ""run""}

FIRST CALL OF A SESSION
  get_project_instructions -> the project's own conventions (wall thicknesses,
  board thickness, gaps, naming). They OVERRIDE any default assumption here.

UNITS
  MILLIMETRES everywhere, in and out; the unit is in the field NAME.
  A position is one POINT of the part's world box, chosen by ref (default
  left-bottom-back = the MINIMUM corner, never the centre). Responses use the
  ref you sent and echo it; posMm read -> anchor_*_mm written = no movement.
  dimZMm = board thickness (smallest side, usually 18 mm).

READING THE SCENE (cheap -> expensive)
  describe_scene {max_chars?, scope?}           -> TEXT digest: one line per module / part
  render_plan {view?, scope?, labels?, px?}      -> PICTURE (PNG): top plan or front elevation
  get_scene_tree                                -> modules, bboxes, type counts
  get {names:[""B4_Side_L""]}                     -> posMm + footprintMm + sizeMm (MM)
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

EDITING SINGLE ELEMENTS (every mutation returns a short placement per part)
  edit_elements {ops:[{name:""P1"", anchor_x_mm:1200}, {name:""P2"", width:600, rot_y:90}]}
     - MANY changes in ONE transactional call, single undo step.
     - dry_run:true = simulate first, nothing is kept.
  create_elements {items:[{name:""Shelf1"", type:""board"", anchor_x_mm:.., width:.., ..}]}
  align_elements {ops:[{name:""Shelf1"", face:""left"", target:""Side_L"",
                       target_face:""right""}]}   - face-to-face, no math
  clone_elements {ops:[{name:""Shelf1"", count:2, offset_y_mm:300}]} -> _2, _3
  distribute_evenly {names:[...3+...], axis:""y""}
  convert_elements / delete_elements / select_elements — all batch, all atomic.

CHECKING
  Look at ""issues"" of each placement in EVERY mutation response: [] means this
  part is clean. sceneViolationDelta.added lists the parts this call broke -
  if it is not empty, call get_violations {names:[...]} to see why.
  An issue ""deep_penetration: B 12.2mm"" means the board is INSIDE B - never OK.
  Severity grades: touching < minor_overlap < overlap < deep_penetration.
  ""on"" tells what the part stands on, ""gaps"" the free distance to the nearest
  neighbour per face (gapMm, up to 500 mm): a floating shelf or a 12 mm slit shows up there.

UNDO INSTEAD OF REDOING
  A result is wrong -> undo {steps:1}; it restores positions, sizes and removed
  or created parts exactly. redo {} re-applies. One step = one earlier mutating
  call (or one edit the human made in the app).

PICTURE, TEXT OR MILLIMETRES - which to reach for
  render_plan {view:""top""|""front"", scope?}   orientation: where is the window, left or
     right of the sink, is there a gap in the run, what stands in front of what.
     About 3 KB. Red outline + triangle + ! = the part has an issue. Names are on the
     parts; the caption lists those that did not fit (scope narrows and zooms, px
     makes it larger). top = x right, z UP (north up, like preview_floorplan); front = x right, y UP.
  describe_scene          text: what touches what, which parts have issues, exact
     sizes. The same names and order as the picture.
  get / place / the placement of a reply   MILLIMETRES. Never read a coordinate off
     the picture - the scale bar says 1 px is several mm.

THE LOOP: DESCRIBE -> PLACE -> READ THE PLACEMENT -> UNDO
  describe_scene {scope?}                   -> one text: modules, parts, what touches what
  get {names:[...]}                         -> exact numbers of a few parts
  place {items:[...]}                       -> no coordinates, see guide {topic:""place""}
  apply_run {id, wall, modules:[...]}       -> a whole ROW of cabinets along a wall, see guide {topic:""run""}
  reply: placements[].issues / touches / gaps and sceneViolationDelta.added
  anything wrong -> undo {} (or place the same name again), never delete+recreate.
  Unsure? place {dry_run:true, items:[...]} (also create_elements / clone_elements /
  align_elements / edit_elements take dry_run) and read the result first.
  A refusal says what is missing or what overlaps and which face to use; nothing
  was applied, so fix it and resend the WHOLE batch.

STEP-BY-STEP EXAMPLE: three shelves between two panels
  1. get_free_space {between:[""Side_L"",""Side_R""]}   -> inner width/position
  2. create_elements {items:[{name:""Shelf1"", anchor_x_mm:.., anchor_y_mm:.., anchor_z_mm:.., width:..,
                              height:.., depth:18}]}
  3. align_elements  {ops:[{name:""Shelf1"", face:""left"", target:""Side_L"",
                            target_face:""right""}]}
  4. clone_elements  {ops:[{name:""Shelf1"", count:2, offset_y_mm:300}]}
  5. read issues of the three placements in the replies -> expect [] each

SAFETY
  LOCKED elements (locked:true in element info) reject changes. Unlock with
  edit_elements {locked:false} ONLY if the user explicitly allowed it.
  create_elements / edit_elements / clone_elements / delete_elements are undoable.
  Prefer them over the ADVANCED raw tools (set_position, set_scale,
  delete_object) — those bypass undo, validation and snapping.

PERSISTING WORK — nothing survives past this connection on its own
  save_project {path} writes the WHOLE current project to a file — the same
  format and the same call the app's own Save/Save As makes. The path must
  resolve inside the directory the app was started with (-mcpSaveDir
  <folder>) — without that startup flag, or for any path outside it, the
  call is refused with an error naming the reason, not a silent no-op.
  load_project {path} replaces the ENTIRE current scene with a project file —
  the same call as Open/Load. A missing or corrupt file is an error, not an
  empty scene; the scene is left untouched. load_project has no directory
  restriction — it only reads.
  Neither remembers a 'last path' for you across calls — always pass the full
  path you mean.",

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
  walls    {id, from, to, kind, height_mm, thickness_mm?}  — explicit single
            walls; thickness_mm overrides the kind's project instruction for
            that one wall (a 150 mm facade in a 250/125 project), so a plan
            can carry more than the two default thicknesses
  floors   {id, poly:[3+ point ids], top_y_mm, thickness_mm}
  openings {id, wall, kind:""window""|""door"", offset_mm (from the wall's
            declared start point), width_mm, height_mm, sill_mm}
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
               width_mm:1200, height_mm:1400, sill_mm:800}]}
  The response is terse: counts + sceneViolationDelta (what THIS call broke or
  fixed, not the whole scene). create_walls / create_floor / add_opening answer
  {ok, created[], updatedCount, sceneViolationDelta}; apply_floorplan {ok, id,
  elementCount, wallCount, floorCount, openingCount, roomCount, deletedCount,
  sceneViolationDelta}. Wall ids generated by a room are <room>_<from>_<to>;
  read them back with get_scene_tree if unsure.",

            ["place"] =
@"PLACE - PUT PARTS WHERE THEY BELONG WITHOUT COORDINATES

place {items:[ITEM...], ref?, dry_run?}   - ITEMs are applied IN ORDER (a later one
may name an earlier one), atomically, as ONE undo step.
ITEM = {
  name        NEW name creates the part; an EXISTING name MOVES that part
  type, width, height, depth, (NEW parts only; like create_elements, default board)
  rot_y       degrees around the vertical axis
  on          ""floor"" (default) or the NAME of the part it rests on
  lift_mm     bottom this many mm above what it stands on (wall cabinet: 1400)
  against     [{target, face, gap_mm}]  touch the named FACE OF THE TARGET
  align       [{target, axis:x|y|z, at:min|center|max, offset_mm}]  line up
}
FACES (same words as align_elements): left|right = X, bottom|top = Y,
back|front = Z. front is the +Z side of the target, back the -Z side. A cabinet
standing in the room in front of a wall uses that wall's face that looks into the
room; right of a neighbour = face ""right"" of the NEIGHBOUR.
Every axis of a NEW part must be fixed: x and z by against/align, y by on (floor by
default). The reply says which is missing.

EXAMPLE - three cabinets in a row under a window, tight to the wall:
  place {items:[
   {name:""Cab2"", width:600, height:720, depth:560,
    against:[{target:""Wall"", face:""front""}],
    align:[{target:""Win1"", axis:""x"", at:""center""}]},
   {name:""Cab1"", width:600, height:720, depth:560,
    against:[{target:""Wall"", face:""front""}, {target:""Cab2"", face:""left""}]},
   {name:""Cab3"", width:600, height:720, depth:560,
    against:[{target:""Wall"", face:""front""}, {target:""Cab2"", face:""right""}]}]}

REPLY: {ok, ref, placements:[...], sceneViolationDelta, dryRun, applied} - see
guide {topic:""fields""}. Wrong result? undo {}, or place the same names again.

REFUSALS (nothing is applied; resend the WHOLE batch after fixing):
  x is not determined...   add align or against on that axis
  x is fixed twice...      two constraints disagree: keep one or change gap/offset
  Element not found: ...   shows the closest real names
  overlaps 'Cab2'...       names the face of Cab2 to stand against (or on:'Cab2')
  type 'window' ...        windows/doors: add_opening; walls: create_walls;
                           floors: create_floor",

            ["run"] = McpRunGuideText.Text,

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
       screw_leg|pipe|pipe_elbow|pipe_coupling|pipe_tee|pipe_cap|
       pipe_supply|pipe_return|toilet|wall_hung_toilet|bathtub|bath_mixer|
       shower_column|socket|light_switch|light
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
  {matchedCount, updatedCount, placements (first 20 changed parts), omittedCount,
  sceneViolationDelta} — no per-element dumps. ref decides which point of the
  part the placements' posMm reports.

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
SofaElement           Sofa-bed (type:""sofa"", 2000x800x900 mm by default). NO armrests:
                      four cushions instead — two upright on the back, two lying
                      flat along the sides where armrests would be. Three parts: the
                      FRONT seat block, the BACKREST (180 mm thick, 700 mm tall, its
                      bottom 100 mm above the floor) and an inner BOX with three
                      compartments hidden under the seat. HEIGHT IS FIXED at 800 mm
                      (a different height is rejected on edit AND on create, exactly
                      800 is accepted); depth changes only the seat
                      (depth - 180 mm), width is free. corner_radius rounds the seat
                      block (120 mm by default); seat_height is its top above the
                      floor (360 mm by default, clamped to 300..460); edge_radius
                      rounds the top edges of the left and right ends of the seat
                      and backrest (40 mm by default). The sofa
                      unfolds in two strictly sequential stages: cycle_drawer_animation
                      moves it Folded -> Extended (seat slides out, the box shows) ->
                      Bed (the backrest lowers flat onto the box) -> Folded. The
                      stage is saved with the project (element.sofa.unfoldStage).
                      Upholstery and cushion decors via tabletop_material/legs_material.
PouffeElement         Pouffe (type:""pouffe"", 450x400x450 mm by default). An
                      upholstered box standing on the floor with NO legs, plus a
                      soft seat cushion on top. corner_radius rounds the whole box
                      in plan (120 mm by default; the maximum makes it round);
                      pouffe_seat_thickness is the cushion (50 mm by default,
                      clamped to 20..height/3) and the rest of the height is the
                      box. Body and cushion decors via tabletop_material/
                      legs_material.
ToiletElement         Compact toilet (type:""toilet""). FIXED 360x790x660 mm — it
                      ignores width/height/depth like a built-in appliance, and
                      only yaw rotation is allowed. Pedestal, bowl, seat, lid and
                      a cistern at the BACK (-Z), plus a chrome flush button flush
                      with the cistern top. seat_height is the only shape
                      parameter (400 mm by default, clamped to 350..502); what is
                      left up to 790 becomes the cistern, which never drops below
                      250 mm. Ceramic and button decors via tabletop_material/
                      legs_material — leave them alone for the factory look, white
                      ceramic and chrome.
WallHungToiletElement Wall-hung toilet / installation (type:""wall_hung_toilet"").
                      FIXED 360x1000x540 mm, yaw only, and it SNAPS to the nearest
                      wall by itself: rot_y is ignored, the element turns its back
                      (-Z) to the wall and slides along it. The envelope starts at
                      the FLOOR on purpose — the bowl hangs, but the concealed
                      cistern (not modelled) occupies the wall from the floor up.
                      seat_height is the top of the bowl above the floor (400 mm
                      by default, clamped to 350..600); flush_plate_height is the
                      BOTTOM of the 240x165 mm dual flush plate (600 mm by
                      default, clamped to seat_height+88..835). Raising the bowl
                      PUSHES the plate up, never the other way round. Ceramic and
                      plate decors via tabletop_material/legs_material.
BathtubElement        Rectangular acrylic bathtub (type:""bathtub"", 1700x600x700 mm by
                      default — width x height x depth, the long side running along
                      X so it sits against a wall). Size IS editable, unlike the
                      toilets. Four shape fields, and each one lowers the ceiling of
                      the next: rim_width (40 mm, the flat rim), bowl_depth (450 mm,
                      down from the rim), bowl_radius (120 mm) and bowl_fillet
                      (80 mm, the wall-to-floor arc inside the bowl). bowl_radius is
                      the radius of the BOWL, not of the outer shell: the shell gets
                      bowl_radius + rim_width, which is what keeps the rim the same
                      width in the corner as along the straight side. Set it to 0 for
                      a strictly rectangular tub, to the maximum for semicircular
                      ends. Decor is a single slot (material); leave it alone for the
                      factory look, white acrylic. The tub does NOT seat itself on
                      the floor — place it yourself.
BathMixerElement      Wall-mounted thermostatic bath mixer (type:""bath_mixer"",
                      270x90x182 mm by default). It MOUNTS ON A WALL: it seats
                      itself flush against the nearest wall face and turns to
                      face away from it, so place it near a wall and let it snap
                      rather than positioning it by hand. Size is COMPUTED from
                      six shape fields and cannot be set directly:
                      mixer_centres (150 mm, the wall inlet spacing),
                      mixer_body_length (270), mixer_body_diameter (70, which is
                      also the body height), mixer_escutcheon_reach (34, how far
                      the round covers stand off the wall), mixer_spout_length
                      (110) and mixer_outlet_diameter (13, G 1/2 for the shower
                      hose). The depth is driven by the SPOUT, not by the body.
                      Decor is a single slot (material); leave it alone for the
                      factory look, chrome. It does NOT set its own height above
                      the floor - place it yourself.
ShowerColumnElement   Shower column with a rain head (type:""shower_column"",
                      250x1414x505 mm by default). Wall-mounted like the mixer.
                      It has NO mixer of its own - the block at the bottom is a
                      diverter, without valves. Eight shape fields:
                      shower_column_height (1150), shower_riser_diameter (32),
                      shower_head_diameter (250), shower_head_thickness (30),
                      shower_arm_reach (380 from the wall), shower_wall_offset
                      (60), shower_hand_diameter (110) and shower_hose_length
                      (1000). Element height is NOT the column height: the hose
                      hangs in a loop below the diverter and the bounding box
                      covers it, so the default 1150 mm column measures 1414 mm.
                      Element width is the rain head diameter. Decor is a single
                      slot (material); leave it alone for the factory look,
                      matte black.
SocketElement         Mains socket on a wall (type:""socket"", 80x80x10 mm by
                      default). It MOUNTS ON A WALL: it seats itself flush
                      against the nearest wall face and turns to face away from
                      it, so place it near a wall and let it snap rather than
                      positioning it by hand. Size is COMPUTED from four shape
                      fields and cannot be set directly:
                      wall_device_plate_width (80), wall_device_plate_height
                      (80), wall_device_protrusion (10, which is the depth and
                      is split into frame, recess and body) and
                      wall_device_posts (1..3 - a double or triple block, which
                      MULTIPLIES the width). Decor has TWO slots: tabletop_material
                      is the frame, legs_material is the contacts; leave them
                      alone for the factory look, white plastic and dark
                      contacts. It does NOT set its own height above the floor -
                      place it yourself.
LightSwitchElement    Light switch on a wall (type:""light_switch"", 80x80x10 mm
                      by default). Same wall mounting and the same four shape
                      fields as the socket, with a key in the frame opening
                      instead of a socket well - wall_device_posts is the number
                      of KEYS. Two more fields drive the light: switch_on
                      (true by default) and switch_lights, the names of the
                      LightSourceElements this switch controls (REPLACES the
                      list, up to 12, unknown names dropped). The link is
                      many-to-many and is stored ONLY on the switch: one switch
                      may control several lamps and one lamp may be listed by
                      several switches. A lamp named by at least one switch is
                      lit when at least one of those switches is on; a lamp no
                      switch names keeps following the global light as before.
                      Decor slots: tabletop_material is the frame,
                      legs_material is the key.
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
FoundationElement     Strip foundation along every CURRENT load-bearing wall
                      (type:""foundation""). Width/height are the sole width
                      and depth (create_elements dim_x/dim_y); the strip's
                      own LENGTH is derived from the load-bearing wall
                      network every time the mesh rebuilds and has no field
                      of its own. foundation_* fields (see get_elements
                      fields) feed the specification, not the mesh.
FloorSlabElement      Horizontal floor slab (type:""floor_slab""). Plain
                      rectangular box: width/depth (create_elements dim_x/
                      dim_z) is the footprint, thickness rides dim_y (default
                      from the construction settings tab). floor_slab_*
                      fields (see get_elements fields) feed the specification
                      -- technology ""joists"" has no quantities yet, so its
                      concrete/rebar numbers stay at whatever they last were
                      and are simply not used.
FenceElement          Straight run of profiled-sheet fence (type:""fence"").
                      width is the run length, height is the fence height
                      above grade (create_elements dim_x/dim_y); posts and
                      pit are procedural, not part of dim_z. fence_* fields
                      (see get_elements fields) feed the specification --
                      rail count is DERIVED from height (2 below 2000mm, 3
                      at or above) and has no field of its own.
DuctElement           Straight ventilation duct run (type:""duct""), round or
                      rectangular (duct_profile_kind). height (dim_y) is the
                      run length; the cross-section is duct_diameter_mm
                      (round) or duct_width_mm/duct_height_mm (rect), not
                      dim_x/dim_z directly. duct_airflow_m3_per_hour is the
                      designed flow used by the VNT-01 velocity check.
GrilleElement         Ventilation grille (type:""grille""), a flat standalone
                      panel -- NOT wall-attached yet. width/height are
                      dim_x/dim_y. grille_airflow_m3_per_hour feeds the
                      VNT-03 room air-exchange check.
RoofElement           Pitch roof over the TOP level's load-bearing walls
                      (type:""roof""). The footprint, ridge height and eave
                      position are derived from those walls every time the
                      mesh rebuilds; width/height/depth are not settable at
                      all. roof_type picks single/gable/hip, roof_ridge_axis
                      picks which plan axis the ridge runs along (auto = the
                      longer side), roof_pitch_deg/roof_overhang_mm/
                      roof_rafter_step_mm feed both the mesh and the
                      specification (rafter section is derived from span via
                      the project's rafter table and has no field of its own).
InsulationElement     Wall insulation layer (type:""insulation""). Seats flush
                      on wall_layer_host_wall_name (or the nearest wall) and
                      TAKES the wall's length/height, re-cutting its window/
                      door openings -- width/height are not settable, only
                      wall_layer_thickness_mm (depth at create time). Moves
                      with the wall automatically.
VentGapElement        Ventilated-facade air gap layer (type:""vent_gap"").
                      Same wall-following rule as InsulationElement, plus
                      wall_layer_batten_step_mm for the vertical battens.
CladdingElement       Outer cladding layer (type:""cladding""). Same
                      wall-following rule as InsulationElement.
ScrewLegElement       Screw-in levelling leg with a threaded insert
                      (type:""screw_leg""). A foot (screw_base_diameter_mm x
                      screw_base_height_mm, 25x8 mm by default) plus a threaded
                      rod (screw_thread M6/M8/M10, screw_thread_length_mm 50 by
                      default) that goes INTO the part above it. How deep it goes
                      is MEASURED, not set: screwLeg.insertionMM in ElementInfo is
                      derived from the geometry (0 when there is no host), and
                      screw_insertion_mm is rejected by edit_elements. Drop it
                      under any part: it takes that part as its host, re-derives
                      the thread length so the foot stands on the floor, and the
                      THREAD shares space with the host legally — a thread poking
                      through a 16 mm board is not a collision. The FOOT does not:
                      it is the pad the leg stands on, so a foot inside the host
                      is COL-01 like any other part. Snapping centres it on the
                      host; off-centre on a side thinner than 25 mm is reported as
                      LEG-01, less than 5 mm of thread inside the host as
                      LEG-02, and a foot touching nothing below it as LEG-03.
                      Where it sits ON the host is
                      screwLeg.leftInHostMM / rightInHostMM / topInHostMM /
                      bottomInHostMM — the distances from the centre of the foot
                      to the edges of the host face the thread enters, measured
                      along that face's own axes, so they turn with the host. Each
                      pair adds up to the host's span, and screw_left_mm /
                      screw_right_mm / screw_top_mm / screw_bottom_mm move the leg
                      by setting one of them.
PipeElement           Straight water pipe, GOST 3262-75 (type:""pipe""). ONE
                      editable number and one list: height is the LENGTH of the
                      run, pipe_size is the nominal bore (dn15..dn50, default
                      dn20 = 3/4""). Everything else about the section is DERIVED
                      from pipe_size and reported read-only in ElementInfo
                      (pipe.outerDiameterMM 26.8, innerDiameterMM 21.2,
                      wallThicknessMM 2.8 for dn20); width and depth follow the
                      outer diameter and are REJECTED by edit_elements. The run
                      goes along the element's own Y, so rotate it to lay it
                      flat. A run inside a wall or a floor is legal; crossing a
                      part or a piece of furniture is PIP-03. A free end with no
                      fitting on it is PIP-01, and two runs of different bore
                      butted together is PIP-02.
Pipe fittings         Elbow, coupling, tee, cap, supply and return
                      (type:""pipe_elbow"" / ""pipe_coupling"" / ""pipe_tee"" /
                      ""pipe_cap"" / ""pipe_supply"" / ""pipe_return""). A fitting has
                      NO editable numbers at all: width, height, depth and
                      pipe_size are all REJECTED by edit_elements. Its bore is
                      not stored anywhere — it is READ from the runs actually
                      butted onto its ports, and reported per port in
                      pipeFitting.bores[] (a port with nothing on it reads
                      ""—""). Port count is fixed by the kind: 1 for a cap, a
                      supply and a return, 2 for a coupling (coaxial) and an
                      elbow (at a right angle), 3 for a tee. Position and
                      rotation are the only things to set: put a port where a
                      run ends and PIP-01 goes quiet there.
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
dimXMm/dimYMm/dimZMm  LOCAL size in MM (dimZMm = thickness). Does NOT change when
                      the board is rotated.
worldDim*Mm           WORLD-axis extents in MM (from AABB). USE THESE when the
                      board is rotated: after rot_y=90 a 600x18 board has
                      worldDimXMm=18, worldDimZMm=600.
posMm                 [x,y,z] in MM of the point chosen by the request's ref
                      (default left-bottom-back = the MIN corner, equal to
                      aabbMin*Mm). This is what edit_elements anchor_x_mm /
                      anchor_y_mm / anchor_z_mm take when sent with the same ref.
rotXDeg/rotYDeg/rotZDeg  Euler angles in DEGREES.
aabbMin*Mm/aabbMax*Mm World bounding box in MM.
effectiveDim*Mm       dim + gaps (any element that has them). NOT rotation-aware
                      - prefer worldDim* for world-space reasoning.
locked                true = move/resize/delete will be rejected (edit_elements
                      {locked:false} unlocks).
transparent           true = drawn see-through (edit_elements {transparent:...}).
                      Appearance only - size, position and the specification are
                      the same as for a solid element.
wallLoadBearing       Признак несущей стены (только WallElement). По умолчанию
                      несущие ВСЕ стены; отключается переключателем в свойствах
                      конкретной стены. От этого флага зависит, попадёт ли стена
                      под ленту фундамента.
wallMasonry           Технология кладки этой стены: brick_single | brick_thickened |
                      aerated_block | timber | frame. Новая стена берёт значение из
                      вкладки настроек «Строительство»; правится полем masonry.
wallJointMm           Растворный шов этой кладки, мм. Чем толще шов, тем меньше
                      камней и больше раствора уходит на ту же стену.
wallWastePct          Запас на бой и подрезку, % сверх посчитанных штук. На объём
                      раствора не влияет.
hasViolations         true = this element overlaps something or is disconnected.
faceGaps              Per-axis nearest OPPOSITE neighbour: {axis, neighbor, gapMM,
                      touching, isOverlap}. touching=true means flush contact
                      (|gap| < 0.5 mm) - that is GOOD, not a violation.
                      Axes with no facing neighbour are omitted.
moduleId/moduleName   Group membership (0/absent = not grouped).
levelId               Storey (floor) this element belongs to - see level_id in
                      edit_elements/create_elements and the levels[] array in
                      get_scene_tree. anchor_y_mm stays an ABSOLUTE world height
                      regardless of levelId; this only tags which floor it is on.
materialId            Decor id (list_materials).
facadeMode            Facade opening mode (""front_left"", ""drawer_out"", ...).
drawer / table / radiusTable / stool / chair / sofa / pouffe / bed / toilet /
wallHungToilet / bathtub / bathMixer / showerColumn / wallDevice / lightSwitch
                      Type-specific sub-objects, absent otherwise.
wallDevice            Socket AND light switch: {plateWidthMM, plateHeightMM,
                      protrusionMM, postCount}. These four are the SOURCE of the
                      element size, not a copy of it: width = plateWidthMM x
                      postCount, height = plateHeightMM, depth = protrusionMM.
                      Edit them, not width/height/depth - those are refused.
lightSwitch           Light switch only: {isOn, lights, maxLights}. lights are
                      the NAMES of the light sources this switch controls, and
                      the link lives ONLY here - there is no field on the lamp
                      saying which switches reach it, so to find that, scan the
                      switches. The relation is many-to-many: one switch may
                      name several lamps, one lamp may be named by several
                      switches. A lamp named by at least one switch is lit when
                      at least one of those switches has isOn - a lamp no switch
                      names keeps following the global light instead. maxLights
                      is the ceiling; a write past it is dropped, not an error,
                      which is why the ceiling is reported next to the list.
foundation            Strip foundation only: {soilKind, sandMm, gravelMm,
                      compacted, concreteGrade, rebarDiameterMm, rebarStepMm,
                      coverMm, frostDepthText}. soilKind/concreteGrade are
                      strings (see edit_elements foundation_soil /
                      foundation_concrete). frostDepthText is READ-ONLY, the
                      same text the properties panel shows, derived from the
                      project's construction region and THIS strip's own
                      soil - there is no matching edit field.
floorSlab             Floor slab only: {technology, thicknessMm, concreteGrade,
                      rebarDiameterMm, rebarStepMm}. technology/concreteGrade
                      are strings (see edit_elements floor_slab_technology /
                      floor_slab_concrete). thicknessMm is READ-ONLY here -
                      it is the element's dim_y, set via edit_elements height
                      like any other dimension, not a separate field.
fence                 Fence run only: {postSectionMm, postStepMm, pitDepthMm,
                      sheetMark, railCount}. sheetMark is a string (see
                      edit_elements fence_sheet_mark). railCount is
                      READ-ONLY -- derived from the run's own height, no
                      matching edit field.
duct                  Duct only: {profileKind, diameterMm, widthMm, heightMm,
                      airflowM3PerHour}. profileKind is ""round""/""rect"" (see
                      edit_elements duct_profile_kind). Only the fields
                      matching the CURRENT profileKind matter -- the other
                      pair still reports whatever it last held.
grille                Grille only: {airflowM3PerHour}.
wallLayer             Insulation/vent-gap/cladding layer only: {hostWallName,
                      thicknessMm, battenStepMm}. hostWallName mirrors
                      edit_elements wall_layer_host_wall_name (null if the
                      layer has not found a wall yet). thicknessMm mirrors
                      wall_layer_thickness_mm. battenStepMm is null except on
                      a vent-gap layer, where it mirrors
                      wall_layer_batten_step_mm.
roof                  Roof only: {type, ridgeAxis, pitchDeg, overhangMm,
                      rafterStepMm, coveringAreaM2, ridgeLengthMm}.
                      type/ridgeAxis are strings (see edit_elements
                      roof_type / roof_ridge_axis). coveringAreaM2 and
                      ridgeLengthMm are READ-ONLY, derived from the built
                      roof frame (footprint, pitch, covering waste %) -
                      there is no matching edit field for either.

COMPACT GEOMETRY (get) — a different, terser shape:
  {ref, elements:[{name, kind, posMm:[x,y,z] in ref, footprintMm:[x,y,z] WORLD
   extents after rotation, sizeMm:[width,height,depth] the part's OWN
   dimensions, rotYDeg, hasViolations, module}]}. Every number is MM.
   Use it for reasoning about layout; use get_elements for full detail.

MUTATION RESPONSES (create/edit/align/clone/distribute/convert/...) return:
  { ok, ref, placements:[PLACEMENT...], sceneViolationDelta:{added, removed} }
  PLACEMENT = { name,
    posMm:[x,y,z]        the point chosen by ref, MM, world axes
    footprintMm:[x,y,z]  WORLD extents of the part after rotation, MM
                         (x = left-right, y = up, z = back-front)
    on                   name of the part it stands on (touches its bottom), or absent
    touches:[{n, face}]  neighbours in flush contact (< 0.5 mm); face is OUR face
    gaps:[{n, face, gapMm}]  nearest free neighbour per face, up to 500 mm
    room, level          the room (floorplan id) and storey it is in
    issues:[]            this part's problems; [] = clean }
  face words: left|right (X), bottom|top (Y), back|front (Z), same as align_elements.
  issues strings: ""<severity>: <neighbour> <depth>mm"" (minor_overlap, overlap,
  deep_penetration), disconnected, facade_facing_inward, ""face_obstruction <n>"",
  ""opening_collision <n>"", ""drawer_invalid <message>"". At most 6 per part.
  sceneViolationDelta: names of parts that BECAME violating (added) or STOPPED
  (removed) because of this very call; violations that were already there are
  not repeated. The whole-scene count is not returned (load_project and
  save_project still report sceneViolationCount).
  Bulk tools (set_attr/move/align/resize_module) add matchedCount/updatedCount;
  placements are capped at 20 parts (omittedCount says how many were left out);
  edit_elements adds dryRun/applied; delete_elements returns deleted names
  instead of placements; undo/redo return steps (descriptions), doneCount,
  undoAvailableCount, redoAvailableCount.
  Full element info (every field of this topic) is returned by get_elements (and
  the legacy get_all_elements) and, only when you ask verbosity:""full"", by every
  mutation tool as elements:[ElementInfo...] next to placements (about 1 KB per
  part, same cap of 20). Default verbosity:""terse"" = placements + delta only.
  posMm obeys the ref of the request and the response echoes it.

DESCRIBE_SCENE returns PLAIN TEXT (not JSON), at most max_chars characters:
  scene: 50 parts, 4 modules, 18 loose; 2 with issues
  mm; size x×y×z; @(x,y,z) = left-bottom-back point; ! = issue
  levels: L1 ""Ground"" 0..2700 (50)
  B4 module(8) 800×720×560 @(1500,0,2000) on Floor; ok
  Cab2 board 600×720×560 @(600,0,0) on Floor; left→Cab1, back→Wall, right→Cab3 gap12; ok
  +12 more, use scope
  A line = name, kind, WORLD size, ref point, what it stands on, face→neighbour
  touches, face→neighbour gapN = free MM, then ok or !issues. Same words as the
  placement block. Parts with issues come first; scope (module name, room:ID or a
  selector) lists the parts of one place.",

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
drawer cycles Closed -> BothOpen -> LowerOnly -> Closed (a sofa steps through
Folded -> Extended -> Bed -> Folded). State is in element.drawer: isOpen,
doubleState.

VALIDATION: drawer problems (bad length, missing pair, ...) appear as
""drawer_invalid ..."" entries in the placement issues of mutation responses
and as drawer_invalid kinds in get_violations.",

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

CHECKING: every mutation response carries the changed parts' issues and the
sceneViolationDelta (what this call broke or fixed). get_violations
{names:[...]} checks specific boards; get_violations {} audits the whole scene."
        };

        public static string InstructionsFor(McpToolProfile profile) =>
            profile == McpToolProfile.Simple ? McpSimpleGuideTexts.Instructions : Instructions;

        public static IReadOnlyDictionary<string, string> TopicsFor(McpToolProfile profile) =>
            profile == McpToolProfile.Simple ? McpSimpleGuideTexts.Topics : Topics;
    }
}
