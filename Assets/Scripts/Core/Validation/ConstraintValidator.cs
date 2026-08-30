using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
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

    /// <summary>Адаптер сцены над <see cref="ValidationCore"/>: собирает снимки
    /// деталей, зовёт ядро и переводит индексы обратно в
    /// <see cref="KitchenElement"/>.
    ///
    /// Сами правила (пересечения, контакты, связность, высота проёмов) живут в
    /// ядре и исполняются без Unity — под dotnet и мутационным тестированием.
    /// Здесь остаётся только то, что без сцены не имеет смысла: кто есть кто и
    /// запросы «по требованию» (near-contacts, недосаженные панели), которые
    /// ходят по сцене напрямую и в горячий путь не входят.</summary>
    public static class ConstraintValidator
    {
        // Скратч переиспользуется между вызовами: Validate идёт каждый кадр при
        // перетаскивании, и лишние аллокации здесь дороже всего.
        private static readonly List<KitchenElement> _elems = new List<KitchenElement>();
        private static readonly List<ValidationElement> _snapshots = new List<ValidationElement>();
        private static readonly CoreValidationResult _core = new CoreValidationResult();

        public static ValidationResult Validate(List<KitchenElement> all)
        {
            var result = new ValidationResult();

            _elems.Clear();
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null) _elems.Add(all[i]);

            if (_elems.Count == 0)
            {
                result.isValid = true;
                return result;
            }

            ValidationSnapshot.Build(_elems, _snapshots);
            // Результат ядра переиспользуется: он живёт ровно до перевода
            // индексов в детали, а Validate идёт каждый кадр при перетаскивании.
            var core = _core;
            ValidationCore.Validate(_snapshots, core);

            foreach (var c in core.Contacts)
                result.contacts.Add(new FaceContact(_elems[c.A], _elems[c.B],
                    c.FaceA, c.FaceB, c.Area, c.IsFaceToFace));

            foreach (int v in core.Violations)
                result.violations.Add(_elems[v]);

            foreach (var group in core.IsolatedGroups)
            {
                var mapped = new List<KitchenElement>(group.Count);
                foreach (int i in group) mapped.Add(_elems[i]);
                result.isolatedGroups.Add(mapped);
            }

            if (core.Diagnostics != null)
                foreach (var d in core.Diagnostics)
                    result.AddDiagnostic(_elems[d.Element],
                        d.Other >= 0 ? _elems[d.Other] : null, d.Kind);

            result.isValid = core.IsValid;
            return result;
        }

        /// <summary>Есть ли среди нарушений результата деталь рядом с element:
        /// сам element или нарушение, чей AABB в пределах radiusUnits от AABB
        /// element. Близость меряется ПО ГАБАРИТАМ, а не по центрам: у крупных
        /// деталей центры соседей дальше любого разумного радиуса, и проверка
        /// по центрам молча пропускала нарушения, стоящие вплотную.</summary>
        public static bool HasViolationNear(ValidationResult result, KitchenElement element, float radiusUnits)
        {
            if (result == null || element == null || result.violations.Count == 0) return false;

            var ea = element.ToGeometry();
            foreach (var v in result.violations)
            {
                if (v == element) return true;
                if (v == null) continue;
                var va = v.ToGeometry();
                // Порог ВКЛЮЧИТЕЛЬНЫЙ: деталь, стоящая ровно в радиусе, считается
                // соседней (AABBsIntersect со строгим сравнением здесь не годится).
                if (va.Min.x <= ea.Max.x + radiusUnits && va.Max.x >= ea.Min.x - radiusUnits &&
                    va.Min.y <= ea.Max.y + radiusUnits && va.Max.y >= ea.Min.y - radiusUnits &&
                    va.Min.z <= ea.Max.z + radiusUnits && va.Max.z >= ea.Min.z - radiusUnits)
                    return true;
            }
            return false;
        }

        public static bool AreInFaceToFaceContact(KitchenElement a, KitchenElement b) =>
            FaceContacts.AreInFaceToFaceContact(a.GetFaces(), b.GetFaces(),
                Tolerance.ContactMm * AppConstants.MM_TO_UNITS);

        /// <summary>Фасад НАВЕШЕН на хозяина: либо стоит с ним гранью к грани,
        /// либо параллелен ему и так же хорошо перекрыт, но отстоит на монтажный
        /// зазор не больше <paramref name="mountGapMm"/> (см.
        /// <see cref="IFacadeHost.FacadeMountGapMm"/>).
        ///
        /// Третьей геометрии здесь нет: зазор меряет
        /// <see cref="FaceContacts.MinParallelGap"/> — та же функция, что
        /// кормит GAP-01, и те же гейты (параллельность, перекрытие не хуже
        /// несущего). При mountGapMm = 0 условие вырождается ровно в
        /// <see cref="AreInFaceToFaceContact"/>, поэтому ящик остаётся на
        /// прежней строгой проверке.</summary>
        public static bool AreFacadeMountable(KitchenElement host, KitchenElement facade,
            float mountGapMm)
        {
            if (host == null || facade == null) return false;
            if (AreInFaceToFaceContact(host, facade)) return true;
            if (mountGapMm <= Tolerance.ContactMm) return false;
            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            return FaceContacts.MinParallelGap(host.GetFaces(), facade.GetFaces(),
                contactDist, mountGapMm * AppConstants.MM_TO_UNITS) > 0f;
        }

        /// <summary>Пара деталей с «почти-касанием» по общему правилу [min, max].
        /// Зазор меряется СУММОЙ по парам встречных граней: торцевая грань фасада
        /// с одной стороны может касаться (0 мм), а с другой — висеть на
        /// миллиметры, и тогда пара «в целом» недожата или чрезмерно разнесена.
        /// <see cref="kind"/> отличает маленький зазор (GAP-01) от большого (GAP-02).</summary>
        public readonly struct NearContact
        {
            public readonly KitchenElement a;
            public readonly KitchenElement b;
            public readonly float gapMm;
            public readonly NearContactKind kind;
            public NearContact(KitchenElement a, KitchenElement b, float gapMm, NearContactKind kind)
            {
                this.a = a; this.b = b; this.gapMm = gapMm; this.kind = kind;
            }
        }

        public enum NearContactKind
        {
            /// <summary>Сумма зазоров по оси меньше минимума — снап не дожат.</summary>
            TooSmall,
            /// <summary>Сумма зазоров по оси больше максимума — снап не сработал.</summary>
            TooLarge,
        }

        /// <summary>Найти пары деталей, у которых сумма зазоров по оси ВЫХОДИТ
        /// за диапазон [minGapMm, maxGapMm]:
        /// • TooSmall → GAP-01 «требуется ≥2мм»;
        /// • TooLarge → GAP-02 «слишком большой зазор».
        /// Внутри [min, max] — зелёная зона, в отчёт не идёт.
        /// НЕ горячий путь: O(n²), вызывается окном анализа/MCP по требованию.
        /// Пары, уже стоящие face-to-face, отсекаются broad-фазой
        /// (AABBsIntersect с −BroadPhaseMm). Пара «прибор ↔ ЕГО фасад» для
        /// посудомойки пропускается: у неё своё правило (≥5мм, см.
        /// <see cref="FindDishwasherFacadeBackGaps"/>).</summary>
        public static List<NearContact> FindNearContacts(List<KitchenElement> all,
            float minGapMm, float maxGapMm)
        {
            var result = new List<NearContact>();
            if (all == null || all.Count < 2) return result;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            // Broad-phase радиус шире допустимого диапазона: нужно ловить и
            // GAP-02 (gap > maxGapMm), иначе снап «не сработал на 5мм» выпал бы
            // из кандидатов. Старый порог 8мм покрывает все «визуально мелкие
            // недолёты», при которых снап вообще мог ошибиться.
            float broadPhaseMm = Mathf.Max(maxGapMm, 8f);
            float broadPhase = broadPhaseMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;

            int n = all.Count;
            var geo = new ElementGeometry[n];
            var ok = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var e = all[i];
                if (e == null || IsAnchor(e) || IsIgnoredInPairs(e)) continue;
                ok[i] = true;
                geo[i] = e.ToGeometry();
            }

            for (int i = 0; i < n; i++)
            {
                if (!ok[i]) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (!ok[j]) continue;
                    // Broad-phase: коробки в пределах broadPhase друг от друга.
                    // margin в AABBsIntersect СУЖАЕТ перекрытие, поэтому
                    // расширяем отрицательным (−broadPhase) — так в кандидаты
                    // попадают и не пересекающиеся, но близкие пары.
                    if (!FaceContacts.AABBsIntersect(geo[i], geo[j], -broadPhase)) continue;
                    // Реально касаются гранями — это не «почти», а контакт.
                    if (FaceContacts.AreInFaceToFaceContact(geo[i].Faces, geo[j].Faces, contactDist)) continue;
                    // Вкладная ДВП, зашедшая в паз соседней детали: зазор между
                    // ГАБАРИТАМИ равен глубине захода в паз и НЕ является «почти
                    // касанием» — эту пару обслуживает логика посадки в паз
                    // (SEAT-01), а не GAP-01/02. Иначе полностью посаженная панель
                    // ложно краснела бы.
                    if (PanelEngagesGroove(all[i], all[j]) || PanelEngagesGroove(all[j], all[i])) continue;
                    // Пара «фасад посудомойки ↔ сам прибор» — отдельное правило
                    // (≥5мм, см. FindDishwasherFacadeBackGaps). ИНАЧЕ сумма по
                    // оси с зазором 5мм дала бы GAP-02, а пользователь этот
                    // зазор требует сам и светить «слишком большой» на нём —
                    // ложь.
                    if (IsDishwasherFacadePair(all[i], all[j])) continue;

                    float sum = FaceContacts.SumParallelGaps(geo[i].Faces, geo[j].Faces, contactDist, broadPhase);
                    if (sum <= 0f) continue;
                    float sumMm = sum * toMm;
                    // Эпсилон 0.01мм: позиция и нормали в float дают шум ~1e-5,
                    // на 5мм-позициях это 0.000005мм — без него идеально
                    // выставленные границы [2..4] ложно сваливаются в GAP.
                    if (sumMm + 0.01f < minGapMm)
                        result.Add(new NearContact(all[i], all[j], sumMm, NearContactKind.TooSmall));
                    else if (sumMm > maxGapMm + 0.01f)
                        result.Add(new NearContact(all[i], all[j], sumMm, NearContactKind.TooLarge));
                }
            }
            return result;
        }

        /// <summary>Пара «посудомойка ↔ ЕЁ пристёгнутый фасад», подлежащая
        /// отдельной проверке «зазор сзади ≥ 5мм». Возвращает true только если
        /// один из элементов — DishwasherElement, второй — его FacadeElement по
        /// имени; чужой фасад рядом с прибором сюда не попадает.</summary>
        private static bool IsDishwasherFacadePair(KitchenElement a, KitchenElement b)
        {
            KitchenElement? dishwasher = null;
            KitchenElement? facade = null;
            if (a is DishwasherElement && b is FacadeElement) { dishwasher = a; facade = b; }
            else if (b is DishwasherElement && a is FacadeElement) { dishwasher = b; facade = a; }
            if (dishwasher == null || facade == null) return false;
            return !string.IsNullOrEmpty(((DishwasherElement)dishwasher).AttachedFacadeName)
                && ((DishwasherElement)dishwasher).AttachedFacadeName == ((FacadeElement)facade).PartName;
        }

        /// <summary>Результат проверки заднего зазора навесного фасада прибора.
        /// Возвращается только когда фасад есть и зазор меньше монтажного
        /// минимума — пара «фасад оторвался от монтажа».</summary>
        public readonly struct DishwasherBackGapIssue
        {
            public readonly DishwasherElement dishwasher;
            public readonly FacadeElement facade;
            public readonly float gapMm;
            public DishwasherBackGapIssue(DishwasherElement dishwasher, FacadeElement facade, float gapMm)
            {
                this.dishwasher = dishwasher;
                this.facade = facade;
                this.gapMm = gapMm;
            }
        }

        /// <summary>Для каждой посудомойки с ПРИСТЁГНУТЫМ фасадом: зазор между
        /// передней гранью бака и тыльной гранью фасада должен быть не меньше
        /// монтажного (<see cref="DishwasherElement.FACADE_MOUNT_GAP_MM"/>). Если
        /// меньше — фасад фактически прижат к прибору, а должен висеть на
        /// кронштейнах с зазором. Общий GAP-01/02 к этой паре не применяется —
        /// правило своё, отменяет зелёную зону [2,4].</summary>
        public static List<DishwasherBackGapIssue> FindDishwasherFacadeBackGaps(List<KitchenElement> all)
        {
            var result = new List<DishwasherBackGapIssue>();
            if (all == null) return result;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float mountMm = DishwasherElement.FACADE_MOUNT_GAP_MM;
            float maxGap = mountMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;

            foreach (var e in all)
            {
                if (!(e is DishwasherElement dw)) continue;
                var facade = dw.FindAttachedFacade();
                if (facade == null) continue;

                // Суммируем зазоры по ВСТРЕЧНЫМ граням в диапазоне (0, mountMm].
                // Если сумма < mountMm — фасад прижат ближе, чем схема требует.
                // Меряем по ЗАКРЫТОЙ позе фасада: у откинутой дверцы он уехал
                // вместе с ней, и монтажный зазор по нему не считается (та же
                // причина, что у DWH-02, см. DrawerLinks.WithFacadeClosed).
                float sum = DrawerLinks.WithFacadeClosed(facade, DrawerLinks.IsFacadeDisplacedBy(dw),
                    () => FaceContacts.SumParallelGaps(e.GetFaces(), facade.GetFaces(), contactDist, maxGap));
                float sumMm = sum * toMm;
                if (sumMm <= 0f) continue;
                // Эпсилон 0.01мм — float-шум позиции и граней (см. тест
                // DishwasherFacadeBackGap_AtFive: идеальная 5мм позиция
                // SumParallelGaps отдаёт как 4.999995). Без него нижняя граница
                // 5мм по схеме светила бы DWH-04 при идеально выставленной
                // посудомойке.
                if (sumMm + 0.01f < mountMm)
                    result.Add(new DishwasherBackGapIssue(dw, facade, sumMm));
            }
            return result;
        }

        /// <summary>Посудомойка стоит не на своём месте по ВЫСОТЕ: либо под её
        /// подошвой нет ничего (висит), либо подошва провалилась внутрь опоры
        /// (утоплена в пол). <see cref="blocker"/> заполнен только во втором
        /// случае — это та деталь, в которую машина въехала.</summary>
        public readonly struct DishwasherSupportIssue
        {
            public readonly DishwasherElement dishwasher;
            public readonly KitchenElement? blocker;
            public readonly float sinkMm;
            public DishwasherSupportIssue(DishwasherElement dishwasher, KitchenElement? blocker, float sinkMm)
            {
                this.dishwasher = dishwasher;
                this.blocker = blocker;
                this.sinkMm = sinkMm;
            }
        }

        /// <summary>Посудомойки, у которых под подошвой НЕТ опоры — DWH-05.
        ///
        /// Зачем отдельное правило, а не общий COL-01/COL-02. Объём валидации
        /// машины — только бак, он начинается на <c>BASE_HEIGHT_MM</c> выше
        /// подошвы (см. <see cref="DishwasherElement.EffectiveScale"/>), и эта
        /// полоса отдана мебели специально: туда встают цоколь и ножки модулей.
        /// Поэтому пол коробку валидации не касается — «висит в воздухе» на
        /// машину не срабатывает, а провалиться в пол она может целиком на 90 мм
        /// и ни одной ошибки не получить. Ровно на это и жаловались.
        ///
        /// Правило поэтому меряет ПОДОШВУ, а не объём: любая деталь, чей верх
        /// лежит под подошвой и перекрывается с прибором в плане, — законная
        /// опора (пол, цоколь, поддон, доска — всё равно). Пересёк подошву
        /// насквозь — машина в него утоплена.
        ///
        /// ЗАЗОР ДО ОПОРЫ допускается до <see cref="DishwasherElement.FEET_ADJUST_MM"/>
        /// (60 мм) — это не допуск «на глазок», а ход регулируемых ножек:
        /// паспортная высота 815–875 набирается именно ими, а ножек в модели
        /// нет. Реальная кухня из docs/example.save.json ровно это и делает:
        /// верх машины подведён под столешницу 820, и корпус висит на 4.5 мм —
        /// физически он стоит на выкрученных ножках. Выше хода ножек висеть уже
        /// не на чем.</summary>
        public static List<DishwasherSupportIssue> FindDishwasherSupportIssues(List<KitchenElement> all)
        {
            var result = new List<DishwasherSupportIssue>();
            if (all == null) return result;

            float eps = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;
            float reach = DishwasherElement.FEET_ADJUST_MM * AppConstants.MM_TO_UNITS;

            foreach (var e in all)
            {
                if (!(e is DishwasherElement dw)) continue;

                float soleY = dw.SoleCenterWorld.y;
                var dwGeo = dw.ToGeometry();
                var facade = dw.FindAttachedFacade();

                KitchenElement? blocker = null;
                float deepest = 0f;
                bool supported = false;

                foreach (var other in all)
                {
                    if (other == null || ReferenceEquals(other, e)) continue;
                    // Свой фасад висит на кронштейнах перед прибором и опорой
                    // ему не является; светильник — декор без физики.
                    if (ReferenceEquals(other, facade)) continue;
                    if (other is LightSourceElement) continue;

                    var g = other.ToGeometry();
                    // Перекрытие В ПЛАНЕ: соседний шкаф касается прибора боком,
                    // но под ним не стоит — касание опорой не считается.
                    if (g.Min.x >= dwGeo.Max.x - eps || g.Max.x <= dwGeo.Min.x + eps) continue;
                    if (g.Min.z >= dwGeo.Max.z - eps || g.Max.z <= dwGeo.Min.z + eps) continue;

                    // Опора: верх детали от подошвы до хода ножек ниже неё.
                    if (g.Max.y <= soleY + eps && g.Max.y >= soleY - reach - eps)
                    {
                        supported = true;
                        break;
                    }

                    // Деталь пересекает плоскость подошвы насквозь — прибор в
                    // неё утоплен. Берём самое глубокое погружение.
                    if (g.Min.y < soleY - eps && g.Max.y > soleY + eps)
                    {
                        float sink = (g.Max.y - soleY) * toMm;
                        if (sink > deepest) { deepest = sink; blocker = other; }
                    }
                }

                if (supported) continue;
                result.Add(new DishwasherSupportIssue(dw, blocker, deepest));
            }
            return result;
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

        /// <summary>Найти вкладные панели, которые НЕ дошли до дна паза (зашли менее
        /// чем на половину его глубины). Пользователь такую щель между кромкой и дном
        /// не видит, а конструктивно панель держится плохо. НЕ горячий путь.</summary>
        public static List<UnseatedPanel> FindUnseatedPanels(List<KitchenElement> all)
        {
            var result = new List<UnseatedPanel>();
            if (all == null || all.Count < 2) return result;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float engageMargin = GrooveSeating.PanelEngageMarginMm * AppConstants.MM_TO_UNITS;
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

                    float depthUnits = GrooveDepthUnits(b);
                    if (depthUnits <= 0f) continue;

                    foreach (var seat in seats)
                    {
                        if (!GrooveSeating.PanelEngagesSeat(pverts, seat, depthUnits, engageMargin,
                                contactDist, out float minAlong))
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

        /// <summary>Панель-ДВП <paramref name="panel"/> зашла (хотя бы устьем) в один
        /// из пазов детали <paramref name="board"/>. Критерий тот же, что у SEAT-01
        /// (<see cref="GrooveSeating.PanelEngagesSeat"/>): такую пару нельзя выдавать
        /// как near-contact/GAP-01 — зазор между их габаритами равен глубине захода в
        /// паз, а недосадку до дна отдельно ловит SEAT-01.</summary>
        private static bool PanelEngagesGroove(KitchenElement panel, KitchenElement board)
        {
            if (!(panel is PanelElement) || board == null || board.Grooves.Count == 0) return false;

            var seats = board.GetGrooveSeatFaces();
            if (seats.Length == 0) return false;

            float depthUnits = GrooveDepthUnits(board);
            if (depthUnits <= 0f) return false;

            float contactDist = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            float engageMargin = GrooveSeating.PanelEngageMarginMm * AppConstants.MM_TO_UNITS;

            var pverts = panel.GetVertices();
            foreach (var seat in seats)
                if (GrooveSeating.PanelEngagesSeat(pverts, seat, depthUnits, engageMargin, contactDist, out _))
                    return true;
            return false;
        }

        private static float GrooveDepthUnits(KitchenElement board) =>
            GrooveMesh.DepthFraction(board.DimensionsMM) * board.transform.localScale.z;

        private static bool IsAnchor(KitchenElement e) =>
            e != null && (e.GetComponent<BasePlate>() != null || e.GetComponent<Wall>() != null
                || e is WindowElement || e is DoorElement || e is FloorElement);

        /// <summary>Деталь не участвует в парных проверках: светильник — декор,
        /// врезная техника по конструкции сидит в столешнице.</summary>
        private static bool IsIgnoredInPairs(KitchenElement e) =>
            e is LightSourceElement || e is SinkElement || e is CooktopElement;
    }
}
