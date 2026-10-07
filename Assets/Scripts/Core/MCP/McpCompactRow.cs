using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Bulk;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal static class McpCompactRow
    {
        public static readonly string[] Fields =
            { "name", "kind", "posMm", "footprintMm", "sizeMm", "rotYDeg", "hasViolations", "module", "wallKind" };

        public static HashSet<string> Allowed() => new HashSet<string>(Fields, StringComparer.OrdinalIgnoreCase);

        public static Dictionary<string, object?> Build(KitchenElement e, HashSet<string> fields,
            McpReference reference, ValidationResult? validation)
        {
            var row = new Dictionary<string, object?>();
            var box = McpAnchor.ToMmBoxStruct(McpAabb.Of(e.GetVertices()));
            if (fields.Contains("name")) row["name"] = e.PartName;
            if (fields.Contains("kind")) row["kind"] = ElementSelector.TypeOf(e);
            if (fields.Contains("posMm")) row["posMm"] = McpAnchor.MmTriple(reference.PointOf(box.Min, box.Max));
            if (fields.Contains("footprintMm")) row["footprintMm"] = McpAnchor.MmTriple(box.Size);
            if (fields.Contains("sizeMm")) row["sizeMm"] = new[] { e.DimensionsMM.x, e.DimensionsMM.y, e.DimensionsMM.z };
            if (fields.Contains("rotYDeg")) row["rotYDeg"] = Mathf.Round(e.transform.eulerAngles.y * 10f) / 10f;
            if (fields.Contains("hasViolations")) row["hasViolations"] = validation != null && validation.violations.Contains(e);
            if (fields.Contains("module")) row["module"] = GroupManager.GroupOf(e)?.name;
            if (fields.Contains("wallKind")) row["wallKind"] = e.GetComponent<Wall>()?.Kind;
            return row;
        }
    }
}
