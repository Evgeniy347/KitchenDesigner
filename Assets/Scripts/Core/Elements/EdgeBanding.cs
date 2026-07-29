using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Торец листовой детали в обозначениях раскроя: L1/L2 — длинные
    /// стороны прямоугольника (полоса кромки длиной L), W1/W2 — короткие.
    /// Номер 1 — сторона по положительному направлению оси детали.</summary>
    public enum EdgeSide
    {
        L1,
        L2,
        W1,
        W2,
    }

    /// <summary>Маска «ручных» сторон: пользователь сам решил, есть ли кромка на
    /// этой стороне, и снял с неё автоматическую проверку. Маска, а не четыре
    /// поля, — чтобы состояние ехало через сохранение, команды и конвертацию
    /// одним числом, как и остальные флаги детали.</summary>
    public static class EdgeManual
    {
        public static int Bit(EdgeSide side) => 1 << (int)side;

        /// <summary>Все четыре стороны разом — сюда мигрирует старый флажок
        /// «не проверять кромки», который стоял на всю деталь.</summary>
        public const int AllMask = 0b1111;

        public static bool Has(int mask, EdgeSide side) => (mask & Bit(side)) != 0;

        public static int With(int mask, EdgeSide side, bool manual) =>
            manual ? mask | Bit(side) : mask & ~Bit(side);
    }

    /// <summary>Разбор габарита листовой детали на «толщина / длина / ширина» и
    /// соответствие сторон L1/L2/W1/W2 индексам граней <see cref="KitchenElement.GetFaces"/>.
    /// Порядок граней — контракт: index/2 = ось (0=X, 1=Y, 2=Z), чётный индекс —
    /// положительное направление.</summary>
    public readonly struct EdgeLayout
    {
        public readonly bool IsValid;
        /// <summary>Ось толщины плиты (сторона тоньше EDGE_MAX_SIDE_MM).</summary>
        public readonly int ThicknessAxis;
        /// <summary>Ось длинной стороны прямоугольника (её размер = LengthMM).</summary>
        public readonly int LengthAxis;
        public readonly int WidthAxis;
        public readonly int LengthMM;
        public readonly int WidthMM;
        public readonly int ThicknessMM;

        public EdgeLayout(int thicknessAxis, int lengthAxis, int widthAxis,
            int lengthMM, int widthMM, int thicknessMM)
        {
            IsValid = true;
            ThicknessAxis = thicknessAxis;
            LengthAxis = lengthAxis;
            WidthAxis = widthAxis;
            LengthMM = lengthMM;
            WidthMM = widthMM;
            ThicknessMM = thicknessMM;
        }

        /// <summary>Индекс грани-торца для стороны. Полоса кромки длиной L лежит
        /// на грани, ПЕРПЕНДИКУЛЯРНОЙ короткой оси (её размеры — L × толщина),
        /// поэтому L-стороны берут ось ширины, а W-стороны — ось длины.</summary>
        public int FaceIndex(EdgeSide side) => side switch
        {
            EdgeSide.L1 => WidthAxis * 2,
            EdgeSide.L2 => WidthAxis * 2 + 1,
            EdgeSide.W1 => LengthAxis * 2,
            _ => LengthAxis * 2 + 1,
        };

        /// <summary>Длина полосы кромки на этой стороне, мм.</summary>
        public int SideLengthMM(EdgeSide side) =>
            side == EdgeSide.L1 || side == EdgeSide.L2 ? LengthMM : WidthMM;
    }

    /// <summary>Доля перекрытия каждого торца соседями (0 — открыт, 1 — закрыт).
    /// Кромка есть там, где торец НЕ перекрыт целиком.</summary>
    public readonly struct EdgeCoverage
    {
        private readonly float _l1, _l2, _w1, _w2;

        public EdgeCoverage(float l1, float l2, float w1, float w2)
        {
            _l1 = l1; _l2 = l2; _w1 = w1; _w2 = w2;
        }

        public float Ratio(EdgeSide side) => side switch
        {
            EdgeSide.L1 => _l1,
            EdgeSide.L2 => _l2,
            EdgeSide.W1 => _w1,
            _ => _w2,
        };

        /// <summary>Торец перекрыт целиком (упирается в соседа/стену/пол) —
        /// кромка не нужна.</summary>
        public bool IsCovered(EdgeSide side) => Ratio(side) >= 1f - EdgeBanding.CoverEpsilon;

        /// <summary>Есть кромка: торец открыт хотя бы частично.</summary>
        public bool HasEdge(EdgeSide side) => !IsCovered(side);

        /// <summary>Торец перекрыт ЧАСТИЧНО — конструктивная ошибка: кромку
        /// клеить придётся, но соседняя деталь на неё наезжает.</summary>
        public bool IsPartial(EdgeSide side)
        {
            float r = Ratio(side);
            return r > EdgeBanding.CoverEpsilon && r < 1f - EdgeBanding.CoverEpsilon;
        }
    }

    /// <summary>
    /// Кромкование торцов: какие торцы детали открыты (кромка есть), а какие
    /// упираются в соседей (кромки нет). Считается ПОЛНОСТЬЮ автоматически по
    /// геометрии сцены — вручную кромка не назначается.
    ///
    /// Перекрытие меряется площадью: собираем прямоугольники встречных граней
    /// соседей, проецируем на плоскость торца и берём площадь их объединения.
    /// Именно объединения, а не «есть контакт с кем-то»: торец, закрытый двумя
    /// деталями пополам, перекрыт целиком, и кромки на нём нет.
    /// </summary>
    public static class EdgeBanding
    {
        /// <summary>Допуск по доле перекрытия. 0.1 % площади торца — заведомо
        /// больше ошибки float и заведомо меньше касания любой реальной детали
        /// (18 мм на торце 500×18 — это 3.6 %).</summary>
        public const float CoverEpsilon = 0.001f;

        /// <summary>Деталь — лист: ровно одна сторона тоньше порога. У бруска
        /// (две и более тонких стороны) торец не определён.</summary>
        public static bool IsSheet(Vector3Int dimsMM) => ThinAxis(dimsMM) >= 0;

        /// <summary>Индекс единственной тонкой оси или −1.</summary>
        public static int ThinAxis(Vector3Int dimsMM)
        {
            int axis = -1;
            for (int i = 0; i < 3; i++)
            {
                if (dimsMM[i] >= AppConstants.EDGE_MAX_SIDE_MM) continue;
                if (axis >= 0) return -1; // тонких сторон больше одной
                axis = i;
            }
            return axis;
        }

        /// <summary>Разбор габарита на толщину/длину/ширину. При равных сторонах
        /// длинной считается ось с меньшим индексом — раскладка детерминирована.</summary>
        public static EdgeLayout LayoutOf(Vector3Int dimsMM)
        {
            int thick = ThinAxis(dimsMM);
            if (thick < 0) return default;

            int a = (thick + 1) % 3;
            int b = (thick + 2) % 3;
            if (a > b) (a, b) = (b, a);

            int lengthAxis = dimsMM[a] >= dimsMM[b] ? a : b;
            int widthAxis = lengthAxis == a ? b : a;
            return new EdgeLayout(thick, lengthAxis, widthAxis,
                dimsMM[lengthAxis], dimsMM[widthAxis], dimsMM[thick]);
        }

        /// <summary>Строка для CSV: толщина кромки с округлением до десятых.
        /// Разделитель — точка (файл читают станки и Excel с любой локалью).</summary>
        public static string FormatThickness(float thicknessMM) =>
            thicknessMM.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Сосед, который торец НЕ закрывает и кромку с него не снимает.
        ///
        /// Лампа — декор, мойка врезана в столешницу: ни та, ни другая торец не
        /// закрывают (те же исключения, что у ConstraintValidator). Опора
        /// подпирает деталь снизу точечно: торец над ней остаётся видимым и
        /// кромкуется целиком, иначе каждая деталь на опорах ловила ложную
        /// EDG-01 на 2–9 %.
        ///
        /// Фасад и ящик ОТКРЫВАЮТСЯ: торец за ними виден в открытом состоянии и
        /// кромкуется. Считать их перекрытием нельзя — передние торцы всего
        /// корпуса уходили в раскрой без кромки, а фасад с зазорами (GappedBox
        /// уменьшает габарит) накрывал торец частично и давал ложную EDG-01.
        /// Грани фасада к тому же берутся от ЗАКРЫТОЙ позы, так что от анимации
        /// это не зависит. AssembledFacadeElement наследует FacadeElement и
        /// попадает сюда же; PanelElement (ХДФ, задняя стенка, дно ящика)
        /// дверцей не является и торец закрывает как обычная деталь.</summary>
        private static bool IsTransparentToEdges(KitchenElement other) =>
            other is LightSourceElement || other is SinkElement || other is CooktopElement || other is PillarElement
            || other is FacadeElement || other is DrawerElement;

        /// <summary>Доля перекрытия каждого торца детали соседями. others —
        /// вся сцена (пол и стены тоже перекрывают торец и снимают кромку).</summary>
        public static EdgeCoverage Coverage(KitchenElement element, IReadOnlyList<KitchenElement> others)
        {
            if (element == null) return default;
            var layout = LayoutOf(element.DimensionsMM);
            if (!layout.IsValid) return default;

            var faces = element.GetFaces();
            var ends = new[]
            {
                faces[layout.FaceIndex(EdgeSide.L1)],
                faces[layout.FaceIndex(EdgeSide.L2)],
                faces[layout.FaceIndex(EdgeSide.W1)],
                faces[layout.FaceIndex(EdgeSide.W2)],
            };

            // Прямоугольники-перекрытия собираются ЗА ОДИН проход по сцене:
            // грани соседа считаются один раз на все четыре торца, а не по разу
            // на каждый (на проекте в 200+ деталей это разница в 4 раза).
            var covers = new List<Rect>[4];
            for (int i = 0; i < 4; i++) covers[i] = new List<Rect>();

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            foreach (var other in others)
            {
                if (other == null || other == element) continue;
                if (!other.gameObject.activeInHierarchy) continue;
                if (IsTransparentToEdges(other)) continue;

                var otherFaces = other.GetFaces();
                for (int i = 0; i < 4; i++)
                {
                    var face = ends[i];
                    Vector3 u = face.rightAxis, v = face.upAxis;
                    Rect target = FaceRect(face, u, v);

                    foreach (var of in otherFaces)
                    {
                        // Перекрывает только ВСТРЕЧНАЯ грань вплотную (dot ≈ −1).
                        if (Vector3.Dot(face.normal, of.normal) > -Tolerance.ParallelDot) continue;
                        if (Mathf.Abs(Vector3.Dot(of.center - face.center, face.normal)) > contactDist) continue;

                        Rect r = FaceRect(of, u, v);
                        float xMin = Mathf.Max(target.xMin, r.xMin), xMax = Mathf.Min(target.xMax, r.xMax);
                        float yMin = Mathf.Max(target.yMin, r.yMin), yMax = Mathf.Min(target.yMax, r.yMax);
                        if (xMax <= xMin || yMax <= yMin) continue;
                        covers[i].Add(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
                    }
                }
            }

            return new EdgeCoverage(
                CoveredRatio(ends[0], covers[0]),
                CoveredRatio(ends[1], covers[1]),
                CoveredRatio(ends[2], covers[2]),
                CoveredRatio(ends[3], covers[3]));
        }

        /// <summary>Деталь, которая закрывает торец больше остальных (по площади
        /// перекрытия), или null. Нужна, чтобы ошибка «торец перекрыт частично»
        /// называла ВИНОВНИКА и подсвечивала его в сцене, а не только саму деталь.
        ///
        /// Отдельный проход, а не побочный результат <see cref="Coverage"/>:
        /// Coverage — горячий путь O(n²) на всю сцену, а частично перекрытые
        /// торцы редки, и точечный доп. проход по ним дешевле, чем таскать
        /// ссылки на детали через каждый вызов.</summary>
        public static KitchenElement? DominantCoverer(KitchenElement element,
            IReadOnlyList<KitchenElement> others, EdgeSide side, out float coveredArea)
        {
            coveredArea = 0f;
            if (element == null) return null;
            var layout = LayoutOf(element.DimensionsMM);
            if (!layout.IsValid) return null;

            var face = element.GetFaces()[layout.FaceIndex(side)];
            Vector3 u = face.rightAxis, v = face.upAxis;
            Rect target = FaceRect(face, u, v);
            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;

            KitchenElement? best = null;
            foreach (var other in others)
            {
                if (other == null || other == element) continue;
                if (!other.gameObject.activeInHierarchy) continue;
                if (IsTransparentToEdges(other)) continue;

                // Сосед может прилегать несколькими гранями — берём их объединение,
                // иначе деталь, накрывшая торец «уголком», недосчитает площадь.
                var rects = new List<Rect>();
                foreach (var of in other.GetFaces())
                {
                    if (Vector3.Dot(face.normal, of.normal) > -Tolerance.ParallelDot) continue;
                    if (Mathf.Abs(Vector3.Dot(of.center - face.center, face.normal)) > contactDist) continue;

                    Rect r = FaceRect(of, u, v);
                    float xMin = Mathf.Max(target.xMin, r.xMin), xMax = Mathf.Min(target.xMax, r.xMax);
                    float yMin = Mathf.Max(target.yMin, r.yMin), yMax = Mathf.Min(target.yMax, r.yMax);
                    if (xMax <= xMin || yMax <= yMin) continue;
                    rects.Add(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
                }
                if (rects.Count == 0) continue;

                float area = UnionArea(rects);
                if (area <= coveredArea) continue;
                coveredArea = area;
                best = other;
            }
            return best;
        }

        /// <summary>Доля площади грани, накрытая собранными прямоугольниками.</summary>
        private static float CoveredRatio(in Face face, List<Rect> covers)
        {
            float area = face.size.x * face.size.y;
            if (area <= 0f || covers.Count == 0) return 0f;
            return Mathf.Clamp01(UnionArea(covers) / area);
        }

        /// <summary>Площадь объединения прямоугольников: сжатие координат в
        /// сетку и суммирование занятых ячеек. Прямоугольников единицы, поэтому
        /// O(n³) здесь дешевле любой заметающей прямой.</summary>
        private static float UnionArea(List<Rect> rects)
        {
            var xs = new List<float>();
            var ys = new List<float>();
            foreach (var r in rects)
            {
                if (!xs.Contains(r.xMin)) xs.Add(r.xMin);
                if (!xs.Contains(r.xMax)) xs.Add(r.xMax);
                if (!ys.Contains(r.yMin)) ys.Add(r.yMin);
                if (!ys.Contains(r.yMax)) ys.Add(r.yMax);
            }
            xs.Sort();
            ys.Sort();

            float total = 0f;
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float cx = (xs[i] + xs[i + 1]) * 0.5f;
                float w = xs[i + 1] - xs[i];
                if (w <= 0f) continue;
                for (int j = 0; j + 1 < ys.Count; j++)
                {
                    float cy = (ys[j] + ys[j + 1]) * 0.5f;
                    float h = ys[j + 1] - ys[j];
                    if (h <= 0f) continue;
                    foreach (var r in rects)
                    {
                        if (cx < r.xMin || cx > r.xMax || cy < r.yMin || cy > r.yMax) continue;
                        total += w * h;
                        break;
                    }
                }
            }
            return total;
        }

        /// <summary>Габарит грани в координатах (u, v) — та же проекция, что в
        /// ConstraintValidator: у повёрнутой детали грань остаётся прямоугольной
        /// только в собственных осях, поэтому берётся её описанный прямоугольник.</summary>
        private static Rect FaceRect(in Face face, Vector3 u, Vector3 v)
        {
            float cu = Vector3.Dot(face.center, u);
            float cv = Vector3.Dot(face.center, v);
            float halfU = Mathf.Abs(Vector3.Dot(face.rightAxis, u)) * face.size.x * 0.5f
                        + Mathf.Abs(Vector3.Dot(face.upAxis, u)) * face.size.y * 0.5f;
            float halfV = Mathf.Abs(Vector3.Dot(face.rightAxis, v)) * face.size.x * 0.5f
                        + Mathf.Abs(Vector3.Dot(face.upAxis, v)) * face.size.y * 0.5f;
            return Rect.MinMaxRect(cu - halfU, cv - halfV, cu + halfU, cv + halfV);
        }
    }
}
