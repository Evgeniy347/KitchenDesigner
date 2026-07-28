using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Элемент, который в спецификации раскладывается на несколько деталей
    /// (напр. сборный фасад → стойки/перекладины/вставка), а не одной строкой.</summary>
    public interface ISpecificationParts
    {
        IEnumerable<AssembledFacadeMesh.Part> GetSpecParts();
    }

    /// <summary>Строка спецификации: группа одинаковых деталей.</summary>
    public struct SpecLine
    {
        public string name;
        public Vector3Int dimensionsMM;
        public int count;
        public float areaPerBoardM2;   // площадь поверхности одной детали, м²
        public float totalAreaM2;      // count * areaPerBoardM2
        public string material;        // декор материала (для заказа раскроя)
        public string grooves;         // пазы через запятую, напр. «Сквозной 16*4*7:Верх»
        // Толщина кромки по торцам «0.5»; пустая строка — кромки на этой
        // стороне нет. Именно пустая, а не «0»: ноль в ведомости раскроя
        // читается как «кромка 0 мм», то есть как ошибка ввода.
        public string edgeL1;
        public string edgeL2;
        public string edgeW1;
        public string edgeW2;
    }

    /// <summary>Колонки кромок одной строки спецификации. Пустая строка =
    /// кромки на этой стороне нет.</summary>
    public readonly struct EdgeColumns
    {
        public readonly string l1, l2, w1, w2;

        public EdgeColumns(string l1, string l2, string w1, string w2)
        {
            this.l1 = l1; this.l2 = l2; this.w1 = w1; this.w2 = w2;
        }

        /// <summary>Часть ключа группировки раскроя.</summary>
        public string Key => $"{l1}/{l2}/{w1}/{w2}";

        /// <summary>Кромки детали по фактической геометрии сцены. Пусто, если
        /// деталь кромкование не поддерживает или галочка снята.</summary>
        public static EdgeColumns For(KitchenElement element, IReadOnlyList<KitchenElement> all)
        {
            if (element == null || !element.EdgeBandingEnabled) return default;

            var coverage = EdgeBanding.Coverage(element, all);
            string t = EdgeBanding.FormatThickness(element.EdgeThicknessMM);
            return new EdgeColumns(
                coverage.HasEdge(EdgeSide.L1) ? t : "",
                coverage.HasEdge(EdgeSide.L2) ? t : "",
                coverage.HasEdge(EdgeSide.W1) ? t : "",
                coverage.HasEdge(EdgeSide.W2) ? t : "");
        }
    }

    public struct SpecResult
    {
        public List<SpecLine> lines;
        public int totalCount;
        public float totalAreaM2;
    }

    public static class SpecificationExport
    {
        public static string ToCsv(SpecResult result)
        {
            var sb = new StringBuilder();
            // Кромки — последними колонками (порядок прежних 9 колонок сохранён).
            sb.AppendLine("Name;Width_mm;Height_mm;Depth_mm;Count;AreaPerBoard_m2;TotalArea_m2;Material;Grooves;" +
                "Кромка L1;Кромка L2;Кромка W1;Кромка W2");
            foreach (var line in result.lines)
            {
                sb.AppendLine($"{EscapeCsv(line.name)};{line.dimensionsMM.x};{line.dimensionsMM.y};" +
                    $"{line.dimensionsMM.z};{line.count};{line.areaPerBoardM2:F4};{line.totalAreaM2:F4};" +
                    $"{EscapeCsv(line.material)};{EscapeCsv(line.grooves)};" +
                    $"{line.edgeL1};{line.edgeL2};{line.edgeW1};{line.edgeW2}");
            }
            sb.AppendLine();
            // 13 колонок: ...;TotalArea(7-я);Material(8-я);Grooves(9-я);кромки(10..13).
            // Итог: count в Count (5-я), площадь в TotalArea (7-я), остальные пусты.
            sb.AppendLine($"Total;;;;{result.totalCount};;{result.totalAreaM2:F4};;;;;;");
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
        /// <summary>
        /// Площадь поверхности детали (все 6 граней), м².
        /// 2*(Ш*В + Ш*Г + В*Г), размеры из мм в метры.
        /// </summary>
        public static float SurfaceAreaM2(Vector3Int dimsMM)
        {
            float w = dimsMM.x * AppConstants.MM_TO_UNITS;
            float h = dimsMM.y * AppConstants.MM_TO_UNITS;
            float d = dimsMM.z * AppConstants.MM_TO_UNITS;
            return 2f * (w * h + w * d + h * d);
        }

        /// <summary>Группирует детали по (размер, материал, пазы) — имя в ключ не
        /// входит, см. Accumulate. BasePlate исключается.</summary>
        public static SpecResult Build(IEnumerable<KitchenElement> elements)
        {
            var order = new List<string>();
            var groups = new Dictionary<string, SpecLine>();

            // Наличие кромки считается по соседям, поэтому список нужен целиком,
            // а не по одному элементу за раз.
            var all = new List<KitchenElement>(elements);

            foreach (var e in all)
            {
                if (e == null) continue;
                if (e.GetComponent<BasePlate>() != null || e.GetComponent<Wall>() != null) continue;

                // Сборный фасад и т.п. — раскладываем на детали (стойки/перекладины/вставка).
                if (e is ISpecificationParts composite)
                {
                    string decor = MaterialCatalog.Get(e.MaterialId).displayName;
                    foreach (var part in composite.GetSpecParts())
                        Accumulate(groups, order, $"{e.PartName}·{part.suffix}",
                            part.dimsMM, part.materialKind ?? decor, "", default);
                    continue;
                }

                // Ящик Movento — деревянный короб: в спецификацию отдельными деталями
                // (боковины, перед, задник, дно), суффикс отличает их в раскрое.
                // Ящик GTV (покупной металлический короб) остаётся одной строкой ниже.
                if (e is DrawerElement drawer && drawer.System == DrawerSystem.Movento)
                {
                    string decor = MaterialCatalog.Get(e.MaterialId).displayName;
                    foreach (var part in drawer.GetSpecParts())
                        Accumulate(groups, order, $"{e.PartName}·{part.suffix}",
                            part.dimsMM, part.materialKind ?? decor, "", default);
                    continue;
                }

                Accumulate(groups, order, e.PartName, e.DimensionsMM,
                    MaterialCatalog.Get(e.MaterialId).displayName, GroovesLabel(e),
                    EdgeColumns.For(e, all));
            }

            var result = new SpecResult { lines = new List<SpecLine>() };
            foreach (var key in order)
            {
                var line = groups[key];
                result.lines.Add(line);
                result.totalCount += line.count;
                result.totalAreaM2 += line.totalAreaM2;
            }
            return result;
        }

        /// <summary>Пазы детали одной строкой для спецификации: «Сквозной 16*4*7:Верх,
        /// Глухой 16*4*7:Лево». Пусто, если пазов нет.</summary>
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

        /// <summary>Добавить одну деталь в группировку по (размер, материал, пазы).
        /// Пазы в ключе: детали с разной врезкой — разные позиции раскроя.
        ///
        /// Название в ключ НЕ входит: имена элементов уникальны в рамках проекта
        /// (ElementNaming добавляет суффикс «_N»), поэтому две одинаковые боковины
        /// зовутся «Bokovina» и «Bokovina_1» — по имени они бы никогда не сошлись,
        /// и ведомость раскроя выродилась бы в список строк по одной штуке.
        /// В строке показывается имя ПЕРВОЙ детали группы.</summary>
        private static void Accumulate(Dictionary<string, SpecLine> groups, List<string> order,
            string name, Vector3Int dims, string material, string grooves, EdgeColumns edges)
        {
            // Кромки — часть ключа: две одинаковые полки, одна из которых
            // кромкуется с трёх сторон, а другая с одной, это РАЗНЫЕ позиции
            // раскроя, и склеивать их в одну строку нельзя.
            string key = $"{dims.x}x{dims.y}x{dims.z}|{material}|{grooves}|{edges.Key}";
            if (!groups.TryGetValue(key, out var line))
            {
                line = new SpecLine
                {
                    name = name,
                    dimensionsMM = dims,
                    count = 0,
                    areaPerBoardM2 = SurfaceAreaM2(dims),
                    material = material,
                    grooves = grooves,
                    edgeL1 = edges.l1,
                    edgeL2 = edges.l2,
                    edgeW1 = edges.w1,
                    edgeW2 = edges.w2,
                };
                order.Add(key);
            }
            line.count++;
            line.totalAreaM2 = line.count * line.areaPerBoardM2;
            groups[key] = line;
        }
    }
}
