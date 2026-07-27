using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Причина нарушения — для окна анализа ошибок. Список
    /// <see cref="ValidationResult.violations"/> сваливает все причины в одну
    /// кучу; диагностики разделяют их и сохраняют вторую деталь для пересечений.</summary>
    public enum ViolationKind
    {
        /// <summary>Две детали занимают одно место (объёмное пересечение).</summary>
        Overlap,
        /// <summary>Деталь не заземлена — висит в воздухе без опоры.</summary>
        Unsupported,
        /// <summary>Окно/дверь выходит за габарит своей стены.</summary>
        OutOfWallBounds,
    }

    /// <summary>Одно структурированное нарушение с указанием причины и (для
    /// пересечений) второй детали.</summary>
    public readonly struct ContactViolation
    {
        public readonly KitchenElement element;
        public readonly KitchenElement? other; // партнёр для Overlap; null для остальных
        public readonly ViolationKind kind;

        public ContactViolation(KitchenElement element, KitchenElement? other, ViolationKind kind)
        {
            this.element = element;
            this.other = other;
            this.kind = kind;
        }
    }

    public class ValidationResult
    {
        public List<FaceContact> contacts = new List<FaceContact>();
        public List<KitchenElement> violations = new List<KitchenElement>();
        public List<List<KitchenElement>> isolatedGroups = new List<List<KitchenElement>>();
        public bool isValid;

        /// <summary>Структурированные нарушения с причинами. Инициализируется
        /// лениво: на валидных сценах (горячий путь перетаскивания — Validate
        /// каждый кадр) остаётся null и не даёт лишних аллокаций GC.</summary>
        public List<ContactViolation>? diagnostics;

        public void AddDiagnostic(KitchenElement element, KitchenElement? other, ViolationKind kind)
        {
            (diagnostics ??= new List<ContactViolation>()).Add(new ContactViolation(element, other, kind));
        }
    }

    public static class ConstraintValidator
    {
        private const float FaceToFaceOverlap = Tolerance.MinSupportOverlap;

        // Размер ячейки равномерной сетки broad-phase (метры). Деталь заносится во ВСЕ
        // ячейки, которых касается её AABB, расширенный на contactDist. Тогда любая пара,
        // способная пересечься или образовать face-контакт (грани в пределах contactDist
        // и перекрывающиеся в плоскости), гарантированно оказывается в общей ячейке:
        // |pa-pb| <= contactDist => pa попадает в расширенный AABB соседа.
        private const float GridCellSize = 1.0f;

        // Якорь графа связности — пол или стена (к ним заземляются детали).
        private static bool IsAnchor(KitchenElement e) =>
            e != null && (e.GetComponent<BasePlate>() != null || e.GetComponent<Wall>() != null || e is WindowElement || e is DoorElement || e is FloorElement);

        /// <summary>Пересечение двух якорей штатно ТОЛЬКО в двух случаях: плита пола
        /// проходит под стенами, а окно/дверь по построению сидит в теле своей стены.
        /// Всё остальное — стена в стене, пол в полу — настоящая ошибка геометрии.</summary>
        private static bool IsLegitAnchorPair(KitchenElement a, KitchenElement b) =>
            IsFloorAnchor(a) || IsFloorAnchor(b) ||
            a is WindowElement || a is DoorElement || b is WindowElement || b is DoorElement;

        private static bool IsFloorAnchor(KitchenElement e) =>
            e != null && (e.GetComponent<BasePlate>() != null || e is FloorElement);

        // ── Статический скратч: контейнеры переиспользуются между вызовами Validate,
        //    чтобы в горячем пути (перетаскивание — Validate каждый кадр) не было
        //    аллокаций. Validate НЕ реентерабелен (вложенных вызовов нет), поэтому
        //    статическое состояние безопасно.
        private static readonly List<KitchenElement> _elems = new List<KitchenElement>();
        private static readonly List<AABB> _aabbs = new List<AABB>();
        private static readonly List<KitchenElement.Face[]> _faces = new List<KitchenElement.Face[]>();
        private static readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
        private static readonly Stack<List<int>> _cellPool = new Stack<List<int>>();
        private static readonly HashSet<long> _seenPairs = new HashSet<long>();
        private static readonly List<(int lo, int hi)> _candidates = new List<(int lo, int hi)>();
        private static readonly HashSet<KitchenElement> _overlapping = new HashSet<KitchenElement>();
        /// <summary>Якоря, чьё пересечение НЕ является штатным (стена в стене).</summary>
        private static readonly HashSet<KitchenElement> _hardOverlapAnchors = new HashSet<KitchenElement>();

        // 21 бит на координату ячейки (сдвиг +Offset => диапазон ±1M ячеек). Три оси
        // упаковываются в 63 бита без знаковых коллизий — ключ ячейки без коллизий.
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
            _elems.Clear();
            _aabbs.Clear();
            _faces.Clear();
            foreach (var kv in _grid)
            {
                kv.Value.Clear();
                _cellPool.Push(kv.Value);
            }
            _grid.Clear();
            _seenPairs.Clear();
            _candidates.Clear();
            _overlapping.Clear();
            _hardOverlapAnchors.Clear();
        }

        public static ValidationResult Validate(List<KitchenElement> all)
        {
            var result = new ValidationResult();
            ClearScratch();

            if (all == null || all.Count == 0)
            {
                result.isValid = true;
                return result;
            }

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;

            // 1) Кэш геометрии: для каждой валидной детали ОДИН раз считаем вершины,
            //    AABB и грани. Раньше GetVertices()/GetFaces() вызывались на каждую пару
            //    (O(n²) аллокаций массивов), теперь — O(n). Этим убирается основной
            //    источник GC-провалов при перетаскивании.
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null) continue;
                _elems.Add(e);
                _aabbs.Add(ComputeAABB(e.GetVertices()));
                _faces.Add(e.GetFaces());
            }

            int m = _elems.Count;
            if (m == 0)
            {
                result.isValid = true;
                return result;
            }

            // 2) Broad-phase: равномерная сетка. Каждая деталь заносится во все ячейки,
            //    которых касается её AABB + contactDist. Пары-кандидаты собираем из
            //    общих ячеек с дедупом по упакованному ключу (lo,hi).
            for (int k = 0; k < m; k++)
            {
                var b = _aabbs[k];
                int cx0 = CellFloor(b.minX - contactDist), cx1 = CellFloor(b.maxX + contactDist);
                int cy0 = CellFloor(b.minY - contactDist), cy1 = CellFloor(b.maxY + contactDist);
                int cz0 = CellFloor(b.minZ - contactDist), cz1 = CellFloor(b.maxZ + contactDist);
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

            // Сортируем кандидатов по (lo,hi) — порядок пар совпадает с исходным
            // двойным циклом for(i){for(j=i+1)}, значит порядок контактов в
            // result.contacts не меняется (на него опираются тесты и подсветка).
            _candidates.Sort((p, q) => p.lo != q.lo ? p.lo.CompareTo(q.lo) : p.hi.CompareTo(q.hi));

            // 3) Точная per-pair проверка по той же логике, что и раньше: пересечение
            //    AABB (с допуском) => особые случаи ящиков => пометка overlap; иначе
            //    CheckPair по кэшированным граням.
            for (int c = 0; c < _candidates.Count; c++)
            {
                var pair = _candidates[c];
                ProcessPair(pair.lo, pair.hi, contactDist, result);
            }

            CheckConnectivity(all, result);
            CheckWallHeightConstraints(all, result);

            // Пересекающиеся детали добавляем к нарушениям поверх проверки связности
            // (BasePlate исключаем — он якорь, его «пересечения» с деталями — это контакт).
            foreach (var e in _overlapping)
            {
                if (e == null) continue;
                if (IsAnchor(e) && !_hardOverlapAnchors.Contains(e)) continue;
                if (!result.violations.Contains(e))
                    result.violations.Add(e);
            }
            result.isValid = result.violations.Count == 0;

            return result;
        }

        /// <summary>Панель <paramref name="panel"/> штатно сидит в пазу детали
        /// <paramref name="board"/>: её номинал доходит до дна паза, но не пробивает
        /// его насквозь. Проверяется по посадочным граням (дну пазов) — если панель
        /// загнали глубже дна, это уже настоящее пересечение и оно останется красным.</summary>
        private static bool IsSeatedInGroove(KitchenElement panel, KitchenElement board,
            out KitchenElement.Face seatFace)
        {
            seatFace = default;
            if (!(panel is PanelElement) || board == null) return false;

            var seats = board.GetGrooveSeatFaces();
            if (seats.Length == 0) return false;

            var verts = panel.GetVertices();
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
        /// должна видеть их СОЕДИНЁННЫМИ — иначе панель, освобождённая от overlap,
        /// тут же становится нарушением как «висящая в воздухе». Паз конструктивно
        /// и есть соединение, поэтому регистрируем полноценный контакт.</summary>
        private static bool TrySeatedGrooveContact(KitchenElement a, KitchenElement b,
            int aIdx, int bIdx, ValidationResult result)
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

        private static void AddSeatContact(KitchenElement a, KitchenElement b,
            int aIdx, int bIdx, KitchenElement.Face seat, bool panelIsA, ValidationResult result)
        {
            // Грань панели смотрит НА дно паза, грань детали — вдоль его нормали.
            int panelFace = FaceIndexByNormal(panelIsA ? _faces[aIdx] : _faces[bIdx], -seat.normal);
            int boardFace = FaceIndexByNormal(panelIsA ? _faces[bIdx] : _faces[aIdx], seat.normal);
            float area = Mathf.Abs(seat.size.x * seat.size.y);

            result.contacts.Add(panelIsA
                ? new FaceContact(a, b, panelFace, boardFace, area, true)
                : new FaceContact(a, b, boardFace, panelFace, area, true));
        }

        private static int FaceIndexByNormal(KitchenElement.Face[] faces, Vector3 normal)
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

        private static void ProcessPair(int aIdx, int bIdx, float contactDist, ValidationResult result)
        {
            var a = _elems[aIdx];
            var b = _elems[bIdx];
            var aabbA = _aabbs[aIdx];
            var aabbB = _aabbs[bIdx];

            // Лампа — декор: не создаёт ни пересечений, ни несущих контактов.
            if (a is LightSourceElement || b is LightSourceElement) return;

            if (AABBsIntersect(aabbA, aabbB, contactDist))
            {
                bool aDrawer = a is DrawerElement;
                bool bDrawer = b is DrawerElement;

                if (aDrawer && bDrawer)
                {
                    // Парная двойная ящика намеренно делят пространство.
                    var da = (DrawerElement)a; var db = (DrawerElement)b;
                    if (!string.IsNullOrEmpty(da.PairedDrawerName) && da.PairedDrawerName == db.PartName) return;
                    if (!string.IsNullOrEmpty(db.PairedDrawerName) && db.PairedDrawerName == da.PartName) return;
                }
                else if (aDrawer != bDrawer)
                {
                    // Ящик живёт ВНУТРИ корпуса — пересечение с панелями своего же
                    // модуля (GroupId) штатно (дно/задняя стенка/боковины). Не нарушение.
                    // Два разных ящика в одном модуле сюда не попадают (см. ветку выше).
                    if (a.GroupId != 0 && a.GroupId == b.GroupId) return;
                }

                // Вкладная панель, сидящая в пазу, ЗАКОННО заходит внутрь габарита
                // детали — это конструкция, а не ошибка. Без этой ветки правильно
                // посаженная ДВП подсвечивалась бы красным по всем четырём деталям.
                if (TrySeatedGrooveContact(a, b, aIdx, bIdx, result)) return;

                // Пересечение объёмов физически недопустимо: две детали не могут
                // занимать одно место. Помечаем обе как нарушение (даже если по
                // связности они валидны) — это и есть «красный» при перетаскивании.
                _overlapping.Add(a);
                _overlapping.Add(b);
                // Пара «якорь+якорь» раньше не регистрировалась целиком: считалось,
                // что пол и стены ставит приложение и столкнуться они не могут. С
                // блочными стенами это неверно — стена въезжает в стену, и ошибка
                // молчала. Пропускаем только штатные пары (см. IsLegitAnchorPair).
                if (!(IsAnchor(a) && IsAnchor(b) && IsLegitAnchorPair(a, b)))
                {
                    result.AddDiagnostic(a, b, ViolationKind.Overlap);
                    if (IsAnchor(a)) _hardOverlapAnchors.Add(a);
                    if (IsAnchor(b)) _hardOverlapAnchors.Add(b);
                }
                return;
            }

            CheckPair(a, b, _faces[aIdx], _faces[bIdx], contactDist, result);
        }

        private static void CheckPair(KitchenElement a, KitchenElement b,
            KitchenElement.Face[] facesA, KitchenElement.Face[] facesB,
            float contactDist, ValidationResult result)
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
                    result.contacts.Add(new FaceContact(a, b, fa, fb, overlapArea, faceToFace));
                }
            }
        }

        /// <summary>Есть ли среди нарушений результата деталь рядом с element:
        /// сам element или нарушение, чей AABB в пределах radiusUnits от AABB
        /// element. Близость меряется ПО ГАБАРИТАМ, а не по центрам: у крупных
        /// деталей центры соседей дальше любого разумного радиуса, и проверка
        /// по центрам молча пропускала нарушения, стоящие вплотную.</summary>
        public static bool HasViolationNear(ValidationResult result, KitchenElement element, float radiusUnits)
        {
            if (result == null || element == null || result.violations.Count == 0) return false;

            var ea = ComputeAABB(element.GetVertices());
            foreach (var v in result.violations)
            {
                if (v == element) return true;
                if (v == null) continue;
                var va = ComputeAABB(v.GetVertices());
                if (va.minX <= ea.maxX + radiusUnits && va.maxX >= ea.minX - radiusUnits &&
                    va.minY <= ea.maxY + radiusUnits && va.maxY >= ea.minY - radiusUnits &&
                    va.minZ <= ea.maxZ + radiusUnits && va.maxZ >= ea.minZ - radiusUnits)
                    return true;
            }
            return false;
        }

        public static bool AreInFaceToFaceContact(KitchenElement a, KitchenElement b)
        {
            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            var facesA = a.GetFaces();
            var facesB = b.GetFaces();
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

                    if (overlapRatio >= FaceToFaceOverlap)
                        return true;
                }
            }
            return false;
        }

        /// <summary>Пара деталей, которые ПОЧТИ касаются: их грани параллельны и
        /// хорошо перекрыты в плоскости, но между ними зазор чуть больше допуска
        /// касания.</summary>
        public readonly struct NearContact
        {
            public readonly KitchenElement a;
            public readonly KitchenElement b;
            public readonly float gapMm;
            public NearContact(KitchenElement a, KitchenElement b, float gapMm)
            {
                this.a = a; this.b = b; this.gapMm = gapMm;
            }
        }

        /// <summary>Найти пары деталей с зазором в (ContactMm, maxGapMm]: грани
        /// параллельны, перекрыты в плоскости не хуже face-to-face, а между ними
        /// щель, которую пользователю визуально трудно заметить (недожатый снэп).
        /// НЕ горячий путь: O(n²), вызывается окном анализа/MCP по требованию.
        /// Пары, уже стоящие face-to-face (реально соприкасаются), пропускаются.</summary>
        public static List<NearContact> FindNearContacts(List<KitchenElement> all, float maxGapMm)
        {
            var result = new List<NearContact>();
            if (all == null || all.Count < 2) return result;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float maxGap = maxGapMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;

            int n = all.Count;
            var faces = new KitchenElement.Face[n][];
            var boxes = new AABB[n];
            var ok = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var e = all[i];
                if (e == null || IsAnchor(e) || e is LightSourceElement) continue;
                ok[i] = true;
                faces[i] = e.GetFaces();
                boxes[i] = ComputeAABB(e.GetVertices());
            }

            for (int i = 0; i < n; i++)
            {
                if (!ok[i]) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (!ok[j]) continue;
                    // Broad-phase: коробки в пределах maxGap друг от друга. margin в
                    // AABBsIntersect СУЖАЕТ перекрытие, поэтому расширяем отрицательным
                    // (−maxGap) — так в кандидаты попадают и не пересекающиеся, но
                    // близкие пары.
                    if (!AABBsIntersect(boxes[i], boxes[j], -maxGap)) continue;
                    // Реально касаются гранями — это не «почти», а контакт.
                    if (AreInFaceToFaceContact(all[i], all[j])) continue;
                    // Вкладная ДВП, зашедшая в паз соседней детали: зазор между
                    // ГАБАРИТАМИ равен глубине захода в паз и НЕ является «почти
                    // касанием» — эту пару обслуживает логика посадки в паз (SEAT-01),
                    // а не GAP-01. Иначе полностью посаженная панель ложно краснела бы
                    // как «почти касается, зазор = глубине паза».
                    if (PanelEngagesGroove(all[i], all[j]) || PanelEngagesGroove(all[j], all[i])) continue;

                    float gap = MinParallelGap(faces[i], faces[j], contactDist, maxGap);
                    if (gap > 0f)
                        result.Add(new NearContact(all[i], all[j], gap * toMm));
                }
            }
            return result;
        }

        /// <summary>Минимальный зазор между параллельными хорошо перекрытыми
        /// гранями в диапазоне (contactDist, maxGap]; 0 — подходящей пары нет.</summary>
        private static float MinParallelGap(KitchenElement.Face[] fa, KitchenElement.Face[] fb,
            float contactDist, float maxGap)
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

        /// <summary>Вкладная панель (ДВП), которая зашла в паз детали не до дна —
        /// «приклеилась снаружи паза». insertionMm — реальная глубина захода,
        /// depthMm — глубина паза.</summary>
        public readonly struct UnseatedPanel
        {
            public readonly KitchenElement panel;
            public readonly KitchenElement board;
            public readonly float insertionMm;
            public readonly float depthMm;
            public UnseatedPanel(KitchenElement panel, KitchenElement board, float insertionMm, float depthMm)
            {
                this.panel = panel; this.board = board;
                this.insertionMm = insertionMm; this.depthMm = depthMm;
            }
        }

        /// <summary>Запас по нормали паза, в пределах которого панель считается
        /// «относящейся» к этому пазу (снаружи устья, мм).</summary>
        private const float PanelEngageMarginMm = 6f;

        /// <summary>Найти вкладные панели, которые НЕ дошли до дна паза (зашли менее
        /// чем на половину его глубины). Пользователь такую щель между кромкой и дном
        /// не видит, а конструктивно панель держится плохо. НЕ горячий путь.</summary>
        public static List<UnseatedPanel> FindUnseatedPanels(List<KitchenElement> all)
        {
            var result = new List<UnseatedPanel>();
            if (all == null || all.Count < 2) return result;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float engageMargin = PanelEngageMarginMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;

            foreach (var p in all)
            {
                if (!(p is PanelElement)) continue;
                var pverts = p.GetVertices();

                foreach (var b in all)
                {
                    if (b == null || b == p || b.Grooves.Count == 0) continue;
                    var seats = b.GetGrooveSeatFaces();
                    if (seats.Length == 0) continue;

                    float depthUnits = GrooveMesh.DepthFraction(b.DimensionsMM) * b.transform.localScale.z;
                    if (depthUnits <= 0f) continue;

                    foreach (var seat in seats)
                    {
                        if (!PanelEngagesSeat(pverts, seat, depthUnits, engageMargin, contactDist, out float minAlong))
                            continue;

                        // Глубина захода = глубина паза − отступ ближайшей кромки от дна.
                        float insertion = depthUnits - minAlong;
                        if (insertion < depthUnits * 0.5f)
                            result.Add(new UnseatedPanel(p, b, Mathf.Max(0f, insertion) * toMm, depthUnits * toMm));
                    }
                }
            }
            return result;
        }

        /// <summary>Панель «относится» к пазу: её ближайшая кромка стоит у устья/внутри
        /// паза (по нормали) и панель перекрывает прямоугольник паза в плоскости.
        /// out minAlong — отступ ближайшей вершины панели от ДНА паза вдоль нормали.</summary>
        private static bool PanelEngagesSeat(Vector3[] pverts, in KitchenElement.Face seat,
            float depthUnits, float engageMargin, float contactDist, out float minAlong)
        {
            minAlong = float.MaxValue;
            float uMin = float.MaxValue, uMax = float.MinValue;
            float vMin = float.MaxValue, vMax = float.MinValue;

            foreach (var v in pverts)
            {
                Vector3 d = v - seat.center;
                float along = Vector3.Dot(d, seat.normal);
                if (along < minAlong) minAlong = along;

                float u = Vector3.Dot(d, seat.rightAxis);
                float w = Vector3.Dot(d, seat.upAxis);
                if (u < uMin) uMin = u; if (u > uMax) uMax = u;
                if (w < vMin) vMin = w; if (w > vMax) vMax = w;
            }

            // Ближайшая кромка должна стоять в диапазоне (дно … устье+запас): иначе
            // панель к этому пазу не относится (стоит где-то ещё).
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

        /// <summary>Панель-ДВП <paramref name="panel"/> зашла (хотя бы устьем) в один
        /// из пазов детали <paramref name="board"/>. Это тот же критерий «относится к
        /// пазу», что и у SEAT-01 (<see cref="PanelEngagesSeat"/>): такую пару нельзя
        /// выдавать как near-contact/GAP-01 — зазор между их габаритами равен глубине
        /// захода в паз, а недосадку до дна отдельно ловит SEAT-01.</summary>
        private static bool PanelEngagesGroove(KitchenElement panel, KitchenElement board)
        {
            if (!(panel is PanelElement) || board == null || board.Grooves.Count == 0) return false;

            var seats = board.GetGrooveSeatFaces();
            if (seats.Length == 0) return false;

            float depthUnits = GrooveMesh.DepthFraction(board.DimensionsMM) * board.transform.localScale.z;
            if (depthUnits <= 0f) return false;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float engageMargin = PanelEngageMarginMm * AppConstants.MM_TO_UNITS;

            var pverts = panel.GetVertices();
            foreach (var seat in seats)
                if (PanelEngagesSeat(pverts, seat, depthUnits, engageMargin, contactDist, out _))
                    return true;
            return false;
        }

        private static bool FacesOverlap(
            KitchenElement.Face a, KitchenElement.Face b,
            out float overlapArea, out float overlapRatio)
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
            // корректно обрабатывает перпендикулярные узкие грани (18×400 и 18×1200),
            // где отношение площадей (18×18 / min(7200, 21600) = 4.5%) слишком строго,
            // но по каждой оси перекрытие составляет 100% от меньшего размера грани.
            float ratioU = Mathf.Min(aRect.width, bRect.width) > 0
                ? overlapU / Mathf.Min(aRect.width, bRect.width) : 0;
            float ratioV = Mathf.Min(aRect.height, bRect.height) > 0
                ? overlapV / Mathf.Min(aRect.height, bRect.height) : 0;
            overlapRatio = ratioU * ratioV;
            return true;
        }

        private static Rect GetFaceRect(KitchenElement.Face face, Vector3 u, Vector3 v)
        {
            Vector2 center = new Vector2(
                Vector3.Dot(face.center, u),
                Vector3.Dot(face.center, v)
            );

            float halfU = Mathf.Abs(Vector3.Dot(face.rightAxis, u)) * face.size.x * 0.5f
                       + Mathf.Abs(Vector3.Dot(face.upAxis, u)) * face.size.y * 0.5f;
            float halfV = Mathf.Abs(Vector3.Dot(face.rightAxis, v)) * face.size.x * 0.5f
                       + Mathf.Abs(Vector3.Dot(face.upAxis, v)) * face.size.y * 0.5f;

            return new Rect(center.x - halfU, center.y - halfV, halfU * 2, halfV * 2);
        }

        // AABB из мировых вершин — идентичен тому, что считала SnapSystem.ElementsIntersect
        // из GetVertices(). Кэшируем один раз на деталь вместо пересчёта на каждую пару.
        private static AABB ComputeAABB(Vector3[] v)
        {
            float minX = v[0].x, maxX = v[0].x;
            float minY = v[0].y, maxY = v[0].y;
            float minZ = v[0].z, maxZ = v[0].z;
            for (int i = 1; i < 8; i++)
            {
                if (v[i].x < minX) minX = v[i].x; else if (v[i].x > maxX) maxX = v[i].x;
                if (v[i].y < minY) minY = v[i].y; else if (v[i].y > maxY) maxY = v[i].y;
                if (v[i].z < minZ) minZ = v[i].z; else if (v[i].z > maxZ) maxZ = v[i].z;
            }
            return new AABB(minX, minY, minZ, maxX, maxY, maxZ);
        }

        // Пересечение AABB твёрдых тел. Порог перекрытия = contactDist (ContactMm,
        // 0.5 мм) — тот же «касание vs столкновение», что у face-контактов и MCP
        // (IsNoiseMm). Иначе деталь, стоящая вплотную с суб-0.5-мм наездом (округление
        // снэпа/резайза), давала «невидимое» AABB-перекрытие и подсвечивалась красной,
        // хотя вся остальная система считает её касающейся.
        private static bool AABBsIntersect(in AABB a, in AABB b, float margin) =>
            Tolerance.IntervalsOverlap(a.minX, a.maxX, b.minX, b.maxX, margin) &&
            Tolerance.IntervalsOverlap(a.minY, a.maxY, b.minY, b.maxY, margin) &&
            Tolerance.IntervalsOverlap(a.minZ, a.maxZ, b.minZ, b.maxZ, margin);

        private readonly struct AABB
        {
            public readonly float minX, minY, minZ, maxX, maxY, maxZ;
            public AABB(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
            {
                this.minX = minX; this.minY = minY; this.minZ = minZ;
                this.maxX = maxX; this.maxY = maxY; this.maxZ = maxZ;
            }
        }

        private static void CheckConnectivity(List<KitchenElement> all, ValidationResult result)
        {
            var adjacency = new Dictionary<KitchenElement, List<KitchenElement>>();
            var hasContact = new HashSet<KitchenElement>();

            foreach (var e in all)
                adjacency[e] = new List<KitchenElement>();

            foreach (var contact in result.contacts)
            {
                if (!contact.isFaceToFace) continue;
                hasContact.Add(contact.elementA);
                hasContact.Add(contact.elementB);
                adjacency[contact.elementA].Add(contact.elementB);
                adjacency[contact.elementB].Add(contact.elementA);
            }

            var visited = new HashSet<KitchenElement>();
            var queue = new Queue<KitchenElement>();

            // Якоря (пол и стены) — корни BFS: всё пристыкованное к ним заземлено.
            bool hasAnchor = false;
            foreach (var e in all)
            {
                if (IsAnchor(e))
                {
                    hasAnchor = true;
                    visited.Add(e);
                    queue.Enqueue(e);
                }
            }

            // Если якорей нет, заземляем первую связную компоненту
            // (без пола/стен отсутствие внешней опоры не считается нарушением).
            if (!hasAnchor)
            {
                KitchenElement? start = null;
                foreach (var e in all)
                    if (hasContact.Contains(e)) { start = e; break; }
                if (start == null) start = all[0];
                visited.Add(start);
                queue.Enqueue(start);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!adjacency.TryGetValue(current, out var neighbors)) continue;
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            foreach (var e in all)
            {
                if (IsAnchor(e)) continue;
                // FacadeElement с gapMM > 0 плавает в проёме с зазором —
                // он не касается соседей face-to-face, это штатное поведение.
                if (e is FacadeElement fe && fe.GapMM > 0) continue;
                // DrawerElements are internal cabinet components, not structural anchors.
                if (e is DrawerElement) continue;
                // Источник света висит в воздухе (лампа/люстра) — отсутствие
                // опоры для него штатно.
                if (e is LightSourceElement) continue;
                if (!visited.Contains(e) || !hasContact.Contains(e))
                {
                    result.violations.Add(e);
                    result.AddDiagnostic(e, null, ViolationKind.Unsupported);
                }
            }

            var unvisited = new List<KitchenElement>(result.violations);
            while (unvisited.Count > 0)
            {
                var group = new List<KitchenElement>();
                var gq = new Queue<KitchenElement>();
                gq.Enqueue(unvisited[0]);

                while (gq.Count > 0)
                {
                    var current = gq.Dequeue();
                    if (!unvisited.Contains(current)) continue;
                    unvisited.Remove(current);
                    group.Add(current);

                    if (!adjacency.TryGetValue(current, out var neighbors)) continue;
                    foreach (var neighbor in neighbors)
                    {
                        if (unvisited.Contains(neighbor) && !group.Contains(neighbor))
                            gq.Enqueue(neighbor);
                    }
                }

                if (group.Count > 0)
                    result.isolatedGroups.Add(group);
            }

            result.isValid = result.violations.Count == 0;
        }

        private static void CheckWallHeightConstraints(List<KitchenElement> all, ValidationResult result)
        {
            // Строим словарь стена → Wall за O(n), чтобы не вызывать FindWallByName
            // за O(n) для каждого окна/двери (итого O(n·k) → O(n)).
            var wallByName = new System.Collections.Generic.Dictionary<string, Wall>();
            foreach (var e in all)
            {
                if (e == null) continue;
                var w = e.GetComponent<Wall>();
                if (w != null) wallByName[e.gameObject.name] = w;
            }

            foreach (var e in all)
            {
                if (e == null) continue;

                string? wallName = null;
                if (e is WindowElement win)
                    wallName = win.AttachedWallName;
                else if (e is DoorElement door)
                    wallName = door.AttachedWallName;
                else continue;

                // Элемент без стены или стена не найдена — проверка связности
                // выполняется отдельно в CheckConnectivity.
                if (string.IsNullOrEmpty(wallName)) continue;
                if (!wallByName.TryGetValue(wallName, out var wall)) continue;

                var wallEl = wall.GetComponent<KitchenElement>();
                if (wallEl == null) continue;

                int wallHeightMM = wallEl.DimensionsMM.y;
                if (wallHeightMM <= 0) continue;

                float toU = AppConstants.MM_TO_UNITS;
                float wallHalfH = wallHeightMM * toU * 0.5f;
                float wallCenterY = wall.FullPosition.y;
                float wallTop = wallCenterY + wallHalfH;
                float wallBottom = wallCenterY - wallHalfH;

                float elemHalfH = e.DimensionsMM.y * toU * 0.5f;
                float elemTop = e.transform.position.y + elemHalfH;
                float elemBottom = e.transform.position.y - elemHalfH;

                if (elemTop > wallTop + Tolerance.EpsilonUnits ||
                    elemBottom < wallBottom - Tolerance.EpsilonUnits)
                {
                    if (!result.violations.Contains(e))
                        result.violations.Add(e);
                    result.AddDiagnostic(e, null, ViolationKind.OutOfWallBounds);
                }
            }
        }
    }
}
