namespace KitchenDesigner.Core.MCP.Contract
{
    public static class McpRunGuideText
    {
        public static string Text => Template
            .Replace("@BASE_H@", RunKindDefaults.BaseHeightMm.ToString())
            .Replace("@BASE_D@", RunKindDefaults.BaseDepthMm.ToString())
            .Replace("@WALL_H@", RunKindDefaults.WallHeightMm.ToString())
            .Replace("@WALL_D@", RunKindDefaults.WallDepthMm.ToString())
            .Replace("@WALL_LIFT@", RunKindDefaults.WallHangsAboveFloorMm.ToString())
            .Replace("@TALL_H@", RunKindDefaults.TallHeightMm.ToString())
            .Replace("@TALL_D@", RunKindDefaults.TallDepthMm.ToString());

        private const string Template =
@"RUN - A ROW OF CABINETS ALONG ONE WALL, DECLARED, NOT PLACED ONE BY ONE

apply_run {id, wall, from?, start_mm?, gap_mm?, base_y_mm?, room_side?, modules:[...], dry_run?}
  id          name of the run. The SAME id again UPDATES the run in place.
  wall        exact name of the wall (along x or along z); the cabinets stand against
              the side that looks into the room (found from the floor beside the wall).
  from        left (default) = the end of the wall with the smaller x (smaller z for a
              wall along z); right = the opposite end, the FIRST cabinet of the list is
              then the rightmost; or the NAME of a part (a fridge, a pillar, a wall): the
              run starts right after its far side and grows towards larger x/z.
  start_mm    distance from that start to the first cabinet (default 0).
  gap_mm      gap between neighbours (default 0 = flush).
  base_y_mm   height of the floor the run stands on (default: the current level's floor).
  room_side   front|back (wall along x) or left|right (wall along z) = the face of the
              wall that looks into the room. Only needed when the server says it cannot
              tell (a wall with floor on both sides, or no floor).
  modules     [{name, kind:base|wall|tall, width_mm, height_mm?, depth_mm?}] IN ORDER from
              the start. width_mm runs ALONG the wall, depth_mm goes away from it.

DEFAULTS BY KIND (mm; used when height_mm / depth_mm are omitted)
  base   height @BASE_H@, depth @BASE_D@, stands on the floor
  wall   height @WALL_H@, depth @WALL_D@, hangs @WALL_LIFT@ above the floor
  tall   height @TALL_H@, depth @TALL_D@, stands on the floor
  The cabinet is one box part named like the module; refine it afterwards with
  edit_elements.

EXAMPLE - three base cabinets, the row starts 1200 mm from the left end of the wall:
  apply_run {id:""Row"", wall:""Wall"", start_mm:1200, modules:[
    {name:""B1"", kind:""base"", width_mm:600},
    {name:""B2"", kind:""base"", width_mm:600},
    {name:""B3"", kind:""base"", width_mm:600}]}
  Make B2 wider: send the same call with B2 width_mm:900 - B3 moves 300 mm along.
  Drop B3: send the call without it - it is removed. Same call twice: nothing changes
  (reply has unchanged:true and adds no undo step).

REPLY: {ok, id, ref, placements:[...], sceneViolationDelta, dryRun, applied, deleted?, unchanged?}
  placements are the same as place; deleted = cabinets removed because they were dropped
  from the list. ONE undo step per call (undo {} takes the whole change back).

REFUSALS (nothing is applied; fix and resend the WHOLE call)
  the run is N mm longer than the room...   cabinets + gaps + start_mm do not fit the wall
  cannot tell which side of wall...         send room_side
  belongs to run / already exists...        a cabinet name is taken by a part outside this run
  wall cabinet ... hangs in front of window ...   shift the run (start_mm), drop that
                                            cabinet or make it kind base
  base/tall cabinet ... in front of door ... shift the run or start after the door with
                                            from:'<door name>'
  overlaps 'Fridge'...                      something stands in the way: start after it
                                            with from:'Fridge'";
    }
}
