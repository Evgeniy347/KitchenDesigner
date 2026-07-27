using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Режим открывания фасада: поворот вокруг одного из 12 рёбер бокса, либо
    /// выдвижение ящиком по нормали одной из 6 граней. Порядок = порядок цикла
    /// кнопкой-переключателем. Значение 0 (HingeFrontLeft) — режим по умолчанию.
    /// </summary>
    public enum DoorMode
    {
        // 4 передних ребра (грань z=−hz) — естественные двери, распахиваются наружу.
        HingeFrontLeft, HingeFrontRight, HingeFrontTop, HingeFrontBottom,
        // 4 задних ребра (грань z=+hz).
        HingeBackLeft, HingeBackRight, HingeBackTop, HingeBackBottom,
        // 4 ребра по толщине (ось Z) в углах фасада.
        HingeEdgeTopLeft, HingeEdgeTopRight, HingeEdgeBottomLeft, HingeEdgeBottomRight,
        // 6 направлений ящика (по нормали каждой грани).
        DrawerOut, DrawerIn, DrawerRight, DrawerLeft, DrawerUp, DrawerDown
    }

    /// <summary>
    /// Кинематика петли: как именно фасад уходит от закрытой позы.
    /// </summary>
    public enum HingeKinematics
    {
        /// <summary>Мебельная четырёхшарнирная (чашечная) петля: ось лежит в чашке
        /// внутри тела фасада, поэтому петлевое ребро при открывании уходит внутрь.
        /// Раскрытие 110°.</summary>
        CupHinge,
        /// <summary>Простой поворот вокруг ребра (дверь и окно помещения): само ребро
        /// стоит на месте. Раскрытие 90°.</summary>
        EdgePivot
    }

    /// <summary>
    /// Чистая математика открывания фасада (без Unity-состояния — покрывается
    /// юнит-тестами). Ребро: поворот вокруг оси петли закрытой позы (центр едет по
    /// дуге). Ящик: сдвиг по нормали грани. Скорость — по синусу (плавный старт и
    /// плавное торможение). 18 режимов заданы таблицей.
    /// </summary>
    public static class FacadeDoor
    {
        /// <summary>Угол полностью открытой дверцы на мебельной петле (градусы).</summary>
        public const float CupMaxAngleDeg = 110f;

        /// <summary>Угол полностью открытой дверцы при повороте вокруг ребра (градусы).</summary>
        public const float EdgeMaxAngleDeg = 90f;

        /// <summary>Ход полностью выдвинутого ящика (метры).</summary>
        public const float DrawerSlideMeters = 0.4f;

        /// <summary>Предельный угол раскрытия для выбранной кинематики (градусы).</summary>
        public static float MaxAngle(HingeKinematics kind) =>
            kind == HingeKinematics.CupHinge ? CupMaxAngleDeg : EdgeMaxAngleDeg;

        // ── Геометрия чашечной петли ────────────────────────────────────
        // Чашка Ø35 утоплена в заднюю пласть фасада. Стандартная присадка — 22 мм до
        // центра чашки, то есть от кромки до края чашки ≈ 4,5 мм (допустимо 3…6, у
        // толстых фасадов больше). Виртуальная ось поворота лежит у ближней к кромке
        // стенки чашки, на её ДНЕ — то есть почти у лицевой пласти фасада.
        //
        // Глубина оси решает, насколько фасад вылезает за линию петли (туда, где стоит
        // соседний фасад). Вылет за ход открывания равен √(side² + A²) − side, где
        // A = толщина − глубина оси. Ось на дне чашки даёт A ≈ 5,5 мм у 18-мм фасада,
        // то есть вылет ~2,6 мм в середине хода и уход ВНУТРЬ линии петли в конце —
        // ровно то, ради чего четырёхшарнирную петлю и придумали. Ось на середине
        // чашки давала бы вылет ~6 мм и упор в соседний фасад.

        private const float CupDepthMM = 12.5f;   // стандартная глубина чашки
        private const float CupWallMM = 3.5f;     // материал, который должен остаться за чашкой
        private const float CupSideMinMM = 3f;    // минимальная присадка от кромки
        private const float CupSideMaxMM = 7f;    // максимальная присадка от кромки

        /// <summary>
        /// Смещение виртуальной оси чашечной петли от ребра фасада ВНУТРЬ бокса, в юнитах:
        /// x/y — вбок от кромки, z — вглубь от задней пласти (до дна чашки). Считается от толщины фасада
        /// (<paramref name="halfExtents"/>.z × 2), поэтому работает и для нестандартных толщин.
        /// </summary>
        public static Vector3 HingePivotOffset(Vector3 halfExtents)
        {
            float thicknessMM = 2f * Mathf.Abs(halfExtents.z) / AppConstants.MM_TO_UNITS;

            float sideMM = Mathf.Clamp(thicknessMM * 0.25f, CupSideMinMM, CupSideMaxMM);
            float depthMM = Mathf.Min(CupDepthMM, Mathf.Max(0f, thicknessMM - CupWallMM));

            float side = sideMM * AppConstants.MM_TO_UNITS;
            float depth = depthMM * AppConstants.MM_TO_UNITS;

            // Смещения отсчитываются от кромки и от задней пласти, то есть через весь
            // габарит: у крошечных фасадов ось не должна перескочить за противоположную грань.
            return new Vector3(
                Mathf.Min(side, 2f * Mathf.Abs(halfExtents.x)),
                Mathf.Min(side, 2f * Mathf.Abs(halfExtents.y)),
                Mathf.Min(depth, 2f * Mathf.Abs(halfExtents.z)));
        }

        private readonly struct Variant
        {
            public readonly bool isDrawer;
            public readonly bool cupHinge;      // режим-грань, где уместна мебельная петля
            public readonly Vector3 pivotSigns; // ребро: pivot = Scale(pivotSigns, half)
            public readonly Vector3 dir;        // ребро: знаковая ось; ящик: направление сдвига (локально)
            public readonly string symbol;      // один знак для компактной подписи
            public readonly string name;        // читаемое название (для выпадающего списка)
            public Variant(bool isDrawer, bool cupHinge, Vector3 pivotSigns, Vector3 dir, string symbol, string name)
            {
                this.isDrawer = isDrawer; this.cupHinge = cupHinge;
                this.pivotSigns = pivotSigns; this.dir = dir;
                this.symbol = symbol; this.name = name;
            }
        }

        private static readonly Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;

        // Оси у рёбер знаковые так, чтобы поворот +90° распахивал их «наружу»
        // (передние — к −Z, задние — к +Z). Порядок строго совпадает с DoorMode.
        // Символы — ASCII, потому что LiberationSans SDF в WebGL не содержит Unicode-стрелок.
        private static readonly Variant[] V =
        {
            // Передняя грань (z=−hz). Распахиваются наружу — к +Z. Мебельная петля.
            new Variant(false, true, new Vector3(-1f, 0f, -1f),  Y, "<", "Дверь: слева"),
            new Variant(false, true, new Vector3( 1f, 0f, -1f), -Y, ">", "Дверь: справа"),
            new Variant(false, true, new Vector3( 0f, 1f, -1f),  X, "^", "Дверь: сверху"),
            new Variant(false, true, new Vector3( 0f,-1f, -1f), -X, "v", "Дверь: снизу"),
            // Задняя грань (z=+hz) — оси зеркальны передним. Тоже мебельная петля.
            new Variant(false, true, new Vector3(-1f, 0f,  1f), -Y, "[", "Сзади: слева"),
            new Variant(false, true, new Vector3( 1f, 0f,  1f),  Y, "]", "Сзади: справа"),
            new Variant(false, true, new Vector3( 0f, 1f,  1f), -X, "{", "Сзади: сверху"),
            new Variant(false, true, new Vector3( 0f,-1f,  1f),  X, "}", "Сзади: снизу"),
            // Рёбра по толщине (ось Z) в 4 углах — не мебельная петля, поворот по ребру.
            new Variant(false, false, new Vector3(-1f, 1f,  0f),  Z, "(", "Угол: верх-лево"),
            new Variant(false, false, new Vector3( 1f, 1f,  0f),  Z, ")", "Угол: верх-право"),
            new Variant(false, false, new Vector3(-1f,-1f,  0f),  Z, "\\", "Угол: низ-лево"),
            new Variant(false, false, new Vector3( 1f,-1f,  0f),  Z, "/", "Угол: низ-право"),
            // Ящик — сдвиг по нормали грани. "Вперёд" = наружу = +Z.
            new Variant(true, false, Vector3.zero,  Z, "O", "Ящик: вперёд"),
            new Variant(true, false, Vector3.zero, -Z, "X", "Ящик: назад"),
            new Variant(true, false, Vector3.zero, -X, "R", "Ящик: вправо"),
            new Variant(true, false, Vector3.zero,  X, "L", "Ящик: влево"),
            new Variant(true, false, Vector3.zero, -Y, "U", "Ящик: вверх"),
            new Variant(true, false, Vector3.zero,  Y, "D", "Ящик: вниз"),
        };

        /// <summary>Число режимов (12 рёбер + 6 ящиков = 18).</summary>
        public static int Count => V.Length;

        /// <summary>Следующий режим по кругу (для кнопки-переключателя).</summary>
        public static DoorMode Next(DoorMode mode) => (DoorMode)(((int)mode + 1) % V.Length);

        /// <summary>Один символ, зависящий от режима (компактная подпись).</summary>
        public static string Symbol(DoorMode mode) => V[(int)mode].symbol;

        // Проводные имена для MCP — тот же словарь, что у параметра mode
        // инструмента set_facade_mode. Порядок строго совпадает с DoorMode.
        private static readonly string[] WireNames =
        {
            "front_left", "front_right", "front_top", "front_bottom",
            "back_left", "back_right", "back_top", "back_bottom",
            "edge_top_left", "edge_top_right", "edge_bottom_left", "edge_bottom_right",
            "drawer_out", "drawer_in", "drawer_right", "drawer_left", "drawer_up", "drawer_down"
        };

        /// <summary>Проводное имя режима для MCP-ответов: агент видит те же строки,
        /// которые сам передаёт в set_facade_mode (а не ASCII-символы UI).</summary>
        public static string WireName(DoorMode mode) => WireNames[(int)mode];

        /// <summary>Читаемая подпись для выпадающего списка: символ + название.</summary>
        public static string Label(DoorMode mode) => V[(int)mode].symbol + " " + V[(int)mode].name;

        /// <summary>Плавность «синус» (ease-in-out): 0→0, 1→1, плавный разгон и торможение.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 0.5f * (1f - Mathf.Cos(Mathf.PI * t));
        }

        /// <summary>
        /// Поза фасада для прогресса <paramref name="progress"/> ∈ [0..1]:
        /// 0 — закрыто (равно закрытой позе), 1 — открыто (поворот 90° либо выдвижение).
        /// Плавность (синус) применяется внутри.
        /// </summary>
        /// <param name="halfExtents">Половины ФИЗИЧЕСКИХ размеров фасада (localScale/2).</param>
        /// <param name="kind">Кинематика петли: мебельная чашечная (по умолчанию) или поворот по ребру.</param>
        public static void Pose(
            Vector3 closedPos, Quaternion closedRot, Vector3 halfExtents,
            DoorMode mode, float progress,
            out Vector3 pos, out Quaternion rot,
            HingeKinematics kind = HingeKinematics.CupHinge)
        {
            var v = V[(int)mode];
            float e = Ease(progress);

            if (v.isDrawer)
            {
                rot = closedRot;
                pos = closedPos + closedRot * (v.dir * (DrawerSlideMeters * e));
                return;
            }

            var pivotLocal = PivotLocal(v, halfExtents, kind);
            // Поворот вокруг знаковой оси — двери распахиваются наружу (+Z для передней грани).
            float angle = -HingeAngle(v, kind) * e;
            var pivotWorld = closedPos + closedRot * pivotLocal;
            var axisWorld = closedRot * v.dir;
            var delta = Quaternion.AngleAxis(angle, axisWorld);

            rot = delta * closedRot;
            pos = pivotWorld + delta * (closedPos - pivotWorld);
        }

        /// <summary>Точка и ось петли режима-ребра (для тестов). Для ящика — false.
        /// У мебельной петли точка — виртуальная ось внутри тела фасада, а не его ребро.</summary>
        public static bool Hinge(DoorMode mode, Vector3 halfExtents,
            out Vector3 pivotLocal, out Vector3 axisLocal,
            HingeKinematics kind = HingeKinematics.CupHinge)
        {
            var v = V[(int)mode];
            axisLocal = v.dir;
            if (v.isDrawer) { pivotLocal = Vector3.zero; return false; }
            pivotLocal = PivotLocal(v, halfExtents, kind);
            return true;
        }

        // Ось поворота: ребро бокса, у мебельной петли сдвинутое внутрь на смещение чашки.
        // Знаки pivotSigns (0/±1) сами задают направление «внутрь» для любой из 8 граней.
        private static Vector3 PivotLocal(Variant v, Vector3 halfExtents, HingeKinematics kind)
        {
            var pivot = Vector3.Scale(v.pivotSigns, halfExtents);
            if (!UsesCup(v, kind)) return pivot;
            return pivot - Vector3.Scale(v.pivotSigns, HingePivotOffset(halfExtents));
        }

        private static float HingeAngle(Variant v, HingeKinematics kind) =>
            UsesCup(v, kind) ? CupMaxAngleDeg : EdgeMaxAngleDeg;

        private static bool UsesCup(Variant v, HingeKinematics kind) =>
            v.cupHinge && kind == HingeKinematics.CupHinge;
    }
}
