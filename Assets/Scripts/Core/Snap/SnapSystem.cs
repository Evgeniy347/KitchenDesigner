using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct SnapResult
    {
        public bool snapped;
        public Vector3 position;
        public string targetName;
        public int faceIndex;
        public Vector3 snapPoint;
        public Vector3 targetPoint;
    }

    /// <summary>Диагностика прилипания к одному соседу: какая пара граней ближайшая
    /// и по какой причине снэп прошёл/не прошёл. Все расстояния — в мм.</summary>
    [System.Serializable]
    public class SnapNeighborReport
    {
        public string name = string.Empty;
        public float centerDistanceMM;      // расстояние между центрами деталей
        public bool intersects;             // AABB-пересечение — снэп с этой деталью невозможен
        public bool hasFacingFaces;         // есть ли встречные параллельные грани (dot≈-1)
        public float bestDot;               // самый «встречный» dot нормалей (-1 = идеально)
        public int movedFaceIndex = -1;     // лучшая пара граней (движимая/цель)
        public int otherFaceIndex = -1;
        public float gapMM = -1f;           // зазор по нормали у лучшей пары
        public float overlapRatio = -1f;    // перекрытие граней у лучшей пары (0..1+)
        public bool withinThreshold;
        public bool overlapEnough;
        public bool wouldSnap;              // все условия для ЭТОЙ детали выполнены
        public string verdict = string.Empty;              // человекочитаемая причина
    }

    /// <summary>Итог диагностики: общие настройки, результат TrySnap и разбор по соседям.</summary>
    [System.Serializable]
    public class SnapDiagnosis
    {
        public bool snapEnabled;
        public float thresholdMM;
        public bool wouldSnap;
        public string? snapTarget;
        public List<SnapNeighborReport> neighbors = new List<SnapNeighborReport>();
    }

    public static class SnapSystem
    {
        // Допуск к порогу: прилипание срабатывает и ровно на границе порога,
        // несмотря на ошибку округления float при вычислении зазора.
        private const float ThresholdEpsilon = Tolerance.SnapEpsilon;

        // «Нулевое» смещение (0.1 мм): кандидат, чей сдвиг меньше, — это уже
        // существующий контакт (деталь и так заподлицо), а не новое прилипание.
        // Такой кандидат не должен побеждать содержательные снэпы — иначе деталь,
        // скользящая по грани соседа (или стоящая на полу), никогда не прилипнет
        // к стене: подтверждение текущего контакта (сдвиг 0) всегда «ближе».
        private const float ZeroShiftEpsilon = Tolerance.EpsilonUnits;

        // Кандидат прилипания с разложением сдвига на компоненты: planeShift —
        // заподлицо вдоль нормали грани (суть снэпа), du/dv — необязательное
        // выравнивание по кромке/центру в плоскости грани. Разложение нужно,
        // чтобы при конфликте с существующим контактом обнулять только
        // мешающую компоненту, а не отбрасывать кандидат целиком.
        private struct Candidate
        {
            public float dist;
            public SnapResult result;
            public Vector3 normal;   // нормаль движимой грани (направление planeShift)
            public float planeShift;
            public Vector3 u, v;
            public float du, dv;
            public bool hasLineContact; // касание по кромке на одной оси (приоритет ниже)
            public string? log;
        }

        // Прилипание — чистая детерминированная функция от (moved, others, testPos).
        // Без скрытого статического состояния: одинаковый вход → одинаковый выход,
        // что критично для предсказуемости в рантайме и для повторяемости тестов.
        public static SnapResult TrySnap(KitchenElement moved, List<KitchenElement> others, Vector3 testPosition)
        {
            if (moved == null || others == null) return default;
            if (!KitchenSettings.Instance.SnapEnabled) return default;
            if (!moved.gameObject.activeInHierarchy) return default;

            float threshold = KitchenSettings.Instance.SnapThreshold * AppConstants.MM_TO_UNITS;
            float maxDist = threshold + ThresholdEpsilon;
            Vector3 prevPos = moved.transform.position;

            // Кандидаты делятся на два сорта:
            //  - «нулевые» (сдвиг ≈ 0) — деталь УЖЕ заподлицо с этой гранью; это
            //    подтверждение текущего контакта, а не новое прилипание;
            //  - содержательные — реальное притяжение к новой грани.
            // Содержательный снэп предпочтительнее нулевого, но не должен рвать
            // существующие контакты: его сдвиг обязан быть ⊥ нормалям нулевых пар.
            SnapResult bestZero = default;
            string? bestZeroLog = null;
            bool anyFullAreaZero = false;
            var zeroNormals = new List<Vector3>();
            var candidates = new List<Candidate>();

            var alignedNormals = new List<Vector3>();
            Collect(moved, others, testPosition, maxDist, candidates, zeroNormals,
                ref bestZero, ref bestZeroLog, ref anyFullAreaZero, alignedNormals);

            // Применяем лучший непротиворечивый кандидат, затем ДОБИРАЕМ ортогональные:
            // в углу (бок соседа + стена) один кандидат чинит только одну ось, вторая
            // оставалась с зазором или проникновением — деталь «прилипла», но красная.
            // Каждый следующий проход обязан быть ⊥ уже применённым нормалям (locked):
            // это не даёт откатывать сделанное и гарантирует сходимость (осей три).
            Vector3 pos = testPosition;
            SnapResult primary = default;
            string? primaryLog = null;
            bool primaryIsLineContact = false;
            var locked = new List<Vector3>(zeroNormals);

            // Кромочные контакты, существующие В ИСХОДНОЙ позиции. Только они имеют
            // право участвовать в доборе. Кромочный контакт, ПОЯВИВШИЙСЯ как следствие
            // основного снэпа, — артефакт: приложив деталь к боку соседа, мы делаем
            // соприкасающимися и остальные его грани, и добор по ним уводит деталь на
            // толщину плиты вбок. Изначальное же касание по ребру — реальная геометрия
            // сборки (стойка у кромки полки), и его доборо́м терять нельзя.
            var initialLineContacts = new HashSet<string>();
            foreach (var c in candidates)
                if (c.hasLineContact) initialLineContacts.Add(LineContactKey(c));

            for (int pass = 0; pass < 3; pass++)
            {
                if (pass > 0)
                {
                    candidates.Clear();
                    zeroNormals.Clear();
                    SnapResult ignoredZero = default;
                    string? ignoredLog = null;
                    bool ignoredFullArea = false;
                    Collect(moved, others, pos, maxDist, candidates, zeroNormals,
                        ref ignoredZero, ref ignoredLog, ref ignoredFullArea);
                    foreach (var n in zeroNormals) locked.Add(n);
                }

                if (!TryPickCandidate(candidates, locked, pos, pass == 0, initialLineContacts,
                        alignedNormals, out Candidate picked, out Vector3 pickedPos))
                    break;

                pos = pickedPos;
                if (!primary.snapped)
                {
                    primary = picked.result;
                    primaryLog = picked.log;
                    primaryIsLineContact = picked.hasLineContact;
                }
                primary.position = pos;
                locked.Add(picked.normal);

                // Фиксируем и оси выравнивания В ПЛОСКОСТИ грани, если по ним был
                // сдвиг. Раньше запирался только planeShift-normal, поэтому проход
                // добора мог заново двигать деталь по той же оси, по которой её уже
                // выровняли: дощечка, поставленная по стенке паза (сдвиг по X через
                // du), тут же утягивалась на стену заподлицо — тоже по X.
                if (Mathf.Abs(picked.du) > ZeroShiftEpsilon) locked.Add(picked.u);
                if (Mathf.Abs(picked.dv) > ZeroShiftEpsilon) locked.Add(picked.v);
            }

            moved.transform.position = prevPos;

            // Кромочный (line contact) снэп не должен утаскивать деталь с уже
            // существующего полноплощадного контакта: полка, стоящая заподлицо к
            // боковине по Z-грани (1.369), иначе отскакивала бы на грань-контакт
            // с низом боковины (1.351). Полноплощадная опора важнее слабого кромочного
            // притяжения — подтверждаем текущий контакт (bestZero). Правило про
            // ОСНОВНОЙ контакт; кромочный ДОБОР по свободной оси им не ограничен.
            if (primary.snapped && !(primaryIsLineContact && anyFullAreaZero))
            {
                if (VerboseLog && primaryLog != null) Debug.Log(primaryLog);
                return primary;
            }

            // Содержательных нет (или все рвут контакты) — подтверждаем текущий
            // контакт (прежнее поведение: снэп «на месте»).
            if (VerboseLog && bestZeroLog != null) Debug.Log(bestZeroLog);
            return bestZero;
        }

        /// <summary>Грань детали «съедена» пазом на участке, куда метит панель:
        /// у грани та же нормаль, что у дна паза, и панель попадает в контур паза.
        /// Тогда контактом служит дно, а не поверхность детали.</summary>
        private static bool SeatSupersedesFace(KitchenElement.Face movedFace,
            KitchenElement.Face otherFace, KitchenElement.Face[] seatFaces)
        {
            foreach (var seat in seatFaces)
            {
                // Пласть, в которой прорезан паз, — грань с той же нормалью.
                if (Vector3.Dot(seat.normal, otherFace.normal) < Tolerance.ParallelDot) continue;
                if (FacesOverlap(movedFace, seat, out float ratio, out _) && ratio > 0f)
                    return true;
            }
            return false;
        }

        /// <summary>Сбор кандидатов прилипания из позиции basePos: нормали
        /// «нулевых» пар (деталь уже заподлицо) идут в zeroNormals, содержательные
        /// кандидаты — в candidates. Оставляет moved в позиции basePos —
        /// вызывающий обязан восстановить исходную позицию.</summary>
        private static void Collect(KitchenElement moved, List<KitchenElement> others, Vector3 basePos,
            float maxDist, List<Candidate> candidates, List<Vector3> zeroNormals,
            ref SnapResult bestZero, ref string? bestZeroLog, ref bool anyFullAreaZero,
            List<Vector3>? alignedNormals = null)
        {
            moved.transform.position = basePos;
            KitchenElement.Face[] movedFaces = FaceCache.GetFaces(moved);

            // Габарит движимой детали в basePos — чтобы проверять, не загонит ли
            // выравнивание по дальней кромке деталь В ТЕЛО соседа (см. ниже).
            Vector3[] mVerts = moved.GetVertices();
            Vector3 mMin = mVerts[0], mMax = mVerts[0];
            for (int k = 1; k < 8; k++)
            {
                mMin = Vector3.Min(mMin, mVerts[k]);
                mMax = Vector3.Max(mMax, mVerts[k]);
            }

            foreach (var other in others)
            {
                if (other == moved || other == null) continue;
                if (!other.gameObject.activeInHierarchy) continue;
                // AABB-пересечение НЕ отсеиваем: для повёрнутых деталей AABB может
                // быть избыточно большим и ложно блокировать снэп перпендикулярных
                // кромок. Face-pair loop ниже сам отфильтрует глубокие пересечения
                // по planeDist > maxDist.

                KitchenElement.Face[] otherFaces = FaceCache.GetFaces(other);

                // Дно паза — посадочное место, и только для вкладной панели:
                // толстая деталь в паз не садится, и предлагать ей дно значит
                // ловить ложные притяжения внутрь короба.
                KitchenElement.Face[] seatFaces = moved is PanelElement
                    ? other.GetGrooveSeatFaces()
                    : System.Array.Empty<KitchenElement.Face>();

                // Стенки паза — разметочный ориентир, доступный ЛЮБОЙ детали:
                // «поставь полку по краю паза». Даёт детенты на 16 и 20 мм от
                // кромки в дополнение к самой кромке.
                KitchenElement.Face[] wallFaces = other.GetGrooveWallFaces();

                // Габарит соседа — для проверки «кандидат не загоняет центр внутрь соседа».
                Vector3[] oVerts = other.GetVertices();
                float oMinX = oVerts[0].x, oMaxX = oVerts[0].x;
                float oMinY = oVerts[0].y, oMaxY = oVerts[0].y;
                float oMinZ = oVerts[0].z, oMaxZ = oVerts[0].z;
                for (int k = 1; k < 8; k++)
                {
                    if (oVerts[k].x < oMinX) oMinX = oVerts[k].x; else if (oVerts[k].x > oMaxX) oMaxX = oVerts[k].x;
                    if (oVerts[k].y < oMinY) oMinY = oVerts[k].y; else if (oVerts[k].y > oMaxY) oMaxY = oVerts[k].y;
                    if (oVerts[k].z < oMinZ) oMinZ = oVerts[k].z; else if (oVerts[k].z > oMaxZ) oMaxZ = oVerts[k].z;
                }

                for (int i = 0; i < 6; i++)
                {
                    // Грани соседа: шесть габаритных, затем дно каждого паза.
                    for (int j = 0; j < 6 + seatFaces.Length; j++)
                    {
                        bool isGroove = j >= 6;
                        var of = isGroove ? seatFaces[j - 6] : otherFaces[j];

                        // Над пазом материала НЕТ: пласть там не поверхность контакта.
                        // Без этого панель никогда бы не села в паз — подходя снаружи,
                        // она всегда ближе к пласти (9 мм), чем к дну паза (2 мм),
                        // и снэп возвращал бы её обратно на поверхность детали.
                        if (!isGroove && SeatSupersedesFace(movedFaces[i], of, seatFaces)) continue;

                        // Работают только параллельные грани. Встречные (dot≈-1) дают
                        // стык, со-направленные (dot≈+1) — выравнивание по дальней
                        // кромке; разбор ниже.
                        float dot = Vector3.Dot(movedFaces[i].normal, of.normal);
                        if (!Tolerance.IsParallel(dot)) continue;

                        // Со-направленная грань стыка не образует, но остаётся
                        // ВЫРАВНИВАЮЩЕЙ плоскостью: деталь можно поставить заподлицо
                        // с ДАЛЬНЕЙ кромкой соседа. У панели толщиной 18 мм это второй
                        // детент рядом с первым, и без него стойка, ползущая вверх мимо
                        // панели, перескакивала с «низ панели» сразу на следующую полку,
                        // пропуская «верх панели». Ресайз обе плоскости видит давно
                        // (ResizeSnap), перемещение — только встречные; расхождение и
                        // ощущалось как «скачет между рёбрами».
                        bool coDirectional = dot > 0;

                        // Дно паза — посадочное место, оно только встречное.
                        if (isGroove && coDirectional) continue;

                        var mf = movedFaces[i];

                        Vector3 offset = of.center - mf.center;
                        float planeDist = Mathf.Abs(Vector3.Dot(offset, mf.normal));
                        if (planeDist > maxDist) continue;

                        if (!FacesOverlap(mf, of, out float overlapRatio, out bool hasLineContact))
                            continue;
                        if (overlapRatio < Tolerance.MinSupportOverlap) continue;

                        // Сдвиг вдоль нормали: плоскости становятся заподлицо.
                        float planeShift = Vector3.Dot(offset, mf.normal);

                        if (coDirectional)
                        {
                            // Уже компланарны — деталь выровнена по этой оси. Добор такую
                            // выровненность рвать не вправе (низ лежащей доски вровень с
                            // низом стоящей): помечаем ось и кандидата не создаём.
                            if (Mathf.Abs(planeShift) <= ZeroShiftEpsilon)
                            {
                                alignedNormals?.Add(mf.normal);
                                continue;
                            }

                            // Выравнивание по дальней кромке — чистый сдвиг вдоль нормали,
                            // без довеска du/dv: контакта тут нет, «подровнять заодно
                            // кромку» не к чему.
                            Vector3 alignShift = planeShift * mf.normal;

                            // ...но только если деталь при этом не влезает В СОСЕДА.
                            // Заподлицо с дальней гранью встают детали РЯДОМ с соседом
                            // (стойка у кромки полки). Если же деталь перекрывает соседа
                            // по остальным осям, «выравнивание» загонит её внутрь: низ
                            // короба так уезжал в стену.
                            if (Tolerance.IntervalsOverlap(mMin.x + alignShift.x, mMax.x + alignShift.x, oMinX, oMaxX) &&
                                Tolerance.IntervalsOverlap(mMin.y + alignShift.y, mMax.y + alignShift.y, oMinY, oMaxY) &&
                                Tolerance.IntervalsOverlap(mMin.z + alignShift.z, mMax.z + alignShift.z, oMinZ, oMaxZ))
                                continue;

                            Vector3 alignPos = basePos + alignShift;
                            candidates.Add(new Candidate
                            {
                                dist = Mathf.Abs(planeShift),
                                result = new SnapResult
                                {
                                    snapped = true,
                                    position = alignPos,
                                    targetName = other.PartName,
                                    faceIndex = j,
                                    snapPoint = mf.center,
                                    targetPoint = of.center
                                },
                                normal = mf.normal,
                                planeShift = planeShift,
                                u = mf.rightAxis,
                                v = mf.upAxis,
                                du = 0f,
                                dv = 0f,
                                hasLineContact = hasLineContact,
                                log = VerboseLog
                                    ? $"[Snap] {moved.Describe()} → {other.Describe()} | выравнивание " +
                                      $"по дальней кромке m{i}/o{j} сдвиг={planeShift * 1000f:F2}мм"
                                    : null
                            });
                            continue;
                        }

                        // Сдвиг в плоскости грани: выравнивание по ближайшей кромке/центру
                        // (а не принудительно по центру — иначе мелкая деталь центрируется).
                        Vector3 u = mf.rightAxis;
                        Vector3 v = mf.upAxis;
                        Rect mRect = GetFaceRect(mf, u, v);
                        Rect oRect = GetFaceRect(of, u, v);
                        float du = BestEdgeDelta(mRect.xMin, mRect.xMax, oRect.xMin, oRect.xMax, maxDist,
                            GrooveEdgeCoords(wallFaces, u), out string labelU);
                        float dv = BestEdgeDelta(mRect.yMin, mRect.yMax, oRect.yMin, oRect.yMax, maxDist,
                            GrooveEdgeCoords(wallFaces, v), out string labelV);

                        // Точное выравнивание заподлицо. Сетку НЕ применяем: при крупном
                        // шаге она сдвинула бы деталь с плоскости контакта и разорвала стык.
                        Vector3 snapPos = basePos + planeShift * mf.normal + du * u + dv * v;

                        // Кандидат не имеет права загонять деталь ВНУТРЬ соседа. Выравнивание
                        // по центру особенно охотно это делает: у тонкой панели центр совпадает
                        // с центром такой же панели-соседа, и «снэп» давал полное наложение
                        // (полка с заподлицо-позиции 1.226 прыгала в центр двери 1.244).
                        // Критерий — центр детали внутри габарита соседа: у контактов заподлицо
                        // центр всегда снаружи, а AABB-пересечение здесь не годится (у деталей,
                        // повёрнутых на 45°, AABB заведомо больше тела и даёт ложный отказ).
                        // Для граней паза проверку не применяем: они лежат ВНУТРИ
                        // габарита детали, и «зайти внутрь» здесь — это ровно то,
                        // что должно произойти.
                        if (!isGroove &&
                            snapPos.x > oMinX && snapPos.x < oMaxX &&
                            snapPos.y > oMinY && snapPos.y < oMaxY &&
                            snapPos.z > oMinZ && snapPos.z < oMaxZ)
                            continue;

                        float dist = Vector3.Distance(snapPos, basePos);
                        var result = new SnapResult
                        {
                            snapped = true,
                            position = snapPos,
                            targetName = other.PartName,
                            faceIndex = j,
                            snapPoint = mf.center,
                            targetPoint = of.center
                        };
                        string? log = VerboseLog
                            ? $"[Snap] {moved.Describe()} → {other.Describe()} | грань m{i}/o{j} " +
                              $"зазор={planeDist * 1000f:F2}мм перекр={overlapRatio:P0} " +
                              $"оси[u:{labelU} v:{labelV}] → поз {snapPos.x:F3},{snapPos.y:F3},{snapPos.z:F3}"
                            : null;

                        // Заподлицо вдоль нормали — ось УЖЕ зафиксирована, независимо от того,
                        // нужно ли ещё выравнивание в плоскости грани (du/dv). Раньше ось
                        // фиксировалась только при полностью нулевом сдвиге, и деталь, стоящая
                        // заподлицо, но не выровненная по кромке, считалась «свободной»:
                        // снэп перекидывал её на соседний детент через всю деталь
                        // (1.244 ⇄ 1.226 у двери/боковины).
                        if (Mathf.Abs(planeShift) <= ZeroShiftEpsilon)
                        {
                            zeroNormals.Add(mf.normal);
                            // Полноплощадной контакт (не кромочный), уже стоящий заподлицо, —
                            // это реальная опора: слабый кромочный (line contact) снэп не
                            // должен утаскивать деталь с него (иначе полка на боковой Z-грани
                            // на 1.369 отскакивала бы на грань-контакт 1.351).
                            if (!hasLineContact) anyFullAreaZero = true;

                            // Подтверждение текущего положения: деталь уже заподлицо вдоль этой
                            // нормали. Позиция — basePos (выравнивание в плоскости грани сюда не
                            // включаем), иначе при зафиксированной оси и неприменимых кандидатах
                            // TrySnap возвращал «не прилипло» прямо на валидном детенте.
                            if (!bestZero.snapped)
                            {
                                var confirm = result;
                                confirm.position = basePos;
                                bestZero = confirm;
                                bestZeroLog = log;
                            }
                        }

                        if (dist > ZeroShiftEpsilon)
                        {
                            candidates.Add(new Candidate
                            {
                                dist = dist,
                                result = result,
                                normal = mf.normal,
                                planeShift = planeShift,
                                u = u,
                                v = v,
                                du = du,
                                dv = dv,
                                hasLineContact = hasLineContact,
                                log = log
                            });
                        }
                    }
                }
            }
        }

        /// <summary>Лучший кандидат, не рвущий зафиксированные контакты/оси (locked):
        ///  - сдвиг заподлицо (planeShift) вдоль locked-нормали — кандидат отбрасывается;
        ///  - выравнивание по кромке (du/dv) вдоль locked-нормали — обнуляется
        ///    ТОЛЬКО эта компонента. Раньше кандидат отбрасывался целиком, и деталь
        ///    на полу отказывалась липнуть к соседу лишь потому, что заодно хотела
        ///    подровнять кромку по вертикали (что оторвало бы её от пола).</summary>
        /// <summary>Опознание пары «деталь ↔ грань соседа» между проходами: позиция
        /// детали меняется, а цель и индекс её грани — нет.</summary>
        private static string LineContactKey(Candidate c) =>
            c.result.targetName + "#" + c.result.faceIndex;

        private static bool TryPickCandidate(List<Candidate> candidates, List<Vector3> locked,
            Vector3 basePos, bool primaryPass, HashSet<string> initialLineContacts,
            List<Vector3> alignedNormals, out Candidate picked, out Vector3 pickedPos)
        {
            picked = default;
            pickedPos = basePos;
            float bestScore = float.MaxValue;
            float bestDist = float.MaxValue;
            bool bestIsLineContact = false;
            bool found = false;

            foreach (var c in candidates)
            {
                // В доборе кромочный контакт участвует, но только ИЗНАЧАЛЬНЫЙ. Раньше
                // он отбрасывался целиком, и снэп по такой оси срабатывал ТОЛЬКО если
                // выигрывал первый проход — то есть если его зазор меньше вообще всех
                // прочих. Стойка у стены (контакт 0.5 мм по Z) из-за этого не ловила
                // по вертикали ни одной полки: Z-кандидат всегда забирал pass 0, а в
                // доборе Y-кандидат, кромочный по своей природе, выбрасывался. При
                // ресайзе та же пара прилипала — отсюда расхождение «тянется, но не
                // перетаскивается».
                if (!primaryPass && c.hasLineContact)
                {
                    if (!initialLineContacts.Contains(LineContactKey(c))) continue;

                    // ...и не по оси, где деталь уже выровнена с соседом заподлицо.
                    bool breaksAlignment = false;
                    foreach (var n in alignedNormals)
                        if (Mathf.Abs(c.planeShift * Vector3.Dot(c.normal, n)) > ZeroShiftEpsilon)
                        { breaksAlignment = true; break; }
                    if (breaksAlignment) continue;
                }

                float du = c.du, dv = c.dv;

                // В доборе (pass>0) применяем ТОЛЬКО заподлицо вдоль нормали (planeShift):
                // выравнивание по кромке/центру (du/dv) — прерогатива основного контакта
                // (pass 0). Иначе крупный сосед (стена) во втором проходе «центрирует»
                // деталь по своей грани и утаскивает её с уже найденного контакта: полка,
                // подтянутая к боковине на 1.369, уезжала к центру стены на 1.350.
                if (!primaryPass) { du = 0f; dv = 0f; }

                bool breaks = false;
                foreach (var n in locked)
                {
                    if (Mathf.Abs(c.planeShift * Vector3.Dot(c.normal, n)) > ZeroShiftEpsilon) { breaks = true; break; }
                    if (Mathf.Abs(du * Vector3.Dot(c.u, n)) > ZeroShiftEpsilon) du = 0f;
                    if (Mathf.Abs(dv * Vector3.Dot(c.v, n)) > ZeroShiftEpsilon) dv = 0f;
                }
                if (breaks) continue;

                Vector3 pos = basePos + c.planeShift * c.normal + du * c.u + dv * c.v;
                float dist = Vector3.Distance(pos, basePos);
                // Выродился в подтверждение текущего контакта — не содержательный.
                if (dist <= ZeroShiftEpsilon) continue;

                // Конкуренцию выигрывает кандидат с МЕНЬШИМ ЗАЗОРОМ (planeShift), а не
                // с меньшим суммарным сдвигом. Выравнивание по кромке/центру (du/dv) —
                // бесплатный довесок к контакту, и оно не вправе этот контакт
                // проигрывать: дно короба в 2 мм над ногой уступало стене в 7 мм лишь
                // потому, что заодно центровалось по опоре (7 мм по X) и суммарный
                // сдвиг выходил 7.3 мм. У чисто выравнивающего кандидата (деталь уже
                // заподлицо, planeShift≈0) зазора нет — его цена и есть сдвиг.
                float shift = Mathf.Abs(c.planeShift);
                float score = shift > ZeroShiftEpsilon ? shift : dist;

                // Порядок сравнения (лексикографический):
                //  1) зазор — см. выше;
                //  2) при равном зазоре полноплощадный контакт важнее кромочного:
                //     тонкая боковина (line contact по одной оси) не должна побить
                //     кандидата с полным face-to-face;
                //  3) при прочих равных — меньший суммарный сдвиг, то есть меньше
                //     «лишнего» выравнивания в плоскости. Иначе в углу побеждал бы
                //     кандидат, тащащий за собой кромочный сдвиг по второй оси: он
                //     запирал эту ось, и добор до стены (тот же зазор 30 мм) уже не
                //     проходил — деталь вставала к боку, но не к стене.
                bool better;
                if (score < bestScore - ZeroShiftEpsilon) better = true;
                else if (score > bestScore + ZeroShiftEpsilon) better = false;
                else if (bestIsLineContact != c.hasLineContact) better = bestIsLineContact;
                else better = dist < bestDist - ZeroShiftEpsilon;

                if (better)
                {
                    bestScore = score;
                    bestDist = dist;
                    bestIsLineContact = c.hasLineContact;
                    picked = c;
                    picked.du = du;
                    picked.dv = dv;
                    picked.result.position = pos;
                    pickedPos = pos;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Логировать выбор снэпа (для отладки прилипания). По умолчанию выкл.</summary>
        public static bool VerboseLog = false;

        /// <summary>
        /// Диагностика: почему деталь прилипает/не прилипает из позиции testPosition.
        /// Прогоняет ту же геометрию, что и <see cref="TrySnap"/>, но вместо раннего
        /// отсева собирает по каждому соседу лучшую пару граней и причину отказа:
        /// пересечение AABB, нет встречных граней, зазор больше порога, перекрытие &lt;30%.
        /// Сцену не меняет (позиция восстанавливается). Соседи отсортированы по зазору.
        /// </summary>
        public static SnapDiagnosis Diagnose(KitchenElement moved, List<KitchenElement> others,
            Vector3 testPosition, int maxNeighbors = 5)
        {
            var settings = KitchenSettings.Instance;
            var report = new SnapDiagnosis
            {
                snapEnabled = settings != null && settings.SnapEnabled,
                thresholdMM = settings != null ? settings.SnapThreshold : 0f,
            };
            if (moved == null || others == null) return report;

            var snap = TrySnap(moved, others, testPosition);
            report.wouldSnap = snap.snapped;
            report.snapTarget = snap.snapped ? snap.targetName : null;

            float maxDist = report.thresholdMM * AppConstants.MM_TO_UNITS + ThresholdEpsilon;

            Vector3 prevPos = moved.transform.position;
        moved.transform.position = testPosition;
        KitchenElement.Face[] movedFaces = FaceCache.GetFaces(moved);

            foreach (var other in others)
            {
                if (other == moved || other == null) continue;
                if (!other.gameObject.activeInHierarchy) continue;

                var n = new SnapNeighborReport
                {
                    name = other.PartName,
                    centerDistanceMM = Vector3.Distance(testPosition, other.transform.position) / AppConstants.MM_TO_UNITS,
                    intersects = ElementsIntersect(moved, other),
                    bestDot = 1f,
                };

                KitchenElement.Face[] otherFaces = FaceCache.GetFaces(other);

                // Те же грани, что видит TrySnap: шесть габаритных плюс дно каждого
                // паза (только для вкладной панели). Без дна паза диагностика
                // сообщала «не прилипнет» там, где TrySnap сажает панель в паз.
                KitchenElement.Face[] seatFaces = moved is PanelElement
                    ? other.GetGrooveSeatFaces()
                    : System.Array.Empty<KitchenElement.Face>();

                int bestRank = int.MaxValue;
                float bestGap = float.MaxValue;

                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 6 + seatFaces.Length; j++)
                    {
                        bool isGroove = j >= 6;
                        var of = isGroove ? seatFaces[j - 6] : otherFaces[j];

                        float dot = Vector3.Dot(movedFaces[i].normal, of.normal);
                        n.bestDot = Mathf.Min(n.bestDot, dot);
                        if (!Tolerance.IsParallel(dot) || dot > 0) continue;

                        var mf = movedFaces[i];
                        // Над пазом материала нет — как и в Collect.
                        if (!isGroove && SeatSupersedesFace(mf, of, seatFaces)) continue;
                        n.hasFacingFaces = true;

                        float gap = Mathf.Abs(Vector3.Dot(of.center - mf.center, mf.normal));
                        bool hasOverlap = FacesOverlap(mf, of, out float ratio, out _);
                        bool within = gap <= maxDist;
                        bool enough = hasOverlap && ratio >= Tolerance.MinSupportOverlap;

                        // Ранг важнее зазора: пара, по которой снэп РЕАЛЬНО возможен,
                        // всегда лучше более близкой, но негодной. Раньше отбор шёл по
                        // одному зазору, и вердикт писался по случайной ближней паре с
                        // перекрытием 10% — Diagnose говорил «не прилипнет», а TrySnap
                        // прилипал по другой, полноценной паре того же соседа.
                        int rank = (within && enough) ? 0 : (hasOverlap ? 1 : 2);
                        if (rank < bestRank || (rank == bestRank && gap < bestGap))
                        {
                            bestRank = rank;
                            bestGap = gap;
                            n.movedFaceIndex = i;
                            n.otherFaceIndex = j;
                            n.gapMM = gap / AppConstants.MM_TO_UNITS;
                            n.overlapRatio = hasOverlap ? ratio : 0f;
                            n.withinThreshold = within;
                            n.overlapEnough = enough;
                        }
                    }
                }

                n.wouldSnap = n.hasFacingFaces && n.withinThreshold && n.overlapEnough;
                n.verdict =
                    !n.hasFacingFaces ? $"нет встречных параллельных граней (лучший dot={n.bestDot:F3}) — деталь повёрнута?"
                    : !n.withinThreshold ? $"зазор {n.gapMM:F1} мм больше порога {report.thresholdMM:F0} мм"
                    : !n.overlapEnough ? $"перекрытие граней {n.overlapRatio:P0} меньше минимума 30%"
                    : n.intersects ? "AABB пересекаются из-за поворота, но снэп сработает (разведёт детали заподлицо)"
                    : "OK — прилипнет";

                report.neighbors.Add(n);
            }

            moved.transform.position = prevPos;

            // Ближние — первыми; у деталей без встречных граней зазора нет,
            // ранжируем их по расстоянию между центрами.
            report.neighbors.Sort((a, b) =>
                (a.gapMM >= 0 ? a.gapMM : a.centerDistanceMM)
                .CompareTo(b.gapMM >= 0 ? b.gapMM : b.centerDistanceMM));
            if (report.neighbors.Count > maxNeighbors)
                report.neighbors.RemoveRange(maxNeighbors, report.neighbors.Count - maxNeighbors);

            return report;
        }

        /// <summary>
        /// Лучшее выравнивание интервала [aMin,aMax] к [bMin,bMax] вдоль оси:
        /// кандидаты — совпадение минимумов, максимумов, центров. Возвращает
        /// наименьший по модулю сдвиг в пределах порога, иначе 0 (ось не снэпится).
        /// </summary>
        /// <summary>Координаты стенок пазов вдоль оси axis — только для стенок,
        /// плоскость которых этой оси перпендикулярна. Это разметочные линии для
        /// выравнивания кромки: паз даёт детенты на 16 и 20 мм от кромки детали.</summary>
        private static List<float> GrooveEdgeCoords(KitchenElement.Face[] wallFaces, Vector3 axis)
        {
            var coords = new List<float>();
            foreach (var w in wallFaces)
            {
                if (Mathf.Abs(Vector3.Dot(w.normal, axis)) < Tolerance.ParallelDot) continue;
                float c = Vector3.Dot(w.center, axis);
                if (!coords.Contains(c)) coords.Add(c);
            }
            return coords;
        }

        private static float BestEdgeDelta(float aMin, float aMax, float bMin, float bMax, float threshold,
            List<float>? grooveCoords, out string label)
        {
            float aCenter = (aMin + aMax) * 0.5f;
            float bCenter = (bMin + bMax) * 0.5f;

            var candidates = new List<(float d, string name)>
            {
                (bMin - aMin, "кромка-"),
                (bMax - aMax, "кромка+"),
                (bCenter - aCenter, "центр"),
            };

            // Кромка детали может встать по любой стенке паза — отсюда детенты
            // «начало паза» и «конец паза» рядом с обычной кромкой соседа.
            if (grooveCoords != null)
            {
                foreach (float g in grooveCoords)
                {
                    candidates.Add((g - aMin, "паз-"));
                    candidates.Add((g - aMax, "паз+"));
                }
            }

            float best = 0f;
            float bestAbs = float.MaxValue;
            label = "своб";
            foreach (var c in candidates)
            {
                float abs = Mathf.Abs(c.d);
                if (abs <= threshold && abs < bestAbs)
                {
                    bestAbs = abs;
                    best = c.d;
                    label = c.name;
                }
            }
            return best;
        }

        private static bool FacesOverlap(KitchenElement.Face a, KitchenElement.Face b, out float overlapRatio, out bool hasLineContact)
        {
            overlapRatio = 0;
            hasLineContact = false;
            Vector3 u = a.rightAxis;
            Vector3 v = a.upAxis;

            Rect aRect = GetFaceRect(a, u, v);
            Rect bRect = GetFaceRect(b, u, v);

            float interLeft = Mathf.Max(aRect.xMin, bRect.xMin);
            float interRight = Mathf.Min(aRect.xMax, bRect.xMax);
            float interBottom = Mathf.Max(aRect.yMin, bRect.yMin);
            float interTop = Mathf.Min(aRect.yMax, bRect.yMax);

            // Полное разнесение по оси (зазор между гранями, а не касание) —
            // перекрытия нет. Касание ровно по кромке (line contact) НЕ отбрасываем:
            // грани выровнены по этой оси, и если по другой оси перекрытие достаточно,
            // снэп должен сработать. Пример: тонкая боковина 18 мм по Z, полка под
            // ней — Y-грани делят кромку Z (line contact), но по X полное перекрытие.
            // Раньше line contact считался нулевым перекрытием и отбрасывал Y-снэп.
            bool noContactU = interLeft > interRight + Tolerance.SnapEpsilon;
            bool noContactV = interBottom > interTop + Tolerance.SnapEpsilon;
            if (noContactU || noContactV)
            {
                overlapRatio = 0;
                return false;
            }

            float overlapU = Mathf.Max(0, interRight - interLeft);
            float overlapV = Mathf.Max(0, interTop - interBottom);

            float minW = Mathf.Min(aRect.width, bRect.width);
            float minH = Mathf.Min(aRect.height, bRect.height);

            // Перекрытие по каждой оси относительно меньшего размера грани по этой оси.
            // В отличие от отношения площадей, произведение полуосевых отношений
            // корректно обрабатывает перпендикулярные узкие грани (18×400 и 18×1200):
            // площадь перекрытия 18×18 = 4.5% площади min-грани (7200), но по каждой
            // оси перекрытие составляет 100% от меньшего размера (18), и произведение
            // даёт 1.0 — снэп срабатывает.
            float ratioU = minW > 0 ? overlapU / minW : 0;
            float ratioV = minH > 0 ? overlapV / minH : 0;

            // Касание по кромке (line contact) на оси — считаем полным выравниванием
            // (ratio = 1.0) по этой оси: грани соприкасаются, просто не перекрываются
            // площадью. Это позволяет тонкой боковине (18 мм по Z) прилипнуть к полке
            // по Y: их Y-грани делят кромку Z, но по X перекрытие полное.
            // Точечное касание (line contact по обеим осям) тоже проходит — грани
            // выровнены по обеим осям, просто касаются в углу.
            bool lineU = overlapU <= Tolerance.SnapEpsilon && minW > 0;
            bool lineV = overlapV <= Tolerance.SnapEpsilon && minH > 0;
            if (lineU) ratioU = 1.0f;
            if (lineV) ratioV = 1.0f;
            hasLineContact = lineU || lineV;

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

        public static bool ElementsIntersect(KitchenElement a, KitchenElement b)
        {
            Vector3[] va = a.GetVertices();
            Vector3[] vb = b.GetVertices();

            float aMinX = va[0].x, aMaxX = va[0].x;
            float aMinY = va[0].y, aMaxY = va[0].y;
            float aMinZ = va[0].z, aMaxZ = va[0].z;
            for (int i = 1; i < 8; i++)
            {
                aMinX = Mathf.Min(aMinX, va[i].x); aMaxX = Mathf.Max(aMaxX, va[i].x);
                aMinY = Mathf.Min(aMinY, va[i].y); aMaxY = Mathf.Max(aMaxY, va[i].y);
                aMinZ = Mathf.Min(aMinZ, va[i].z); aMaxZ = Mathf.Max(aMaxZ, va[i].z);
            }

            float bMinX = vb[0].x, bMaxX = vb[0].x;
            float bMinY = vb[0].y, bMaxY = vb[0].y;
            float bMinZ = vb[0].z, bMaxZ = vb[0].z;
            for (int i = 1; i < 8; i++)
            {
                bMinX = Mathf.Min(bMinX, vb[i].x); bMaxX = Mathf.Max(bMaxX, vb[i].x);
                bMinY = Mathf.Min(bMinY, vb[i].y); bMaxY = Mathf.Max(bMaxY, vb[i].y);
                bMinZ = Mathf.Min(bMinZ, vb[i].z); bMaxZ = Mathf.Max(bMaxZ, vb[i].z);
            }

            return Tolerance.IntervalsOverlap(aMinX, aMaxX, bMinX, bMaxX) &&
                   Tolerance.IntervalsOverlap(aMinY, aMaxY, bMinY, bMaxY) &&
                   Tolerance.IntervalsOverlap(aMinZ, aMaxZ, bMinZ, bMaxZ);
        }
    }
}
