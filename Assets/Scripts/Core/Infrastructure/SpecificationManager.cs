using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface ISpecificationParts
    {
        IEnumerable<AssembledFacadeMesh.Part> GetSpecParts();
    }

    public interface IQuantifies
    {
        IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements);
    }

    public struct SpecLine
    {
        public string name;
        public Vector3Int dimensionsMM;
        public int count;
        public float areaPerBoardM2;
        public float totalAreaM2;
        public string material;
        public string grooves;
        public string edgeL1;
        public string edgeL2;
        public string edgeW1;
        public string edgeW2;

        public string section;
        public SpecUnit unit;
        public bool hasDims;

        public float qtyPerItem;
        public float qtyTotal;
    }

    public readonly struct EdgeColumns
    {
        public readonly string l1, l2, w1, w2;

        public EdgeColumns(string l1, string l2, string w1, string w2)
        {
            this.l1 = l1; this.l2 = l2; this.w1 = w1; this.w2 = w2;
        }

        public string Key => $"{l1}/{l2}/{w1}/{w2}";

        public static EdgeColumns For(KitchenElement element, IReadOnlyList<KitchenElement> all)
        {
            if (element == null || !element.EdgeBandingEnabled) return default;

            var coverage = EdgeBanding.Coverage(element, all);
            string t = EdgeBanding.FormatThickness(element.EdgeThicknessMM);
            return new EdgeColumns(
                EdgeBanding.HasEdgeEffective(element, coverage, EdgeSide.L1) ? t : "",
                EdgeBanding.HasEdgeEffective(element, coverage, EdgeSide.L2) ? t : "",
                EdgeBanding.HasEdgeEffective(element, coverage, EdgeSide.W1) ? t : "",
                EdgeBanding.HasEdgeEffective(element, coverage, EdgeSide.W2) ? t : "");
        }
    }

    public struct SpecResult
    {
        public List<SpecLine> lines;
        public int totalCount;
        public float totalAreaM2;

        public Dictionary<SpecUnit, float> totalsByUnit;
    }

    public static class SpecificationExport
    {
        public static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("ru-RU");

        private static readonly string[] HeaderCells =
        {
            "Name", "Width_mm", "Height_mm", "Depth_mm", "Count", "AreaPerBoard_m2", "TotalArea_m2",
            "Material", "Grooves", "Кромка L1", "Кромка L2", "Кромка W1", "Кромка W2",
            "Section", "Unit", "QtyPerItem", "QtyTotal",
        };
        private const int ColCount = 4, ColArea = 6, ColUnit = 14, ColQtyTotal = 16;

        private static string Row(params string[] cells)
        {
            var padded = new string[HeaderCells.Length];
            for (int i = 0; i < padded.Length; i++) padded[i] = i < cells.Length ? cells[i] ?? "" : "";
            return string.Join(";", padded);
        }

        public static string ToCsv(SpecResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", HeaderCells));
            foreach (var line in result.lines)
            {
                sb.AppendLine($"{EscapeCsv(line.name)};{line.dimensionsMM.x};{line.dimensionsMM.y};" +
                    $"{line.dimensionsMM.z};{line.count};{line.areaPerBoardM2.ToString("F4", NumberCulture)};{line.totalAreaM2.ToString("F4", NumberCulture)};" +
                    $"{EscapeCsv(line.material)};{EscapeCsv(line.grooves)};" +
                    $"{line.edgeL1};{line.edgeL2};{line.edgeW1};{line.edgeW2};" +
                    $"{EscapeCsv(line.section)};{line.unit.Label()};{line.qtyPerItem.ToString("F4", NumberCulture)};{line.qtyTotal.ToString("F4", NumberCulture)}");
            }
            sb.AppendLine();

            var totalCells = new string[HeaderCells.Length];
            totalCells[0] = "Total";
            totalCells[ColCount] = result.totalCount.ToString();
            totalCells[ColArea] = result.totalAreaM2.ToString("F4", NumberCulture);
            sb.AppendLine(Row(totalCells));

            if (result.totalsByUnit != null)
            {
                foreach (var unit in result.totalsByUnit.Keys.OrderBy(u => (int)u))
                {
                    var unitCells = new string[HeaderCells.Length];
                    unitCells[0] = "Итого";
                    unitCells[ColUnit] = unit.Label();
                    unitCells[ColQtyTotal] = result.totalsByUnit[unit].ToString("F4", NumberCulture);
                    sb.AppendLine(Row(unitCells));
                }
            }
            return sb.ToString();
        }

        public static bool SaveToFile(SpecResult result, string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, ToCsv(result), Encoding.UTF8);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SpecExport] Failed: {ex.Message}");
                return false;
            }
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(";") || s.Contains("\"") || s.Contains("\n"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }

    public static class SpecificationManager
    {
        public static float SurfaceAreaM2(Vector3Int dimsMM)
        {
            float w = dimsMM.x * AppConstants.MM_TO_UNITS;
            float h = dimsMM.y * AppConstants.MM_TO_UNITS;
            float d = dimsMM.z * AppConstants.MM_TO_UNITS;
            return 2f * (w * h + w * d + h * d);
        }

        public static SpecResult Build(IEnumerable<KitchenElement> elements)
        {
            var order = new List<string>();
            var groups = new Dictionary<string, SpecLine>();

            var all = new List<KitchenElement>(elements);

            foreach (var e in all)
            {
                if (e == null) continue;

                bool selfQuantified = false;
                foreach (var quantifies in e.GetComponents<IQuantifies>())
                {
                    selfQuantified = true;
                    foreach (var item in quantifies.GetSpecItems(all))
                        AccumulateItem(groups, order, item);
                }
                if (selfQuantified) continue;

                if (e is ISpecificationParts composite)
                {
                    string decor = MaterialCatalog.Get(e.MaterialId).displayName;
                    foreach (var part in composite.GetSpecParts())
                        Accumulate(groups, order, $"{e.PartName}·{part.suffix}",
                            part.dimsMM, part.materialKind ?? decor, "", default);
                    continue;
                }

                if (!e.IsFlatBoardElement) continue;

                var edges = EdgeColumns.For(e, all);
                Accumulate(groups, order, e.PartName, e.DimensionsMM,
                    MaterialCatalog.Get(e.MaterialId).displayName, GroovesLabel(e), edges);

                var layout = EdgeBanding.LayoutOf(e.DimensionsMM);
                if (layout.IsValid)
                {
                    AddEdgeBandingItem(groups, order, edges.l1, layout.SideLengthMM(EdgeSide.L1));
                    AddEdgeBandingItem(groups, order, edges.l2, layout.SideLengthMM(EdgeSide.L2));
                    AddEdgeBandingItem(groups, order, edges.w1, layout.SideLengthMM(EdgeSide.W1));
                    AddEdgeBandingItem(groups, order, edges.w2, layout.SideLengthMM(EdgeSide.W2));
                }
            }

            var result = new SpecResult { lines = new List<SpecLine>() };
            foreach (var key in order)
            {
                var line = groups[key];
                result.lines.Add(line);
                if (line.unit == SpecUnit.AreaM2)
                {
                    result.totalCount += line.count;
                    result.totalAreaM2 += line.totalAreaM2;
                }
            }

            result.lines = result.lines
                .OrderBy(l => l.material, System.StringComparer.Ordinal)
                .ToList();

            result.totalsByUnit = SpecTotals.ByUnit(result.lines.Select(l => (l.unit, l.qtyTotal)));

            return result;
        }

        public static string GroovesLabel(KitchenElement element)
        {
            if (element == null) return "";
            var grooves = element.Grooves;
            if (grooves.Count == 0) return "";

            var sb = new StringBuilder();
            for (int i = 0; i < grooves.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(grooves[i].ToString());
            }
            return sb.ToString();
        }

        private static void Accumulate(Dictionary<string, SpecLine> groups, List<string> order,
            string name, Vector3Int dims, string material, string grooves, EdgeColumns edges)
        {
            string key = $"{dims.x}x{dims.y}x{dims.z}|{material}|{grooves}|{edges.Key}";
            if (!groups.TryGetValue(key, out var line))
            {
                float faceArea = BoardFaceArea.FaceAreaM2(dims);
                line = new SpecLine
                {
                    name = name,
                    dimensionsMM = dims,
                    count = 0,
                    areaPerBoardM2 = faceArea,
                    material = material,
                    grooves = grooves,
                    edgeL1 = edges.l1,
                    edgeL2 = edges.l2,
                    edgeW1 = edges.w1,
                    edgeW2 = edges.w2,
                    section = SpecSections.Furniture,
                    unit = SpecUnit.AreaM2,
                    hasDims = true,
                    qtyPerItem = faceArea,
                };
                order.Add(key);
            }
            line.count++;
            line.totalAreaM2 = line.count * line.areaPerBoardM2;
            line.qtyTotal = line.count * line.qtyPerItem;
            groups[key] = line;
        }

        private static void AccumulateItem(Dictionary<string, SpecLine> groups, List<string> order, SpecItem item)
        {
            string key = item.GroupKey();
            if (!groups.TryGetValue(key, out var line))
            {
                line = new SpecLine
                {
                    name = item.name,
                    dimensionsMM = item.hasDims ? item.dimsMM : default,
                    count = 0,
                    areaPerBoardM2 = 0f,
                    totalAreaM2 = 0f,
                    material = item.material,
                    grooves = "",
                    section = item.section,
                    unit = item.unit,
                    hasDims = item.hasDims,
                    qtyPerItem = item.hasDims ? item.qty : 0f,
                };
                order.Add(key);
            }
            line.count++;
            line.qtyTotal += item.qty;
            if (line.unit == SpecUnit.AreaM2)
            {
                line.areaPerBoardM2 = item.hasDims ? item.qty : 0f;
                line.totalAreaM2 = line.qtyTotal;
            }
            groups[key] = line;
        }

        private static void AddEdgeBandingItem(Dictionary<string, SpecLine> groups, List<string> order,
            string thicknessLabel, int sideLengthMM)
        {
            if (string.IsNullOrEmpty(thicknessLabel)) return;
            AccumulateItem(groups, order, EdgeBandingSpecItems.For(thicknessLabel, sideLengthMM));
        }
    }
}
