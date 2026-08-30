using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Встраиваемая посудомоечная машина Bosch SMV25EX02E — третья готовая модель
    /// группы «Техника» (docs/APPLIANCES-BRIEF.md §3). Габариты заданы
    /// производителем и не редактируются (<see cref="IFixedSizeElement"/>): поля
    /// Ш/В/Г в окне свойств серые, ручек ресайза нет, edit_elements правку
    /// размера отклоняет.
    ///
    /// ВОСЕМЬ параллелепипедов, все — дети при ЕДИНИЧНОМ масштабе корня. Бак —
    /// ПОЛЫЙ короб из пяти стенок (внутрь встают корзины):
    ///   0 «BodyBottom», 1 «BodyTop», 2 «BodyLeft», 3 «BodyRight», 4 «BodyBack».
    /// Под баком — собственное ОСНОВАНИЕ прибора:
    ///   5 «Base»         — нижние BASE_HEIGHT_MM, утопленные вглубь на
    ///                      BASE_SETBACK_MM (место для ног, см. ниже).
    /// Дверца — две коробки, которые ездят вместе:
    ///   6 «Door»         — полотно двери во всю переднюю плоскость бака;
    ///   7 «ControlPanel» — узкая чёрная полоса панели управления.
    ///
    /// ДВЕРЦА ОТКИДНАЯ, как у духовки (см. OvenElement): поворот вокруг НИЖНЕЙ
    /// горизонтальной кромки, 0° → 90°. Поворачивается не корень, а две дочерние
    /// коробки — поза валидации при открывании не двигается вовсе. ПРИСТЁГНУТЫЙ
    /// ФАСАД едет вместе с дверцей: он к ней и прикручен, и той же петлёй
    /// приводится в движение — фасад становится «пассажиром» (IsPassenger), его
    /// собственная анимация выключена, а <see cref="ApplyFacadePose"/> каждый
    /// кадр ставит его в позу, которую даёт вращение вокруг <see cref="HingeLocalMM"/>.
    ///
    /// НИЗ ПРИБОРА — ДВА РАЗНЫХ ОБЪЁМА, и путать их нельзя:
    ///   • собственное ОСНОВАНИЕ машины (<see cref="BASE_HEIGHT_MM"/> = 90) —
    ///     сплошное, стоит на полу и рисуется коробкой «Base». По глубине оно
    ///     утоплено на <see cref="BASE_SETBACK_MM"/> (100), чтобы к машине можно
    ///     было подойти вплотную — носки обуви уходят под фасад;
    ///   • передняя полоса перед основанием — НИША под мебельный цоколь
    ///     (<see cref="PLINTH_NICHE_MM"/> высотой, глубиной ровно в утопление).
    ///     Она пуста по построению: туда встают цоколь и ножки модулей, и в
    ///     объём валидации не входит (см. <see cref="EffectiveScale"/>).
    /// Сумма: 725 (дверца) + 90 (основание) = 815 габарита.
    ///
    /// Опору под собой машина ОБЯЗАНА иметь — пол, цоколь, любую деталь. Это
    /// проверяет DWH-05 (ConstraintValidator.FindDishwasherSupportIssues):
    /// без него прибор молча висел в воздухе или проваливался в пол, потому что
    /// объём валидации начинается выше подошвы.
    ///
    /// ГЛАВНОЕ ОТЛИЧИЕ от варочной и духовки: машина ПОЛНОВСТРАИВАЕМАЯ, своей
    /// фасадной панели у неё НЕТ. Лицо ей делает обычный мебельный фасад, который
    /// пользователь пристёгивает по имени — ровно как у ящика (<see cref="IFacadeHost"/>,
    /// <see cref="DrawerLinks.Rename"/>, <see cref="DrawerLinks.IsFacadeInContact"/>).
    /// Поэтому фасада в этой модели и нет: включить его сюда значило бы нарисовать
    /// прибору вторую переднюю плоскость поверх пристёгнутой.
    ///
    /// Как и духовка, машина НЕ врезная: не <see cref="IPartCutout"/>, ни к какой
    /// детали не привязана, проёмов не режет — обычный корпусный элемент со
    /// штатными снапами и валидацией.
    /// </summary>
    public class DishwasherElement : KitchenElement, IFixedSizeElement, IFacadeHost, IOpenable
    {

        public override string DisplayTypeName => MODEL;

        public bool IsClosedPose => !IsOpen && !IsAnimating;

        public string OpenActionLabel => IsOpen ? OpenLabels.CloseDoor : OpenLabels.OpenDoor;

        public void CycleOpenState() => ToggleOpen();
        /// <summary>Единственная модель — см. <see cref="ApplianceModels.All"/>.</summary>
        public const string MODEL = "Bosch SMV25EX02E";

        // ── Корпус ──────────────────────────────────────────────────────
        public const int BODY_WIDTH_MM = 598;
        public const int BODY_DEPTH_MM = 550;

        /// <summary>Диапазон высоты корпуса на регулируемых ножках.</summary>
        public const int HEIGHT_MIN_MM = 815;
        public const int HEIGHT_MAX_MM = 875;

        /// <summary>НОМИНАЛЬНАЯ высота корпуса — ножки завинчены до упора.
        ///
        /// Выбраны 815 (нижняя граница и номинал DNS), и это не «взяли первое
        /// число»: вся арифметика ниши сходится именно на границах диапазона.
        /// Фасад пристёгивается от верха цоколя до верха корпуса, значит
        /// «цоколь = высота корпуса − высота фасада». Подставляя границы:
        ///   815 − 725 = 90  = минимальный цоколь;
        ///   875 − 655 = 220 = максимальный цоколь.
        /// То есть диапазон фасада 655–725 и диапазон цоколя 90–220 — это ОДИН
        /// и тот же диапазон высоты, пересчитанный через корпус, и 815 стоит
        /// ровно на его нижнем конце. На номинальном фасаде 720 цоколь выходит
        /// 95 (а не минимальные 90) — фасад упирается в столешницу 820 с зазором
        /// 5 мм под ней, что для встраиваемой техники и нужно.
        ///
        /// Размер фиксирован, поэтому «настроить ножки» пользователь не может:
        /// у прибора берётся паспортный номинал, а подъём до 875 — дело монтажа,
        /// а не модели.</summary>
        public const int BODY_HEIGHT_MM = HEIGHT_MIN_MM;

        // ── Ниша ────────────────────────────────────────────────────────
        public const int NICHE_WIDTH_MM = 600;
        public const int NICHE_MIN_DEPTH_MM = 550;

        // ── Мебельный фасад (пристёгивается, в модель НЕ входит) ─────────
        /// <summary>Ширина фасада — по ширине ниши.</summary>
        public const int FACADE_WIDTH_MM = NICHE_WIDTH_MM;

        public const int FACADE_MIN_HEIGHT_MM = 655;
        public const int FACADE_MAX_HEIGHT_MM = 725;

        /// <summary>Номинал под столешницу 820 (720 фасада + 95 цоколя + 5 мм
        /// зазора под столешницей = 820).</summary>
        public const int FACADE_NOMINAL_HEIGHT_MM = 720;

        // ── Цоколь под фасадом ──────────────────────────────────────────
        public const int PLINTH_MIN_MM = 90;
        public const int PLINTH_MAX_MM = 220;

        /// <summary>Со схемы: ниша под МЕБЕЛЬНЫЙ цоколь 89 мм — это ПРОЁМ перед
        /// основанием прибора, а не высота самого основания (та —
        /// <see cref="BASE_HEIGHT_MM"/> = 90).
        ///
        /// РАСХОЖДЕНИЕ В БРИФЕ на 1 мм: та же схема подписывает цоколь «min 90».
        /// Спорить тут не о чем — 89 это проём ниши, 90 это цоколь, который в
        /// него встаёт, и лишний миллиметр съедает допуск. Ограничением берём
        /// 90 (<see cref="PLINTH_MIN_MM"/>): проверка на «цоколь мельче
        /// допустимого» должна опираться на сам цоколь, а не на его нишу.</summary>
        public const int PLINTH_NICHE_MM = 89;

        /// <summary>Со схемы: отступ низа корпуса от плоскости фасада, мм —
        /// цоколь стоит не заподлицо с фасадом, а утоплен под него. Модель
        /// (две коробки) этого не рисует; число нужно тому, кто ставит цоколь,
        /// и отдаётся через MCP.</summary>
        public const int PLINTH_SETBACK_MM = 53;

        /// <summary>Со схемы: выступ ножек вперёд, мм.</summary>
        public const int FEET_PROTRUSION_MM = 100;

        /// <summary>Ход регулируемых ножек, мм: вся разница между паспортными
        /// 815 и 875 набирается ими. Ножек в модели нет (габарит фиксирован на
        /// 815), поэтому ровно на столько корпус вправе висеть НАД своей опорой
        /// — этот зазор и есть выкрученные ножки. См. DWH-05.</summary>
        public const int FEET_ADJUST_MM = HEIGHT_MAX_MM - HEIGHT_MIN_MM;

        // ── Собственное основание прибора ───────────────────────────────
        /// <summary>Высота ОСНОВАНИЯ — нижней сплошной части прибора, на которой
        /// он стоит: габарит минус дверца, 815 − 725 = 90. Это ровно
        /// <see cref="PLINTH_MIN_MM"/>, и не случайно: фасад закрывает машину от
        /// верха до низа дверцы, а всё, что ниже, и есть цокольная зона.</summary>
        public const int BASE_HEIGHT_MM = BODY_HEIGHT_MM - FACADE_MAX_HEIGHT_MM;

        /// <summary>ГОРИЗОНТАЛЬНОЕ утопление основания вглубь от передней
        /// плоскости прибора, мм. Ради него основание и отделено от бака: перед
        /// машиной остаётся пустая полоса под носки обуви, и к столешнице можно
        /// встать вплотную. Совпадает с выступом ножек по схеме
        /// (<see cref="FEET_PROTRUSION_MM"/>) — это одно и то же число с двух
        /// сторон: насколько ножки вынесены вперёд от задней стенки, настолько
        /// же низ отступает от переда.</summary>
        public const int BASE_SETBACK_MM = FEET_PROTRUSION_MM;

        /// <summary>Глубина основания: 550 − 100 = 450.</summary>
        public const int BASE_DEPTH_MM = BODY_DEPTH_MM - BASE_SETBACK_MM;

        /// <summary>Высота БАКА — она же высота дверцы: габарит минус основание,
        /// 815 − 90 = 725. Ровно <see cref="FACADE_MAX_HEIGHT_MM"/>: самый
        /// высокий допустимый фасад закрывает дверцу один в один.</summary>
        public const int TANK_HEIGHT_MM = BODY_HEIGHT_MM - BASE_HEIGHT_MM;

        // ── Навеска мебельного фасада ───────────────────────────────────
        /// <summary>Монтажный зазор навески фасада, мм (см.
        /// <see cref="IFacadeHost.FacadeMountGapMm"/>).
        ///
        /// Фасад полновстраиваемой машины держат ДВА КРОНШТЕЙНА, привинченные к
        /// его тыльной стороне и заведённые в пазы двери, — он висит на них, а
        /// не прилегает к прибору, и по монтажной схеме обязан стоять с зазором,
        /// иначе дверь не откинется. Прямой контакт как у ящика (фронт короба
        /// стянут с фасадом винтами заподлицо) для такой навески — не критерий.
        ///
        /// Правило «фасад отстоит от корпуса на ≥FACADE_MOUNT_GAP_MM», а не
        /// «попадает в диапазон [FACADE_MOUNT_GAP_MM..]»: реальная навеска даёт
        /// 2–4 мм и пользователь по требованию схемы монтажа ОБЯЗАН выставить
        /// ≥5мм — в противном случае фасад считается оторванным (DWH-04). Эта
        /// проверка отменяет общий GAP-01/02 для конкретной пары
        /// «прибор ↔ его фасад», чтобы 5мм по общему правилу не светились как
        /// GAP-02 «слишком большой».</summary>
        public const float FACADE_MOUNT_GAP_MM = 5f;

        // ── Панель управления ───────────────────────────────────────────
        /// <summary>Высота чёрной полосы панели управления. На настоящей машине
        /// панель лежит на ВЕРХНЕМ ТОРЦЕ двери — при закрытой двери её видно
        /// только сверху, а сверху у встроенной машины столешница. Рисуем её
        /// самой верхней полосой переда: так прибор узнаётся, и «полновстраиваемая,
        /// органов управления на лице нет» не превращается в «лицо пустое».</summary>
        public const int CONTROL_PANEL_HEIGHT_MM = 14;

        /// <summary>Толщина накладной полосы: она лежит заподлицо с передней
        /// плоскостью корпуса, а не поверх неё, — иначе прибор вышел бы за
        /// собственный габарит.</summary>
        public const float OVERLAY_THICKNESS_MM = 2f;

        // ── Полый бак и дверца ──────────────────────────────────────────
        /// <summary>Толщина стенки полого бака. Число условное — снаружи её не
        /// видно, а внутри она задаёт полезный объём под корзины.</summary>
        public const int BODY_WALL_MM = 20;

        /// <summary>Толщина полотна двери. Дверь лежит В переднем проёме бака,
        /// заподлицо с его передней плоскостью, а не поверх неё, — иначе прибор
        /// вышел бы за собственный габарит.</summary>
        public const int DOOR_THICKNESS_MM = 20;

        /// <summary>Откидная дверца раскрывается ровно в горизонталь.</summary>
        public const float DOOR_OPEN_ANGLE_DEG = 90f;

        private const float OpenSeconds = 0.4f;

        /// <summary>Габарит прибора: 598 × 815 × 550. Ниша (600) и фасад в него
        /// НЕ входят — коробка описывает сам прибор, как у духовки. В объём
        /// валидации входит не он, а бак, см. <see cref="EffectiveScale"/>.</summary>
        public static Vector3Int ModelDimensionsMM =>
            new Vector3Int(BODY_WIDTH_MM, BODY_HEIGHT_MM, BODY_DEPTH_MM);

        /// <summary>Высота цоколя, которая получается под фасадом такой высоты:
        /// фасад стоит от верха цоколя до верха корпуса.</summary>
        public static int PlinthForFacade(int facadeHeightMM) => BODY_HEIGHT_MM - facadeHeightMM;

        /// <summary>Высота фасада в допустимом для модели диапазоне 655–725.</summary>
        public static bool IsFacadeHeightValid(int facadeHeightMM) =>
            facadeHeightMM >= FACADE_MIN_HEIGHT_MM && facadeHeightMM <= FACADE_MAX_HEIGHT_MM;

        [SerializeField] private string _attachedFacadeName = "";
        [SerializeField] private bool _open;
        private float _t;

        private readonly List<GameObject> _children = new List<GameObject>();

        /// <summary>Габариты заданы производителем — всегда и у любого экземпляра.</summary>
        public bool HasFixedSize => true;

        /// <summary>Имя пристёгнутого мебельного фасада. Пометка та же, что у
        /// ящика: связь ведёт <see cref="DrawerLinks"/>, а не правка свойств —
        /// откат делают Create/DeleteCommand самого фасада.</summary>
        [NotUndoable("обратная ссылка на фасад, ведёт DrawerLinks")]
        public string AttachedFacadeName
        {
            get => _attachedFacadeName;
            set => _attachedFacadeName = value ?? "";
        }

        /// <summary>Пристёгнутый фасад по имени, либо null. Удалённый фасад
        /// выпадает из PartRegistry — ссылка перестаёт находиться сама, и
        /// висячего указателя на объект не остаётся (а сам факт «имя есть,
        /// фасада нет» ловит анализатор сцены кодом DWH-02).</summary>
        public FacadeElement? FindAttachedFacade()
        {
            if (string.IsNullOrEmpty(_attachedFacadeName)) return null;
            foreach (var e in PartRegistry.All)
                if (e is FacadeElement f && f.PartName == _attachedFacadeName) return f;
            return null;
        }

        /// <summary>Монтажный зазор навески — см. <see cref="FACADE_MOUNT_GAP_MM"/>.</summary>
        public float FacadeMountGapMm => FACADE_MOUNT_GAP_MM;

        /// <summary>При пристёгивании фасада к посудомойке — ВКЛЮЧИТЬ ему
        /// пассажирский режим (трансформом владеет дверца машины, своей анимации
        /// быть не должно). При отстёгивании — СНЯТЬ пассажира, чтобы фасад
        /// снова мог открываться по своей петле как обычная дверца.
        ///
        /// Дополнительно: захватываем текущую позу фасада как «закрытую» —
        /// петля дверцы будет крутить фасад вокруг той точки, в которой его
        /// поставил пользователь (он стоит лицом к переду машины), а не вокруг
        /// мирового нуля.</summary>
        public void OnAttachedFacadeChanged(FacadeElement? oldFacade, FacadeElement? newFacade)
        {
            if (oldFacade != null && oldFacade.IsPassenger) oldFacade.IsPassenger = false;
            if (newFacade != null)
            {
                newFacade.CaptureClosedPose();
                newFacade.IsPassenger = true;
            }
        }

        /// <summary>Дверца откинута (или едет туда). Читается окном свойств,
        /// MCP и сейвом; правится через <see cref="SetOpen"/> — как у ящика,
        /// фасада и духовки, поэтому в реестре отмены свойства нет вовсе.</summary>
        public bool IsOpen => _open;

        /// <summary>Прогресс анимации 0..1 — 0 закрыто, 1 откинуто на 90°.</summary>
        public float DoorProgress => _t;

        public bool IsAnimating => !Mathf.Approximately(_t, _open ? 1f : 0f);

        /// <summary>В объём валидации и прилипания входит ТОЛЬКО БАК —
        /// 598 × 725 × 550, поднятый на полвысоты основания.
        ///
        /// Нижние BASE_HEIGHT_MM (90) габарита — цокольная зона: сам прибор
        /// стоит там своим утопленным основанием, а перед основанием, в полосе
        /// глубиной BASE_SETBACK_MM (100), проходит МЕБЕЛЬНЫЙ цоколь и стоят
        /// ножки модулей. Именно эта полоса давала ложный COL-01
        /// «Leg ↔ Posudomoyka» с областью перекрытия ровно в размер ниши.
        ///
        /// Почему полоса выведена ЦЕЛИКОМ, а не только передние 100 мм: объём
        /// валидации — ОДНА коробка (ValidationElement хранит восемь вершин), и
        /// вырезать угол ей нечем. Из двух возможных приближений — «нижняя
        /// полоса пуста» и «передние 100 мм пусты по всей высоте» — верно первое:
        /// под баком у машины ничего, кроме основания на ножках, и нет, тогда
        /// как передняя плоскость выше основания сплошная и обязана
        /// сталкиваться с тем, что в неё встанет.
        ///
        /// ЦЕНА этого приближения — прибор не чувствует пола подошвой: объём
        /// начинается на 90 мм выше неё, и машина проваливалась в пол без единой
        /// ошибки. Закрывает дыру не коробка, а отдельная проверка опоры
        /// (DWH-05, <see cref="ConstraintValidator.FindDishwasherSupportIssues"/>):
        /// она меряет подошву основания, а не объём валидации, и потому не
        /// возвращает ложный COL-01 на цоколь и ножки.
        ///
        /// Прецеденты те же, что у духовки: там в объём идёт корпус, а не фасад;
        /// у варочной — плита 5 мм, а короб выреза живёт вне габарита. Полная
        /// коробка осталась за коллайдером выбора и за DimensionsMM в окне
        /// свойств: пользователь покупает прибор 598 × 815 × 550.</summary>
        protected override Vector3 EffectiveScale => new Vector3(
            BODY_WIDTH_MM * AppConstants.MM_TO_UNITS,
            TANK_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            BODY_DEPTH_MM * AppConstants.MM_TO_UNITS);

        /// <summary>Смещение центра БАКА от центра габаритной коробки, мм: бак
        /// стоит на основании и потому поднят на его половину.</summary>
        public const float TANK_CENTER_Y_MM = BASE_HEIGHT_MM * 0.5f;

        /// <summary>Мировая точка ПОДОШВЫ прибора — центр нижней грани габарита.
        /// Не низ объёма валидации: тот начинается на BASE_HEIGHT_MM выше, а
        /// стоит машина именно подошвой. На неё смотрит проверка опоры
        /// (DWH-05).</summary>
        public Vector3 SoleCenterWorld => transform.position + transform.rotation *
            new Vector3(0f, -BODY_HEIGHT_MM * 0.5f, 0f) * AppConstants.MM_TO_UNITS;

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        // Бак смещён относительно центра габарита — сдвиг обязан ехать вместе с
        // ПРИМЕРЯЕМОЙ позицией, иначе снэп считает машину не там, где её
        // проверяет валидация (та же обязанность, что у духовки и варочной).
        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            transformPosition + transform.rotation *
                new Vector3(0f, TANK_CENTER_Y_MM, 0f) * AppConstants.MM_TO_UNITS;

        public override void ApplyDimensions()
        {
            // Единичный масштаб корня: геометрия детей задана в мировых единицах,
            // иначе они масштабировались бы дважды.
            transform.localScale = Vector3.one;

            // Габарит возвращается на место ЗДЕСЬ, а не в сеттерах: через
            // ApplyDimensions проходит любой путь правки (окно свойств,
            // ResizeCommand, MCP, загрузка сейва), и одна строка запирает все.
            Data.DimensionsMM = ModelDimensionsMM;

            UpdateCollider();
            if (SuppressVisualRebuild) return;
            EnsureChildren();
            RebuildGeometry();
        }

        // ── Геометрия ───────────────────────────────────────────────────

        private const int IdxBodyBottom = 0;
        private const int IdxBodyTop = 1;
        private const int IdxBodyLeft = 2;
        private const int IdxBodyRight = 3;
        private const int IdxBodyBack = 4;
        private const int IdxBase = 5;
        private const int IdxDoor = 6;
        private const int IdxPanel = 7;
        // Основание идёт последним в группе «корпус»: оно неподвижно, как
        // стенки бака, и отделено от группы дверцы, которая ездит.
        private const int BodyPartCount = 6;
        private const int DoorPartCount = 2;
        private const int ChildCount = BodyPartCount + DoorPartCount;

        private static string ChildName(int idx) => idx switch
        {
            IdxBodyBottom => "BodyBottom",
            IdxBodyTop => "BodyTop",
            IdxBodyLeft => "BodyLeft",
            IdxBodyRight => "BodyRight",
            IdxBodyBack => "BodyBack",
            IdxBase => "Base",
            IdxDoor => "Door",
            _ => "ControlPanel",
        };

        /// <summary>Коллайдер — по ПОЛНОЙ коробке прибора, а не по
        /// <see cref="EffectiveScale"/>: кликают в том числе по нише цоколя, и
        /// клик обязан попадать в машину. Ровно так же поступают духовка и
        /// варочная.</summary>
        private void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            float toU = AppConstants.MM_TO_UNITS;
            var dims = ModelDimensionsMM;
            box.size = new Vector3(dims.x * toU, dims.y * toU, dims.z * toU);
            box.center = Vector3.zero;
        }

        private void EnsureChildren()
        {
            while (_children.Count < ChildCount)
            {
                int idx = _children.Count;
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = ChildName(idx);
                // Коллайдер у детей не нужен — клик ловит BoxCollider корня, а в
                // WebGL-сборке примитив всё равно приходит без него.
                var col = child.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                child.transform.SetParent(transform, false);
                _children.Add(child);
            }
        }

        /// <summary>Низ БАКА в локальных мм: верх основания.</summary>
        private const float TankBottomMM = -BODY_HEIGHT_MM * 0.5f + BASE_HEIGHT_MM;

        /// <summary>Раскладка КОРПУСА в ЛОКАЛЬНЫХ мм от центра прибора: пять
        /// стенок полого бака в порядке индексов 0..4 плюс основание (5). Перёд —
        /// сторона +Z, как у варочной и духовки; ось Y вверх. Передний проём
        /// закрывает дверца, поэтому стенки не доходят до переда на её толщину,
        /// а основание не доходит до него на BASE_SETBACK_MM — там ниша под
        /// мебельный цоколь и место для ног.</summary>
        private static (Vector3 centerMM, Vector3 sizeMM)[] BodyPartsMM()
        {
            float halfH = BODY_HEIGHT_MM * 0.5f;
            float halfD = BODY_DEPTH_MM * 0.5f;
            float t = BODY_WALL_MM;

            float cy = TANK_CENTER_Y_MM;
            float bottom = TankBottomMM;
            float top = halfH;
            float back = -halfD;
            float depth = BODY_DEPTH_MM - DOOR_THICKNESS_MM;
            float cz = back + depth * 0.5f;

            float sideX = (BODY_WIDTH_MM - t) * 0.5f;
            float innerH = TANK_HEIGHT_MM - 2 * t;
            float innerW = BODY_WIDTH_MM - 2 * t;

            return new[]
            {
                (new Vector3(0f, bottom + t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, depth)),
                (new Vector3(0f, top - t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, depth)),
                (new Vector3(-sideX, cy, cz), new Vector3(t, innerH, depth)),
                (new Vector3(sideX, cy, cz), new Vector3(t, innerH, depth)),
                (new Vector3(0f, cy, back + t * 0.5f), new Vector3(innerW, innerH, t)),
                // Основание: от задней плоскости вперёд на BASE_DEPTH_MM, то
                // есть с утоплением BASE_SETBACK_MM от переда. Подошва — низ
                // габарита: на ней машина и стоит.
                (new Vector3(0f, -halfH + BASE_HEIGHT_MM * 0.5f, back + BASE_DEPTH_MM * 0.5f),
                 new Vector3(BODY_WIDTH_MM, BASE_HEIGHT_MM, BASE_DEPTH_MM)),
            };
        }

        /// <summary>Раскладка ДВЕРЦЫ в ЗАКРЫТОЙ позе, локальные мм, в порядке
        /// индексов 5..6 (полотно, панель управления).</summary>
        private static (Vector3 centerMM, Vector3 sizeMM)[] DoorPartsMM()
        {
            float halfH = BODY_HEIGHT_MM * 0.5f;
            float halfD = BODY_DEPTH_MM * 0.5f;

            return new[]
            {
                // Полотно двери — во всю переднюю плоскость бака.
                (new Vector3(0f, TANK_CENTER_Y_MM, halfD - DOOR_THICKNESS_MM * 0.5f),
                 new Vector3(BODY_WIDTH_MM, TANK_HEIGHT_MM, DOOR_THICKNESS_MM)),
                // Полоса панели утоплена в полотно: её передняя грань совпадает
                // с передней гранью двери.
                (new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f,
                     halfD - OVERLAY_THICKNESS_MM * 0.5f),
                 new Vector3(BODY_WIDTH_MM, CONTROL_PANEL_HEIGHT_MM, OVERLAY_THICKNESS_MM)),
            };
        }

        /// <summary>Ось откидывания — НИЖНЯЯ горизонтальная кромка двери на её
        /// задней плоскости (там, где дверь прилегает к баку). Локальные мм.</summary>
        public static Vector3 HingeLocalMM =>
            new Vector3(0f, TankBottomMM, BODY_DEPTH_MM * 0.5f - DOOR_THICKNESS_MM);

        /// <summary>Поворот дверцы при заданном прогрессе: вокруг локальной оси
        /// X, вниз-вперёд. Одна и та же функция кормит и сцену, и расчёт
        /// габаритов открывания — разойтись они не могут.</summary>
        public static Quaternion DoorLocalRotation(float progress) =>
            Quaternion.AngleAxis(DOOR_OPEN_ANGLE_DEG * Mathf.Clamp01(progress), Vector3.right);

        private void RebuildGeometry()
        {
            if (_children.Count < ChildCount) return;

            var body = BodyPartsMM();
            for (int i = 0; i < BodyPartCount; i++)
                Box(i, body[i].centerMM, body[i].sizeMM, Quaternion.identity);

            ApplyDoorPose();
            ApplyMaterials();
        }

        /// <summary>Поставить дверцу по текущему прогрессу. Корень при этом не
        /// двигается вовсе — открывание живёт целиком в дочерних трансформах.</summary>
        internal void ApplyDoorPose()
        {
            if (_children.Count < ChildCount) return;

            float toU = AppConstants.MM_TO_UNITS;
            var hinge = HingeLocalMM * toU;
            var rot = DoorLocalRotation(_t);
            var parts = DoorPartsMM();

            for (int i = 0; i < DoorPartCount; i++)
            {
                var go = _children[BodyPartCount + i];
                if (go == null) continue;
                var closed = parts[i].centerMM * toU;
                go.transform.localPosition = hinge + rot * (closed - hinge);
                go.transform.localRotation = rot;
                go.transform.localScale = parts[i].sizeMM * toU;
            }

            // Фасад — лицо дверцы: после того, как дверца встала в текущую позу,
            // пристёгнутый фасад-пассажир едет по ТОЙ ЖЕ петле, иначе его
            // собственная кинематика (другая петля, другой режим) уведёт его
            // мимо дверцы.
            ApplyFacadePose(rot, hinge);
        }

        /// <summary>Поставить фасад-пассажир в позу, которую даёт вращение
        /// дверцы вокруг её петли. Закрытая поза фасада (<see cref="FacadeElement.ClosedPosition"/>/
        /// <see cref="FacadeElement.ClosedRotation"/>) берётся за основу, а
        /// поворачивается она тем же <paramref name="doorRot"/> вокруг того же
        /// <paramref name="hingeLocal"/>, что и сама дверца — так они остаются
        /// склеенными на любом <c>_t</c>.</summary>
        private void ApplyFacadePose(Quaternion doorRot, Vector3 hingeLocal)
        {
            var f = FindAttachedFacade();
            if (f == null) return;

            // Закрытая поза фасада в МИРОВЫХ координатах: для пассажира его
            // трансформ и есть закрытая поза (хост её не двигает между кадрами,
            // а сам фасад не анимируется).
            var closedPos = f.ClosedPosition;
            var closedRot = f.ClosedRotation;

            var facadeLocal = transform.InverseTransformPoint(closedPos);
            var facadeLocalRot = Quaternion.Inverse(transform.rotation) * closedRot;

            var rotatedLocal = hingeLocal + doorRot * (facadeLocal - hingeLocal);
            var rotatedLocalRot = doorRot * facadeLocalRot;

            var worldPos = transform.TransformPoint(rotatedLocal);
            var worldRot = transform.rotation * rotatedLocalRot;
            f.transform.SetPositionAndRotation(worldPos, worldRot);
        }

        /// <summary>Поставить дочернюю коробку: позиция и размер — в ЛОКАЛЬНЫХ мм.</summary>
        private void Box(int idx, Vector3 centerMM, Vector3 sizeMM, Quaternion rot)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var go = _children[idx];
            if (go == null) return;
            go.transform.localPosition = centerMM * toU;
            go.transform.localRotation = rot;
            go.transform.localScale = sizeMM * toU;
        }

        // ── Анимация дверцы ─────────────────────────────────────────────
        // Механика — та же, что у духовки: целевое состояние + прогресс +
        // гашение о препятствие через OpeningCollision. Отличие одно и оно
        // существенное: у машины СВОЕЙ фасадной панели нет, поэтому вместе с
        // дверцей обязан ехать ПРИСТЁГНУТЫЙ фасад — он к ней и прикручен.

        public void SetOpen(bool open)
        {
            _open = open;
            SyncAttachedFacade();
            if (!Mathf.Approximately(_t, open ? 1f : 0f))
                FrameRateManager.KeepAwake(OpenSeconds + 0.2f);
        }

        public void ToggleOpen() => SetOpen(!_open);

        /// <summary>Мгновенно захлопнуть. Для пассажира его
        /// <see cref="FacadeElement.ForceClose"/> — no-op (трансформ вернёт
        /// <see cref="ApplyDoorPose"/> при нулевом прогрессе); для не-пассажира
        /// это единственный способ погасить его собственную анимацию. Зовём
        /// всегда: хуже не будет, а пропуск ломает совместимость с фасадами,
        /// пристёгнутыми до включения пассажирского режима.</summary>
        public void ForceClose()
        {
            var f = FindAttachedFacade();
            if (f != null) f.ForceClose();
            if (_t <= 0f && !_open) return;
            _open = false;
            _t = 0f;
            ApplyDoorPose();
        }

        /// <summary>Фасад-пассажир: трансформом владеет дверца, поэтому
        /// достаточно синхронизировать ЦЕЛЕВОЕ СОСТОЯНИЕ (для IsOpen и кнопки
        /// в окне свойств), а анимацию фасад не крутит — её каждый кадр делает
        /// <see cref="ApplyFacadePose"/> через петлю дверцы.</summary>
        private void SyncAttachedFacade()
        {
            var f = FindAttachedFacade();
            if (f != null) f.SetOpen(_open);
        }

        /// <summary>Internal: Unity зовёт Update независимо от видимости, а тесты
        /// гоняют тот же путь, что и цикл кадра (StepDoor).</summary>
        internal void Update() => StepDoor(Time.deltaTime);

        public void StepDoor(float dt)
        {
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _t = Mathf.MoveTowards(_t, target, step);

            if (_open && _t > 0f)
            {
                var exclude = new List<KitchenElement>();
                var f = FindAttachedFacade();
                if (f != null) exclude.Add(f);
                float safe = OpeningCollision.FindMaxProgress(this, GetOpenBounds, exclude);
                if (safe < _t) _t = Mathf.Max(_t - step, safe);
            }

            ApplyDoorPose();
        }

        /// <summary>Мировые границы (AABB) ОТКИНУТОЙ ДВЕРЦЫ при заданном
        /// прогрессе [0..1], вместе с пристёгнутым фасадом. Бак сюда не входит:
        /// он не движется, и его касание соседей — не помеха открыванию.
        ///
        /// Фасад считается ТОЙ ЖЕ петлёй, что и дверца: иначе его собственная
        /// кинематика (другая петля, другой режим) разъедется с дверцей и
        /// коллизия будет ловить призрак.</summary>
        public (Vector3 min, Vector3 max) GetOpenBounds(float progress)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var hinge = HingeLocalMM * toU;
            var localRot = DoorLocalRotation(progress);
            var parts = DoorPartsMM();

            var world = new Vector3[DoorPartCount * 8];
            int n = 0;
            for (int i = 0; i < DoorPartCount; i++)
            {
                var closed = parts[i].centerMM * toU;
                var localPos = hinge + localRot * (closed - hinge);
                var worldPos = transform.TransformPoint(localPos);
                var worldRot = transform.rotation * localRot;
                var half = parts[i].sizeMM * toU * 0.5f;
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3(
                        (c & 1) == 0 ? -half.x : half.x,
                        (c & 2) == 0 ? -half.y : half.y,
                        (c & 4) == 0 ? -half.z : half.z);
                    world[n++] = worldPos + worldRot * corner;
                }
            }
            var (min, max) = OpeningCollision.MinMax(world);

            var facade = FindAttachedFacade();
            if (facade != null)
            {
                var (fMin, fMax) = FacadeOpenBoundsOnDoor(facade, localRot, hinge);
                min = Vector3.Min(min, fMin);
                max = Vector3.Max(max, fMax);
            }
            return (min, max);
        }

        /// <summary>AABB фасада при том же вращении вокруг той же петли, что
        /// дверца. Использует <see cref="FacadeElement.ClosedPosition"/> и
        /// <see cref="FacadeElement.ClosedRotation"/> (для пассажира это и есть
        /// его текущая мировая поза) и его локальные half-extents.</summary>
        private (Vector3 min, Vector3 max) FacadeOpenBoundsOnDoor(
            FacadeElement facade, Quaternion doorRot, Vector3 hingeLocal)
        {
            var closedPos = facade.ClosedPosition;
            var closedRot = facade.ClosedRotation;

            var facadeLocal = transform.InverseTransformPoint(closedPos);
            var facadeLocalRot = Quaternion.Inverse(transform.rotation) * closedRot;

            var rotatedLocal = hingeLocal + doorRot * (facadeLocal - hingeLocal);
            var rotatedLocalRot = doorRot * facadeLocalRot;

            var worldPos = transform.TransformPoint(rotatedLocal);
            var worldRot = transform.rotation * rotatedLocalRot;

            var half = facade.transform.localScale * 0.5f;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                corners[i] = worldPos + worldRot * new Vector3(
                    (i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y,
                    (i & 4) == 0 ? -half.z : half.z);
            }
            return OpeningCollision.MinMax(corners);
        }

        // ── Материалы ───────────────────────────────────────────────────
        // Цвета прибора фиксированы вместе с размерами: посудомойка — купленная
        // техника, а не отделываемая деталь, поэтому декор из каталога к ней не
        // применяется (как у мойки и духовки).

        private static Material? _bodyMat;
        private static Material? _doorMat;
        private static Material? _panelMat;

        private static Material Lit(Color color, float metallic, float smoothness)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        // Сравнение с null — именно `== null`, а не `??=`: у UnityEngine.Object
        // своё «уничтоженное, но не null» состояние, и `??=` его не видит.

        /// <summary>Бак — СВЕТЛАЯ нержавейка: у настоящей машины камера
        /// стальная, и тёмный полый короб внутри сливался бы сам с собой и с
        /// тёмной дверцей (та же причина, что у серого корпуса духовки).</summary>
        private static Material BodyMaterial()
        {
            if (_bodyMat == null) _bodyMat = Lit(new Color(0.62f, 0.63f, 0.65f, 1f), 0.5f, 0.55f);
            return _bodyMat!;
        }

        /// <summary>Полотно двери — тёмно-серое матовое (прежний цвет прибора).</summary>
        private static Material DoorMaterial()
        {
            if (_doorMat == null) _doorMat = Lit(new Color(0.18f, 0.18f, 0.19f, 1f), 0.1f, 0.35f);
            return _doorMat!;
        }

        /// <summary>Панель управления — чёрная.</summary>
        private static Material PanelMaterial()
        {
            if (_panelMat == null) _panelMat = Lit(new Color(0.03f, 0.03f, 0.035f, 1f), 0.05f, 0.7f);
            return _panelMat!;
        }

        private void ApplyMaterials()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (child == null) continue;
                var mr = child.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                mr.sharedMaterial = i switch
                {
                    IdxPanel => PanelMaterial(),
                    // Основание у настоящей машины тёмное (чёрный поддон), и
                    // светлая нержавейка бака под фасадом смотрелась бы полкой.
                    IdxDoor or IdxBase => DoorMaterial(),
                    _ => BodyMaterial(),
                };
            }
        }

        public void DestroyChildren()
        {
            foreach (var child in _children)
            {
                if (child == null) continue;
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
            _children.Clear();
        }

        private void OnDestroy()
        {
            PartRegistry.Unregister(this);
            DestroyChildren();
        }
    }
}
