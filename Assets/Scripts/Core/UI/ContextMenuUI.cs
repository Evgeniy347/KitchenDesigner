using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Контекстное меню по клику ЛКМ на детали: размеры, позиция, поворот, действия.</summary>
    public class ContextMenuUI : MonoBehaviour
    {
        public static ContextMenuUI? Instance { get; private set; }

        private GameObject? _root;
        private KitchenElement? _target;
        private TMP_Text? _titleLabel;

		private TMP_InputField? _name, _w, _h, _d, _radius,
			_gapLeft, _gapRight, _gapTop, _gapBottom,
			_x, _y, _z, _rx, _ry, _rz, _legInset, _midHeight,
			_lightTemp, _lightPower, _lightDiffusion, _lightUp, _lightBeam;
        private Toggle? _lockToggle;
        private Toggle? _transparentToggle;
        private RectTransform? _panelRt;
        private TMP_Text? _doorButtonLabel;
        private TMP_Text? _winDoorButtonLabel;
        private TMP_Dropdown? _modeDropdown;
        private TMP_Dropdown? _fillDropdown;
        private TMP_Dropdown? _materialDropdown;
        private TMP_Dropdown? _tabletopMaterialDropdown;
        private TMP_Dropdown? _legsMaterialDropdown;
        private TMP_Dropdown? _typeDropdown;
        private TMP_Dropdown? _drawerTypeDropdown, _drawerLengthDropdown, _drawerColorDropdown;
        private TMP_Dropdown? _drawerUpperLenDropdown;
        private TMP_InputField? _drawerWidth;
        private TMP_Text? _drawerAnimLabel;
        private TMP_Dropdown? _drawerFacadeDropdown;
        private TMP_Dropdown? _tintDropdown;
        private TMP_Dropdown? _sashTypeDropdown;
        private TMP_InputField? _sillProtrusion;
        private TMP_Dropdown? _winModeDropdown;
        private TMP_Text? _grooveCountLabel;
        private TMP_Dropdown? _grooveSideDropdown, _grooveKindDropdown;
        // Строки-слоты пазов: сторона и тип редактируются на месте (правка =
        // прямое действие, отдельного режима «редактирования» нет).
        private readonly TMP_Dropdown?[] _grooveRowSide = new TMP_Dropdown?[AppConstants.GROOVE_MAX_PER_PART];
        private readonly TMP_Dropdown?[] _grooveRowKind = new TMP_Dropdown?[AppConstants.GROOVE_MAX_PER_PART];
        private bool _groovesExpanded;  // раскрыт ли список пазов
        private int _grooveFingerprint; // отлов изменений пазов извне (undo/MCP)

        // ── Кромки ─────────────────────────────────────────────────────
        // Схема детали со сторонами L1/L2/W1/W2: зелёная сторона — кромка есть,
        // светло-серая — торец упирается в соседа. Размер схемы фиксирован и от
        // габарита детали не зависит — это условная схема, а не чертёж.
        private Toggle? _edgeToggle, _edgeSkipToggle;
        private TMP_InputField? _edgeThickness;
        private RectTransform? _edgeDiagram;
        private Image? _edgeStripL1, _edgeStripL2, _edgeStripW1, _edgeStripW2;
        private TMP_Text? _edgeLengthLabel, _edgeWidthLabel;
        // Кромки пересчитываются по всей сцене, поэтому в Update это делается
        // не каждый кадр: соседи двигаются заметно медленнее 60 Гц.
        private const int EdgeRefreshFrames = 15;
        private int _edgeRefreshCountdown;

        // ── Подсветка изменённых полей ──────────────────────────────────
        private readonly Dictionary<TMP_InputField, string> _cleanValues = new();
        // Поля, чей последний ввод не был принят (красная рамка до следующей правки).
        private readonly List<TMP_InputField> _errorFields = new();
        private int _applyFrame = -1;  // защита от двойного Apply
        private bool _opening;  // защита от OnSelectionChanged → Close() внутри Open()
        private bool _currentIsTable;  // true когда текущий элемент — стол
        private bool _currentIsDoor;   // true когда текущий элемент — дверь
        private string _currentTypeName = "Деталь"; // для заголовка «Тип — Имя»

        // ── Раскладка ──────────────────────────────────────────────────
        // Меню собирается один раз (Build), а позиции пересчитываются в Layout
        // сверху вниз. Каждый видимый блок — одна строка в _layout. Всё якорится
        // к ВЕРХУ панели, поэтому изменение высоты панели не двигает содержимое,
        // а добавление нового пункта = добавить строку в список (без ручных
        // сдвигов и без риска забыть «затрекать» элемент).
        private struct LayoutRow
        {
            public RectTransform[] rects; // элементы одной вертикальной полосы (общий верх)
            public float height;          // высота полосы
            public float gapAfter;        // отступ под полосой
            public bool facadeOnly;       // показывать только для фасадов (секция зазоров)
            public bool drawerOnly;       // показывать только для ящиков
            public bool assembledOnly;    // показывать только для сборного фасада
            public bool radialOnly;       // показывать только для радиусной полки
			public bool tableOnly;        // показывать только для столов
			public bool pillarOnly;       // показывать только для опор
			public bool windowOnly;       // показывать только для окон
			public bool doorOnly;         // показывать только для дверей
			public bool lightOnly;        // показывать только для источников света
			public bool partOnly;         // показывать только для базовой «детали» (пазы)
			public bool gapsRow;          // секция зазоров: фасад ИЛИ ДВП/ХДФ
			public bool hideForWindow;    // скрывать для окон (повороты — окно живёт на стене)
            public GameObject toggleGO;   // объект, который включать/выключать по режиму
            public System.Func<bool>? visibleWhen; // доп. условие видимости (состояние элемента)
        }
        private readonly List<LayoutRow> _layout = new();
        private readonly List<RectTransform> _triLabels = new();
        private readonly List<RectTransform> _triFields = new();

        // Геометрия
        private const float LabelW = 140f;     // ширина колонки подписей (вмещает «Ширина короба, мм» почти без переноса)
        private const float LabelX = -80f;     // центр подписи (панель 364px → края ±182)
        private const float FieldX = 100f;     // центр поля ввода
        // Строка «Название» — своя геометрия во всю ширину панели (364px → края
        // ±182, поля по 10px → рабочая зона -172..172). Подпись занимает ровно
        // свою ширину, поле начинается сразу за ней и идёт до правого края.
        private const float NameLabelW = 76f;    // «Название» при 15px
        private const float NameLabelX = -134f;  // -172 + 76/2
        private const float NameFieldW = 264f;   // от -92 до 172
        private const float NameFieldX = 40f;    // (-92 + 172) / 2

        private const float LabelH = 24f;
        private const float FieldH = 24f;
        private const float RowH = 24f;      // высота строки «подпись + поле»
        private const float RowGap = 7f;     // отступ между строками (шаг ≈ 31)
        private const float TitleH = 28f;
        private const float TitleGap = 8f;
        private const float RotLblH = 22f;
        private const float RotLblGap = 4f;
        private const float BtnH = 28f;
        private const float ActionGap = 8f;
        private const float TopPad = 12f;
        private const float BottomPad = 12f;

        private const float TriCol1 = -110f;
        private const float TriCol2 = 0f;
        private const float TriCol3 = 110f;
        private const float TriLabelW = 95f;
        private const float TriFieldW = 70f;
        private const float TriLabelH = 18f;

        private void Awake()
        {
            Instance = this;
        }

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("ContextMenu", canvas, Vector2.zero, new Vector2(364, 560));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(-10, -60);
            _root = panel.gameObject;
            _panelRt = panel.rectTransform;
            WindowDrag.Attach(panel.rectTransform, TopPad + TitleH + TitleGap);
            _layout.Clear();

            // Заголовок — первая строка потока (стоит вплотную под верхом панели).
            // Показывает «Тип — Имя», чтобы окна разных элементов были различимы.
            _titleLabel = UIFactory.CreateLabel("CtxTitle", panel.transform, "Деталь", 20,
                Vector2.zero, new Vector2(300, TitleH), TextAnchor.MiddleCenter);
            _titleLabel.overflowMode = TextOverflowModes.Ellipsis;
            _titleLabel.enableWordWrapping = false;
            AddRow(TitleH, TitleGap, _titleLabel.rectTransform);

            // Тип: конвертация ТОЛЬКО внутри родственной группы (см. GroupOf):
            //   • структурная  — Деталь ↔ Фасад ↔ Сборный фасад ↔ Радиусная полка
            //     (пересоздаёт элемент через ElementConverter);
            //   • ящик         — Ящик GTV ↔ Ящик Movento (смена системы выдвижения).
            // Опции наполняются по элементу в Open(); у элементов без группы (окно,
            // дверь, стол, опора, ДВП, свет, стена) строка скрыта через visibleWhen.
            var typeOptions = new List<string>(); // реальный список ставит RefreshTypeDropdown
            foreach (var (_, label) in StructuralChoices) typeOptions.Add(label);
            _typeDropdown = LabeledDropdownRow(panel.transform, "Тип", typeOptions, OnTypeSelected,
                (h, g, rects) => AddRow(h, g, () => _target != null && GroupOf(_target) != TypeGroup.None, rects),
                "CtxType");

            // Размеры.
            _name = NameRow(panel.transform);
            AddRow(18f, RowGap, UIFactory.CreateSectionHeader("CtxSecDims", panel.transform, "Размеры", 332f));
            _w = Row(panel.transform, "Ширина");
            _h = Row(panel.transform, "Высота");
            _d = Row(panel.transform, "Глубина");
            _radius = RadialRow(panel.transform, "Радиус угла");

            // ── Пазы (только «деталь») ──────────────────────────────────
            // Кнопка-раскрывашка «Пазы (N) ▼» на всю ширину. В раскрытом виде —
            // строка-подсказка с размерами паза, строки текущих пазов (сторона и
            // тип редактируются на месте, справа — удаление), в конце — выбор
            // параметров нового паза и кнопка «Добавить».
            var grooveBtn = UIFactory.CreateButton("CtxGrooves", panel.transform, "Пазы (0)",
                new Vector2(0, 0), new Vector2(332, BtnH), ToggleGrooves);
            _grooveCountLabel = grooveBtn.GetComponentInChildren<TMP_Text>();
            AddPartRow(BtnH, RowGap, grooveBtn.GetComponent<RectTransform>());

            // Размеры паза фиксированы технологией — показываем их с единицами,
            // а не шифром «16*4*7».
            var grooveHint = UIFactory.CreateLabel("CtxGrooveHint", panel.transform,
                $"Паз: ширина {AppConstants.GROOVE_WIDTH_MM} мм, глубина {AppConstants.GROOVE_DEPTH_MM} мм, отступ от кромки {AppConstants.GROOVE_OFFSET_MM} мм",
                12, new Vector2(0, 0), new Vector2(332, 16), TextAnchor.MiddleLeft);
            grooveHint.color = UIStyle.TextSecondary;
            AddPartRowWhen(() => _groovesExpanded, 16f, 4f, grooveHint.rectTransform);

            // Порядок пунктов совпадает с порядком значений GrooveSide/GrooveKind.
            var grooveSideOptions = new List<string> { "Верх", "Низ", "Лево", "Право" };
            var grooveKindOptions = new List<string> { "Сквозной", "Глухой" };

            for (int i = 0; i < AppConstants.GROOVE_MAX_PER_PART; i++)
            {
                int index = i; // копия для замыкания: иначе все кнопки правили бы последний
                var sideDd = UIFactory.CreateDropdown($"CtxGrooveSide{i}", panel.transform,
                    new List<string>(grooveSideOptions), new Vector2(-114, 0), new Vector2(104, 28),
                    _ => EditGroove(index));
                var kindDd = UIFactory.CreateDropdown($"CtxGrooveKind{i}", panel.transform,
                    new List<string>(grooveKindOptions), new Vector2(26, 0), new Vector2(168, 28),
                    _ => EditGroove(index));
                var delBtn = UIFactory.CreateDangerButton($"CtxGrooveDel{i}", panel.transform, "×",
                    new Vector2(148, 0), new Vector2(28, 28), () => RemoveGroove(index));
                _grooveRowSide[i] = sideDd;
                _grooveRowKind[i] = kindDd;
                AddPartRowWhen(() => _groovesExpanded && GrooveCount() > index, 28f, 4f,
                    sideDd.GetComponent<RectTransform>(),
                    kindDd.GetComponent<RectTransform>(),
                    delBtn.GetComponent<RectTransform>());
            }

            _grooveSideDropdown = UIFactory.CreateDropdown("CtxGrooveSide", panel.transform,
                new List<string>(grooveSideOptions), new Vector2(-114, 0), new Vector2(104, 28), _ => { });
            _grooveKindDropdown = UIFactory.CreateDropdown("CtxGrooveKind", panel.transform,
                new List<string>(grooveKindOptions), new Vector2(0, 0), new Vector2(116, 28), _ => { });
            var grooveAddBtn = UIFactory.CreateButton("CtxGrooveAdd", panel.transform, "Добавить",
                new Vector2(116, 0), new Vector2(100, 28), AddGrooveFromUI);
            AddPartRowWhen(() => _groovesExpanded, 28f, ActionGap,
                _grooveSideDropdown.GetComponent<RectTransform>(),
                _grooveKindDropdown.GetComponent<RectTransform>(),
                grooveAddBtn.GetComponent<RectTransform>());

            // ── Кромки (деталь-лист: ровно одна сторона < 50 мм) ────────
            // Наличие кромки не редактируется — оно вычисляется по геометрии
            // (открытый торец = кромка). Здесь только выключатель, толщина
            // ленты и отказ от валидации.
            _edgeToggle = UIFactory.CreateToggle("CtxEdges", panel.transform, "Кромки", true,
                new Vector2(0, 0), new Vector2(332, 26), OnEdgeBandingToggled);
            AddPartRowWhen(EdgesEligible, 26f, RowGap,
                _edgeToggle.GetComponent<RectTransform>());

            _edgeDiagram = BuildEdgeDiagram(panel.transform, out float edgeDiagramH);
            AddPartRowWhen(EdgesShown, edgeDiagramH, RowGap, _edgeDiagram);

            var edgeThicknessLbl = UIFactory.CreateLabel("L_EdgeThickness", panel.transform,
                "Толщина кромки", 15, new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            _edgeThickness = UIFactory.CreateNumberField("F_EdgeThickness", panel.transform, "",
                new Vector2(FieldX, 0), new Vector2(120, FieldH), "мм");
            AddPartRowWhen(EdgesShown, RowH, RowGap, edgeThicknessLbl.rectTransform,
                _edgeThickness.GetComponent<RectTransform>());

            _edgeSkipToggle = UIFactory.CreateToggle("CtxEdgeSkipValidation", panel.transform,
                "Отключить валидацию", false, new Vector2(0, 0), new Vector2(332, 26),
                OnEdgeSkipValidationToggled);
            AddPartRowWhen(EdgesShown, 26f, ActionGap,
                _edgeSkipToggle.GetComponent<RectTransform>());

            // Зазоры (только для фасадов) — блок скрывается в режиме «деталь».
            var gapSection = CreateGapSection(panel.transform, out float gapSectionH);
            AddGapsRow(gapSection, gapSection.GetComponent<RectTransform>(), gapSectionH, RowGap);

            // Открывание фасада (только фасад): выпадающий список режима (12 рёбер +
            // 6 ящиков) и кнопка Открыть/Закрыть — отдельными строками.
            var modeOptions = new List<string>();
            for (int i = 0; i < FacadeDoor.Count; i++)
                modeOptions.Add(FacadeDoor.Label((DoorMode)i));
            _modeDropdown = LabeledDropdownRow(panel.transform, "Дверца", modeOptions,
                OnModeSelected, AddFacadeRow, "CtxMode");

            var doorButton = UIFactory.CreateButton("CtxDoor", panel.transform, "Открыть",
                new Vector2(0, 0), new Vector2(332, BtnH), ToggleDoor);
            _doorButtonLabel = doorButton.GetComponentInChildren<TMP_Text>();
            AddFacadeRow(BtnH, ActionGap, doorButton.GetComponent<RectTransform>());

            // Центр сборного фасада (только для сборного): Глухой / Витрина / Стекло.
            var fillOptions = new List<string> { "Глухой (панель)", "Витрина (пусто)", "Стекло" };
            _fillDropdown = LabeledDropdownRow(panel.transform, "Заполнение", fillOptions,
                OnFillSelected, AddAssembledRow, "CtxFill");

            // Ящик GTV: тип, длина, цвет, ширина, двойной ящик, анимация.
            var drawerTypeNames = new List<string> { "A — борт 86 мм", "B — борт 120 мм", "C — борт 168 мм", "D — борт 200 мм" };
            _drawerTypeDropdown = LabeledDropdownRow(panel.transform, "Тип ящика", drawerTypeNames,
                OnDrawerTypeChanged, AddDrawerRow, "CtxDrawerType");

            var drawerLenNames = new List<string>();
            foreach (var l in DrawerConstants.ValidLengths) drawerLenNames.Add($"{l} мм");
            _drawerLengthDropdown = LabeledDropdownRow(panel.transform, "Длина", drawerLenNames,
                OnDrawerLengthChanged, AddDrawerRow, "CtxDrawerLen");

            var drawerColorNames = new List<string> { "Антрацит", "Белый", "Чёрный" };
            _drawerColorDropdown = LabeledDropdownRow(panel.transform, "Цвет", drawerColorNames,
                OnDrawerColorChanged, AddDrawerRow, "CtxDrawerColor");

            _drawerWidth = DrawerFieldRow(panel.transform, "Ширина короба");

            // «Двойной ящик» — только для одиночного нижнего, когда над контуром
            // есть место под верхний внутренний ящик (мин. проём типа A).
            var drawerDoubleBtn = UIFactory.CreateButton("CtxDrawerDouble", panel.transform, "Двойной ящик",
                new Vector2(0, 0), new Vector2(332, BtnH), CreatePairedDrawer);
            AddDrawerRowWhen(CanCreateDoubleDrawer, BtnH, ActionGap,
                drawerDoubleBtn.GetComponent<RectTransform>());

            // Опции пары (виден только у двойного): длина верхнего ящика + удаление.
            var upperLenNames = new List<string>();
            foreach (var l in DrawerConstants.ValidLengths) upperLenNames.Add($"{l} мм");
            var upperLenLbl = UIFactory.CreateLabel("L_Верхний ящик", panel.transform, "Верхний ящик", 15,
                new Vector2(-103, 0), new Vector2(126, LabelH));
            _drawerUpperLenDropdown = UIFactory.CreateDropdown("CtxDrawerUpperLen", panel.transform, upperLenNames,
                new Vector2(65, 0), new Vector2(202, 28), OnDrawerUpperLengthChanged);
            AddDrawerRowWhen(HasUpperDrawer, 28f, ActionGap,
                upperLenLbl.rectTransform,
                _drawerUpperLenDropdown.GetComponent<RectTransform>());

            var drawerRemoveUpperBtn = UIFactory.CreateButton("CtxDrawerRemoveUpper", panel.transform,
                "Убрать верхний ящик", new Vector2(0, 0), new Vector2(332, BtnH), RemoveUpperDrawer);
            AddDrawerRowWhen(HasUpperDrawer, BtnH, ActionGap,
                drawerRemoveUpperBtn.GetComponent<RectTransform>());

            var drawerAnimBtn = UIFactory.CreateButton("CtxDrawerAnim", panel.transform, "Открыть",
                new Vector2(0, 0), new Vector2(332, BtnH), CycleDrawerAnimation);
            _drawerAnimLabel = drawerAnimBtn.GetComponentInChildren<TMP_Text>();
            AddDrawerRow(BtnH, ActionGap, drawerAnimBtn.GetComponent<RectTransform>());

            // Фасад ящика: выбор из существующих (создание/настройка — через сам фасад).
            _drawerFacadeDropdown = LabeledDropdownRow(panel.transform, "Фасад ящика",
                new List<string> { "(нет фасада)" }, OnDrawerFacadeSelected, AddDrawerRow, "CtxDrawerFacade");

            // Окно: тонировка стекла и выступ подоконника.
            var tintOptions = new List<string> { "Прозрачное", "Тонированное" };
            _tintDropdown = LabeledDropdownRow(panel.transform, "Стекло", tintOptions,
                OnTintSelected, (h, g, rects) => AddWindowRow(h, g, () => !_currentIsDoor, rects), "CtxTint");

            _sillProtrusion = WindowFieldRow(panel.transform, "Подоконник", () => !_currentIsDoor);

            // Дверь: тип створки (стекло/глухая).
            var sashTypeOptions = new List<string> { "Стекло", "Глухая" };
            _sashTypeDropdown = LabeledDropdownRow(panel.transform, "Створка", sashTypeOptions,
                OnSashTypeSelected, AddDoorRow, "CtxSashType");

            // Режим открывания окна и кнопка Открыть/Закрыть (как фасад).
            var winModeOptions = new List<string>
            {
                FacadeDoor.Label(DoorMode.HingeFrontLeft),
                FacadeDoor.Label(DoorMode.HingeFrontRight),
                FacadeDoor.Label(DoorMode.HingeFrontTop),
                FacadeDoor.Label(DoorMode.HingeFrontBottom),
            };
            _winModeDropdown = LabeledDropdownRow(panel.transform, "Открывание", winModeOptions,
                OnWindowModeSelected, AddWindowRow, "CtxWinMode");

            var winDoorBtn = UIFactory.CreateButton("CtxWinDoor", panel.transform, "Открыть",
                new Vector2(0, 0), new Vector2(332, BtnH), ToggleWindowDoor);
            _winDoorButtonLabel = winDoorBtn.GetComponentInChildren<TMP_Text>();
            AddWindowRow(BtnH, ActionGap, winDoorBtn.GetComponent<RectTransform>());

			// Сдвиг ножек внутрь стола (только для столов).
			_legInset = TableFieldRow(panel.transform, "Сдвиг ножек");

			// Высота средней секции опоры (только для опор).
			_midHeight = PillarFieldRow(panel.transform, "Средняя секция");

			// Параметры лампы (только для источников света).
			_lightTemp = LightFieldRow(panel.transform, "Температура", "K");
			_lightPower = LightFieldRow(panel.transform, "Мощность", "Вт");
			_lightDiffusion = LightFieldRow(panel.transform, "Рассеивание", "%");
			_lightBeam = LightFieldRow(panel.transform, "Угол пучка", "°");
			_lightUp = LightFieldRow(panel.transform, "Свет вверх", "%");

            // ── Положение ───────────────────────────────────────────────
            AddRow(18f, RowGap, UIFactory.CreateSectionHeader("CtxSecPos", panel.transform, "Положение", 332f));

            // Позиция и поворот — компактная раскладка 3 колонки. Всё в мм
            // (правило 1 UI-GUIDELINES: никаких метров в UI).
            _x = TriField(panel.transform, "X, мм", TriCol1);
            _y = TriField(panel.transform, "Y, мм", TriCol2);
            _z = TriField(panel.transform, "Z, мм", TriCol3);
            TriEndRow();

            // Поля поворота у окна скрыты: ориентацию диктует стена.
            _rx = TriField(panel.transform, "X, °", TriCol1);
            _ry = TriField(panel.transform, "Y, °", TriCol2);
            _rz = TriField(panel.transform, "Z, °", TriCol3);
            TriEndRow(hideForWindow: true);

			foreach (var f in new[] { _w, _h, _d, _radius, _drawerWidth, _legInset, _midHeight, _sillProtrusion, _lightTemp, _lightPower, _lightDiffusion, _lightUp, _lightBeam }) f!.contentType = TMP_InputField.ContentType.Custom;
            foreach (var f in new[] { _gapLeft, _gapRight, _gapTop, _gapBottom }) f!.contentType = TMP_InputField.ContentType.Custom;
            // Позиция — целые мм; углы — десятичные градусы.
            foreach (var f in new[] { _x, _y, _z }) f!.contentType = TMP_InputField.ContentType.Custom;
            foreach (var f in new[] { _rx, _ry, _rz }) f!.contentType = TMP_InputField.ContentType.Custom;

            // Арифметика: разрешаем + и - (пробелы допускаются, удаляются при вычислении).
            foreach (var f in new[] { _w, _h, _d, _radius, _drawerWidth, _legInset, _midHeight, _sillProtrusion, _lightTemp, _lightPower, _lightDiffusion, _lightUp, _lightBeam, _gapLeft, _gapRight, _gapTop, _gapBottom, _x, _y, _z })
                if (f != null) f.onValidateInput = (text, idx, ch) => char.IsDigit(ch) || ch == '+' || ch == '-' || ch == ' ' ? ch : '\0';
            foreach (var f in new[] { _rx, _ry, _rz })
                if (f != null) f.onValidateInput = (text, idx, ch) => char.IsDigit(ch) || ch == '+' || ch == '-' || ch == '.' || ch == ' ' ? ch : '\0';

            // Имя: недопустимые символы не даём набрать вовсе — иначе поле
            // показывало бы одно, а применилось бы очищенное другое.
            // Алфавит — ^[A-Za-z0-9_-]+$, см. ElementNaming.
            _name!.onValidateInput = (text, charIndex, ch) => ElementNaming.IsValid(ch.ToString()) ? ch : '\0';

            // Повороты на 90° вокруг каждой мировой оси. Отдельные X/Y/Z — чтобы
            // ставить детали вертикально (поворот по X/Z), а не только крутить по Y.
            // Для окна вся секция скрыта: окно стоит на стене, из поворотов
            // осмыслен только разворот на 180° (подоконником в другую сторону).
            var rotLbl = UIFactory.CreateLabel("CtxRotLbl", panel.transform, "Повернуть на 90°:", 15,
                Vector2.zero, new Vector2(340, RotLblH), TextAnchor.MiddleCenter);
            AddRowNoWindow(RotLblH, RotLblGap, rotLbl.rectTransform);

            var rotX = UIFactory.CreateButton("CtxRotX", panel.transform, "X 90°",
                new Vector2(-112, 0), new Vector2(112, BtnH), () => RotateAxis(Vector3.right));
            var rotY = UIFactory.CreateButton("CtxRotY", panel.transform, "Y 90°",
                new Vector2(0, 0), new Vector2(112, BtnH), () => RotateAxis(Vector3.up));
            var rotZ = UIFactory.CreateButton("CtxRotZ", panel.transform, "Z 90°",
                new Vector2(112, 0), new Vector2(112, BtnH), () => RotateAxis(Vector3.forward));
            AddRowNoWindow(BtnH, ActionGap,
                rotX.GetComponent<RectTransform>(),
                rotY.GetComponent<RectTransform>(),
                rotZ.GetComponent<RectTransform>());

            var rotY180 = UIFactory.CreateButton("CtxRotY180", panel.transform, "Y 180°",
                new Vector2(0, 0), new Vector2(332, BtnH), () => RotateAxis(Vector3.up, 180f));
            AddWindowRow(BtnH, ActionGap, rotY180.GetComponent<RectTransform>());

            // ── Материал (после положения — порядок секций по правилу 6) ──
            AddRow(18f, RowGap, UIFactory.CreateSectionHeader("CtxSecMat", panel.transform, "Материал", 332f));

            var matOptions = new List<string>();
            foreach (var m in MaterialCatalog.All) matOptions.Add(m.displayName);
            _materialDropdown = LabeledDropdownRow(panel.transform, "Текстура", matOptions,
                OnMaterialSelected, (h, g, rects) => AddRow(h, g, () => !_currentIsTable, rects), "CtxMaterial");

            // Текстуры столешницы и ножек (только для столов).
            _tabletopMaterialDropdown = LabeledDropdownRow(panel.transform, "Столешница",
                new List<string>(matOptions), OnMaterialSelected, AddTableRow, "CtxTableTop");
            _legsMaterialDropdown = LabeledDropdownRow(panel.transform, "Ножки",
                new List<string>(matOptions), OnLegsMaterialSelected, AddTableRow, "CtxTableLegs");

            // ── Свойства ────────────────────────────────────────────────
            _transparentToggle = UIFactory.CreateToggle("CtxTransparent", panel.transform, "Прозрачный", false,
                new Vector2(0, 0), new Vector2(332, 26), v =>
                {
                    if (_target == null) return;
                    _target.Transparent = v;
                    if (ElementHighlighter.Instance != null)
                        ElementHighlighter.Instance.ApplyForElement(_target);
                    if (SelectionManager.Instance != null)
                        SelectionManager.Instance.RefreshHighlight(_target);
                });
            AddRow(26f, 7f, _transparentToggle.GetComponent<RectTransform>());

            // «Закрепить», а не «Запретить перемещение»: позитивная формулировка
            // без двойного отрицания (правило 5 UI-GUIDELINES).
            _lockToggle = UIFactory.CreateToggle("CtxLock", panel.transform, "Закрепить", false,
                new Vector2(0, 0), new Vector2(332, 26), v => { if (_target != null) _target.Movable = !v; });
            AddRow(26f, UIStyle.GapSection, _lockToggle.GetComponent<RectTransform>());

            // ── Действия ────────────────────────────────────────────────
            // «Удалить» — danger: красная, не на всю ширину, отделена отступом
            // (правило 3). Кнопки «Применить» нет: поля применяются по
            // Enter/потере фокуса, единственная модель применения (правило 2).
            var dup = UIFactory.CreateButton("CtxDup", panel.transform, "Дублировать",
                new Vector2(-91, 0), new Vector2(150, 32), Duplicate);
            var del = UIFactory.CreateDangerButton("CtxDel", panel.transform, "Удалить",
                new Vector2(91, 0), new Vector2(150, 32), Delete);
            AddRow(32f, 0f,
                dup.GetComponent<RectTransform>(),
                del.GetComponent<RectTransform>());

            // Кнопка закрытия живёт в углу панели, вне потока раскладки.
            UIFactory.CreateCloseButton(panel.transform, Close);

			Layout(isFacade: false, isAssembled: false, isRadial: false, isDrawer: false, isTable: false, isPillar: false, isWindow: false, isDoor: false);
            _root!.SetActive(false);

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged += OnSelectionChanged;
        }

        private void OnDestroy()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
        }

        // ── Отложенное закрытие меню ──────────────────────────────────
        // Когда SelectionManager.Select() вызывает DeselectAll(),
        // OnSelectionChanged(null) приходит ПЕРЕД OnSelectionChanged(newElement).
        // Чтобы меню не закрылось и тут же не открылось заново — откладываем
        // закрытие на конец кадра. Если в том же кадре пришёл новый элемент —
        // отмена отложенного закрытия и обновление меню.
        private int _deferCloseFrame = -1;

        private void OnSelectionChanged(KitchenElement? element)
        {
            if (_opening) return;
            if (_root == null || !_root.activeSelf) return;
            if (element == null)
            {
                _deferCloseFrame = Time.frameCount;
                return;
            }
            _deferCloseFrame = -1;
            if (element != _target)
                Open(element);
        }

        private void ProcessDeferredClose()
        {
            if (_deferCloseFrame >= 0 && _deferCloseFrame < Time.frameCount)
            {
                _deferCloseFrame = -1;
                Close();
            }
        }

        // ── Построение элементов ───────────────────────────────────────

        private delegate void RowAdder(float height, float gapAfter, params RectTransform[] rects);

        /// <summary>Строка «подпись + выпадающий список»: правило 5 UI-GUIDELINES —
        /// дропдаун без подписи запрещён. Раскладку строки задаёт addRow
        /// (обычная, фасадная, ящичная и т.д.).</summary>
        private TMP_Dropdown LabeledDropdownRow(Transform parent, string label,
            List<string> options, System.Action<int> onChanged, RowAdder addRow,
            string? nodeName = null)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(-103, 0), new Vector2(126, LabelH));
            var dd = UIFactory.CreateDropdown(nodeName ?? ("Dd_" + label), parent, options,
                new Vector2(65, 0), new Vector2(202, 28), onChanged);
            addRow(28f, RowGap, lbl.rectTransform, dd.GetComponent<RectTransform>());
            return dd;
        }

        /// <summary>Строка «Название»: в отличие от Row подпись занимает не всю
        /// колонку под самую длинную надпись («Ширина короба, мм»), а ровно свою
        /// ширину — поле начинается сразу за ней и тянется до правого края панели.
        /// Имена длинные (Fasad_600x400_1), и в общие 120px они не влезали.</summary>
        private TMP_InputField NameRow(Transform parent)
        {
            var lbl = UIFactory.CreateLabel("L_Название", parent, "Название", 15,
                new Vector2(NameLabelX, 0), new Vector2(NameLabelW, LabelH));
            var field = UIFactory.CreateInputField("F_Название", parent, "",
                new Vector2(NameFieldX, 0), new Vector2(NameFieldW, FieldH));
            AddRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        // Единица измерения — серым суффиксом в поле («800 мм»), подпись без
        // неё (правило 1 UI-GUIDELINES).
        private TMP_InputField Row(Transform parent, string label, string unit = "мм")
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateNumberField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(120, FieldH), unit);
            AddRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        private TMP_InputField RadialRow(Transform parent, string label, string unit = "мм")
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateNumberField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(120, FieldH), unit);
            AddRadialRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        private GameObject CreateGapSection(Transform parent, out float sectionH)
        {
            var root = new GameObject("_GapSection");
            var rt = root.AddComponent<RectTransform>();
            rt.SetParent(parent, false);

            const float headerH = 20f;
            const float fieldH = 22f;
            const float pad = 2f;
            const float innerGap = 4f;
            const float smallFieldW = 45f;
            const float fieldGap = 4f;
            const float gapFieldX = FieldX - 27f;

            float top = pad;
            AddTopAnchoredChild(UIFactory.CreateLabel("CtxGapHdr", root.transform, "Зазоры:", 14,
                new Vector2(LabelX, -top), new Vector2(130, headerH), TextAnchor.MiddleLeft).rectTransform);
            top += headerH + innerGap;

            // Левый и правый зазор — стороны названы явно, а не «Ширина X».
            AddTopAnchoredChild(UIFactory.CreateLabel("Gap_LR", root.transform, "Слева / справа, мм", 13,
                new Vector2(LabelX, -top), new Vector2(140, 20), TextAnchor.MiddleLeft).rectTransform);
            _gapLeft = UIFactory.CreateInputField("F_gapLeft", root.transform, "0",
                new Vector2(gapFieldX, -top), new Vector2(smallFieldW, 22));
            AddTopAnchoredChild(_gapLeft.GetComponent<RectTransform>());
            _gapRight = UIFactory.CreateInputField("F_gapRight", root.transform, "0",
                new Vector2(gapFieldX + smallFieldW + fieldGap, -top), new Vector2(smallFieldW, 22));
            AddTopAnchoredChild(_gapRight.GetComponent<RectTransform>());
            top += fieldH + innerGap;

            // Верхний и нижний зазор.
            AddTopAnchoredChild(UIFactory.CreateLabel("Gap_TB", root.transform, "Сверху / снизу, мм", 13,
                new Vector2(LabelX, -top), new Vector2(140, 20), TextAnchor.MiddleLeft).rectTransform);
            _gapTop = UIFactory.CreateInputField("F_gapTop", root.transform, "0",
                new Vector2(gapFieldX, -top), new Vector2(smallFieldW, 22));
            AddTopAnchoredChild(_gapTop.GetComponent<RectTransform>());
            _gapBottom = UIFactory.CreateInputField("F_gapBottom", root.transform, "0",
                new Vector2(gapFieldX + smallFieldW + fieldGap, -top), new Vector2(smallFieldW, 22));
            AddTopAnchoredChild(_gapBottom.GetComponent<RectTransform>());
            top += fieldH + pad;

            sectionH = top;
            rt.sizeDelta = new Vector2(340, sectionH);
            root.SetActive(false);
            return root;
        }

        private static void AddTopAnchoredChild(RectTransform rt) => AnchorTop(rt);

        // ── Регистрация строк раскладки ────────────────────────────────

        private void AddRow(float height, float gapAfter, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter });
        }

        private void AddRow(float height, float gapAfter, System.Func<bool> visibleWhen, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, visibleWhen = visibleWhen });
        }

        private void AddRadialRow(float height, float gapAfter, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, radialOnly = true });
        }

        // Секция зазоров: у фасада это отступ от проёма, у ДВП/ХДФ —
        // технологический зазор в пазу. Механика одна (см. GappedBox).
        private void AddGapsRow(GameObject toggleGO, RectTransform rt, float height, float gapAfter)
        {
            AnchorTop(rt);
            _layout.Add(new LayoutRow
            {
                rects = new[] { rt },
                height = height,
                gapAfter = gapAfter,
                gapsRow = true,
                toggleGO = toggleGO
            });
        }

        // Фасад-строка без контейнера: сами rect'ы включаются/выключаются по режиму
        // (для строки выбора ребра-петли и кнопки «Открыть/Закрыть»).
        private void AddFacadeRow(float height, float gapAfter, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, facadeOnly = true });
        }

        // Строка только для сборного фасада (выпадающий список центра).
        private void AddAssembledRow(float height, float gapAfter, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, assembledOnly = true });
        }

        private void AddDrawerRow(float height, float gapAfter, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, drawerOnly = true });
        }

        // Drawer-строка с доп. условием видимости (например, «только когда есть пара»).
        private void AddDrawerRowWhen(System.Func<bool> visibleWhen, float height, float gapAfter,
            params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow
            {
                rects = rects, height = height, gapAfter = gapAfter,
                drawerOnly = true, visibleWhen = visibleWhen,
            });
        }

		private void AddTableRow(float height, float gapAfter, params RectTransform[] rects)
		{
			foreach (var rt in rects)
				if (rt != null) AnchorTop(rt);
			_layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, tableOnly = true });
		}

		private void AddPillarRow(float height, float gapAfter, params RectTransform[] rects)
		{
			foreach (var rt in rects)
				if (rt != null) AnchorTop(rt);
			_layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, pillarOnly = true });
		}

		private void AddWindowRow(float height, float gapAfter, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, windowOnly = true });
        }

        private void AddWindowRow(float height, float gapAfter, System.Func<bool> visibleWhen, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, windowOnly = true, visibleWhen = visibleWhen });
        }

        private void AddDoorRow(float height, float gapAfter, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, doorOnly = true });
        }

        // Строки секции пазов: видны только у базовой «детали».
        private void AddPartRow(float height, float gapAfter, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, partOnly = true });
        }

        // Part-строка с доп. условием (раскрыт ли список, есть ли паз с таким номером).
        private void AddPartRowWhen(System.Func<bool> visibleWhen, float height, float gapAfter,
            params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow
            {
                rects = rects, height = height, gapAfter = gapAfter,
                partOnly = true, visibleWhen = visibleWhen,
            });
        }

        // Строка, скрываемая для окон (повороты: окно всегда стоит на стене).
        private void AddRowNoWindow(float height, float gapAfter, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, hideForWindow = true });
        }

        // Строка «подпись + поле» только для ящика (обе части в одной drawer-строке —
        // иначе подпись и поле раскладывались бы разными циклами и разъезжались).
        private TMP_InputField DrawerFieldRow(Transform parent, string label)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateNumberField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(120, FieldH), "мм");
            AddDrawerRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

		private TMP_InputField TableFieldRow(Transform parent, string label)
		{
			var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
				new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
			var field = UIFactory.CreateNumberField("F_" + label, parent, "",
				new Vector2(FieldX, 0), new Vector2(120, FieldH), "мм");
			AddTableRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
			return field;
		}

		private TMP_InputField PillarFieldRow(Transform parent, string label)
		{
			var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
				new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
			var field = UIFactory.CreateNumberField("F_" + label, parent, "",
				new Vector2(FieldX, 0), new Vector2(120, FieldH), "мм");
			AddPillarRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
			return field;
		}

		private void AddLightRow(float height, float gapAfter, params RectTransform[] rects)
		{
			foreach (var rt in rects)
				if (rt != null) AnchorTop(rt);
			_layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter, lightOnly = true });
		}

		private TMP_InputField LightFieldRow(Transform parent, string label, string unit)
		{
			var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
				new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
			var field = UIFactory.CreateNumberField("F_" + label, parent, "",
				new Vector2(FieldX, 0), new Vector2(120, FieldH), unit);
			AddLightRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
			return field;
		}

		private TMP_InputField WindowFieldRow(Transform parent, string label, System.Func<bool>? visibleWhen = null)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateNumberField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(120, FieldH), "мм");
            if (visibleWhen != null)
                AddWindowRow(RowH, RowGap, visibleWhen, lbl.rectTransform, field.GetComponent<RectTransform>());
            else
                AddWindowRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        private TMP_InputField TriField(Transform parent, string label, float colX)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 12,
                new Vector2(colX, 0), new Vector2(TriLabelW, TriLabelH), TextAnchor.MiddleCenter);
            var field = UIFactory.CreateInputField("F_" + label, parent, "",
                new Vector2(colX, 0), new Vector2(TriFieldW, FieldH));
            _triLabels.Add(lbl.rectTransform);
            _triFields.Add(field.GetComponent<RectTransform>());
            return field;
        }

        private void TriEndRow(bool hideForWindow = false)
        {
            if (hideForWindow)
            {
                AddRowNoWindow(TriLabelH, 2f, _triLabels.ToArray());
                AddRowNoWindow(FieldH, RowGap, _triFields.ToArray());
            }
            else
            {
                AddRow(TriLabelH, 2f, _triLabels.ToArray());
                AddRow(FieldH, RowGap, _triFields.ToArray());
            }
            _triLabels.Clear();
            _triFields.Clear();
        }

        // Якорим к верхней кромке панели, pivot тоже сверху — тогда
        // anchoredPosition.y = отступ верхней кромки элемента от верха панели
        // (со знаком минус). Это домовая конвенция панелей проекта.
        private static void AnchorTop(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        }

        // ── Раскладка сверху вниз ───────────────────────────────────────

		private void Layout(bool isFacade, bool isAssembled, bool isRadial, bool isDrawer, bool isTable, bool isPillar = false, bool isWindow = false, bool isDoor = false, bool isPart = false, bool isPanel = false, bool isLight = false)
		{
			float cursor = TopPad;
			float contentBottom = TopPad;
			foreach (var row in _layout)
			{
				bool visible = (!row.facadeOnly || isFacade)
					&& (!row.assembledOnly || isAssembled)
					&& (!row.radialOnly || isRadial)
					&& (!row.drawerOnly || isDrawer)
					&& (!row.tableOnly || isTable)
					&& (!row.pillarOnly || isPillar)
					&& (!row.windowOnly || isWindow)
					&& (!row.doorOnly || isDoor)
					&& (!row.lightOnly || isLight)
					&& (!row.partOnly || isPart)
					&& (!row.gapsRow || isFacade || isPanel)
					&& !(row.hideForWindow && isWindow)
					&& (row.visibleWhen == null || row.visibleWhen());

				// Строку с visibleWhen тоже надо гасить: иначе скрытая строка
				// оставалась бы на экране в позиции от прошлой раскладки.
				if (row.toggleGO != null)
					row.toggleGO.SetActive(visible);
				else if (row.facadeOnly || row.assembledOnly || row.radialOnly || row.drawerOnly || row.tableOnly || row.pillarOnly || row.windowOnly || row.doorOnly || row.lightOnly || row.partOnly || row.gapsRow || row.hideForWindow || row.visibleWhen != null)
                    foreach (var rt in row.rects)
                        if (rt != null) rt.gameObject.SetActive(visible);

                if (!visible) continue;

                float topY = -cursor; // pivot сверху → это и есть верхняя кромка
                foreach (var rt in row.rects)
                    if (rt != null)
                        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, topY);

                contentBottom = cursor + row.height;
                cursor = contentBottom + row.gapAfter;
            }

            if (_panelRt != null)
                _panelRt.sizeDelta = new Vector2(_panelRt.sizeDelta.x, contentBottom + BottomPad);
        }

        private void Update()
        {
            ProcessDeferredClose();

            if (Input.GetKeyDown(KeyCode.Escape) && _root != null && _root.activeSelf)
                Close();

            if (_root != null && _root.activeSelf && _target != null)
            {
                RefreshTransformFields();

                // Пазы могли измениться мимо меню (undo/redo, MCP-команды).
                if (_target.SupportsGrooves
                    && GrooveFingerprint(_target.Grooves) != _grooveFingerprint)
                {
                    RefreshGrooveUI();
                    RelayoutForTarget();
                }

                // Кромки зависят от соседей, а не только от самой детали:
                // отодвинули полку от боковины — торец открылся. Опрашиваем
                // сцену редко (см. EdgeRefreshFrames), это не горячий путь.
                if (_target.SupportsEdges && --_edgeRefreshCountdown <= 0)
                    RefreshEdgeUI();
            }
        }

        private bool IsAnyFieldFocused()
        {
			foreach (var f in new[] { _name, _w, _h, _d, _radius, _drawerWidth, _legInset, _midHeight, _sillProtrusion, _edgeThickness, _gapLeft, _gapRight, _gapTop, _gapBottom, _x, _y, _z, _rx, _ry, _rz })
                if (f != null && f.isFocused) return true;
            return false;
        }

        private void RefreshTransformFields()
        {
            if (_target == null) return;
            if (_target is FacadeElement f && !f.IsDoorClosed) return;
            if (_target is WindowElement w && !w.IsDoorClosed) return;
            if (_target is DoorElement d && !d.IsDoorClosed) return;

            var pos = _target.transform.position;
            MaybeRefresh(_x, ToMM(pos.x));
            MaybeRefresh(_y, ToMM(pos.y));
            MaybeRefresh(_z, ToMM(pos.z));

            var eu = _target.transform.eulerAngles;
            MaybeRefresh(_rx, eu.x.ToString("F1"));
            MaybeRefresh(_ry, eu.y.ToString("F1"));
            MaybeRefresh(_rz, eu.z.ToString("F1"));

            // Размеры, имя, радиус угла, зазоры — тоже обновляем в реальном времени
            var dims = _target.DimensionsMM;
            MaybeRefresh(_w, dims.x.ToString());
            MaybeRefresh(_h, dims.y.ToString());
            MaybeRefresh(_d, dims.z.ToString());
            var radial = _target as RadialShelfElement;
            if (radial != null)
                MaybeRefresh(_radius, radial.CornerRadius.ToString());

            MaybeRefresh(_name, _target.PartName);
            RefreshTitle();

            var facade = _target as FacadeElement;
            if (facade != null)
            {
                MaybeRefresh(_gapLeft, facade.GapLeft.ToString());
                MaybeRefresh(_gapRight, facade.GapRight.ToString());
                MaybeRefresh(_gapTop, facade.GapTop.ToString());
                MaybeRefresh(_gapBottom, facade.GapBottom.ToString());
            }
            else if (_target is PanelElement panelRefresh)
            {
                MaybeRefresh(_gapLeft, panelRefresh.GapLeft.ToString());
                MaybeRefresh(_gapRight, panelRefresh.GapRight.ToString());
                MaybeRefresh(_gapTop, panelRefresh.GapTop.ToString());
                MaybeRefresh(_gapBottom, panelRefresh.GapBottom.ToString());
            }

            var table = _target as TableElement;
            if (table != null && _legInset != null)
                MaybeRefresh(_legInset, table.LegInsetMM.ToString());

			var radiusTable = _target as RadiusTableElement;
			if (radiusTable != null && _legInset != null)
				MaybeRefresh(_legInset, radiusTable.LegInsetMM.ToString());

			var pillar = _target as PillarElement;
			if (pillar != null && _midHeight != null)
				MaybeRefresh(_midHeight, pillar.MidHeightMM.ToString());

			var lightRt = _target as LightSourceElement;
			if (lightRt != null)
			{
				if (_lightTemp != null) MaybeRefresh(_lightTemp, lightRt.TemperatureK.ToString());
				if (_lightPower != null) MaybeRefresh(_lightPower, lightRt.PowerW.ToString());
				if (_lightDiffusion != null) MaybeRefresh(_lightDiffusion, lightRt.DiffusionPct.ToString());
				if (_lightBeam != null) MaybeRefresh(_lightBeam, lightRt.BeamAngleDeg.ToString());
				if (_lightUp != null) MaybeRefresh(_lightUp, lightRt.UpLightPct.ToString());
			}

			var window = _target as WindowElement;
            if (window != null && _sillProtrusion != null)
                MaybeRefresh(_sillProtrusion, window.SillProtrusionMM.ToString());
        }

        /// <summary>Позиция в мм: единый формат чисел UI (правило 1).</summary>
        private static string ToMM(float meters) =>
            Mathf.RoundToInt(meters / AppConstants.MM_TO_UNITS).ToString();

        /// <summary>Заголовок «Тип — Имя» (обновляется при открытии и переименовании).</summary>
        private void RefreshTitle()
        {
            if (_titleLabel == null || _target == null) return;
            _titleLabel.text = $"{_currentTypeName} — {_target.PartName}";
        }

        /// <summary>Обновить поле, если оно не в фокусе (юзер не редактирует).
        /// Также синхронизирует _cleanValues, чтобы подсветка не сбивалась.</summary>
        private void MaybeRefresh(TMP_InputField? field, string newValue)
        {
            if (field == null || field.isFocused) return;
            field.SetTextWithoutNotify(newValue);
            _cleanValues[field] = newValue;
        }

        public void Open(KitchenElement element)
        {
            if (element == null) return;

            // Верхний ящик пары своего окна свойств не имеет — открываем нижний.
            if (element is DrawerElement upper && upper.IsUpperDrawer)
            {
                var lower = upper.FindPaired();
                if (lower != null) element = lower;
            }

            _opening = true;
            try
            {
                _target = element;
                _groovesExpanded = false; // список пазов открывается свёрнутым
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);

                bool isFacade = element is FacadeElement;
                bool isRadial = element is RadialShelfElement;
                bool isDrawer = element is DrawerElement;
				bool isTable = element is TableElement;
				bool isRadiusTable = element is RadiusTableElement;
				bool isPillar = element is PillarElement;
				bool isWindow = element is WindowElement;
				bool isDoor = element is DoorElement;
			_currentIsTable = isTable || isRadiusTable;
			_currentIsDoor = isDoor;
				// Заголовок различает и подтипы («Сборный фасад» ≠ «Фасад») и
				// конкретный элемент (имя после тире).
				_currentTypeName = element is SinkElement ? "Мойка"
					: element is LightSourceElement ? "Источник света"
					: isPillar ? "Опора"
					: isRadiusTable ? "Радиусный стол"
					: isTable ? "Стол"
					: isDrawer ? DrawerConstants.GetDefaultName(((DrawerElement)element).System)
					: isWindow ? "Окно"
					: isDoor ? "Дверь"
					: element is PanelElement ? "ДВП/ХДФ"
					: isRadial ? "Радиусная полка"
					: element is AssembledFacadeElement ? "Сборный фасад"
					: isFacade ? "Фасад"
					: "Деталь";
				RefreshTitle();
                RefreshTypeDropdown(element);

                var dims = element.DimensionsMM;
                _name!.text = element.PartName;
                _w!.text = dims.x.ToString();
                _h!.text = dims.y.ToString();
                _d!.text = dims.z.ToString();
                var radial = element as RadialShelfElement;
                _radius!.text = radial != null
                    ? radial.CornerRadius.ToString()
                    : AppConstants.RADIAL_CORNER_RADIUS_DEFAULT.ToString();

                var facade = element as FacadeElement;
                if (facade != null)
                {
                    _gapLeft!.text = facade.GapLeft.ToString();
                    _gapRight!.text = facade.GapRight.ToString();
                    _gapTop!.text = facade.GapTop.ToString();
                    _gapBottom!.text = facade.GapBottom.ToString();
                }
                else if (element is PanelElement panelEl)
                {
                    _gapLeft!.text = panelEl.GapLeft.ToString();
                    _gapRight!.text = panelEl.GapRight.ToString();
                    _gapTop!.text = panelEl.GapTop.ToString();
                    _gapBottom!.text = panelEl.GapBottom.ToString();
                }
                UpdateDoorButton(facade);
                UpdateModeDropdown(facade);

                var assembled = element as AssembledFacadeElement;
                if (assembled != null && _fillDropdown != null)
                    _fillDropdown.SetValueWithoutNotify(FillToIndex(assembled.Fill));

                var drawer = element as DrawerElement;
                if (drawer != null)
                {
                    if (_drawerTypeDropdown != null)
                        _drawerTypeDropdown.SetValueWithoutNotify(DrawerConstants.TypeIndex(drawer.Type));
                    if (_drawerLengthDropdown != null)
                        _drawerLengthDropdown.SetValueWithoutNotify(System.Array.IndexOf(DrawerConstants.ValidLengths, drawer.NominalLength));
                    if (_drawerColorDropdown != null)
                        _drawerColorDropdown.SetValueWithoutNotify((int)drawer.Color);
                    if (_drawerWidth != null)
                        _drawerWidth.text = drawer.InternalWidth.ToString();
                    var upperDrawer = drawer.FindPaired();
                    if (_drawerUpperLenDropdown != null && upperDrawer != null)
                        _drawerUpperLenDropdown.SetValueWithoutNotify(
                            System.Array.IndexOf(DrawerConstants.ValidLengths, upperDrawer.NominalLength));
                    UpdateDrawerAnimButton(drawer);
                    RebuildDrawerFacadeOptions();
                    SetDrawerFacadeValue(drawer.AttachedFacadeName);
                }

                var table = element as TableElement;
                if (table != null && _legInset != null)
                    _legInset.text = table.LegInsetMM.ToString();

				var radiusTable = element as RadiusTableElement;
				if (radiusTable != null && _legInset != null)
					_legInset.text = radiusTable.LegInsetMM.ToString();

				var pillar = element as PillarElement;
				if (pillar != null && _midHeight != null)
					_midHeight.text = pillar.MidHeightMM.ToString();

				var lightEl = element as LightSourceElement;
				if (lightEl != null)
				{
					if (_lightTemp != null) _lightTemp.text = lightEl.TemperatureK.ToString();
					if (_lightPower != null) _lightPower.text = lightEl.PowerW.ToString();
					if (_lightDiffusion != null) _lightDiffusion.text = lightEl.DiffusionPct.ToString();
					if (_lightBeam != null) _lightBeam.text = lightEl.BeamAngleDeg.ToString();
					if (_lightUp != null) _lightUp.text = lightEl.UpLightPct.ToString();
				}

				var window = element as WindowElement;
                if (window != null)
                {
                    if (_tintDropdown != null)
                        _tintDropdown.SetValueWithoutNotify((int)window.Tint);
                    if (_sillProtrusion != null)
                        _sillProtrusion.text = window.SillProtrusionMM.ToString();
                    if (_winDoorButtonLabel != null)
                        _winDoorButtonLabel.text = window.IsOpen ? "Закрыть" : "Открыть";
                    if (_winModeDropdown != null)
                        _winModeDropdown.SetValueWithoutNotify((int)window.Mode);
                }

				var door = element as DoorElement;
                if (door != null)
                {
                    if (_sashTypeDropdown != null)
                        _sashTypeDropdown.SetValueWithoutNotify((int)door.SashType);
                    if (_winDoorButtonLabel != null)
                        _winDoorButtonLabel.text = door.IsOpen ? "Закрыть" : "Открыть";
                    if (_winModeDropdown != null)
                        _winModeDropdown.SetValueWithoutNotify((int)door.Mode);
                }

                // Габариты ящика (контурный бокс) вычисляются из типа/длины/ширины —
                // прямое редактирование недоступно, поля затемняются.
                SetDimensionFieldsEditable(!isDrawer);
                // Глубину окна диктует толщина стены — поле только для чтения.
                if (isWindow || isDoor) SetDimensionFieldEditable(_d, false);
                // Ширина и глубина опоры фиксированы — только для чтения.
                if (isPillar) { SetDimensionFieldEditable(_w, false); SetDimensionFieldEditable(_d, false); }

                if (_materialDropdown != null)
                {
                    RebuildMaterialOptions();
                    _materialDropdown.SetValueWithoutNotify(MaterialIndex(element.MaterialId));
                    _materialDropdown.RefreshShownValue();
                }

                var tbl = element as TableElement;
                var rTbl = element as RadiusTableElement;
                if (_tabletopMaterialDropdown != null && (tbl != null || rTbl != null))
                {
                    RebuildTabletopMaterialOptions();
                    var topId = tbl != null ? tbl.TabletopMaterialId : rTbl!.TabletopMaterialId;
                    _tabletopMaterialDropdown.SetValueWithoutNotify(MaterialIndex(topId));
                    _tabletopMaterialDropdown.RefreshShownValue();
                }
                if (_legsMaterialDropdown != null && (tbl != null || rTbl != null))
                {
                    RebuildLegsMaterialOptions();
                    var legsId = tbl != null ? tbl.LegsMaterialId : rTbl!.LegsMaterialId;
                    _legsMaterialDropdown.SetValueWithoutNotify(MaterialIndex(legsId));
                    _legsMaterialDropdown.RefreshShownValue();
                }

                // Пересчитываем раскладку под режим: секция зазоров показывается
                // только для фасадов, радиус — только для радиусной полки, сдвиг ножек — только для столов (включая радиусные), пазы — только для базовой детали, панель сама подгоняется по высоте.
			RefreshGrooveUI();
			RefreshEdgeUI();
			RelayoutForTarget();

                RefreshTransformFields();
                _transparentToggle!.SetIsOnWithoutNotify(element.Transparent);
                _lockToggle!.SetIsOnWithoutNotify(!element.Movable);

                ClearAllHighlights();
                TrackAllFields();

                _root!.transform.SetAsLastSibling();
                _root.SetActive(true);
            }
            finally
            {
                _opening = false;
            }
        }

        public void Close()
        {
            _target = null;
            if (_root != null) _root.SetActive(false);
        }

        private void Apply()
        {
            if (_target == null) return;
            var target = _target;
            _errorFields.Clear(); // ошибки прошлого применения сняты новым вводом
            // Правки размеров/позиции применяем к закрытой (логической) позе.
            if (target is FacadeElement fac) { fac.ForceClose(); UpdateDoorButton(fac); }
            if (target is DrawerElement dr) { dr.ForceClose(); UpdateDrawerAnimButton(dr); }
            if (target is WindowElement win) { win.ForceClose(); if (_winDoorButtonLabel != null) _winDoorButtonLabel.text = "Открыть"; }
            if (target is DoorElement doorElApp) { doorElApp.ForceClose(); if (_winDoorButtonLabel != null) _winDoorButtonLabel.text = "Открыть"; }

            var oldDims = target.DimensionsMM;
            var oldPos = target.transform.position;
            var oldRot = target.transform.rotation;

            // Через DrawerLinks: переименование обновляет обратные ссылки
            // (PairedDrawerName пары, AttachedFacadeName ящиков с этим фасадом)
            // и само чистит имя/разрешает коллизию суффиксом «_N».
            DrawerLinks.Rename(target, string.IsNullOrWhiteSpace(_name!.text) ? "Board" : _name!.text);
            target.gameObject.name = target.PartName;

			var radial = target as RadialShelfElement;
			var drawer = target as DrawerElement;
			var pillar = target as PillarElement;
			var table = target as TableElement;
			var radiusTable = target as RadiusTableElement;
			if (radial != null)
            {
                target.DimensionsMM = new Vector3Int(
                    ParseIntField(_w, oldDims.x),
                    ParseIntField(_h, oldDims.y),
                    ParseIntField(_d, oldDims.z));
                radial.CornerRadius = ParseIntField(_radius, radial.CornerRadius);
            }
            else if (drawer != null)
            {
                if (_drawerWidth != null) drawer.InternalWidth = ParseIntField(_drawerWidth, drawer.InternalWidth);
            }
            else if (pillar != null)
            {
                int newTotalH = ParseIntField(_h, oldDims.y);
                if (newTotalH != oldDims.y)
                {
                    int newMidH = Mathf.Clamp(
                        newTotalH - PillarElement.TopHeightMM - PillarElement.BottomHeightMM,
                        PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);
                    pillar.MidHeightMM = newMidH;
                    if (_midHeight != null) _midHeight.text = newMidH.ToString();
                }
                else if (_midHeight != null)
                {
                    pillar.MidHeightMM = ParseIntField(_midHeight, pillar.MidHeightMM);
                    _midHeight.text = pillar.MidHeightMM.ToString();
                }
                _h!.text = pillar.TotalHeightMM.ToString();
            }
            else
            {
                // Глубину окна диктует стена — поле Г игнорируется.
                target.DimensionsMM = new Vector3Int(
                    ParseIntField(_w, oldDims.x),
                    ParseIntField(_h, oldDims.y),
                    target is WindowElement || target is DoorElement ? oldDims.z : ParseIntField(_d, oldDims.z));
            }

            if (table != null && _legInset != null)
                table.LegInsetMM = ParseIntField(_legInset, table.LegInsetMM);

			if (radiusTable != null && _legInset != null)
				radiusTable.LegInsetMM = ParseIntField(_legInset, radiusTable.LegInsetMM);

			var windowEl = target as WindowElement;
            if (windowEl != null && _sillProtrusion != null)
                windowEl.SillProtrusionMM = ParseIntField(_sillProtrusion, windowEl.SillProtrusionMM);

			var lightApp = target as LightSourceElement;
			if (lightApp != null)
			{
				if (_lightTemp != null)
				{
					lightApp.TemperatureK = ParseIntField(_lightTemp, lightApp.TemperatureK);
					_lightTemp.text = lightApp.TemperatureK.ToString();
				}
				if (_lightPower != null)
				{
					lightApp.PowerW = ParseIntField(_lightPower, lightApp.PowerW);
					_lightPower.text = lightApp.PowerW.ToString();
				}
				if (_lightDiffusion != null)
				{
					lightApp.DiffusionPct = ParseIntField(_lightDiffusion, lightApp.DiffusionPct);
					_lightDiffusion.text = lightApp.DiffusionPct.ToString();
				}
				if (_lightBeam != null)
				{
					lightApp.BeamAngleDeg = ParseIntField(_lightBeam, lightApp.BeamAngleDeg);
					_lightBeam.text = lightApp.BeamAngleDeg.ToString();
				}
				if (_lightUp != null)
				{
					lightApp.UpLightPct = ParseIntField(_lightUp, lightApp.UpLightPct);
					_lightUp.text = lightApp.UpLightPct.ToString();
				}
			}

            // Сохраняем материал ножек (материал столешницы применяется через дропдаун).
            if (_legsMaterialDropdown != null && _currentIsTable)
            {
                var legsIndex = _legsMaterialDropdown.value;
                var legsAll = MaterialCatalog.All;
                if (legsIndex >= 0 && legsIndex < legsAll.Count)
                {
                    var legDef = legsAll[legsIndex];
                    if (table != null)
                    {
                        table.LegsMaterialId = legDef.id;
                        MaterialManager.ApplyLegs(table, legDef);
                    }
                    else if (radiusTable != null)
                    {
                        radiusTable.LegsMaterialId = legDef.id;
                        MaterialManager.ApplyLegs(radiusTable, legDef);
                    }
                }
            }

            var facade = target as FacadeElement;
            if (facade != null)
            {
                facade.GapLeft = ParseIntField(_gapLeft, facade.GapLeft);
                facade.GapRight = ParseIntField(_gapRight, facade.GapRight);
                facade.GapTop = ParseIntField(_gapTop, facade.GapTop);
                facade.GapBottom = ParseIntField(_gapBottom, facade.GapBottom);
            }
            else if (target is PanelElement panelApply)
            {
                panelApply.GapLeft = ParseIntField(_gapLeft, panelApply.GapLeft);
                panelApply.GapRight = ParseIntField(_gapRight, panelApply.GapRight);
                panelApply.GapTop = ParseIntField(_gapTop, panelApply.GapTop);
                panelApply.GapBottom = ParseIntField(_gapBottom, panelApply.GapBottom);
            }

            // Толщина кромки — отдельной командой: она не часть геометрии
            // детали, и складывать её в ResizeCommand нечестно по отношению
            // к откату («Ctrl+Z вернул размер, а толщину — нет»).
            if (target.SupportsEdges && _edgeThickness != null)
            {
                var edgesBefore = EdgeBandingState.Of(target);
                var edgesAfter = new EdgeBandingState(edgesBefore.enabled,
                    ParseEdgeThickness(_edgeThickness, edgesBefore.thicknessMM),
                    edgesBefore.skipValidation);
                if (!edgesAfter.Equals(edgesBefore))
                    CommandStack.Execute(new SetEdgeBandingCommand(target, edgesBefore, edgesAfter));
                _edgeThickness.text = EdgeBanding.FormatThickness(target.EdgeThicknessMM);
            }

            // Поля позиции — целые мм; внутренняя модель остаётся в метрах.
            target.transform.position = new Vector3(
                ParseMM(_x, oldPos.x),
                ParseMM(_y, oldPos.y),
                ParseMM(_z, oldPos.z));

            // У окна поля поворота скрыты (ориентацию диктует стена) — не трогаем.
            if (!(target is WindowElement) && !(target is DoorElement))
            {
                var euler = oldRot.eulerAngles;
                target.transform.rotation = Quaternion.Euler(
                    ParseAngle(_rx, euler.x),
                    ParseAngle(_ry, euler.y),
                    ParseAngle(_rz, euler.z));
            }

            // Окно живёт только на стене — сразу возвращаем его на стену,
            // чтобы команда в стеке хранила уже «прилипшую» позу.
            if (target is WindowElement winSnap) winSnap.SnapToWall();
            if (target is DoorElement doorSnap) doorSnap.SnapToWall();

            if (KitchenSettings.Instance.BlockOnViolation && WouldCauseViolation())
            {
                target.DimensionsMM = oldDims;
                target.transform.position = oldPos;
                target.transform.rotation = oldRot;
            }
            else
            {
                CommandStack.Execute(new ResizeCommand(target,
                    oldDims, target.DimensionsMM,
                    oldPos, target.transform.position,
                    oldRot, target.transform.rotation));
            }

            var newDims = target.DimensionsMM;
            _w!.text = newDims.x.ToString();
            if (pillar == null) _h!.text = newDims.y.ToString();
            _d!.text = newDims.z.ToString();
            if (radial != null)
                _radius!.text = radial.CornerRadius.ToString();

            if (table != null && _legInset != null)
                _legInset.text = table.LegInsetMM.ToString();

			if (radiusTable != null && _legInset != null)
				_legInset.text = radiusTable.LegInsetMM.ToString();

			if (pillar != null && _midHeight != null)
				_midHeight.text = pillar.MidHeightMM.ToString();

			if (facade != null)
            {
                _gapLeft!.text = facade.GapLeft.ToString();
                _gapRight!.text = facade.GapRight.ToString();
                _gapTop!.text = facade.GapTop.ToString();
                _gapBottom!.text = facade.GapBottom.ToString();
            }

            RefreshTitle();
            RefreshEdgeUI();
            RefreshHighlights();

            ClearAllHighlights();
            TrackAllFields();

            // Красные рамки непринятых значений — после сброса жёлтых подсветок,
            // чтобы пользователь видел, какое именно поле не применилось.
            foreach (var f in _errorFields)
                UIFactory.SetErrorHighlight(f);
        }

        private bool WouldCauseViolation()
        {
            if (_target == null) return false;
            var list = PartRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);
            return result.violations.Contains(_target);
        }

        private void RotateAxis(Vector3 axis, float angle = 90f)
        {
            if (_target == null) return;
            var oldRot = _target.transform.rotation;
            _target.RotateAroundAxis(axis, angle);
            if (_target is WindowElement win) win.SnapToWall();
            if (_target is DoorElement doorRot) doorRot.SnapToWall();
            CommandStack.Execute(new MoveCommand(_target,
                _target.transform.position, _target.transform.position,
                oldRot, _target.transform.rotation));
            RefreshTransformFields();
            RefreshHighlights();
        }

        // ── Открывание дверцы (только фасад) ───────────────────────────

        private void ToggleDoor()
        {
            if (_target is FacadeElement f)
            {
                var drawer = FindDrawerForFacade(f);
                if (drawer != null)
                {
                    if (drawer.FindPaired() != null) drawer.CycleDoubleState();
                    else drawer.ToggleOpen();
                }
                else
                {
                    f.ToggleDoor();
                }
                UpdateDoorButton(f);
            }
        }

        private void OnModeSelected(int index)
        {
            if (_target is FacadeElement f)
                f.Mode = (DoorMode)index;
        }

        // ── Открывание окна ─────────────────────────────────────────

        private void ToggleWindowDoor()
        {
            if (_target is WindowElement w)
            {
                w.ToggleOpen();
                if (_winDoorButtonLabel != null)
                    _winDoorButtonLabel.text = w.IsOpen ? "Закрыть" : "Открыть";
            }
            else if (_target is DoorElement d)
            {
                d.ToggleOpen();
                if (_winDoorButtonLabel != null)
                    _winDoorButtonLabel.text = d.IsOpen ? "Закрыть" : "Открыть";
            }
        }

        private void OnTintSelected(int index)
        {
            if (_target is WindowElement w)
                w.Tint = (GlassTint)index;
        }

        private void OnSashTypeSelected(int index)
        {
            if (_target is DoorElement d)
                d.SashType = (DoorSashType)index;
        }

        private void OnWindowModeSelected(int index)
        {
            if (_target is WindowElement w)
                w.Mode = (DoorMode)index;
            else if (_target is DoorElement d)
                d.Mode = (DoorMode)index;
        }

        // Порядок пунктов списка центра: 0=Глухой, 1=Витрина(пусто), 2=Стекло.
        private static readonly AssembledFill[] FillOrder =
            { AssembledFill.Blind, AssembledFill.Open, AssembledFill.Glass };

        private static int FillToIndex(AssembledFill fill)
        {
            for (int i = 0; i < FillOrder.Length; i++)
                if (FillOrder[i] == fill) return i;
            return 0;
        }

        private void OnFillSelected(int index)
        {
            if (_target is AssembledFacadeElement a && index >= 0 && index < FillOrder.Length)
            {
                a.Fill = FillOrder[index];
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.RefreshHighlight(a);
            }
        }

        // ── Пазы детали ───────────────────────────────────────────────
        // Все правки набора пазов идут через SetGroovesCommand: Ctrl+Z обязан
        // работать для любой мутации (правило 2 UI-GUIDELINES).

        private int GrooveCount() => _target != null ? _target.Grooves.Count : 0;

        private void ToggleGrooves()
        {
            _groovesExpanded = !_groovesExpanded;
            RefreshGrooveUI();
            RelayoutForTarget();
        }

        private void AddGrooveFromUI()
        {
            if (_target == null || _grooveSideDropdown == null || _grooveKindDropdown == null) return;
            var spec = new GrooveSpec((GrooveKind)_grooveKindDropdown.value,
                (GrooveSide)_grooveSideDropdown.value);

            var after = new List<GrooveSpec>(_target.Grooves);
            if (after.Contains(spec))
            {
                ToastNotification.Instance?.Show("Такой паз уже есть");
                return;
            }
            if (after.Count >= AppConstants.GROOVE_MAX_PER_PART)
            {
                ToastNotification.Instance?.Show($"Не больше {AppConstants.GROOVE_MAX_PER_PART} пазов на деталь");
                return;
            }
            after.Add(spec);
            ApplyGrooves(after);
            _groovesExpanded = true;
            AfterGroovesChanged();
        }

        private void RemoveGroove(int index)
        {
            if (_target == null || index < 0 || index >= _target.Grooves.Count) return;
            var after = new List<GrooveSpec>(_target.Grooves);
            after.RemoveAt(index);
            ApplyGrooves(after);
            AfterGroovesChanged();
        }

        /// <summary>Правка паза на месте: сторона/тип берутся из дропдаунов строки.</summary>
        private void EditGroove(int index)
        {
            if (_target == null || index < 0 || index >= _target.Grooves.Count) return;
            var sideDd = _grooveRowSide[index];
            var kindDd = _grooveRowKind[index];
            if (sideDd == null || kindDd == null) return;

            var spec = new GrooveSpec((GrooveKind)kindDd.value, (GrooveSide)sideDd.value);
            var after = new List<GrooveSpec>(_target.Grooves);
            if (after[index].Equals(spec)) return;
            for (int i = 0; i < after.Count; i++)
                if (i != index && after[i].Equals(spec))
                {
                    ToastNotification.Instance?.Show("Такой паз уже есть");
                    RefreshGrooveUI(); // вернуть дропдауны к фактическому набору
                    return;
                }
            after[index] = spec;
            ApplyGrooves(after);
            AfterGroovesChanged();
        }

        private void ApplyGrooves(List<GrooveSpec> after)
        {
            if (_target == null) return;
            CommandStack.Execute(new SetGroovesCommand(_target, _target.Grooves, after));
        }

        private void AfterGroovesChanged()
        {
            RefreshGrooveUI();
            RelayoutForTarget();
            RefreshHighlights();
        }

        /// <summary>Обновить кнопку-раскрывашку и строки пазов.</summary>
        private void RefreshGrooveUI()
        {
            if (_grooveCountLabel != null)
                _grooveCountLabel.text =
                    $"Пазы ({GrooveCount()})  {(_groovesExpanded ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed)}";

            IReadOnlyList<GrooveSpec>? grooves = _target != null ? _target.Grooves : null;
            _grooveFingerprint = GrooveFingerprint(grooves);
            for (int i = 0; i < _grooveRowSide.Length; i++)
            {
                if (grooves == null || i >= grooves.Count) continue;
                _grooveRowSide[i]?.SetValueWithoutNotify((int)grooves[i].side);
                _grooveRowSide[i]?.RefreshShownValue();
                _grooveRowKind[i]?.SetValueWithoutNotify((int)grooves[i].kind);
                _grooveRowKind[i]?.RefreshShownValue();
            }
        }

        /// <summary>Дешёвый отпечаток набора пазов: ловим изменения мимо меню
        /// (undo/redo, MCP), чтобы строки не показывали устаревший набор.</summary>
        private static int GrooveFingerprint(IReadOnlyList<GrooveSpec>? grooves)
        {
            if (grooves == null) return 0;
            unchecked
            {
                int h = 17;
                foreach (var g in grooves) h = h * 31 + g.GetHashCode();
                return h;
            }
        }

        // ── Кромки детали ─────────────────────────────────────────────
        // Кромка на конкретном торце не хранится и не редактируется: она
        // вычисляется из геометрии (открытый торец → кромка). Пользователь
        // правит только выключатель, толщину ленты и отказ от валидации —
        // все три через одну команду, чтобы Ctrl+Z возвращал блок целиком.

        /// <summary>Деталь вообще может кромковаться (лист с ровно одной
        /// стороной тоньше 50 мм).</summary>
        private bool EdgesEligible() => _target != null && _target.SupportsEdges;

        /// <summary>Показывать схему и параметры кромки.</summary>
        private bool EdgesShown() => EdgesEligible() && _target!.EdgeBandingEnabled;

        /// <summary>Схема детали: пласть с четырьмя торцами-полосами и подписи
        /// длины/ширины. Размер фиксирован — под габарит детали не подгоняется.</summary>
        private RectTransform BuildEdgeDiagram(Transform parent, out float height)
        {
            const float boardW = 200f, boardH = 110f, strip = 10f;
            const float boardX = -50f, boardY = -10f;
            height = 140f;

            var root = UIFactory.CreateRect("CtxEdgeDiagram", parent);
            root.sizeDelta = new Vector2(332f, height);

            UIFactory.CreatePanel("CtxEdgeBoard", root, new Vector2(boardX, boardY),
                new Vector2(boardW, boardH), UIStyle.EdgeBoard);

            // Полосы-торцы. L1/W1 — стороны по положительному направлению осей
            // детали (верх и право), L2/W2 — по отрицательному.
            _edgeStripL1 = UIFactory.CreatePanel("CtxEdgeL1", root,
                new Vector2(boardX, boardY + (boardH - strip) * 0.5f),
                new Vector2(boardW, strip), UIStyle.EdgeAbsent);
            _edgeStripL2 = UIFactory.CreatePanel("CtxEdgeL2", root,
                new Vector2(boardX, boardY - (boardH - strip) * 0.5f),
                new Vector2(boardW, strip), UIStyle.EdgeAbsent);
            _edgeStripW1 = UIFactory.CreatePanel("CtxEdgeW1", root,
                new Vector2(boardX + (boardW - strip) * 0.5f, boardY),
                new Vector2(strip, boardH), UIStyle.EdgeAbsent);
            _edgeStripW2 = UIFactory.CreatePanel("CtxEdgeW2", root,
                new Vector2(boardX - (boardW - strip) * 0.5f, boardY),
                new Vector2(strip, boardH), UIStyle.EdgeAbsent);

            // Подписи сторон — те же имена, что и колонки CSV.
            EdgeSideLabel(root, "CtxEdgeLblL1", "L1", new Vector2(boardX, boardY + 36f));
            EdgeSideLabel(root, "CtxEdgeLblL2", "L2", new Vector2(boardX, boardY - 36f));
            EdgeSideLabel(root, "CtxEdgeLblW1", "W1", new Vector2(boardX + 76f, boardY));
            EdgeSideLabel(root, "CtxEdgeLblW2", "W2", new Vector2(boardX - 76f, boardY));

            _edgeLengthLabel = UIFactory.CreateLabel("CtxEdgeLen", root, "", 12,
                new Vector2(boardX, boardY + boardH * 0.5f + 11f), new Vector2(120, 18),
                TextAnchor.MiddleCenter);
            _edgeLengthLabel.color = UIStyle.TextSecondary;
            _edgeWidthLabel = UIFactory.CreateLabel("CtxEdgeWid", root, "", 12,
                new Vector2(boardX + boardW * 0.5f + 43f, boardY), new Vector2(70, 18),
                TextAnchor.MiddleLeft);
            _edgeWidthLabel.color = UIStyle.TextSecondary;

            return root;
        }

        private static void EdgeSideLabel(Transform parent, string name, string text, Vector2 pos)
        {
            var lbl = UIFactory.CreateLabel(name, parent, text, 11, pos, new Vector2(30, 14),
                TextAnchor.MiddleCenter);
            lbl.color = UIStyle.TextSecondary;
            lbl.raycastTarget = false;
        }

        private void OnEdgeBandingToggled(bool on)
        {
            if (_target == null || !_target.SupportsEdges) return;
            var before = EdgeBandingState.Of(_target);
            ApplyEdgeState(before, new EdgeBandingState(on, before.thicknessMM, before.skipValidation));
        }

        private void OnEdgeSkipValidationToggled(bool on)
        {
            if (_target == null || !_target.SupportsEdges) return;
            var before = EdgeBandingState.Of(_target);
            ApplyEdgeState(before, new EdgeBandingState(before.enabled, before.thicknessMM, on));
        }

        private void ApplyEdgeState(EdgeBandingState before, EdgeBandingState after)
        {
            if (_target == null || after.Equals(before)) return;
            CommandStack.Execute(new SetEdgeBandingCommand(_target, before, after));
            RefreshEdgeUI();
            RelayoutForTarget();
        }

        /// <summary>Обновить схему кромок и параметры под текущую геометрию.</summary>
        private void RefreshEdgeUI()
        {
            _edgeRefreshCountdown = EdgeRefreshFrames;
            if (_target == null || !_target.SupportsEdges) return;

            _edgeToggle?.SetIsOnWithoutNotify(_target.EdgeBandingEnabled);
            _edgeSkipToggle?.SetIsOnWithoutNotify(_target.EdgeSkipValidation);
            MaybeRefresh(_edgeThickness, EdgeBanding.FormatThickness(_target.EdgeThicknessMM));

            if (!_target.EdgeBandingEnabled) return;

            var layout = EdgeBanding.LayoutOf(_target.DimensionsMM);
            if (_edgeLengthLabel != null) _edgeLengthLabel.text = $"{layout.LengthMM} мм";
            if (_edgeWidthLabel != null) _edgeWidthLabel.text = $"{layout.WidthMM} мм";

            var coverage = EdgeBanding.Coverage(_target, PartRegistry.GetAll());
            PaintEdgeStrip(_edgeStripL1, coverage.HasEdge(EdgeSide.L1));
            PaintEdgeStrip(_edgeStripL2, coverage.HasEdge(EdgeSide.L2));
            PaintEdgeStrip(_edgeStripW1, coverage.HasEdge(EdgeSide.W1));
            PaintEdgeStrip(_edgeStripW2, coverage.HasEdge(EdgeSide.W2));
        }

        private static void PaintEdgeStrip(Image? strip, bool hasEdge)
        {
            if (strip != null) strip.color = hasEdge ? UIStyle.EdgePresent : UIStyle.EdgeAbsent;
        }

        /// <summary>Толщина кромки из поля: дробное число в мм. Значение вне
        /// допустимого диапазона не молча клампится, а помечается ошибкой.</summary>
        private float ParseEdgeThickness(TMP_InputField? f, float fallback)
        {
            if (f == null) return fallback;
            // Запятая как разделитель: пользователь набирает «0,5» на русской
            // раскладке, а формат вывода — всегда с точкой.
            var text = f.text.Replace(',', '.');
            if (float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v)
                && v >= AppConstants.EDGE_THICKNESS_MIN_MM
                && v <= AppConstants.EDGE_THICKNESS_MAX_MM)
                return v;

            MarkError(f);
            return fallback;
        }

        /// <summary>Пересчитать раскладку под текущий элемент. Нужна там, где
        /// меняется состав видимых строк без переоткрытия меню (пазы).</summary>
        private void RelayoutForTarget()
        {
            if (_target == null) return;
            bool isTable = _target is TableElement || _target is RadiusTableElement;
            bool isWindow = _target is WindowElement;
            bool isDoor = _target is DoorElement;
            Layout(_target is FacadeElement, _target is AssembledFacadeElement,
                _target is RadialShelfElement, _target is DrawerElement,
                isTable, _target is PillarElement,
                isWindow || isDoor, isDoor, _target.SupportsGrooves,
                _target is PanelElement, _target is LightSourceElement);
        }

        // ── Текстура/декор (детали и фасады) ────────────────────────────

        private static int MaterialIndex(string materialId)
        {
            var all = MaterialCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].id == materialId) return i;
            return 0; // неизвестный/дефолтный — первый пункт
        }

        private void RebuildMaterialOptions()
        {
            var opts = new List<TMP_Dropdown.OptionData>();
            foreach (var m in MaterialCatalog.All)
                opts.Add(new TMP_Dropdown.OptionData(m.displayName));
            _materialDropdown!.options = opts;
        }

        private void RebuildTabletopMaterialOptions()
        {
            var opts = new List<TMP_Dropdown.OptionData>();
            foreach (var m in MaterialCatalog.All)
                opts.Add(new TMP_Dropdown.OptionData(m.displayName));
            _tabletopMaterialDropdown!.options = opts;
        }

        private void RebuildLegsMaterialOptions()
        {
            var opts = new List<TMP_Dropdown.OptionData>();
            foreach (var m in MaterialCatalog.All)
                opts.Add(new TMP_Dropdown.OptionData(m.displayName));
            _legsMaterialDropdown!.options = opts;
        }

        private void OnMaterialSelected(int index)
        {
            if (_target == null) return;
            var all = MaterialCatalog.All;
            if (index < 0 || index >= all.Count) return;

            if (_currentIsTable)
            {
                var def = all[index];
                if (_target is TableElement tableEl)
                    MaterialManager.ApplyTabletop(tableEl, def);
                else if (_target is RadiusTableElement rtEl)
                    MaterialManager.ApplyTabletop(rtEl, def);
            }
            else
            {
                MaterialManager.Apply(_target, all[index]);
            }

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.RefreshHighlight(_target);
            RefreshHighlights();
        }

        private void OnLegsMaterialSelected(int index)
        {
            if (_target == null) return;
            var all = MaterialCatalog.All;
            if (index < 0 || index >= all.Count) return;

            var def = all[index];
            if (_target is TableElement tableEl)
                MaterialManager.ApplyLegs(tableEl, def);
            else if (_target is RadiusTableElement rtEl)
                MaterialManager.ApplyLegs(rtEl, def);

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.RefreshHighlight(_target);
            RefreshHighlights();
        }

        // ── Тип: родственные группы конвертации ─────────────────────────
        private enum TypeGroup { None, Structural, Drawer }

        private enum TypeChoice { Part, Facade, AssembledFacade, RadialShelf, DrawerGtv, DrawerMovento }

        private static readonly (TypeChoice choice, string label)[] StructuralChoices =
        {
            (TypeChoice.Part, "Деталь"),
            (TypeChoice.Facade, "Фасад"),
            (TypeChoice.AssembledFacade, "Сборный фасад"),
            (TypeChoice.RadialShelf, "Радиусная полка"),
        };

        private static readonly (TypeChoice choice, string label)[] DrawerChoices =
        {
            (TypeChoice.DrawerGtv, "Ящик GTV"),
            (TypeChoice.DrawerMovento, "Ящик Movento"),
        };

        // Выбор, соответствующий индексу текущего списка (наполняется в RefreshTypeDropdown).
        private readonly List<TypeChoice> _typeChoices = new();

        /// <summary>Родственная группа элемента для конвертации типа. None → строка «Тип»
        /// скрыта (окно/дверь/стол/опора/ДВП/свет/стена/пол — своя роль, конвертации нет).</summary>
        private static TypeGroup GroupOf(KitchenElement e)
        {
            if (e == null) return TypeGroup.None;
            if (e is DrawerElement) return TypeGroup.Drawer;
            if (e is TableElement || e is RadiusTableElement || e is PillarElement
                || e is WindowElement || e is DoorElement || e is PanelElement
                || e is LightSourceElement || e is FloorElement
                || e is SinkElement) return TypeGroup.None;
            if (e.GetComponent<Wall>() != null || e.GetComponent<BasePlate>() != null) return TypeGroup.None;
            // AssembledFacade — подкласс Facade; порядок проверок не важен, обе → структурная.
            if (e is AssembledFacadeElement || e is RadialShelfElement || e is FacadeElement)
                return TypeGroup.Structural;
            if (e.GetType() == typeof(KitchenElement)) return TypeGroup.Structural; // голая «Деталь»
            return TypeGroup.None;
        }

        private static TypeChoice CurrentChoice(KitchenElement e)
        {
            if (e is DrawerElement d)
                return d.System == DrawerSystem.Movento ? TypeChoice.DrawerMovento : TypeChoice.DrawerGtv;
            if (e is AssembledFacadeElement) return TypeChoice.AssembledFacade;
            if (e is RadialShelfElement) return TypeChoice.RadialShelf;
            if (e is FacadeElement) return TypeChoice.Facade;
            return TypeChoice.Part;
        }

        /// <summary>Наполнить список «Тип» опциями родственной группы элемента и
        /// выставить текущее значение. Для группы None список пуст (строку гасит Layout).</summary>
        private void RefreshTypeDropdown(KitchenElement element)
        {
            if (_typeDropdown == null) return;
            _typeChoices.Clear();
            _typeDropdown.ClearOptions();

            var group = GroupOf(element);
            if (group == TypeGroup.None) return;

            var set = group == TypeGroup.Drawer ? DrawerChoices : StructuralChoices;
            var labels = new List<string>(set.Length);
            foreach (var (choice, label) in set) { _typeChoices.Add(choice); labels.Add(label); }
            _typeDropdown.AddOptions(labels);

            int idx = _typeChoices.IndexOf(CurrentChoice(element));
            _typeDropdown.SetValueWithoutNotify(idx < 0 ? 0 : idx);
            _typeDropdown.RefreshShownValue();
        }

        private void OnTypeSelected(int index)
        {
            if (_target == null || index < 0 || index >= _typeChoices.Count) return;
            var choice = _typeChoices[index];

            // Ящик: смена системы выдвижения — тот же элемент, пересобираем меш и меню.
            if (choice == TypeChoice.DrawerGtv || choice == TypeChoice.DrawerMovento)
            {
                if (_target is DrawerElement drawer)
                {
                    var sys = choice == TypeChoice.DrawerMovento ? DrawerSystem.Movento : DrawerSystem.Gtv;
                    if (drawer.System != sys)
                    {
                        drawer.System = sys;
                        Open(drawer); // обновить заголовок, значение и спецификацию
                        RefreshHighlights();
                    }
                }
                return;
            }

            // Родственные структурные типы: конвертация пересоздаёт элемент.
            var targetType = choice switch
            {
                TypeChoice.Facade => ElementConverter.TargetType.Facade,
                TypeChoice.AssembledFacade => ElementConverter.TargetType.AssembledFacade,
                TypeChoice.RadialShelf => ElementConverter.TargetType.RadialShelf,
                _ => ElementConverter.TargetType.Part,
            };
            if (ElementConverter.GetElementType(_target) == targetType) return;

            var converted = ElementConverter.Convert(_target, targetType);
            if (converted != null)
                Open(converted);
            RefreshHighlights();
        }

        private void OnDrawerTypeChanged(int index)
        {
            // Значения enum DrawerType — высоты в мм; индекс дропдауна кастовать нельзя.
            if (_target is DrawerElement d)
                d.Type = DrawerConstants.TypeFromIndex(index);
        }

        private void OnDrawerLengthChanged(int index)
        {
            if (_target is DrawerElement d && index >= 0 && index < DrawerConstants.ValidLengths.Length)
                d.NominalLength = DrawerConstants.ValidLengths[index];
        }

        private void OnDrawerColorChanged(int index)
        {
            if (_target is DrawerElement d && index >= 0 && index <= 2)
                d.Color = (DrawerColor)index;
        }

        private void CycleDrawerAnimation()
        {
            if (_target is DrawerElement d)
            {
                // Цикл трёх состояний — только при живой паре; флаг IsDouble без
                // пары (битые ссылки, старые сцены) ведёт себя как одиночный.
                if (d.FindPaired() != null) d.CycleDoubleState();
                else d.ToggleOpen();
                UpdateDrawerAnimButton(d);
            }
        }

        // ── Двойной ящик ─────────────────────────────────────────────
        // Верхний внутренний ящик (тип A) жёстко привязан к нижнему: своего окна
        // свойств не имеет, из окна нижнего настраивается только его длина.

        private bool HasUpperDrawer() =>
            _target is DrawerElement d && !d.IsUpperDrawer && d.FindPaired() != null;

        private bool CanCreateDoubleDrawer()
        {
            if (!(_target is DrawerElement d) || d.IsUpperDrawer || d.FindPaired() != null)
                return false;
            float freeMM = DrawerValidator.FreeHeightAboveMM(d, PartRegistry.GetAll());
            return freeMM >= DrawerConstants.GetMinOpeningHeight(DrawerConstants.UPPER_DRAWER_TYPE);
        }

        private void CreatePairedDrawer()
        {
            if (!(_target is DrawerElement d)) return;
            var pair = DrawerLinks.CreatePair(d);
            if (pair == null) return;
            CommandStack.Execute(new CreateCommand(pair.gameObject));
            RefreshHighlights();
            Open(d); // перестроить строки пары и подпись кнопки анимации
        }

        private void RemoveUpperDrawer()
        {
            if (!(_target is DrawerElement d)) return;
            var upperGo = DrawerLinks.DetachPair(d);
            if (upperGo != null)
                CommandStack.Execute(new DeleteCommand(upperGo));
            RefreshHighlights();
            Open(d);
        }

        private void OnDrawerUpperLengthChanged(int index)
        {
            if (!(_target is DrawerElement d)) return;
            if (index < 0 || index >= DrawerConstants.ValidLengths.Length) return;
            var upper = d.FindPaired();
            if (upper != null) upper.NominalLength = DrawerConstants.ValidLengths[index];
        }

        // Затемнить/вернуть поля Ш/В/Г: у ящика они вычисляемые (только чтение).
        private Color _dimsTextColor = Color.clear;

        private void SetDimensionFieldsEditable(bool editable)
        {
            foreach (var f in new[] { _w, _h, _d })
                SetDimensionFieldEditable(f, editable);
        }

        private void SetDimensionFieldEditable(TMP_InputField? f, bool editable)
        {
            if (f == null) return;
            var disabled = new Color(0.55f, 0.55f, 0.55f, 1f);
            if (_dimsTextColor == Color.clear && f.textComponent != null)
                _dimsTextColor = f.textComponent.color;
            f.interactable = editable;
            if (f.textComponent != null)
                f.textComponent.color = editable ? _dimsTextColor : disabled;
        }

        private void UpdateDrawerAnimButton(DrawerElement d)
        {
            if (_drawerAnimLabel == null || d == null) return;
            if (d.FindPaired() != null)
                _drawerAnimLabel.text = DrawerConstants.GetCycleButtonLabel(d.DoubleState);
            else
                _drawerAnimLabel.text = d.IsOpen ? "Закрыть ящик" : "Открыть ящик";
        }

        // ── Фасад ящика ───────────────────────────────────────────────
        // Фасад — отдельный элемент: его можно выбрать из существующих, создать
        // (фронт ящика, линейное открывание) или перейти к его настройке.

        private void RebuildDrawerFacadeOptions()
        {
            if (_drawerFacadeDropdown == null) return;
            var opts = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData("(нет фасада)") };
            var drawer = _target as DrawerElement;
            var attachedName = drawer?.AttachedFacadeName ?? "";
            foreach (var el in PartRegistry.GetAll())
            {
                if (!(el is FacadeElement fe) || string.IsNullOrEmpty(fe.PartName)) continue;
                bool isAttached = !string.IsNullOrEmpty(attachedName) && fe.PartName == attachedName;
                bool inContact = drawer != null && DrawerLinks.IsFacadeInContact(drawer, fe);
                if (!isAttached && !inContact) continue;
                opts.Add(new TMP_Dropdown.OptionData(fe.PartName));
            }
            _drawerFacadeDropdown.options = opts;
        }

        private int DrawerFacadeIndex(string name)
        {
            if (string.IsNullOrEmpty(name) || _drawerFacadeDropdown == null) return 0;
            var opts = _drawerFacadeDropdown.options;
            for (int i = 1; i < opts.Count; i++)
                if (opts[i].text == name) return i;
            return 0;
        }

        private void SetDrawerFacadeValue(string name)
        {
            if (_drawerFacadeDropdown == null) return;
            _drawerFacadeDropdown.SetValueWithoutNotify(DrawerFacadeIndex(name));
            _drawerFacadeDropdown.RefreshShownValue();
        }

        private void OnDrawerFacadeSelected(int index)
        {
            if (!(_target is DrawerElement d)) return;
            if (index <= 0 || _drawerFacadeDropdown == null) { d.AttachedFacadeName = ""; return; }
            d.AttachedFacadeName = _drawerFacadeDropdown.options[index].text;
        }

        private void UpdateDoorButton(FacadeElement? facade)
        {
            if (_doorButtonLabel == null) return;
            if (facade == null) { _doorButtonLabel.text = "Открыть"; return; }
            var drawer = FindDrawerForFacade(facade);
            if (drawer != null)
            {
                if (drawer.FindPaired() != null)
                    _doorButtonLabel.text = DrawerConstants.GetCycleButtonLabel(drawer.DoubleState);
                else
                    _doorButtonLabel.text = drawer.IsOpen ? "Закрыть ящик" : "Открыть ящик";
            }
            else
            {
                _doorButtonLabel.text = facade.IsOpen ? "Закрыть" : "Открыть";
            }
        }

        private static DrawerElement? FindDrawerForFacade(FacadeElement facade)
        {
            if (string.IsNullOrEmpty(facade.PartName)) return null;
            foreach (var e in PartRegistry.GetAll())
                if (e is DrawerElement d && d.AttachedFacadeName == facade.PartName) return d;
            return null;
        }

        private void UpdateModeDropdown(FacadeElement? facade)
        {
            if (_modeDropdown == null) return;
            _modeDropdown.SetValueWithoutNotify(facade != null ? (int)facade.Mode : 0);
            _modeDropdown.RefreshShownValue();
        }

        private void Duplicate()
        {
            if (_target == null) return;
            var dup = ElementFactory.Duplicate(_target);
            var element = dup != null ? dup.GetComponent<KitchenElement>() : null;
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(dup!));
                Open(element);
            }
            RefreshHighlights();
        }

        private void Delete()
        {
            if (_target == null) return;
            var go = _target.gameObject;
            string deletedName = _target.PartName;

            // Верхний ящик пары жёстко привязан — удаляется вместе с нижним.
            if (_target is DrawerElement d && !d.IsUpperDrawer)
            {
                var upperGo = DrawerLinks.DetachPair(d);
                if (upperGo != null) CommandStack.Execute(new DeleteCommand(upperGo));
            }

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.Deselect();
            Close();
            CommandStack.Execute(new DeleteCommand(go));
            RefreshHighlights();

            // Подтверждения нет намеренно: удаление обратимо на месте — тост
            // с «Отменить» (правило 3 UI-GUIDELINES).
            string expected = $"Delete {deletedName}";
            ToastNotification.Instance?.Show($"Удалено: {deletedName}", 5f, "Отменить", () =>
            {
                // Отменяем только если удаление всё ещё наверху стека — иначе
                // Ctrl+Z-семантика тоста откатила бы чужое действие.
                if (CommandStack.CanUndo && CommandStack.PeekUndoDescription() == expected)
                {
                    CommandStack.Undo();
                    RefreshHighlights();
                }
            });
        }

        private static void RefreshHighlights()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        // ── Парсинг полей ───────────────────────────────────────────────
        // Невалидный ввод не откатывается молча: поле помечается красной
        // рамкой (правило 2 UI-GUIDELINES), значение остаётся прежним.

        private int ParseIntField(TMP_InputField? f, int fallback)
        {
            if (f == null) return fallback;
            var expr = f.text;
            if (expr.Contains('+') || expr.Contains('-'))
            {
                var result = ExpressionParser.EvaluateInt(expr);
                if (result.HasValue)
                {
                    f.text = result.Value.ToString();
                    return result.Value;
                }
            }
            if (int.TryParse(expr, out int v)) return v;
            MarkError(f);
            return fallback;
        }

        private float ParseAngle(TMP_InputField? f, float fallback)
        {
            if (f == null) return fallback;
            var expr = f.text;
            if (expr.Contains('+') || expr.Contains('-'))
            {
                var result = ExpressionParser.EvaluateFloat(expr);
                if (result.HasValue)
                {
                    f.text = result.Value.ToString("F1");
                    return result.Value;
                }
            }
            if (float.TryParse(expr, out float v)) return v;
            MarkError(f);
            return fallback;
        }

        /// <summary>Поле в мм → метры внутренней модели.</summary>
        private float ParseMM(TMP_InputField? f, float fallbackMeters)
        {
            if (f == null) return fallbackMeters;
            var expr = f.text;
            if (expr.Contains('+') || expr.Contains('-'))
            {
                var result = ExpressionParser.EvaluateInt(expr);
                if (result.HasValue)
                {
                    f.text = result.Value.ToString();
                    return result.Value * AppConstants.MM_TO_UNITS;
                }
            }
            if (int.TryParse(expr, out int mm)) return mm * AppConstants.MM_TO_UNITS;
            MarkError(f);
            return fallbackMeters;
        }

        private void MarkError(TMP_InputField f)
        {
            if (!_errorFields.Contains(f)) _errorFields.Add(f);
        }

        // ── Подсветка изменённых полей ──────────────────────────────────

        private void TrackField(TMP_InputField? field, string cleanValue)
        {
            if (field == null) return;
            _cleanValues[field] = cleanValue;
            field.onValueChanged.RemoveAllListeners();
            field.onValueChanged.AddListener(_ => UpdateFieldHighlight(field));
            field.onEndEdit.RemoveAllListeners();
            field.onEndEdit.AddListener(_ => ApplyFromField());
        }

        /// <summary>Apply по Enter/focus-loss. Защита от двойного срабатывания
        /// (кнопка Apply тоже зовёт Apply, а перед этим поле теряет фокус).</summary>
        private void ApplyFromField()
        {
            if (Time.frameCount == _applyFrame) return;
            _applyFrame = Time.frameCount;
            Apply();
        }

        private void UpdateFieldHighlight(TMP_InputField field)
        {
            if (field == null) return;
            var clean = _cleanValues.TryGetValue(field, out var v) ? v : field.text;
            UIFactory.SetHighlight(field, field.text != clean);
        }

        private void UpdateAllHighlights()
        {
            foreach (var kv in _cleanValues)
                UpdateFieldHighlight(kv.Key);
        }

        private void ClearAllHighlights()
        {
            foreach (var kv in _cleanValues)
            {
                UIFactory.SetHighlight(kv.Key, false);
                kv.Key.onValueChanged.RemoveAllListeners();
            }
            _cleanValues.Clear();
        }

        private void TrackAllFields()
        {
            if (_target == null) return;
            TrackField(_name, _target.PartName);
            var dims = _target.DimensionsMM;
            TrackField(_w, dims.x.ToString());
            TrackField(_h, dims.y.ToString());
            TrackField(_d, dims.z.ToString());
            var radial = _target as RadialShelfElement;
            TrackField(_radius, radial != null
                ? radial.CornerRadius.ToString()
                : AppConstants.RADIAL_CORNER_RADIUS_DEFAULT.ToString());
            var facade = _target as FacadeElement;
            var panelTrack = _target as PanelElement;
            TrackField(_gapLeft, facade != null ? facade.GapLeft.ToString() : panelTrack != null ? panelTrack.GapLeft.ToString() : "0");
            TrackField(_gapRight, facade != null ? facade.GapRight.ToString() : panelTrack != null ? panelTrack.GapRight.ToString() : "0");
            TrackField(_gapTop, facade != null ? facade.GapTop.ToString() : panelTrack != null ? panelTrack.GapTop.ToString() : "0");
            TrackField(_gapBottom, facade != null ? facade.GapBottom.ToString() : panelTrack != null ? panelTrack.GapBottom.ToString() : "0");
            var drawerEl2 = _target as DrawerElement;
            TrackField(_drawerWidth, drawerEl2 != null ? drawerEl2.InternalWidth.ToString() : "400");
			var windowEl2 = _target as WindowElement;
			TrackField(_sillProtrusion, windowEl2 != null ? windowEl2.SillProtrusionMM.ToString() : "50");
			var tableEl2 = _target as TableElement;
			var radiusTableEl2 = _target as RadiusTableElement;
			TrackField(_legInset, tableEl2 != null ? tableEl2.LegInsetMM.ToString() : (radiusTableEl2 != null ? radiusTableEl2.LegInsetMM.ToString() : "100"));
			var pillarEl = _target as PillarElement;
			TrackField(_midHeight, pillarEl != null ? pillarEl.MidHeightMM.ToString() : PillarElement.MidHeightMM_Default.ToString());
			var lightTrack = _target as LightSourceElement;
			TrackField(_lightTemp, lightTrack != null ? lightTrack.TemperatureK.ToString() : LightSourceElement.DEFAULT_TEMPERATURE_K.ToString());
			TrackField(_lightPower, lightTrack != null ? lightTrack.PowerW.ToString() : LightSourceElement.DEFAULT_POWER_W.ToString());
			TrackField(_lightDiffusion, lightTrack != null ? lightTrack.DiffusionPct.ToString() : LightSourceElement.DEFAULT_DIFFUSION_PCT.ToString());
			TrackField(_lightBeam, lightTrack != null ? lightTrack.BeamAngleDeg.ToString() : LightSourceElement.DEFAULT_BEAM_DEG.ToString());
			TrackField(_lightUp, lightTrack != null ? lightTrack.UpLightPct.ToString() : LightSourceElement.DEFAULT_UP_PCT.ToString());
			TrackField(_edgeThickness, EdgeBanding.FormatThickness(_target.EdgeThicknessMM));
            var pos = _target.transform.position;
            TrackField(_x, ToMM(pos.x));
            TrackField(_y, ToMM(pos.y));
            TrackField(_z, ToMM(pos.z));
            var e = _target.transform.eulerAngles;
            TrackField(_rx, e.x.ToString("F1"));
            TrackField(_ry, e.y.ToString("F1"));
            TrackField(_rz, e.z.ToString("F1"));
        }
    }
}
