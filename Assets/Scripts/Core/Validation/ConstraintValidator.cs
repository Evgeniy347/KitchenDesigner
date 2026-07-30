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
            ValidationCore.AreInFaceToFaceContact(a.GetFaces(), b.GetFaces(),
                Tolerance.ContactMm * AppConstants.MM_TO_UNITS);

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
                    // Broad-phase: коробки в пределах maxGap друг от друга. margin
                    // в AABBsIntersect СУЖАЕТ перекрытие, поэтому расширяем
                    // отрицательным (−maxGap) — так в кандидаты попадают и не
                    // пересекающиеся, но близкие пары.
                    if (!ValidationCore.AABBsIntersect(geo[i], geo[j], -maxGap)) continue;
                    // Реально касаются гранями — это не «почти», а контакт.
                    if (ValidationCore.AreInFaceToFaceContact(geo[i].Faces, geo[j].Faces, contactDist)) continue;
                    // Вкладная ДВП, зашедшая в паз соседней детали: зазор между
                    // ГАБАРИТАМИ равен глубине захода в паз и НЕ является «почти
                    // касанием» — эту пару обслуживает логика посадки в паз
                    // (SEAT-01), а не GAP-01. Иначе полностью посаженная панель
                    // ложно краснела бы как «почти касается».
                    if (PanelEngagesGroove(all[i], all[j]) || PanelEngagesGroove(all[j], all[i])) continue;

                    float gap = ValidationCore.MinParallelGap(geo[i].Faces, geo[j].Faces, contactDist, maxGap);
                    if (gap > 0f)
                        result.Add(new NearContact(all[i], all[j], gap * toMm));
                }
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
            float engageMargin = ValidationCore.PanelEngageMarginMm * AppConstants.MM_TO_UNITS;
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
                        if (!ValidationCore.PanelEngagesSeat(pverts, seat, depthUnits, engageMargin,
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
        /// (<see cref="ValidationCore.PanelEngagesSeat"/>): такую пару нельзя выдавать
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
            float engageMargin = ValidationCore.PanelEngageMarginMm * AppConstants.MM_TO_UNITS;

            var pverts = panel.GetVertices();
            foreach (var seat in seats)
                if (ValidationCore.PanelEngagesSeat(pverts, seat, depthUnits, engageMargin, contactDist, out _))
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
