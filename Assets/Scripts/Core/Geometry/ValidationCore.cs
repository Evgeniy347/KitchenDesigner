using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Семантика детали для валидации — ВТОРАЯ абстракция границы рядом
    /// с <see cref="ElementGeometry"/>.
    ///
    /// Зачем отдельный тип. У снэпа связь с движком была ГЕОМЕТРИЧЕСКАЯ — «дай
    /// грани в этой позе», и снимок геометрии её закрыл целиком. У валидации
    /// связь СЕМАНТИЧЕСКАЯ: «это пол или стена», «это проём», «это мойка —
    /// пропустить», «это фасад с зазором». Снимком координат это не выражается,
    /// поэтому роль детали приезжает в ядро отдельным набором флагов, а решение
    /// «кто есть кто» остаётся в сцене (см. ValidationElementExtensions).
    ///
    /// Флаги НЕ взаимоисключающие: пол — это Anchor|FloorAnchor, окно —
    /// Anchor|Opening.</summary>
    [Flags]
    public enum ElementKind
    {
        None = 0,

        /// <summary>Якорь графа связности — пол или стена. К якорям заземляются
        /// остальные детали, сами они опоры не требуют.</summary>
        Anchor = 1 << 0,

        /// <summary>Пол (плита основания или FloorElement) — подмножество
        /// <see cref="Anchor"/>. Пол штатно проходит ПОД стенами, поэтому его
        /// пересечение с другим якорем ошибкой не считается.</summary>
        FloorAnchor = 1 << 1,

        /// <summary>Проём — окно или дверь. По построению сидит в теле своей
        /// стены, поэтому пересечение с якорем законно; зато проверяется, что
        /// проём не выходит за габарит стены по высоте.</summary>
        Opening = 1 << 2,

        /// <summary>Ящик — внутренность корпуса. Штатно пересекается с панелями
        /// своего модуля и с парным ящиком, опоры не требует.</summary>
        Drawer = 1 << 3,

        /// <summary>Декор без физики — светильник. Ни пересечений, ни несущих
        /// контактов, висит в воздухе штатно.</summary>
        Decor = 1 << 4,

        /// <summary>Врезная техника — мойка, варочная панель. По определению
        /// «пересекает» столешницу: проём в детали это и оформляет. Держится на
        /// бортике, а не на face-контакте.</summary>
        Recessed = 1 << 5,

        /// <summary>Фасад с зазором: плавает в проёме и соседей не касается —
        /// отсутствие face-контакта для него штатно.</summary>
        FloatingFacade = 1 << 6,
    }

    /// <summary>Отрезок по вертикали (мировые единицы) — габарит по высоте для
    /// проверки «проём не выходит за стену».</summary>
    public readonly struct Span
    {
        public readonly float Min;
        public readonly float Max;

        public Span(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Size => Max - Min;

        public static Span FromCenter(float center, float size) =>
            new Span(center - size * 0.5f, center + size * 0.5f);
    }

    /// <summary>Деталь глазами валидации: геометрия + роль + те немногие
    /// скаляры, по которым решается «это штатное пересечение или ошибка».</summary>
    public readonly struct ValidationElement
    {
        public readonly ElementGeometry Geometry;

        /// <summary>Восемь мировых вершин. Нужны там, где габарита мало:
        /// посадка вкладной панели в паз меряется по КАЖДОЙ вершине.</summary>
        public readonly Vector3[] Vertices;

        public readonly ElementKind Kind;

        /// <summary>Модуль. Ящик пересекается с панелями СВОЕГО модуля штатно;
        /// 0 — деталь вне модуля.</summary>
        public readonly int GroupId;

        /// <summary>Имя парного ящика двойной сборки — эти двое намеренно делят
        /// пространство.</summary>
        public readonly string? PairedName;

        /// <summary>Габарит по высоте: у стены — от ЛОГИЧЕСКОЙ позы (стена
        /// может быть визуально подрезана), у остальных — от текущей.</summary>
        public readonly Span HeightSpan;

        /// <summary>Индекс стены, в которую вставлен проём, в том же списке;
        /// −1 — стены нет или она не найдена.</summary>
        public readonly int AttachedWallIndex;

        public ValidationElement(ElementGeometry geometry, Vector3[] vertices, ElementKind kind,
            int groupId, string? pairedName, Span heightSpan, int attachedWallIndex)
        {
            Geometry = geometry;
            Vertices = vertices;
            Kind = kind;
            GroupId = groupId;
            PairedName = pairedName;
            HeightSpan = heightSpan;
            AttachedWallIndex = attachedWallIndex;
        }

        public string Name => Geometry.Name;
        public Face[] Faces => Geometry.Faces;
        public bool IsPanel => Geometry.IsPanel;
        public bool Is(ElementKind kind) => (Kind & kind) != 0;

        /// <summary>Деталь не участвует в парных проверках вовсе: светильник
        /// не имеет физики, врезная техника «пересекает» столешницу по
        /// конструкции.</summary>
        public bool IgnoredInPairs => Is(ElementKind.Decor | ElementKind.Recessed);

        /// <summary>Опора не требуется: якоря сами и есть опора, ящик живёт
        /// внутри корпуса, фасад с зазором плавает, светильник висит, врезная
        /// техника держится бортиком.</summary>
        public bool NeedsNoSupport => Is(ElementKind.Anchor | ElementKind.Drawer
            | ElementKind.Decor | ElementKind.Recessed | ElementKind.FloatingFacade);
    }

    /// <summary>Причина нарушения. Список нарушений сваливает все причины в одну
    /// кучу; диагностики разделяют их и сохраняют вторую деталь для
    /// пересечений.</summary>
    public enum ViolationKind
    {
        /// <summary>Две детали занимают одно место (объёмное пересечение).</summary>
        Overlap,
        /// <summary>Деталь не заземлена — висит в воздухе без опоры.</summary>
        Unsupported,
        /// <summary>Окно/дверь выходит за габарит своей стены.</summary>
        OutOfWallBounds,
    }

    /// <summary>Контакт двух граней. Детали — индексами в том списке, который
    /// передали в <see cref="ValidationCore.Validate"/>.</summary>
    public readonly struct CoreContact
    {
        public readonly int A;
        public readonly int B;
        public readonly int FaceA;
        public readonly int FaceB;
        public readonly float Area;
        public readonly bool IsFaceToFace;

        public CoreContact(int a, int b, int faceA, int faceB, float area, bool isFaceToFace)
        {
            A = a; B = b; FaceA = faceA; FaceB = faceB;
            Area = area; IsFaceToFace = isFaceToFace;
        }
    }

    /// <summary>Одно структурированное нарушение: кто, с кем (для пересечений)
    /// и почему.</summary>
    public readonly struct CoreViolation
    {
        public readonly int Element;
        /// <summary>Партнёр для <see cref="ViolationKind.Overlap"/>; −1 у остальных.</summary>
        public readonly int Other;
        public readonly ViolationKind Kind;

        public CoreViolation(int element, int other, ViolationKind kind)
        {
            Element = element; Other = other; Kind = kind;
        }
    }

    public sealed class CoreValidationResult
    {
        public readonly List<CoreContact> Contacts = new List<CoreContact>();
        public readonly List<int> Violations = new List<int>();
        public readonly List<List<int>> IsolatedGroups = new List<List<int>>();
        public bool IsValid;

        /// <summary>Диагностики заводятся лениво: на валидных сценах (горячий
        /// путь перетаскивания — валидация каждый кадр) список остаётся null и
        /// не даёт лишних аллокаций GC.</summary>
        public List<CoreViolation>? Diagnostics;

        public void AddDiagnostic(int element, int other, ViolationKind kind)
        {
            (Diagnostics ??= new List<CoreViolation>()).Add(new CoreViolation(element, other, kind));
        }

        public void Clear()
        {
            Contacts.Clear();
            Violations.Clear();
            IsolatedGroups.Clear();
            Diagnostics = null;
            IsValid = false;
        }
    }

    /// <summary>Правила валидации без сцены: пересечения, face-контакты,
    /// связность, высота проёмов. Работает со снимками
    /// (<see cref="ValidationElement"/>), поэтому исполняется под dotnet и
    /// мутируется наравне со снэпом.</summary>
    public static class ValidationCore
    {
        private const float FaceToFaceOverlap = Tolerance.MinSupportOverlap;

        /// <summary>Размер ячейки равномерной сетки broad-phase (метры). Деталь
        /// заносится во ВСЕ ячейки, которых касается её AABB, расширенный на
        /// contactDist. Тогда любая пара, способная пересечься или образовать
        /// face-контакт, гарантированно окажется в общей ячейке.</summary>
        private const float GridCellSize = 1.0f;

        /// <summary>Запас по нормали паза, в пределах которого панель считается
        /// «относящейся» к этому пазу (снаружи устья, мм).</summary>
        public const float PanelEngageMarginMm = 6f;

        // ── Статический скратч: контейнеры переиспользуются между вызовами,
        //    чтобы в горячем пути (перетаскивание — валидация каждый кадр) не
        //    было аллокаций. Validate НЕ реентерабелен.
        private static readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
        private static readonly Stack<List<int>> _cellPool = new Stack<List<int>>();
        private static readonly HashSet<long> _seenPairs = new HashSet<long>();
        private static readonly List<(int lo, int hi)> _candidates = new List<(int lo, int hi)>();
        private static readonly List<int> _overlapping = new List<int>();
        private static readonly HashSet<int> _overlappingSet = new HashSet<int>();
        /// <summary>Якоря, чьё пересечение НЕ является штатным (стена в стене).</summary>
        private static readonly HashSet<int> _hardOverlapAnchors = new HashSet<int>();

        // 21 бит на координату ячейки (сдвиг +Offset => диапазон ±1M ячеек). Три
        // оси упаковываются в 63 бита без знаковых коллизий.
        private const long CellOffset = 1 << 20;

        private static long CellKey(int cx, int cy, int cz) =>
            ((long)(cx + CellOffset) << 42) | ((long)(cy + CellOffset) << 21) | (long)(cz + CellOffset);

        private static int CellFloor(float coord) => Mathf.FloorToInt(coord / GridCellSize);

        private static List<int> GridCell(int cx, int cy, int cz)
        {
            long key = CellKey(cx, cy, cz);
            if (!_grid.TryGetValue(key, out var list))
            {
                list = _cellPool.Count > 0 ? _cellPool.Pop() : new List<int>();
                _grid[key] = list;
            }
            return list;
        }

        private static void ClearScratch()
        {
            foreach (var kv in _grid)
            {
                kv.Value.Clear();
                _cellPool.Push(kv.Value);
            }
            _grid.Clear();
            _seenPairs.Clear();
            _candidates.Clear();
            _overlapping.Clear();
            _overlappingSet.Clear();
            _hardOverlapAnchors.Clear();
        }

        /// <summary>Полная валидация набора снимков. Индексы в результате —
        /// позиции в <paramref name="all"/>.</summary>
        public static CoreValidationResult Validate(IReadOnlyList<ValidationElement> all)
        {
            var result = new CoreValidationResult();
            Validate(all, result);
            return result;
        }

        /// <summary>Перегрузка с переиспользуемым результатом — для горячего
        /// пути, где валидация идёт каждый кадр.</summary>
        public static void Validate(IReadOnlyList<ValidationElement> all, CoreValidationResult result)
        {
            result.Clear();
            ClearScratch();

            int n = all?.Count ?? 0;
            if (n == 0)
            {
                result.IsValid = true;
                return;
            }

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;

            // Broad-phase: равномерная сетка, пары-кандидаты из общих ячеек с
            // дедупом по упакованному ключу (lo,hi).
            for (int k = 0; k < n; k++)
            {
                var g = all![k].Geometry;
                int cx0 = CellFloor(g.Min.x - contactDist), cx1 = CellFloor(g.Max.x + contactDist);
                int cy0 = CellFloor(g.Min.y - contactDist), cy1 = CellFloor(g.Max.y + contactDist);
                int cz0 = CellFloor(g.Min.z - contactDist), cz1 = CellFloor(g.Max.z + contactDist);
                for (int cx = cx0; cx <= cx1; cx++)
                    for (int cy = cy0; cy <= cy1; cy++)
                        for (int cz = cz0; cz <= cz1; cz++)
                            GridCell(cx, cy, cz).Add(k);
            }

            foreach (var kv in _grid)
            {
                var cell = kv.Value;
                int count = cell.Count;
                if (count < 2) continue;
                for (int x = 0; x < count; x++)
                {
                    int a = cell[x];
                    for (int y = x + 1; y < count; y++)
                    {
                        int b = cell[y];
                        int lo = a < b ? a : b;
                        int hi = a < b ? b : a;
                        long key = (long)lo << 32 | (uint)hi;
                        if (!_seenPairs.Add(key)) continue;
                        _candidates.Add((lo, hi));
                    }
                }
            }

            // Сортировка по (lo,hi) даёт тот же порядок пар, что и двойной цикл
            // for(i){for(j=i+1)} — значит порядок контактов не зависит от того,
            // как сетка разложила детали по ячейкам. На него опираются тесты и
            // подсветка.
            _candidates.Sort((p, q) => p.lo != q.lo ? p.lo.CompareTo(q.lo) : p.hi.CompareTo(q.hi));

            for (int c = 0; c < _candidates.Count; c++)
                ProcessPair(all!, _candidates[c].lo, _candidates[c].hi, contactDist, result);

            CheckConnectivity(all!, result);
            CheckWallHeightConstraints(all!, result);

            // Пересекающиеся детали добавляем поверх проверки связности. Якорь
            // исключаем: его «пересечение» с деталью — это контакт, а не ошибка.
            foreach (int i in _overlapping)
            {
                if (all![i].Is(ElementKind.Anchor) && !_hardOverlapAnchors.Contains(i)) continue;
                if (!result.Violations.Contains(i))
                    result.Violations.Add(i);
            }
            result.IsValid = result.Violations.Count == 0;
        }

        private static void MarkOverlapping(int index)
        {
            if (_overlappingSet.Add(index)) _overlapping.Add(index);
        }

        private static void ProcessPair(IReadOnlyList<ValidationElement> all,
            int aIdx, int bIdx, float contactDist, CoreValidationResult result)
        {
            var a = all[aIdx];
            var b = all[bIdx];

            // Светильник — декор: не создаёт ни пересечений, ни несущих контактов.
            // Мойка/варочная по определению «пересекают» столешницу — они в неё
            // врезаны, и проём в детали как раз это и оформляет.
            if (a.IgnoredInPairs || b.IgnoredInPairs) return;

            if (AABBsIntersect(a.Geometry, b.Geometry, contactDist))
            {
                bool aDrawer = a.Is(ElementKind.Drawer);
                bool bDrawer = b.Is(ElementKind.Drawer);

                if (aDrawer && bDrawer)
                {
                    // Парные ящики двойной сборки намеренно делят пространство.
                    if (IsPairedWith(a, b) || IsPairedWith(b, a)) return;
                }
                else if (aDrawer != bDrawer)
                {
                    // Ящик живёт ВНУТРИ корпуса — пересечение с панелями своего
                    // же модуля штатно (дно/задняя стенка/боковины). Два разных
                    // ящика одного модуля сюда не попадают (ветка выше).
                    if (a.GroupId != 0 && a.GroupId == b.GroupId) return;
                }

                // Вкладная панель, сидящая в пазу, ЗАКОННО заходит внутрь
                // габарита детали — это конструкция, а не ошибка. Без этой ветки
                // правильно посаженная ДВП краснела бы по всем четырём деталям.
                if (TrySeatedGrooveContact(a, b, aIdx, bIdx, result)) return;

                // Пересечение объёмов физически недопустимо: две детали не могут
                // занимать одно место. Помечаем обе (даже если по связности они
                // валидны) — это и есть «красный» при перетаскивании.
                MarkOverlapping(aIdx);
                MarkOverlapping(bIdx);

                // Пара «якорь+якорь» раньше не регистрировалась целиком:
                // считалось, что пол и стены ставит приложение и столкнуться они
                // не могут. С блочными стенами это неверно — стена въезжает в
                // стену, и ошибка молчала. Пропускаем только штатные пары.
                if (!(a.Is(ElementKind.Anchor) && b.Is(ElementKind.Anchor) && IsLegitAnchorPair(a, b)))
                {
                    result.AddDiagnostic(aIdx, bIdx, ViolationKind.Overlap);
                    if (a.Is(ElementKind.Anchor)) _hardOverlapAnchors.Add(aIdx);
                    if (b.Is(ElementKind.Anchor)) _hardOverlapAnchors.Add(bIdx);
                }
                return;
            }

            CheckPair(aIdx, bIdx, a.Faces, b.Faces, contactDist, result);
        }

        private static bool IsPairedWith(in ValidationElement a, in ValidationElement b) =>
            !string.IsNullOrEmpty(a.PairedName) && a.PairedName == b.Name;

        /// <summary>Пересечение двух якорей штатно ТОЛЬКО в двух случаях: плита
        /// пола проходит под стенами, а окно/дверь по построению сидит в теле
        /// своей стены. Всё остальное — стена в стене, пол в полу — настоящая
        /// ошибка геометрии.</summary>
        private static bool IsLegitAnchorPair(in ValidationElement a, in ValidationElement b) =>
            a.Is(ElementKind.FloorAnchor) || b.Is(ElementKind.FloorAnchor) ||
            a.Is(ElementKind.Opening) || b.Is(ElementKind.Opening);

        private static void CheckPair(int aIdx, int bIdx, Face[] facesA, Face[] facesB,
            float contactDist, CoreValidationResult result)
        {
            for (int fa = 0; fa < 6; fa++)
            {
                for (int fb = 0; fb < 6; fb++)
                {
                    float dot = Vector3.Dot(facesA[fa].normal, facesB[fb].normal);
                    if (!Tolerance.IsParallel(dot)) continue;

                    Vector3 offset = facesB[fb].center - facesA[fa].center;
                    float planeDist = Mathf.Abs(Vector3.Dot(offset, facesA[fa].normal));
                    if (planeDist > contactDist) continue;

                    if (!FacesOverlap(facesA[fa], facesB[fb], out float overlapArea, out float overlapRatio))
                        continue;

                    bool faceToFace = overlapRatio >= FaceToFaceOverlap;
                    result.Contacts.Add(new CoreContact(aIdx, bIdx, fa, fb, overlapArea, faceToFace));
                }
            }
        }

        // ── Пазы ────────────────────────────────────────────────────────────

        /// <summary>Панель <paramref name="panel"/> штатно сидит в пазу детали
        /// <paramref name="board"/>: её номинал доходит до дна паза, но не
        /// пробивает его насквозь. Если панель загнали глубже дна — это уже
        /// настоящее пересечение, и оно останется красным.</summary>
        public static bool IsSeatedInGroove(in ValidationElement panel, in ValidationElement board,
            out Face seatFace)
        {
            seatFace = default;
            if (!panel.IsPanel) return false;

            var seats = board.Geometry.GrooveSeatFaces;
            if (seats == null || seats.Length == 0) return false;

            var verts = panel.Vertices;
            foreach (var seat in seats)
            {
                float minAlong = float.MaxValue;
                foreach (var v in verts)
                    minAlong = Mathf.Min(minAlong, Vector3.Dot(v - seat.center, seat.normal));

                // Все вершины панели — на внешней стороне дна паза (с допуском).
                if (minAlong >= -Tolerance.SnapEpsilon) { seatFace = seat; return true; }
            }
            return false;
        }

        /// <summary>Панель в пазу: пересечение габаритов законно, но связность
        /// обязана видеть их СОЕДИНЁННЫМИ — иначе панель, освобождённая от
        /// overlap, тут же станет нарушением как «висящая в воздухе». Паз
        /// конструктивно и есть соединение, поэтому регистрируем контакт.</summary>
        private static bool TrySeatedGrooveContact(in ValidationElement a, in ValidationElement b,
            int aIdx, int bIdx, CoreValidationResult result)
        {
            if (IsSeatedInGroove(a, b, out var seat))
            {
                AddSeatContact(a, b, aIdx, bIdx, seat, panelIsA: true, result);
                return true;
            }
            if (IsSeatedInGroove(b, a, out seat))
            {
                AddSeatContact(a, b, aIdx, bIdx, seat, panelIsA: false, result);
                return true;
            }
            return false;
        }

        private static void AddSeatContact(in ValidationElement a, in ValidationElement b,
            int aIdx, int bIdx, Face seat, bool panelIsA, CoreValidationResult result)
        {
            // Грань панели смотрит НА дно паза, грань детали — вдоль его нормали.
            int panelFace = FaceIndexByNormal(panelIsA ? a.Faces : b.Faces, -seat.normal);
            int boardFace = FaceIndexByNormal(panelIsA ? b.Faces : a.Faces, seat.normal);
            float area = Mathf.Abs(seat.size.x * seat.size.y);

            result.Contacts.Add(panelIsA
                ? new CoreContact(aIdx, bIdx, panelFace, boardFace, area, true)
                : new CoreContact(aIdx, bIdx, boardFace, panelFace, area, true));
        }

        private static int FaceIndexByNormal(Face[] faces, Vector3 normal)
        {
            int best = 0;
            float bestDot = float.MinValue;
            for (int i = 0; i < faces.Length; i++)
            {
                float d = Vector3.Dot(faces[i].normal, normal);
                if (d > bestDot) { bestDot = d; best = i; }
            }
            return best;
        }

        /// <summary>Панель «относится» к пазу: её ближайшая кромка стоит у устья
        /// или внутри паза (по нормали) и панель перекрывает прямоугольник паза
        /// в плоскости. out minAlong — отступ ближайшей вершины панели от ДНА
        /// паза вдоль нормали.</summary>
        public static bool PanelEngagesSeat(Vector3[] panelVertices, in Face seat,
            float depthUnits, float engageMargin, float contactDist, out float minAlong)
        {
            minAlong = float.MaxValue;
            float uMin = float.MaxValue, uMax = float.MinValue;
            float vMin = float.MaxValue, vMax = float.MinValue;

            foreach (var v in panelVertices)
            {
                Vector3 d = v - seat.center;
                float along = Vector3.Dot(d, seat.normal);
                if (along < minAlong) minAlong = along;

                float u = Vector3.Dot(d, seat.rightAxis);
                float w = Vector3.Dot(d, seat.upAxis);
                if (u < uMin) uMin = u; if (u > uMax) uMax = u;
                if (w < vMin) vMin = w; if (w > vMax) vMax = w;
            }

            // Ближайшая кромка должна стоять в диапазоне (дно … устье+запас):
            // иначе панель к этому пазу не относится (стоит где-то ещё).
            if (minAlong < -contactDist || minAlong > depthUnits + engageMargin) return false;

            // Панель должна перекрывать прямоугольник паза в плоскости пласти.
            float hu = seat.size.x * 0.5f, hv = seat.size.y * 0.5f;
            float interU = Mathf.Min(uMax, hu) - Mathf.Max(uMin, -hu);
            float interV = Mathf.Min(vMax, hv) - Mathf.Max(vMin, -hv);
            if (interU <= 0f || interV <= 0f) return false;

            float seatArea = seat.size.x * seat.size.y;
            if (seatArea <= 0f) return false;
            return (interU * interV) / seatArea >= 0.5f;
        }

        // ── Связность ───────────────────────────────────────────────────────

        private static void CheckConnectivity(IReadOnlyList<ValidationElement> all,
            CoreValidationResult result)
        {
            int n = all.Count;
            var adjacency = new List<int>[n];
            for (int i = 0; i < n; i++) adjacency[i] = new List<int>();

            var hasContact = new bool[n];
            foreach (var contact in result.Contacts)
            {
                if (!contact.IsFaceToFace) continue;
                hasContact[contact.A] = true;
                hasContact[contact.B] = true;
                adjacency[contact.A].Add(contact.B);
                adjacency[contact.B].Add(contact.A);
            }

            var visited = new bool[n];
            var queue = new Queue<int>();

            // Якоря (пол и стены) — корни BFS: всё пристыкованное к ним заземлено.
            bool hasAnchor = false;
            for (int i = 0; i < n; i++)
            {
                if (!all[i].Is(ElementKind.Anchor)) continue;
                hasAnchor = true;
                visited[i] = true;
                queue.Enqueue(i);
            }

            // Якорей нет — заземляем первую связную компоненту: без пола и стен
            // отсутствие внешней опоры нарушением не считается.
            if (!hasAnchor)
            {
                int start = 0;
                for (int i = 0; i < n; i++)
                    if (hasContact[i]) { start = i; break; }
                visited[start] = true;
                queue.Enqueue(start);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighbor in adjacency[current])
                {
                    if (visited[neighbor]) continue;
                    visited[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (all[i].NeedsNoSupport) continue;
                if (!visited[i] || !hasContact[i])
                {
                    result.Violations.Add(i);
                    result.AddDiagnostic(i, -1, ViolationKind.Unsupported);
                }
            }

            // Незаземлённые детали группируем по связности: пользователю
            // показывается «висит целый блок», а не десять отдельных деталей.
            var unvisited = new List<int>(result.Violations);
            while (unvisited.Count > 0)
            {
                var group = new List<int>();
                var gq = new Queue<int>();
                gq.Enqueue(unvisited[0]);

                while (gq.Count > 0)
                {
                    int current = gq.Dequeue();
                    if (!unvisited.Remove(current)) continue;
                    group.Add(current);

                    foreach (int neighbor in adjacency[current])
                        if (unvisited.Contains(neighbor) && !group.Contains(neighbor))
                            gq.Enqueue(neighbor);
                }

                if (group.Count > 0)
                    result.IsolatedGroups.Add(group);
            }

            result.IsValid = result.Violations.Count == 0;
        }

        private static void CheckWallHeightConstraints(IReadOnlyList<ValidationElement> all,
            CoreValidationResult result)
        {
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (!e.Is(ElementKind.Opening)) continue;

                int wallIndex = e.AttachedWallIndex;
                if (wallIndex < 0 || wallIndex >= all.Count) continue;

                var wall = all[wallIndex];
                if (wall.HeightSpan.Size <= 0f) continue;

                if (e.HeightSpan.Max > wall.HeightSpan.Max + Tolerance.EpsilonUnits ||
                    e.HeightSpan.Min < wall.HeightSpan.Min - Tolerance.EpsilonUnits)
                {
                    if (!result.Violations.Contains(i))
                        result.Violations.Add(i);
                    result.AddDiagnostic(i, -1, ViolationKind.OutOfWallBounds);
                }
            }
        }

        // ── Геометрические примитивы (общие с адаптером сцены) ───────────────

        /// <summary>Две детали стоят гранью к грани: параллельные грани в
        /// пределах contactDist и перекрытие не хуже несущего.</summary>
        public static bool AreInFaceToFaceContact(Face[] facesA, Face[] facesB, float contactDist)
        {
            for (int fa = 0; fa < 6; fa++)
            {
                for (int fb = 0; fb < 6; fb++)
                {
                    float dot = Vector3.Dot(facesA[fa].normal, facesB[fb].normal);
                    if (!Tolerance.IsParallel(dot)) continue;

                    Vector3 offset = facesB[fb].center - facesA[fa].center;
                    float planeDist = Mathf.Abs(Vector3.Dot(offset, facesA[fa].normal));
                    if (planeDist > contactDist) continue;

                    if (!FacesOverlap(facesA[fa], facesB[fb], out _, out float overlapRatio))
                        continue;

                    if (overlapRatio >= FaceToFaceOverlap) return true;
                }
            }
            return false;
        }

        /// <summary>Минимальный зазор между параллельными хорошо перекрытыми
        /// гранями в диапазоне (contactDist, maxGap]; 0 — подходящей пары
        /// нет.</summary>
        public static float MinParallelGap(Face[] fa, Face[] fb, float contactDist, float maxGap)
        {
            float best = 0f;
            for (int a = 0; a < 6; a++)
            {
                for (int b = 0; b < 6; b++)
                {
                    float dot = Vector3.Dot(fa[a].normal, fb[b].normal);
                    if (!Tolerance.IsParallel(dot)) continue;

                    Vector3 offset = fb[b].center - fa[a].center;
                    float planeDist = Mathf.Abs(Vector3.Dot(offset, fa[a].normal));
                    if (planeDist <= contactDist || planeDist > maxGap) continue;

                    if (!FacesOverlap(fa[a], fb[b], out _, out float ratio)) continue;
                    if (ratio < FaceToFaceOverlap) continue;

                    if (best == 0f || planeDist < best) best = planeDist;
                }
            }
            return best;
        }

        public static bool FacesOverlap(Face a, Face b, out float overlapArea, out float overlapRatio)
        {
            Vector3 u = a.rightAxis;
            Vector3 v = a.upAxis;

            Rect aRect = GetFaceRect(a, u, v);
            Rect bRect = GetFaceRect(b, u, v);

            float interLeft = Mathf.Max(aRect.xMin, bRect.xMin);
            float interRight = Mathf.Min(aRect.xMax, bRect.xMax);
            float interBottom = Mathf.Max(aRect.yMin, bRect.yMin);
            float interTop = Mathf.Min(aRect.yMax, bRect.yMax);

            if (interLeft >= interRight || interBottom >= interTop)
            {
                overlapArea = 0;
                overlapRatio = 0;
                return false;
            }

            float overlapU = interRight - interLeft;
            float overlapV = interTop - interBottom;
            overlapArea = overlapU * overlapV;

            // Полуосевое перекрытие (произведение отношений по каждой оси):
            // корректно обрабатывает перпендикулярные узкие грани (18×400 и
            // 18×1200), где отношение площадей (4.5%) слишком строго, но по
            // каждой оси перекрытие — 100% от меньшего размера грани.
            float ratioU = Mathf.Min(aRect.width, bRect.width) > 0
                ? overlapU / Mathf.Min(aRect.width, bRect.width) : 0;
            float ratioV = Mathf.Min(aRect.height, bRect.height) > 0
                ? overlapV / Mathf.Min(aRect.height, bRect.height) : 0;
            overlapRatio = ratioU * ratioV;
            return true;
        }

        private static Rect GetFaceRect(Face face, Vector3 u, Vector3 v)
        {
            var center = new Vector2(
                Vector3.Dot(face.center, u),
                Vector3.Dot(face.center, v));

            float halfU = Mathf.Abs(Vector3.Dot(face.rightAxis, u)) * face.size.x * 0.5f
                        + Mathf.Abs(Vector3.Dot(face.upAxis, u)) * face.size.y * 0.5f;
            float halfV = Mathf.Abs(Vector3.Dot(face.rightAxis, v)) * face.size.x * 0.5f
                        + Mathf.Abs(Vector3.Dot(face.upAxis, v)) * face.size.y * 0.5f;

            return new Rect(center.x - halfU, center.y - halfV, halfU * 2, halfV * 2);
        }

        /// <summary>Пересечение габаритов твёрдых тел. Порог перекрытия =
        /// contactDist (ContactMm, 0.5 мм) — тот же «касание vs столкновение»,
        /// что у face-контактов и MCP. Иначе деталь, стоящая вплотную с
        /// суб-0.5-мм наездом (округление снэпа), давала бы «невидимое»
        /// перекрытие и краснела, хотя вся остальная система считает её
        /// касающейся.</summary>
        public static bool AABBsIntersect(in ElementGeometry a, in ElementGeometry b, float margin) =>
            Tolerance.IntervalsOverlap(a.Min.x, a.Max.x, b.Min.x, b.Max.x, margin) &&
            Tolerance.IntervalsOverlap(a.Min.y, a.Max.y, b.Min.y, b.Max.y, margin) &&
            Tolerance.IntervalsOverlap(a.Min.z, a.Max.z, b.Min.z, b.Max.z, margin);
    }
}
