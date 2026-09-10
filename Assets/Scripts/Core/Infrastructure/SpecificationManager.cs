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
        public bool isBoardArea;

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

            var layout = EdgeBanding.LayoutOf(element.DimensionsMM);
            if (!layout.IsValid) return default;

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
        public Dictionary<(string section, SpecUnit unit), float> totalsBySection;
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
        private const int ColCount = 4, ColArea = 6, ColSection = 13, ColUnit = 14, ColQtyTotal = 16;

        private static string Row(params string[] cells)
        {
            var padded = new string[HeaderCells.Length];
            for (int i = 0; i < padded.Length; i++) padded[i] = i < cells.Length ? cells[i] ?? "" : "";
            return string.Join(";", padded);
        }

        private static string AreaCell(float value, bool hasDims) =>
            hasDims ? value.ToString("F4", NumberCulture) : "";

        public static string ToCsv(SpecResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", HeaderCells));
            foreach (var line in result.lines)
            {
                sb.AppendLine($"{EscapeCsv(line.name)};{SpecCellFormat.DimCell(line.dimensionsMM.x, line.hasDims)};" +
                    $"{SpecCellFormat.DimCell(line.dimensionsMM.y, line.hasDims)};{SpecCellFormat.DimCell(line.dimensionsMM.z, line.hasDims)};" +
                    $"{SpecCellFormat.CountCell(line.count, line.hasDims)};{AreaCell(line.areaPerBoardM2, line.hasDims)};{AreaCell(line.totalAreaM2, line.hasDims)};" +
                    $"{EscapeCsv(line.material)};{EscapeCsv(line.grooves)};" +
                    $"{line.edgeL1};{line.edgeL2};{line.edgeW1};{line.edgeW2};" +
                    $"{EscapeCsv(line.section)};{line.unit.Label()};{AreaCell(line.qtyPerItem, line.hasDims)};{line.qtyTotal.ToString("F4", NumberCulture)}");
            }
            sb.AppendLine();

            var totalCells = new string[HeaderCells.Length];
            totalCells[0] = "Total";
            totalCells[ColCount] = result.totalCount.ToString();
            totalCells[ColArea] = result.totalAreaM2.ToString("F4", NumberCulture);
            sb.AppendLine(Row(totalCells));

            if (result.totalsBySection != null)
            {
                foreach (var key in result.totalsBySection.Keys
                    .OrderBy(k => (int)k.unit)
                    .ThenBy(k => k.section, System.StringComparer.Ordinal))
                {
                    var sectionCells = new string[HeaderCells.Length];
                    sectionCells[0] = "Итого";
                    sectionCells[ColSection] = key.section;
                    sectionCells[ColUnit] = key.unit.Label();
                    sectionCells[ColQtyTotal] = result.totalsBySection[key].ToString("F4", NumberCulture);
                    sb.AppendLine(Row(sectionCells));
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

                string boardDecor = MaterialCatalog.Get(e.MaterialId).displayName;
                var edges = EdgeColumns.For(e, all);
                Accumulate(groups, order, e.PartName, e.DimensionsMM,
                    boardDecor, GroovesLabel(e), edges);

                var layout = EdgeBanding.LayoutOf(e.DimensionsMM);
                if (layout.IsValid)
                {
                    AddEdgeBandingItem(groups, order, edges.l1, layout.SideLengthMM(EdgeSide.L1), boardDecor);
                    AddEdgeBandingItem(groups, order, edges.l2, layout.SideLengthMM(EdgeSide.L2), boardDecor);
                    AddEdgeBandingItem(groups, order, edges.w1, layout.SideLengthMM(EdgeSide.W1), boardDecor);
                    AddEdgeBandingItem(groups, order, edges.w2, layout.SideLengthMM(EdgeSide.W2), boardDecor);
                }
            }

            var result = new SpecResult
            {
                lines = order.Select(key => groups[key])
                    .OrderBy(l => l.material, System.StringComparer.Ordinal)
                    .ToList(),
            };

            var byUnit = new Dictionary<SpecUnit, float>();
            var bySection = new Dictionary<(string, SpecUnit), float>();
            foreach (var line in result.lines)
            {
                byUnit[line.unit] = byUnit.TryGetValue(line.unit, out var uSum) ? uSum + line.qtyTotal : line.qtyTotal;
                var sectionKey = (line.section, line.unit);
                bySection[sectionKey] = bySection.TryGetValue(sectionKey, out var sSum)
                    ? sSum + line.qtyTotal : line.qtyTotal;

                if (!line.isBoardArea) continue;
                result.totalCount += line.count;
                result.totalAreaM2 += line.totalAreaM2;
            }
            result.totalsByUnit = byUnit;
            result.totalsBySection = bySection;

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
                    isBoardArea = true,
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
                    isBoardArea = item.hasDims && item.unit == SpecUnit.AreaM2,
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
            string thicknessLabel, int sideLengthMM, string material)
        {
            if (string.IsNullOrEmpty(thicknessLabel)) return;
            AccumulateItem(groups, order, EdgeBandingSpecItems.For(thicknessLabel, sideLengthMM, material));
        }
    }
}
