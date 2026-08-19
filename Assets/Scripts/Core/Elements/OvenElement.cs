using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Электрический духовой шкаф Bosch HBA514BB3 (чёрный) — готовая модель из
    /// группы «Техника»: габариты заданы производителем и не редактируются
    /// (<see cref="IFixedSizeElement"/>), поэтому поля Ш/В/Г в окне свойств серые,
    /// ручек ресайза нет, а edit_elements правку размера отклоняет.
    ///
    /// ДЕВЯТЬ параллелепипедов, все — дети при ЕДИНИЧНОМ масштабе корня.
    /// Корпус — ПОЛЫЙ короб из пяти стенок (духовка внутри пустая, туда встают
    /// противни):
    ///   0 «BodyBottom», 1 «BodyTop», 2 «BodyLeft», 3 «BodyRight», 4 «BodyBack».
    /// Дверца — четыре коробки, которые ездят вместе:
    ///   5 «Facade»       — рамка фасада на всю его площадь;
    ///   6 «Glass»        — чёрное стекло двери, утоплено в рамку;
    ///   7 «ControlPanel» — верхняя полоса фасада;
    ///   8 «Handle»       — ручка по низу панели управления.
    ///
    /// Фасад собран ЗДЕСЬ, а не через <see cref="AssembledFacadeElement"/>:
    /// у сборного фасада рамка считается по формуле каталога (доля A от стороны,
    /// стойки + перекладины + вставка) и живёт одним процедурным мешем с двумя
    /// сабмешами под фрезеровки. Духовке нужна не рамка мебельного фасада, а
    /// четыре коробки с размерами из монтажной схемы (полоса 96 сверху, стекло
    /// 499 снизу, ручка со своим выступом) — переиспользование потребовало бы
    /// параметризовать AssembledFacadeMesh тремя чужими случаями ради геометрии,
    /// которая проще самой формулы.
    ///
    /// ДВЕРЦА ОТКИДНАЯ (карточка DNS): поворот вокруг НИЖНЕЙ горизонтальной
    /// кромки фасада, 0° → 90°. Поворачивается не корень, а четыре дочерние
    /// коробки: у духовки, в отличие от ящика и фасада, открывание вообще не
    /// трогает позу элемента — значит и замораживать позу валидации
    /// (ClosedPosition ящика) не нужно, см. <see cref="EffectiveScale"/>.
    ///
    /// В отличие от варочной духовка НЕ врезная: она не <see cref="IPartCutout"/>,
    /// ни к какой детали не привязывается и не режет проёмов — обычный корпусный
    /// элемент со штатными снапами и валидацией.
    ///
    /// Все размеры — из docs/APPLIANCES-BRIEF.md §2 (монтажная схема Bosch).
    /// О расхождении брифа см. HANDLE_TOP_MM.
    /// </summary>
    public class OvenElement : KitchenElement, IFixedSizeElement, IOpenable
    {
        /// <summary>Единственная модель. Второй духовке хватило бы таблицы
        /// «модель → размеры», как у <see cref="CooktopElement.ModelDimensionsMM"/>;
        /// заводить её ради одной строки — лишний слой.</summary>
        public const string MODEL = "Bosch HBA514BB3";

        // ── Фасад ───────────────────────────────────────────────────────
        public const int FACADE_WIDTH_MM = 594;
        public const int FACADE_HEIGHT_MM = 595;

        /// <summary>Толщина фасадной рамки/двери. Дробная — поэтому геометрия
        /// считается во float мм, а в целочисленный габарит уходит округление.</summary>
        public const float FACADE_THICKNESS_MM = 19.5f;

        // ── Корпус за фасадом ───────────────────────────────────────────
        /// <summary>Ширина корпуса. Ниша колонны по схеме — 560⁺⁸, и корпус
        /// обязан входить в типовой проём 564 с зазором на сторону; 570 в первой
        /// редакции были ошибкой чтения чертежа (570 — это ВЫСОТА).</summary>
        public const int BODY_WIDTH_MM = 560;

        /// <summary>Глубина встраивания по характеристикам DNS — 54.8 см.</summary>
        public const int BODY_DEPTH_MM = 548;

        /// <summary>Высота встраивания по характеристикам DNS — 57 см.</summary>
        public const int BODY_HEIGHT_MM = 570;

        /// <summary>Фасад выступает над корпусом сверху — на этой отметке прибор
        /// висит во фланце ниши, поэтому она и взята за опорную.</summary>
        public const int FACADE_TOP_OVERHANG_MM = 25;

        /// <summary>Выступ фасада ПОД корпусом — величина ПРОИЗВОДНАЯ и равная
        /// НУЛЮ: 595 = 25 + 570, то есть низ фасада заподлицо с низом корпуса.
        /// (Число 7.5 с бокового чертежа — монтажный зазор ПОД прибором, а не
        /// выступ фасада.) Формула оставлена, чтобы уточнение любой из трёх
        /// величин сходилось само.</summary>
        public const int FACADE_BOTTOM_OVERHANG_MM =
            FACADE_HEIGHT_MM - FACADE_TOP_OVERHANG_MM - BODY_HEIGHT_MM;

        /// <summary>Толщина стенки полого короба. Число условное — снаружи её не
        /// видно, а внутри она задаёт полезный объём; 20 мм читаемо на модели и
        /// оставляет камеру 520 × 530 × 528, что близко к паспортным 71 л.</summary>
        public const int BODY_WALL_MM = 20;

        // ── Разбивка фасада ─────────────────────────────────────────────
        /// <summary>Панель управления — верхняя полоса фасада.</summary>
        public const int CONTROL_PANEL_HEIGHT_MM = 96;

        /// <summary>Стекло двери — весь остаток фасада ниже панели.</summary>
        public const int GLASS_HEIGHT_MM = FACADE_HEIGHT_MM - CONTROL_PANEL_HEIGHT_MM;

        /// <summary>Ширина рамки вокруг стеклянной вставки двери.</summary>
        public const int DOOR_FRAME_MM = 15;

        /// <summary>Толщина накладных деталей фасада (стекло, полоса панели):
        /// они лежат заподлицо с передней плоскостью рамки, а не поверх неё, —
        /// иначе фасад выходил бы за собственный габарит.</summary>
        public const float OVERLAY_THICKNESS_MM = 2f;

        // ── Ручка ───────────────────────────────────────────────────────
        /// <summary>Верх ручки — по низу панели управления, т.е. на отметке
        /// <see cref="CONTROL_PANEL_HEIGHT_MM"/> от верха фасада.
        ///
        /// РАСХОЖДЕНИЕ В БРИФЕ. Там ручка описана и как «по низу панели
        /// управления», и как «на отметке 405 от верха фасада» — одновременно эти
        /// два условия несовместимы: низ панели лежит на 96 мм. 405 мм от верха
        /// пришлось бы на середину стекла, где у HBA5 ручки нет; значит, это
        /// размер со схемы от другой базы (линии ниши/столешницы). Держимся
        /// геометрически определённого условия — низа панели управления.</summary>
        public const int HANDLE_TOP_MM = CONTROL_PANEL_HEIGHT_MM;
        public const int HANDLE_HEIGHT_MM = 28;
        public const int HANDLE_SIDE_INSET_MM = 12;

        /// <summary>Максимальный выступ ручки вперёд от плоскости фасада. Ручка
        /// СОЗНАТЕЛЬНО выходит за габаритную коробку: коробка описывает то, что
        /// встаёт в нишу колонны (корпус + фасад), и включить в неё накладную
        /// ручку значило бы, что духовка перестала помещаться в свою же нишу.</summary>
        public const int HANDLE_PROTRUSION_MM = 50;

        // ── Габарит ─────────────────────────────────────────────────────
        /// <summary>Корпус плюс фасад — 548 + 19.5 = 567.5 мм.</summary>
        public const float TOTAL_DEPTH_MM = BODY_DEPTH_MM + FACADE_THICKNESS_MM;

        /// <summary>Глубина в целочисленном габарите: 567.5 округлены ВВЕРХ,
        /// чтобы коробка накрывала прибор целиком. Полмиллиметра расхождения с
        /// геометрией — цена того, что DimensionsMM целые.</summary>
        public const int DEPTH_MM = 568;

        /// <summary>Габарит прибора: 594 × 595 × 568. Это то, что видит
        /// пользователь в окне свойств, — размер ИЗДЕЛИЯ. В объём валидации
        /// входит не он, а корпус, см. <see cref="EffectiveScale"/>.</summary>
        public static Vector3Int ModelDimensionsMM =>
            new Vector3Int(FACADE_WIDTH_MM, FACADE_HEIGHT_MM, DEPTH_MM);

        // ── Открывание дверцы ───────────────────────────────────────────
        /// <summary>Откидная дверца раскрывается ровно в горизонталь.</summary>
        public const float DOOR_OPEN_ANGLE_DEG = 90f;

        private const float OpenSeconds = 0.4f;

        [SerializeField] private bool _open;
        private float _t;

        private readonly List<GameObject> _children = new List<GameObject>();

        /// <summary>Габариты заданы производителем — всегда и у любого экземпляра
        /// (в отличие от варочной, где один класс обслуживает и свободный
        /// элемент, и пресет).</summary>
        public bool HasFixedSize => true;

        /// <summary>Дверца откинута (или едет туда). Читается окном свойств,
        /// MCP и сейвом; правится через <see cref="SetOpen"/> — как у ящика и
        /// фасада, поэтому в реестре отмены свойства нет вовсе.</summary>
        public bool IsOpen => _open;

        /// <summary>Прогресс анимации 0..1 — 0 закрыто, 1 откинуто на 90°.</summary>
        public float DoorProgress => _t;

        public bool IsAnimating => !Mathf.Approximately(_t, _open ? 1f : 0f);

        /// <summary>В объём валидации и прилипания входит ТОЛЬКО КОРПУС.
        ///
        /// Полная коробка 594 × 595 × 568 включает фасад, а фасад по определению
        /// шире проёма (594 против типовых 564) и обязан лежать ПОВЕРХ боковин —
        /// иначе прибор нельзя смонтировать нигде, и штатный модуль 600 давал
        /// три COL-01 разом (боковины + полка). В нишу встаёт корпус, он и есть
        /// физический объём прибора.
        ///
        /// Прецедент — <see cref="CooktopElement"/>: там в EffectiveScale входит
        /// только плита 5 мм, а короб выреза живёт вне габарита.
        ///
        /// Что осталось по ПОЛНОЙ коробке: коллайдер выбора (см. UpdateCollider) —
        /// клик по фасаду обязан выделять духовку, а фасад в объём корпуса не
        /// попадает; и <see cref="DimensionsMM"/> в окне свойств — пользователь
        /// покупает изделие 594 × 595 × 568, а не его корпус. Подсветка сюда не
        /// смотрит вовсе: ElementHighlighter подменяет материалы на рендерерах,
        /// то есть красит все девять коробок независимо от габарита.</summary>
        protected override Vector3 EffectiveScale => new Vector3(
            BODY_WIDTH_MM * AppConstants.MM_TO_UNITS,
            BODY_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            BODY_DEPTH_MM * AppConstants.MM_TO_UNITS);

        /// <summary>Смещение центра КОРПУСА от центра габаритной коробки, мм:
        /// корпус утоплен за фасад и опущен на верхний выступ.</summary>
        public const float BODY_CENTER_Y_MM =
            FACADE_HEIGHT_MM * 0.5f - FACADE_TOP_OVERHANG_MM - BODY_HEIGHT_MM * 0.5f;

        public const float BODY_CENTER_Z_MM =
            TOTAL_DEPTH_MM * 0.5f - FACADE_THICKNESS_MM - BODY_DEPTH_MM * 0.5f;

        protected override Vector3 ValidationPosition => ValidationPositionAt(transform.position);

        // Корпус смещён относительно центра габарита — сдвиг обязан ехать вместе
        // с ПРИМЕРЯЕМОЙ позицией, иначе снэп считает духовку не там, где её
        // проверяет валидация (ровно та же обязанность, что у варочной и мойки).
        protected override Vector3 ValidationPositionAt(Vector3 transformPosition) =>
            transformPosition + transform.rotation *
                new Vector3(0f, BODY_CENTER_Y_MM, BODY_CENTER_Z_MM) * AppConstants.MM_TO_UNITS;

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
        private const int IdxFacade = 5;
        private const int IdxGlass = 6;
        private const int IdxPanel = 7;
        private const int IdxHandle = 8;
        private const int BodyPartCount = 5;
        private const int DoorPartCount = 4;
        private const int ChildCount = BodyPartCount + DoorPartCount;

        private static string ChildName(int idx) => idx switch
        {
            IdxBodyBottom => "BodyBottom",
            IdxBodyTop => "BodyTop",
            IdxBodyLeft => "BodyLeft",
            IdxBodyRight => "BodyRight",
            IdxBodyBack => "BodyBack",
            IdxFacade => "Facade",
            IdxGlass => "Glass",
            IdxPanel => "ControlPanel",
            _ => "Handle",
        };

        /// <summary>Коллайдер — по ПОЛНОЙ коробке изделия, а не по
        /// <see cref="EffectiveScale"/>: кликают по фасаду, и он обязан попадать
        /// в духовку. Ровно так же поступает варочная (её коллайдер накрывает и
        /// короб выреза, которого в габарите нет).</summary>
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

        /// <summary>Раскладка КОРПУСА в ЛОКАЛЬНЫХ мм от центра прибора: пять
        /// стенок полого короба в порядке индексов 0..4. Перёд — сторона +Z, как
        /// у варочной; ось Y вверх.</summary>
        private static (Vector3 centerMM, Vector3 sizeMM)[] BodyPartsMM()
        {
            float halfH = FACADE_HEIGHT_MM * 0.5f;
            float cy = BODY_CENTER_Y_MM;
            float cz = BODY_CENTER_Z_MM;
            float t = BODY_WALL_MM;

            float top = halfH - FACADE_TOP_OVERHANG_MM;
            float bottom = top - BODY_HEIGHT_MM;
            float back = cz - BODY_DEPTH_MM * 0.5f;
            float sideX = (BODY_WIDTH_MM - t) * 0.5f;
            float innerH = BODY_HEIGHT_MM - 2 * t;
            float innerW = BODY_WIDTH_MM - 2 * t;

            return new[]
            {
                (new Vector3(0f, bottom + t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, BODY_DEPTH_MM)),
                (new Vector3(0f, top - t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, BODY_DEPTH_MM)),
                (new Vector3(-sideX, cy, cz), new Vector3(t, innerH, BODY_DEPTH_MM)),
                (new Vector3(sideX, cy, cz), new Vector3(t, innerH, BODY_DEPTH_MM)),
                (new Vector3(0f, cy, back + t * 0.5f), new Vector3(innerW, innerH, t)),
            };
        }

        /// <summary>Раскладка ДВЕРЦЫ в ЗАКРЫТОЙ позе, локальные мм, в порядке
        /// индексов 5..8 (рамка, стекло, панель, ручка).</summary>
        private static (Vector3 centerMM, Vector3 sizeMM)[] DoorPartsMM()
        {
            float halfH = FACADE_HEIGHT_MM * 0.5f;
            // Полглубины считаем по НАСТОЯЩИМ 567.5, а не по округлённым 568:
            // геометрия обязана сойтись сама с собой, округление живёт только в
            // габаритной коробке.
            float halfD = TOTAL_DEPTH_MM * 0.5f;

            // Накладки утоплены в рамку: их передняя грань совпадает с передней
            // гранью фасада.
            float overlayZ = halfD - OVERLAY_THICKNESS_MM * 0.5f;
            float doorTopY = halfH - CONTROL_PANEL_HEIGHT_MM;

            return new[]
            {
                // Рамка фасада — на всю его площадь.
                (new Vector3(0f, 0f, halfD - FACADE_THICKNESS_MM * 0.5f),
                 new Vector3(FACADE_WIDTH_MM, FACADE_HEIGHT_MM, FACADE_THICKNESS_MM)),
                // Стекло двери — в проёме рамки под панелью управления.
                (new Vector3(0f, (doorTopY - halfH) * 0.5f, overlayZ),
                 new Vector3(FACADE_WIDTH_MM - 2 * DOOR_FRAME_MM,
                     GLASS_HEIGHT_MM - 2 * DOOR_FRAME_MM, OVERLAY_THICKNESS_MM)),
                // Панель управления — верхняя полоса.
                (new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f, overlayZ),
                 new Vector3(FACADE_WIDTH_MM, CONTROL_PANEL_HEIGHT_MM, OVERLAY_THICKNESS_MM)),
                // Ручка: верхняя грань — по низу панели управления, вперёд
                // выступает за габарит (см. HANDLE_PROTRUSION_MM).
                (new Vector3(0f, halfH - HANDLE_TOP_MM - HANDLE_HEIGHT_MM * 0.5f,
                     halfD + HANDLE_PROTRUSION_MM * 0.5f),
                 new Vector3(FACADE_WIDTH_MM - 2 * HANDLE_SIDE_INSET_MM,
                     HANDLE_HEIGHT_MM, HANDLE_PROTRUSION_MM)),
            };
        }

        /// <summary>Ось откидывания — НИЖНЯЯ горизонтальная кромка фасада на его
        /// задней плоскости (там, где дверца прилегает к корпусу). Локальные мм.</summary>
        public static Vector3 HingeLocalMM => new Vector3(
            0f, -FACADE_HEIGHT_MM * 0.5f, TOTAL_DEPTH_MM * 0.5f - FACADE_THICKNESS_MM);

        /// <summary>Поворот дверцы при заданном прогрессе: вокруг локальной оси X,
        /// вниз-вперёд. Одна и та же функция кормит и сцену, и расчёт габаритов
        /// открывания — разойтись они не могут.</summary>
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
        private void ApplyDoorPose()
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
        // Механика — та же, что у двери помещения (DoorElement): целевое
        // состояние + прогресс + гашение о препятствие через OpeningCollision.
        // Своей третьей реализации духовке не нужно.

        public void SetOpen(bool open)
        {
            _open = open;
            if (!Mathf.Approximately(_t, open ? 1f : 0f))
                FrameRateManager.KeepAwake(OpenSeconds + 0.2f);
        }

        public void ToggleOpen() => SetOpen(!_open);

        /// <summary>Мгновенно захлопнуть — правки размеров/позиции применяются к
        /// закрытой позе, как у фасада и ящика.</summary>
        public void ForceClose()
        {
            if (_t <= 0f && !_open) return;
            _open = false;
            _t = 0f;
            ApplyDoorPose();
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
                float safe = OpeningCollision.FindMaxProgress(this, GetOpenBounds);
                if (safe < _t) _t = Mathf.Max(_t - step, safe);
            }

            ApplyDoorPose();
        }

        /// <summary>Мировые границы (AABB) ОТКИНУТОЙ ДВЕРЦЫ при заданном
        /// прогрессе [0..1]. Корпус сюда не входит: он не движется, и его
        /// касание соседей — не помеха открыванию.</summary>
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
            return OpeningCollision.MinMax(world);
        }

        // ── Материалы ───────────────────────────────────────────────────
        // Цвета прибора фиксированы вместе с размерами: духовка — купленная
        // техника, а не отделываемая деталь, поэтому декор из каталога к ней не
        // применяется (как у мойки).

        private static Material? _bodyMat;
        private static Material? _facadeMat;
        private static Material? _glassMat;
        private static Material? _panelMat;
        private static Material? _handleMat;

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

        /// <summary>Корпус — СЕРЫЙ: чёрное у духовки только лицо, а полый короб
        /// внутри чёрным сливался бы сам с собой и с чёрной дверцей.</summary>
        private static Material BodyMaterial()
        {
            if (_bodyMat == null) _bodyMat = Lit(new Color(0.45f, 0.45f, 0.46f, 1f), 0.15f, 0.35f);
            return _bodyMat!;
        }

        /// <summary>Рамка дверцы — чёрный матовый.</summary>
        private static Material FacadeMaterial()
        {
            if (_facadeMat == null) _facadeMat = Lit(new Color(0.05f, 0.05f, 0.05f, 1f), 0.05f, 0.25f);
            return _facadeMat!;
        }

        /// <summary>Стекло двери — чёрное глянцевое.</summary>
        private static Material GlassMaterial()
        {
            if (_glassMat == null) _glassMat = Lit(new Color(0.03f, 0.03f, 0.035f, 1f), 0.05f, 0.95f);
            return _glassMat!;
        }

        /// <summary>Панель управления — чуть светлее стекла, иначе на чёрном её
        /// не видно вовсе.</summary>
        private static Material PanelMaterial()
        {
            if (_panelMat == null) _panelMat = Lit(new Color(0.14f, 0.14f, 0.15f, 1f), 0.05f, 0.6f);
            return _panelMat!;
        }

        /// <summary>Ручка — тёмный металл.</summary>
        private static Material HandleMaterial()
        {
            if (_handleMat == null) _handleMat = Lit(new Color(0.22f, 0.22f, 0.23f, 1f), 0.6f, 0.7f);
            return _handleMat!;
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
                    IdxFacade => FacadeMaterial(),
                    IdxGlass => GlassMaterial(),
                    IdxPanel => PanelMaterial(),
                    IdxHandle => HandleMaterial(),
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
