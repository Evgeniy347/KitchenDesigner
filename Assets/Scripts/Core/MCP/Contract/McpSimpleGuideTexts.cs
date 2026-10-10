using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP.Contract
{
    public static class McpSimpleGuideTexts
    {
        public const string OtherToolsSentence =
            "Other tools exist (cut list, materials, modules, settings, photo mode, raw objects) and work when called by exact name; "
            + "this set does not need them - ask the user before using one.";

        public const string GuideDescription =
            "Usage cheat-sheet. Topics: workflow (default: the whole loop), place (put single parts where they belong without coordinates), "
            + "run (a row of cabinets along a wall), planning (rooms, walls, floors, windows and doors), fields (what each reply field means). Call this first if unsure.";

        public static readonly string[] TopicWords = { "workflow", "place", "run", "planning", "fields" };

        public const string Instructions =
@"This server controls a 3D kitchen / furniture designer running in Unity. You have a SMALL tool set: describe_scene, render_plan, get, place, apply_run, edit_elements, delete_elements, undo, redo, apply_floorplan, get_violations, save_project, guide.

UNITS: MILLIMETRES everywhere, in and out; the unit is in the field NAME (width_mm, anchor_x_mm, posMm). A position is one point of the part's box chosen by ref (default left-bottom-back = the MINIMUM corner, never the centre).

THE LOOP - look, declare, read the reply, undo if wrong:
1. LOOK     describe_scene {} = a short TEXT of the scene; render_plan {view:""top""} = a picture for orientation only; get {names:[...]} = exact millimetres of a few parts.
2. DECLARE  say WHAT you want, never compute coordinates:
            rooms, walls, floors, windows, doors -> apply_floorplan
            a row of cabinets along a wall       -> apply_run
            single parts                          -> place
            change or resize a part               -> edit_elements
            remove parts                          -> delete_elements
3. READ      every reply carries placements (where each part stands, what it touches, issues) and sceneViolationDelta (what THIS call broke or fixed).
4. WRONG?    undo {} takes the last call back (redo {} returns it). get_violations {} audits the whole scene. Never delete and re-create to fix a mistake.
5. SAVE      save_project {path} - only inside the folder the user allowed.

" + OtherToolsSentence + @"

Call guide {topic:""workflow""|""place""|""run""|""planning""|""fields""} any time.";

        public static readonly Dictionary<string, string> Topics = new Dictionary<string, string>
        {
            ["workflow"] =
@"KITCHEN DESIGNER - SIMPLE TOOL SET, THE WHOLE LOOP

" + Instructions + @"

READING (cheap -> exact)
  describe_scene {max_chars?, scope?}   text: one line per module / part, summary first
  render_plan {view?, scope?}           picture of the scene (top plan or front elevation)
  get {names:[""B1"",""B2""]}               posMm [x,y,z], footprintMm, sizeMm of exactly these parts

CHANGING
  place {items:[...]}                   guide topic place
  apply_run {id, wall, modules:[...]}   guide topic run
  apply_floorplan {...}                 guide topic planning
  edit_elements {ops:[{name, width, height, depth, anchor_x_mm, anchor_y_mm, anchor_z_mm, rot_y, locked, material}], dry_run?}
      the whole batch is atomic and ONE undo step; dry_run:true previews it
  delete_elements {names:[...]}         atomic, undoable

EVERY CHANGE IS ONE UNDO STEP: undo {steps?} / redo {steps?}.",

            ["place"] =
@"PLACE - PUT PARTS WHERE THEY BELONG WITHOUT COORDINATES

place {items:[ITEM...], ref?, dry_run?} - ITEMs are applied IN ORDER (a later one may name an earlier one), atomically, as ONE undo step.
ITEM = {
  name        a NEW name creates the part; an EXISTING name shifts that part
  type, width, height, depth   (NEW parts only, default type board; sizes in mm)
  rot_y       degrees around the vertical axis
  on          ""floor"" (default) or the NAME of the part it rests on
  lift_mm     bottom this many mm above what it stands on (a wall cabinet: 1400)
  against     [{target, face, gap_mm}]  touch the named FACE OF THE TARGET
  align       [{target, axis:x|y|z, at:min|center|max, offset_mm}]  line up with a target
}
FACES: left|right = X, bottom|top = Y, back|front = Z. front is the +Z side of the target, back the -Z side. A part standing in the room in front of a wall uses the wall's face that looks into the room; right of a neighbour = face ""right"" of the NEIGHBOUR.
Every axis of a NEW part must be fixed: x and z by against/align, y by on (floor by default); the reply says which one is missing.

EXAMPLE - three cabinets in a row under a window, tight to the wall:
  place {items:[
   {name:""Cab2"", width:600, height:720, depth:560,
    against:[{target:""Wall"", face:""front""}],
    align:[{target:""Win1"", axis:""x"", at:""center""}]},
   {name:""Cab1"", width:600, height:720, depth:560,
    against:[{target:""Wall"", face:""front""}, {target:""Cab2"", face:""left""}]},
   {name:""Cab3"", width:600, height:720, depth:560,
    against:[{target:""Wall"", face:""front""}, {target:""Cab2"", face:""right""}]}]}

For a whole row along one wall prefer apply_run (guide topic run).
REPLY: {ok, ref, placements:[...], sceneViolationDelta, dryRun, applied}. Wrong result? undo {}, or place the same names again.

REFUSALS (nothing is applied; resend the WHOLE batch after fixing): the text names the missing axis, the two constraints that disagree, the closest real names, or the face to stand against.",

            ["run"] = McpRunGuideText.Text,

            ["planning"] =
@"PLANNING - ROOMS, WALLS, FLOORS, WINDOWS AND DOORS IN ONE CALL

apply_floorplan {id, origin_x_mm?, origin_z_mm?, points, rooms, walls, floors, openings}
  Creates or updates the whole plan in ONE call and ONE undo step. IDEMPOTENT by id: send the same id again to change the plan, elements that are no longer declared are removed. Wall thickness comes from the project, or per wall via thickness_mm.
  points   [{id, x, z}] corners in mm (z grows toward the viewer in the top plan)
  walls    [{id, from, to, kind:""bearing""|""partition"", height_mm, thickness_mm?}] between two points
  floors   [{id, poly:[point ids], top_y_mm, thickness_mm?}]
  rooms    [{id, poly:[point ids], kind, height}] shortcut: one floor plus walls around it
  openings [{id, wall, kind:""window""|""door"", offset_mm, width_mm, height_mm, sill_mm}] wall is a wall id; offset_mm is measured from the wall start point

EXAMPLE - one room with a window:
  apply_floorplan {id:""flat"", origin_x_mm:0, origin_z_mm:0,
    points:[{id:""sw"",x:0,z:0},{id:""se"",x:4000,z:0},{id:""ne"",x:4000,z:3000},{id:""nw"",x:0,z:3000}],
    walls:[{id:""south"", from:""sw"", to:""se"", kind:""bearing"", height_mm:2700},
           {id:""east"", from:""se"", to:""ne"", kind:""bearing"", height_mm:2700},
           {id:""north"", from:""ne"", to:""nw"", kind:""bearing"", height_mm:2700},
           {id:""west"", from:""nw"", to:""sw"", kind:""bearing"", height_mm:2700}],
    floors:[{id:""floor"", poly:[""sw"",""se"",""ne"",""nw""], top_y_mm:0}],
    openings:[{id:""w1"", wall:""south"", kind:""window"", offset_mm:1500, width_mm:1200, height_mm:1200, sill_mm:900}]}
  Name the walls yourself (south, east, ...): openings refer to them by id.
REPLY: {ok, id, elementCount, wallCount, floorCount, openingCount, roomCount, deletedCount, sceneViolationDelta}.",

            ["fields"] =
@"REPLY FIELDS

placements[]   one per changed part:
  name         the part
  posMm        [x,y,z] of the point chosen by ref (default the minimum corner) - send the same numbers back and the part does not move
  footprintMm  [x,y,z] WORLD size after rotation
  on           what it stands on
  touches      [{n, face}] neighbours it touches, by face
  gaps         [{n, face, gapMm}] free distance to a neighbour where it does not touch
  room, level  where it is
  issues       [] = clean; otherwise short codes with the cause
sceneViolationDelta {added, removed}   parts that BECAME violating / STOPPED because of this very call; old problems are not repeated
dryRun / applied   true / false = previewed and reverted; false / true = done
unchanged          true = the declaration matched the scene, nothing was changed (apply_run)
deleted            parts removed by this call
describe_scene returns PLAIN TEXT: `B4 board 600×720×560 @(1200,0,0) on Floor; back→Wall_N, left→B3, right→B5 gap2; ok` = name, kind, world size in mm, position of the ref point, what it stands on, the neighbour on each face (gapN = free mm), then ok or !issues.",
        };
    }
}
