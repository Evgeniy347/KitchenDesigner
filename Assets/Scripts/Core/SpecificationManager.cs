using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Строка спецификации: группа одинаковых досок.</summary>
    public struct SpecLine
    {
        public string name;
        public Vector3Int dimensionsMM;
        public int count;
        public float areaPerBoardM2;   // площадь поверхности одной доски, м²
        public float totalAreaM2;      // count * areaPerBoardM2
    }

    public struct SpecResult
    {
        public List<SpecLine> lines;
        public int totalCount;
        public float totalAreaM2;
    }

    public static class SpecificationManager
    {
        /// <summary>
        /// Площадь поверхности доски (все 6 граней), м².
        /// 2*(Ш*В + Ш*Г + В*Г), размеры из мм в метры.
        /// </summary>
        public static float SurfaceAreaM2(Vector3Int dimsMM)
        {
            float w = dimsMM.x * AppConstants.MM_TO_UNITS;
            float h = dimsMM.y * AppConstants.MM_TO_UNITS;
            float d = dimsMM.z * AppConstants.MM_TO_UNITS;
            return 2f * (w * h + w * d + h * d);
        }

        /// <summary>Группирует доски по (размер, название). BasePlate исключается.</summary>
        public static SpecResult Build(IEnumerable<KitchenElement> elements)
        {
            var order = new List<string>();
            var groups = new Dictionary<string, SpecLine>();

            foreach (var e in elements)
            {
                if (e == null) continue;
                if (e.GetComponent<BasePlate>() != null) continue;

                var dims = e.DimensionsMM;
                string key = $"{e.BoardName}|{dims.x}x{dims.y}x{dims.z}";

                if (!groups.TryGetValue(key, out var line))
                {
                    line = new SpecLine
                    {
                        name = e.BoardName,
                        dimensionsMM = dims,
                        count = 0,
                        areaPerBoardM2 = SurfaceAreaM2(dims)
                    };
                    order.Add(key);
                }

                line.count++;
                line.totalAreaM2 = line.count * line.areaPerBoardM2;
                groups[key] = line;
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
    }
}
