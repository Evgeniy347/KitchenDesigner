using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security;
using System.Text;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public sealed class CompiledFloorplan
    {
        public string id = "";
        public int originX, originZ;
        public readonly Dictionary<string, Vector2Int> points = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<CompiledPlanWall> walls = new();
        public readonly List<CompiledPlanFloor> floors = new();
        public readonly List<FloorplanOpening> openings = new();
        public readonly List<CompiledPlanRoom> rooms = new();
        public readonly List<string> errors = new();
        public bool IsValid => errors.Count == 0;
    }

    public sealed class CompiledPlanWall
    {
        public string id = "", from = "", to = "", kind = "";
        public int height;
        /// <summary>Per-wall thickness override; null = take it from the project instructions.</summary>
        public int? thickness;
    }

    public sealed class CompiledPlanFloor
    {
        public string id = "";
        public string[] poly = Array.Empty<string>();
        public int topY;
        public int? thickness;
    }

    public sealed class CompiledPlanRoom
    {
        public string id = "", floor = "";
        public readonly List<string> walls = new();
        public readonly List<string> openings = new();
    }

    public static class FloorplanCompiler
    {
        public static CompiledFloorplan Compile(ParamsFloorplanDeclaration? d)
        {
            var c = new CompiledFloorplan();
            if (d == null) { c.errors.Add("declaration required"); return c; }
            c.id = d.id ?? ""; c.originX = d.origin_x_mm; c.originZ = d.origin_z_mm;
            if (!ElementNaming.IsValid(c.id)) c.errors.Add($"Invalid floorplan id '{c.id}'");
            foreach (var p in d.points ?? Array.Empty<FloorplanPoint>())
            {
                if (p == null || !ElementNaming.IsValid(p.id)) { c.errors.Add($"Invalid point id '{p?.id}'"); continue; }
                if (!c.points.TryAdd(p.id, new Vector2Int(p.x, p.z))) c.errors.Add($"Duplicate point '{p.id}'");
            }
            if (c.points.Count < 2) c.errors.Add("At least 2 unique points required");

            var elementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var edges = new Dictionary<string, CompiledPlanWall>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in d.walls ?? Array.Empty<FloorplanWall>())
                AddWall(c, w?.id, w?.from, w?.to, w?.kind, w?.height ?? 0, w?.thickness_mm, elementIds, edges);
            foreach (var f in d.floors ?? Array.Empty<FloorplanFloor>())
            {
                if (f == null) continue;
                if (!Reserve(c, f.id, elementIds, "floor")) continue;
                ValidatePoly(c, f.poly, $"Floor '{f.id}'");
                c.floors.Add(new CompiledPlanFloor { id = f.id, poly = f.poly ?? Array.Empty<string>(), topY = f.top_y_mm, thickness = f.thickness_mm });
            }

            foreach (var r in d.rooms ?? Array.Empty<FloorplanRoom>())
            {
                if (r == null || !ElementNaming.IsValid(r.id)) { c.errors.Add($"Invalid room id '{r?.id}'"); continue; }
                ValidatePoly(c, r.poly, $"Room '{r.id}'");
                if (r.poly == null || r.poly.Length < 3) continue;
                var room = new CompiledPlanRoom { id = r.id, floor = r.id + "_floor" };
                if (Reserve(c, room.floor, elementIds, "room floor"))
                    c.floors.Add(new CompiledPlanFloor { id = room.floor, poly = r.poly, topY = r.top_y_mm, thickness = r.thickness_mm });
                for (int i = 0; i < r.poly.Length; i++)
                {
                    string a = r.poly[i], b = r.poly[(i + 1) % r.poly.Length];
                    string edge = EdgeKey(a, b);
                    if (!edges.TryGetValue(edge, out var wall))
                    {
                        string id = "wall_" + (string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? a + "_" + b : b + "_" + a);
                        AddWall(c, id, a, b, string.IsNullOrEmpty(r.kind) ? "partition" : r.kind,
                            r.height, null, elementIds, edges);
                        edges.TryGetValue(edge, out wall);
                    }
                    if (wall != null) room.walls.Add(wall.id);
                }
                c.rooms.Add(room);
            }

            var openingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in d.openings ?? Array.Empty<FloorplanOpening>())
            {
                if (o == null || !Reserve(c, o.id, elementIds, "opening")) continue;
                if (!openingIds.Add(o.id)) { c.errors.Add($"Duplicate opening '{o.id}'"); continue; }
                if (!HasWall(c, o.wall)) c.errors.Add($"Opening '{o.id}' references unknown wall '{o.wall}'");
                if (o.kind != "window" && o.kind != "door") c.errors.Add($"Opening '{o.id}' has invalid kind '{o.kind}'");
                if (o.offset_mm < 0 || o.width <= 0 || o.height <= 0 || o.sill_mm < 0)
                    c.errors.Add($"Opening '{o.id}' has invalid dimensions/offset");
                c.openings.Add(o);
                foreach (var room in c.rooms) if (room.walls.Contains(o.wall)) room.openings.Add(o.id);
            }
            return c;
        }

        public static string ToSvg(CompiledFloorplan c)
        {
            if (!c.IsValid) return "";
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var p in c.points.Values) { minX = Math.Min(minX, p.x); maxX = Math.Max(maxX, p.x); minZ = Math.Min(minZ, p.y); maxZ = Math.Max(maxZ, p.y); }
            int pad = 200, width = Math.Max(1, maxX - minX + pad * 2), height = Math.Max(1, maxZ - minZ + pad * 2);
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{0} {1} {2} {3}\">\n",
                minX - pad, -maxZ - pad, width, height);
            sb.Append("<g fill=\"#eeeeee\" stroke=\"none\">\n");
            foreach (var f in c.floors)
            {
                sb.Append("<polygon data-id=\"").Append(SecurityElement.Escape(f.id)).Append("\" points=\"");
                foreach (var id in f.poly) if (c.points.TryGetValue(id, out var p)) sb.Append(p.x).Append(',').Append(-p.y).Append(' ');
                sb.Append("\"/>\n");
            }
            sb.Append("</g>\n<g fill=\"none\" stroke-linecap=\"square\">\n");
            foreach (var w in c.walls)
            {
                var a = c.points[w.from]; var b = c.points[w.to];
                string color = w.kind == "bearing" ? "#444" : "#999";
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<line data-id=\"{0}\" x1=\"{1}\" y1=\"{2}\" x2=\"{3}\" y2=\"{4}\" stroke=\"{5}\" stroke-width=\"40\"/>\n",
                    SecurityElement.Escape(w.id), a.x, -a.y, b.x, -b.y, color);
            }
            foreach (var o in c.openings)
            {
                var w = c.walls.Find(x => string.Equals(x.id, o.wall, StringComparison.OrdinalIgnoreCase));
                if (w == null) continue;
                var a = c.points[w.from]; var b = c.points[w.to];
                var d = ((Vector2)(b - a)).normalized;
                Vector2 p1 = (Vector2)a + d * o.offset_mm, p2 = p1 + d * o.width;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<line data-id=\"{0}\" x1=\"{1:0.###}\" y1=\"{2:0.###}\" x2=\"{3:0.###}\" y2=\"{4:0.###}\" stroke=\"#fff\" stroke-width=\"55\"/>\n",
                    SecurityElement.Escape(o.id), p1.x, -p1.y, p2.x, -p2.y);
            }
            return sb.Append("</g>\n</svg>").ToString();
        }

        private static void AddWall(CompiledFloorplan c, string? id, string? from, string? to,
            string? kind, int height, int? thickness, HashSet<string> elementIds,
            Dictionary<string, CompiledPlanWall> edges)
        {
            id ??= ""; from ??= ""; to ??= ""; kind ??= "";
            if (!Reserve(c, id, elementIds, "wall")) return;
            if (!c.points.ContainsKey(from) || !c.points.ContainsKey(to)) c.errors.Add($"Wall '{id}' references unknown points '{from}'/'{to}'");
            if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) c.errors.Add($"Wall '{id}' endpoints must differ");
            if (kind != "bearing" && kind != "partition") c.errors.Add($"Wall '{id}' has invalid kind '{kind}'");
            if (height <= 0) c.errors.Add($"Wall '{id}' height must be positive");
            if (thickness.HasValue && thickness.Value <= 0) c.errors.Add($"Wall '{id}' thickness_mm must be positive");
            var wall = new CompiledPlanWall { id = id, from = from, to = to, kind = kind, height = height, thickness = thickness };
            c.walls.Add(wall);
            string edge = EdgeKey(from, to);
            if (!edges.TryAdd(edge, wall)) c.errors.Add($"Multiple walls declare edge '{from}-{to}'");
        }

        private static bool Reserve(CompiledFloorplan c, string id, HashSet<string> ids, string type)
        {
            if (!ElementNaming.IsValid(id)) { c.errors.Add($"Invalid {type} id '{id}'"); return false; }
            if (!ids.Add(id)) { c.errors.Add($"Duplicate element id '{id}'"); return false; }
            return true;
        }

        private static void ValidatePoly(CompiledFloorplan c, string[]? poly, string owner)
        {
            if (poly == null || poly.Length < 3) { c.errors.Add(owner + " requires at least 3 points"); return; }
            foreach (var id in poly) if (!c.points.ContainsKey(id)) c.errors.Add(owner + $" references unknown point '{id}'");
        }

        private static string EdgeKey(string a, string b) =>
            string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? a + "|" + b : b + "|" + a;
        private static bool HasWall(CompiledFloorplan c, string id) =>
            c.walls.Exists(w => string.Equals(w.id, id, StringComparison.OrdinalIgnoreCase));
    }
}
