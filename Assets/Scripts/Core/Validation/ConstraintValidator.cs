using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ValidationResult
    {
        public List<FaceContact> contacts = new List<FaceContact>();
        public List<KitchenElement> violations = new List<KitchenElement>();
        public List<List<KitchenElement>> isolatedGroups = new List<List<KitchenElement>>();
        public bool isValid;
    }

    public static class ConstraintValidator
    {
        private const float FaceToFaceOverlap = 0.5f;

        // Размер ячейки равномерной сетки broad-phase (метры). Деталь заносится во ВСЕ
        // ячейки, которых касается её AABB, расширенный на contactDist. Тогда любая пара,
        // способная пересечься или образовать face-контакт (грани в пределах contactDist
        // и перекрывающиеся в плоскости), гарантированно оказывается в общей ячейке:
        // |pa-pb| <= contactDist => pa попадает в расширенный AABB соседа.
        private const float GridCellSize = 1.0f;

        // Якорь графа связности — пол или стена (к ним заземляются детали).
        private static bool IsAnchor(KitchenElement e) =>
            e != null && (e.GetComponent<BasePlate>() != null || e.GetComponent<Wall>() != null || e is WindowElement || e is DoorElement);

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
                if (e == null || IsAnchor(e)) continue;
                if (!result.violations.Contains(e))
                    result.violations.Add(e);
            }
            result.isValid = result.violations.Count == 0;

            return result;
        }

        private static void ProcessPair(int aIdx, int bIdx, float contactDist, ValidationResult result)
        {
            var a = _elems[aIdx];
            var b = _elems[bIdx];
            var aabbA = _aabbs[aIdx];
            var aabbB = _aabbs[bIdx];

            if (AABBsIntersect(aabbA, aabbB))
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

                // Пересечение объёмов физически недопустимо: две детали не могут
                // занимать одно место. Помечаем обе как нарушение (даже если по
                // связности они валидны) — это и есть «красный» при перетаскивании.
                _overlapping.Add(a);
                _overlapping.Add(b);
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

        // Та же проверка пересечения AABB с допуском, что в SnapSystem.ElementsIntersect:
        // строгое «<» с epsilon, чтобы плотный face-контакт не считался пересечением.
        private static bool AABBsIntersect(in AABB a, in AABB b) =>
            Tolerance.IntervalsOverlap(a.minX, a.maxX, b.minX, b.maxX) &&
            Tolerance.IntervalsOverlap(a.minY, a.maxY, b.minY, b.maxY) &&
            Tolerance.IntervalsOverlap(a.minZ, a.maxZ, b.minZ, b.maxZ);

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
                if (!visited.Contains(e) || !hasContact.Contains(e))
                    result.violations.Add(e);
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
            foreach (var e in all)
            {
                if (e == null) continue;

                Wall? wall = null;
                if (e is WindowElement win)
                    wall = FindWallByName(win.AttachedWallName, all);
                else if (e is DoorElement door)
                    wall = FindWallByName(door.AttachedWallName, all);
                else continue;

                // Элемент без стены или стена не найдена — проверка связности
                // выполняется отдельно в CheckConnectivity.
                if (wall == null) continue;

                var wallEl = wall.GetComponent<KitchenElement>();
                if (wallEl == null) continue;

                int wallHeightMM = wallEl.DimensionsMM.y;
                if (wallHeightMM <= 0) continue; // защита от нулевой/отрицательной стены

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
                }
            }
        }

        private static Wall? FindWallByName(string name, List<KitchenElement> all)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var e in all)
            {
                if (e == null) continue;
                if (e.gameObject.name == name)
                    return e.GetComponent<Wall>();
            }
            return null;
        }
    }
}
