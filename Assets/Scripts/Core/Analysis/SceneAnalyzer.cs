using System.Collections.Generic;

namespace KitchenDesigner.Core.Analysis
{
    /// <summary>
    /// Сборщик проблем сцены для окна анализа ошибок и MCP. Errors — коллизии
    /// (<see cref="ConstraintValidator"/>) и неверная геометрия установки
    /// (DWH-05: посудомойке не на чем стоять). Warnings — потенциальные дефекты
    /// сборки: почти-касания (недожатый снэп), зазоры фасада, ящик без фасада.
    /// Новый источник = новый Collect-метод + коды в <see cref="IssueCatalog"/>.
    /// Предупреждения НЕ подсвечиваются на сцене — только этот список.
    /// </summary>
    public static class SceneAnalyzer
    {
        /// <summary>Минимум «общего зазора по оси» (мм): сумма зазоров по
        /// встречным граням меньше этого — GAP-01 «требуется ≥2мм». Пара
        /// «прибор ↔ фасад» для посудомойки проверяется своим правилом, см.
        /// <see cref="DishwasherBackGapMinMm"/>.</summary>
        public const float NearContactMinGapMm = 2f;

        /// <summary>Максимум «общего зазора по оси» (мм): сумма зазоров по
        /// встречным граням больше этого — GAP-02 «слишком большой». 4мм
        /// совпадает с верхней границей монтажного зазора обычных навесок
        /// (ящик, фасад); свыше — снап не дотянул.</summary>
        public const float NearContactMaxGapMm = 4f;

        /// <summary>Минимальный монтажный зазор сзади между корпусом посудомойки
        /// и пристёгнутым к ней фасадом, мм. Соответствует
        /// <see cref="DishwasherElement.FACADE_MOUNT_GAP_MM"/>.</summary>
        public const float DishwasherBackGapMinMm = 5f;

        /// <summary>Минимальный технологический зазор фасада с каждой стороны, мм.</summary>
        public const int FacadeMinGapMm = 1;

        public static List<AnalysisIssue> Analyze()
        {
            var issues = new List<AnalysisIssue>();
            var all = PartRegistry.GetAll();
            if (all == null || all.Count == 0) return issues;

            CollectCollisions(all, issues);
            CollectEdgeCover(all, issues);
            CollectNearContacts(all, issues);
            CollectDishwasherFacadeBackGaps(all, issues);
            CollectDishwasherSupport(all, issues);
            CollectPanelSeating(all, issues);
            CollectFacadeGaps(all, issues);
            CollectDrawerFacadeLinks(all, issues);
            CollectAttachLinks(all, issues);
            CollectDishwasherFacadeLinks(all, issues);
            return issues;
        }

        // ── Errors: коллизии ─────────────────────────────────────────────
        private static void CollectCollisions(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            var result = ConstraintValidator.Validate(all);
            if (result.diagnostics == null) return;
            foreach (var diag in result.diagnostics)
                issues.Add(IssueCatalog.FromViolation(diag));
        }

        // ── Error: торец под кромку перекрыт частично ────────────────────
        // Кромку клеят на весь торец: если сосед закрывает его наполовину,
        // деталь либо не встанет на место (кромка мешает), либо кромка
        // оборвётся посередине. Полностью закрытый торец — норма (кромки нет),
        // полностью открытый — норма (кромка есть); ошибка ровно посередине.
        private static void CollectEdgeCover(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            // Планка или царга задевает торец на пару процентов — это нормальная
            // конструкция, а не наезд на кромку. Порог настраивается («Нижний
            // порог кромки»), 0 — сообщать о любом перекрытии.
            var settings = KitchenSettings.Instance;
            float minRatio = (settings != null ? settings.EdgePartialThresholdPct : 0) / 100f;

            foreach (var e in all)
            {
                if (e == null || !e.EdgeBandingEnabled) continue;

                var coverage = EdgeBanding.Coverage(e, all);
                var sides = new List<string>();

                // Виновник — сосед, накрывший торец больше всех. Торцов может быть
                // испорчено несколько, а вторая деталь в отчёте одна: берём того,
                // у кого площадь перекрытия максимальна по всем сторонам.
                KitchenElement? culprit = null;
                float culpritArea = 0f;

                foreach (EdgeSide side in System.Enum.GetValues(typeof(EdgeSide)))
                {
                    // Ручную сторону пользователь взял на себя: геометрия про неё
                    // больше не спорит.
                    if (e.IsEdgeManual(side)) continue;
                    if (!coverage.IsPartial(side)) continue;
                    if (coverage.Ratio(side) < minRatio) continue;
                    sides.Add($"{side} {coverage.Ratio(side) * 100f:F0}%");

                    var coverer = EdgeBanding.DominantCoverer(e, all, side, out float area);
                    if (coverer == null || area <= culpritArea) continue;
                    culprit = coverer;
                    culpritArea = area;
                }

                if (sides.Count > 0)
                    issues.Add(IssueCatalog.EdgePartialCover(e, string.Join(", ", sides), culprit));
            }
        }

        // ── Warning: почти касание (общий зазор по оси вне [2..4]) ───────
        private static void CollectNearContacts(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var nc in ConstraintValidator.FindNearContacts(all, NearContactMinGapMm, NearContactMaxGapMm))
            {
                if (nc.kind == ConstraintValidator.NearContactKind.TooSmall)
                    issues.Add(IssueCatalog.NearContact(nc.a, nc.b, nc.gapMm));
                else
                    issues.Add(IssueCatalog.NearContactFar(nc.a, nc.b, nc.gapMm));
            }
        }

        // ── Warning: задний зазор фасада посудомойки меньше 5мм ─────────
        // Пара «посудомойка ↔ ЕЁ фасад» из общего правила GAP-01/02
        // исключена (FindNearContacts пропускает её), а здесь проверяется
        // отдельно: монтажный минимум 5мм обязателен, иначе кронштейны
        // схемы прибора не работают (дверца не откинется).
        private static void CollectDishwasherFacadeBackGaps(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var d in ConstraintValidator.FindDishwasherFacadeBackGaps(all))
                issues.Add(IssueCatalog.DishwasherFacadeBackGap(d.dishwasher, d.facade, d.gapMm));
        }

        // ── Error: посудомойке не на чем стоять ─────────────────────────
        // Прибор обязан опираться подошвой на пол, цоколь или любую деталь.
        // Общий COL-02 «висит в воздухе» его не ловит: объём валидации машины
        // начинается выше подошвы (цокольная полоса отдана мебели), поэтому и
        // «висит», и «провалилась в пол» проходили молча.
        private static void CollectDishwasherSupport(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var s in ConstraintValidator.FindDishwasherSupportIssues(all))
                issues.Add(s.blocker != null
                    ? IssueCatalog.DishwasherSunk(s.dishwasher, s.blocker, s.sinkMm)
                    : IssueCatalog.DishwasherNoSupport(s.dishwasher));
        }

        // ── Warning: вкладная панель (ДВП) не дошла до дна паза ──────────
        private static void CollectPanelSeating(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var u in ConstraintValidator.FindUnseatedPanels(all))
                issues.Add(IssueCatalog.PanelNotSeated(u.panel, u.board, u.insertionMm, u.depthMm));
        }

        // ── Warning: зазоры фасада < минимума ────────────────────────────
        private static void CollectFacadeGaps(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is FacadeElement f)) continue;
                var bad = new List<string>();
                if (f.GapLeft < FacadeMinGapMm) bad.Add($"слева {f.GapLeft}");
                if (f.GapRight < FacadeMinGapMm) bad.Add($"справа {f.GapRight}");
                if (f.GapTop < FacadeMinGapMm) bad.Add($"сверху {f.GapTop}");
                if (f.GapBottom < FacadeMinGapMm) bad.Add($"снизу {f.GapBottom}");
                if (bad.Count > 0)
                    issues.Add(IssueCatalog.FacadeGap(f, string.Join(", ", bad)));
            }
        }

        // ── Warning: ящик без ссылки на фасад или фасад оторвался ─────────
        private static void CollectDrawerFacadeLinks(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is DrawerElement d)) continue;
                if (string.IsNullOrEmpty(d.AttachedFacadeName))
                {
                    // Верхний ящик двойной пары штатно без своего фасада — фасад у нижнего.
                    if (d.IsUpperDrawer && d.IsDouble) continue;
                    issues.Add(IssueCatalog.DrawerNoFacade(d));
                    continue;
                }
                // Фасад указан, но физического контакта нет — ящик или фасад сдвинули.
                var facade = FindFacade(all, d.AttachedFacadeName);
                if (facade != null && !DrawerLinks.IsFacadeInContact(d, facade))
                    issues.Add(IssueCatalog.DrawerFacadeOrphaned(d, facade));
            }
        }

        // ── Warning: фасад посудомоечной машины ──────────────────────────
        // Машина полновстраиваемая: без пристёгнутого фасада на кухне зияет
        // дыра, а фасад не той высоты не сходится с цоколем. Всё это
        // ПРЕДУПРЕЖДЕНИЯ, а не запреты: пользователь вправе собирать кухню в
        // любом порядке и доводить размеры потом — отказ на полпути сборки
        // только мешал бы (так же ведут себя зазоры фасада, FAC-01).
        private static void CollectDishwasherFacadeLinks(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (!(e is DishwasherElement dw)) continue;
                if (string.IsNullOrEmpty(dw.AttachedFacadeName))
                {
                    issues.Add(IssueCatalog.DishwasherNoFacade(dw));
                    continue;
                }

                var facade = FindFacade(all, dw.AttachedFacadeName);
                if (facade == null)
                {
                    // Фасад удалили, а имя осталось. У ящика этот случай
                    // молчал (DRW-02 требует найденный фасад) — здесь он
                    // сообщается: у машины фасад один и он обязателен, так что
                    // «ссылка есть, фасада нет» это уже дефект сборки.
                    issues.Add(IssueCatalog.DishwasherFacadeMissing(dw, dw.AttachedFacadeName));
                    continue;
                }

                if (!DrawerLinks.IsFacadeInContact(dw, facade))
                    issues.Add(IssueCatalog.DishwasherFacadeOrphaned(dw, facade));

                int facadeHeight = facade.DimensionsMM.y;
                if (!DishwasherElement.IsFacadeHeightValid(facadeHeight))
                    issues.Add(IssueCatalog.DishwasherFacadeHeight(dw, facade, facadeHeight));
            }
        }

        // ── Error: прикреплённая деталь отошла от родителя ───────────────
        // Прикрепление означает «стоят вплотную»: дно прикручено к фасаду,
        // задняя стенка — ко дну. Появился зазор — сборка разъехалась, и
        // анимация открывания растащит её ещё дальше. Это ОШИБКА, а не
        // предупреждение: связь объявил сам пользователь, и геометрия ей
        // противоречит.
        //
        // Пропавший родитель ошибкой НЕ считается: удаление родителя отцепляет
        // детей (см. AttachLinks.Parent), и жаловаться там не на что.
        private static void CollectAttachLinks(List<KitchenElement> all, List<AnalysisIssue> issues)
        {
            foreach (var e in all)
            {
                if (e == null || string.IsNullOrEmpty(e.AttachedToName)) continue;
                var parent = AttachLinks.Parent(e);
                if (parent == null) continue;
                if (!AttachLinks.InContact(e, parent))
                    issues.Add(IssueCatalog.AttachDetached(e, parent));
            }
        }

        private static FacadeElement? FindFacade(List<KitchenElement> all, string name)
        {
            foreach (var e in all)
                if (e is FacadeElement f && f.PartName == name)
                    return f;
            return null;
        }
    }

    /// <summary>Каталог кодов: единственный источник «причина → код + уровень +
    /// текст». Коды стабильны (на них завязан фильтр по кодам) — только добавляются.
    /// Errors: COL-xx (коллизии). Warnings: GAP-xx (зазор), FAC-xx (фасад),
    /// DRW-xx (ящик), DWH-xx (посудомоечная машина).</summary>
    public static class IssueCatalog
    {
        // Коллизии геометрии (ConstraintValidator).
        public const string CodeOverlap = "COL-01";
        public const string CodeUnsupported = "COL-02";
        public const string CodeOutOfWallBounds = "COL-03";
        public const string CodeEdgePartialCover = "EDG-01";
        // Предупреждения.
        public const string CodeNearContact = "GAP-01";
        public const string CodeNearContactFar = "GAP-02";
        public const string CodePanelNotSeated = "SEAT-01";
        public const string CodeFacadeGap = "FAC-01";
        public const string CodeDrawerNoFacade = "DRW-01";
        public const string CodeDrawerFacadeOrphaned = "DRW-02";
        public const string CodeDishwasherNoFacade = "DWH-01";
        public const string CodeDishwasherFacadeOrphaned = "DWH-02";
        public const string CodeDishwasherFacadeHeight = "DWH-03";
        public const string CodeDishwasherBackGap = "DWH-04";
        /// <summary>Единственный DWH с уровнем Error: «не на чем стоять» — это
        /// не недоделанная сборка, а неверная геометрия.</summary>
        public const string CodeDishwasherNoSupport = "DWH-05";

        /// <summary>Прикреплённая деталь отошла от родителя (см.
        /// <see cref="AttachLinks"/>).</summary>
        public const string CodeAttachDetached = "ATT-01";


        public static AnalysisIssue FromViolation(ContactViolation v)
        {
            switch (v.kind)
            {
                case ViolationKind.Overlap:
                    return new AnalysisIssue(IssueLevel.Error, CodeOverlap,
                        PairDetail(v.element, v.other), "Детали пересекаются в объёме",
                        v.element, v.other);

                case ViolationKind.Unsupported:
                    return new AnalysisIssue(IssueLevel.Error, CodeUnsupported,
                        Name(v.element), "Деталь не имеет опоры — висит в воздухе",
                        v.element);

                case ViolationKind.OutOfWallBounds:
                    return new AnalysisIssue(IssueLevel.Error, CodeOutOfWallBounds,
                        Name(v.element), "Элемент выходит за габарит стены",
                        v.element);

                default:
                    return new AnalysisIssue(IssueLevel.Error, "COL-00",
                        Name(v.element), "Нарушение геометрии", v.element);
            }
        }

        /// <summary>culprit — сосед, накрывший торец больше всех; может быть null,
        /// если перекрытие дают только якоря вроде пола или стены.</summary>
        public static AnalysisIssue EdgePartialCover(KitchenElement element, string sides,
            KitchenElement? culprit = null) =>
            culprit != null
                // Пара в колонке «Деталь» — как у GAP-01/COL-01: по клику
                // ErrorPanelUI выделяет обе детали, и виновник виден в сцене.
                ? new AnalysisIssue(IssueLevel.Error, CodeEdgePartialCover,
                    PairDetail(element, culprit),
                    $"Торец под кромку перекрыт частично: {sides}", element, culprit)
                : new AnalysisIssue(IssueLevel.Error, CodeEdgePartialCover,
                    Name(element), $"Торец под кромку перекрыт частично: {sides}", element);

        public static AnalysisIssue NearContact(KitchenElement a, KitchenElement b, float gapMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodeNearContact,
                PairDetail(a, b),
                $"Зазор по оси {gapMm:F1} мм — требуется ≥{SceneAnalyzer.NearContactMinGapMm:F0} мм (нет прямого контакта)",
                a, b);

        public static AnalysisIssue NearContactFar(KitchenElement a, KitchenElement b, float gapMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodeNearContactFar,
                PairDetail(a, b),
                $"Зазор по оси {gapMm:F1} мм — слишком большой (допустимо ≤{SceneAnalyzer.NearContactMaxGapMm:F0} мм)",
                a, b);

        /// <summary>Задний зазор между фасадом и корпусом посудомойки меньше
        /// монтажного минимума: фасад прижат к прибору, а должен висеть на
        /// кронштейнах. Свой код DWH-04, GAP-01/02 к этой паре не относится.</summary>
        public static AnalysisIssue DishwasherFacadeBackGap(KitchenElement dishwasher, KitchenElement facade,
            float gapMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherBackGap,
                PairDetail(dishwasher, facade),
                $"Задний зазор фасада {gapMm:F1} мм — требуется ≥{SceneAnalyzer.DishwasherBackGapMinMm:F0} мм",
                dishwasher, facade);

        public static AnalysisIssue PanelNotSeated(KitchenElement panel, KitchenElement board,
            float insertionMm, float depthMm) =>
            new AnalysisIssue(IssueLevel.Warning, CodePanelNotSeated,
                $"{Name(panel)} ↔ {Name(board)}",
                $"Панель вошла в паз не до дна: {insertionMm:F1} из {depthMm:F1} мм",
                panel, board);

        public static AnalysisIssue FacadeGap(KitchenElement facade, string sides) =>
            new AnalysisIssue(IssueLevel.Warning, CodeFacadeGap,
                Name(facade), $"Зазор фасада меньше {SceneAnalyzer.FacadeMinGapMm} мм: {sides}",
                facade);

        public static AnalysisIssue DrawerNoFacade(KitchenElement drawer) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDrawerNoFacade,
                Name(drawer), "Ящик без ссылки на фасад", drawer);

        public static AnalysisIssue DrawerFacadeOrphaned(KitchenElement drawer, KitchenElement? facade) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDrawerFacadeOrphaned,
                PairDetail(drawer, facade),
                $"Фасад ящика не на месте — {Name(drawer)} и {Name(facade)} не в контакте",
                drawer, facade);

        public static AnalysisIssue DishwasherNoFacade(KitchenElement dishwasher) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherNoFacade,
                Name(dishwasher), "Посудомойка без фасада — прибор полновстраиваемый, лица у него нет",
                dishwasher);

        /// <summary>Имя фасада есть, а самого фасада в сцене нет — его удалили.
        /// Пара для отчёта здесь одна: показывать нечего, кроме машины.</summary>
        public static AnalysisIssue DishwasherFacadeMissing(KitchenElement dishwasher, string facadeName) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeOrphaned,
                Name(dishwasher), $"Фасад «{facadeName}» удалён — посудомойка осталась без лица",
                dishwasher);

        public static AnalysisIssue DishwasherFacadeOrphaned(KitchenElement dishwasher, KitchenElement? facade) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeOrphaned,
                PairDetail(dishwasher, facade),
                $"Фасад посудомойки не на месте — {Name(dishwasher)} и {Name(facade)} не в контакте",
                dishwasher, facade);

        /// <summary>Под подошвой прибора пусто — машина стоит в воздухе.</summary>
        public static AnalysisIssue DishwasherNoSupport(KitchenElement dishwasher) =>
            new AnalysisIssue(IssueLevel.Error, CodeDishwasherNoSupport,
                Name(dishwasher),
                "Посудомойке не на чем стоять — под низом нужен пол, цоколь или опорная деталь",
                dishwasher);

        /// <summary>Подошва прибора ушла ВНУТРЬ опоры — машина провалилась в
        /// пол (или в ту деталь, на которой должна стоять).</summary>
        public static AnalysisIssue DishwasherSunk(KitchenElement dishwasher, KitchenElement? blocker,
            float sinkMm) =>
            new AnalysisIssue(IssueLevel.Error, CodeDishwasherNoSupport,
                PairDetail(dishwasher, blocker),
                $"Посудомойка утоплена в {Name(blocker)} на {sinkMm:F0} мм — низ прибора должен стоять на опоре",
                dishwasher, blocker);

        public static AnalysisIssue DishwasherFacadeHeight(KitchenElement dishwasher,
            KitchenElement? facade, int facadeHeightMM) =>
            new AnalysisIssue(IssueLevel.Warning, CodeDishwasherFacadeHeight,
                PairDetail(dishwasher, facade),
                $"Высота фасада {facadeHeightMM} мм вне диапазона "
                + $"{DishwasherElement.FACADE_MIN_HEIGHT_MM}–{DishwasherElement.FACADE_MAX_HEIGHT_MM} мм: "
                + $"цоколь получится {DishwasherElement.PlinthForFacade(facadeHeightMM)} мм "
                + $"(допустимо {DishwasherElement.PLINTH_MIN_MM}–{DishwasherElement.PLINTH_MAX_MM})",
                dishwasher, facade);

        /// <summary>Связь есть, контакта нет — деталь и её родитель разъехались.</summary>
        public static AnalysisIssue AttachDetached(KitchenElement child, KitchenElement parent) =>
            new AnalysisIssue(IssueLevel.Error, CodeAttachDetached,
                PairDetail(child, parent),
                $"Прикреплённая деталь отошла от родителя — {Name(child)} и {Name(parent)} не в контакте",
                child, parent);

        private static string Name(KitchenElement? e) =>
            e != null && !string.IsNullOrEmpty(e.PartName) ? e.PartName : "—";

        private static string PairDetail(KitchenElement? a, KitchenElement? b) =>
            b != null ? $"{Name(a)} ↔ {Name(b)}" : Name(a);
    }
}
