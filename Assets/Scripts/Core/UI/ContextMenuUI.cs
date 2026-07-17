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
			_x, _y, _z, _rx, _ry, _rz, _legInset, _midHeight;
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

        // ── Подсветка изменённых полей ──────────────────────────────────
        private readonly Dictionary<TMP_InputField, string> _cleanValues = new();
        private int _applyFrame = -1;  // защита от двойного Apply
        private bool _opening;  // защита от OnSelectionChanged → Close() внутри Open()
        private bool _currentIsTable;  // true когда текущий элемент — стол
        private bool _currentIsDoor;   // true когда текущий элемент — дверь

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
            _layout.Clear();

            // Заголовок — первая строка потока (стоит вплотную под верхом панели).
            _titleLabel = UIFactory.CreateLabel("CtxTitle", panel.transform, "деталь", 20,
                Vector2.zero, new Vector2(340, TitleH), TextAnchor.MiddleCenter);
            AddRow(TitleH, TitleGap, _titleLabel.rectTransform);

            // Тип детали: конвертация между Part / Facade / AssembledFacade / RadialShelf.
            var typeOptions = new List<string> { "Деталь", "Фасад", "Сборный фасад", "Радиусная полка", "Ящик GTV", "Окно", "Дверь" };
            _typeDropdown = UIFactory.CreateDropdown("CtxType", panel.transform, typeOptions,
                new Vector2(0, 0), new Vector2(332, 28), OnTypeSelected);
            AddRow(28f, RowGap, _typeDropdown.GetComponent<RectTransform>());

            // Размеры.
            _name = Row(panel.transform, "Название");
            _w = Row(panel.transform, "Ширина, мм");
            _h = Row(panel.transform, "Высота, мм");
            _d = Row(panel.transform, "Глубина, мм");
            _radius = RadialRow(panel.transform, "Радиус угла, мм");

            // Зазоры (только для фасадов) — блок скрывается в режиме «деталь».
            var gapSection = CreateGapSection(panel.transform, out float gapSectionH);
            AddFacadeRow(gapSection, gapSection.GetComponent<RectTransform>(), gapSectionH, RowGap);

            // Открывание фасада (только фасад): выпадающий список режима (12 рёбер +
            // 6 ящиков) и кнопка Открыть/Закрыть — отдельными строками.
            var modeOptions = new List<string>();
            for (int i = 0; i < FacadeDoor.Count; i++)
                modeOptions.Add(FacadeDoor.Label((DoorMode)i));
            _modeDropdown = UIFactory.CreateDropdown("CtxMode", panel.transform, modeOptions,
                new Vector2(0, 0), new Vector2(332, 28), OnModeSelected);
            AddFacadeRow(28f, RowGap, _modeDropdown.GetComponent<RectTransform>());

            var doorButton = UIFactory.CreateButton("CtxDoor", panel.transform, "Открыть",
                new Vector2(0, 0), new Vector2(332, BtnH), ToggleDoor);
            _doorButtonLabel = doorButton.GetComponentInChildren<TMP_Text>();
            AddFacadeRow(BtnH, ActionGap, doorButton.GetComponent<RectTransform>());

            // Центр сборного фасада (только для сборного): Глухой / Витрина / Стекло.
            var fillOptions = new List<string> { "Глухой (панель)", "Витрина (пусто)", "Стекло" };
            _fillDropdown = UIFactory.CreateDropdown("CtxFill", panel.transform, fillOptions,
                new Vector2(0, 0), new Vector2(332, 28), OnFillSelected);
            AddAssembledRow(28f, ActionGap, _fillDropdown.GetComponent<RectTransform>());

            // Ящик GTV: тип, длина, цвет, ширина, двойной ящик, анимация.
            var drawerTypeNames = new List<string> { "A (86 мм)", "B (120 мм)", "C (168 мм)", "D (200 мм)" };
            _drawerTypeDropdown = UIFactory.CreateDropdown("CtxDrawerType", panel.transform, drawerTypeNames,
                new Vector2(0, 0), new Vector2(332, 28), OnDrawerTypeChanged);
            AddDrawerRow(28f, ActionGap, _drawerTypeDropdown.GetComponent<RectTransform>());

            var drawerLenNames = new List<string>();
            foreach (var l in DrawerConstants.ValidLengths) drawerLenNames.Add($"L={l} мм");
            _drawerLengthDropdown = UIFactory.CreateDropdown("CtxDrawerLen", panel.transform, drawerLenNames,
                new Vector2(0, 0), new Vector2(332, 28), OnDrawerLengthChanged);
            AddDrawerRow(28f, ActionGap, _drawerLengthDropdown.GetComponent<RectTransform>());

            var drawerColorNames = new List<string> { "Антрацит", "Белый", "Чёрный" };
            _drawerColorDropdown = UIFactory.CreateDropdown("CtxDrawerColor", panel.transform, drawerColorNames,
                new Vector2(0, 0), new Vector2(332, 28), OnDrawerColorChanged);
            AddDrawerRow(28f, ActionGap, _drawerColorDropdown.GetComponent<RectTransform>());

            _drawerWidth = DrawerFieldRow(panel.transform, "Ширина короба, мм");

            // «Двойной ящик» — только для одиночного нижнего, когда над контуром
            // есть место под верхний внутренний ящик (мин. проём типа A).
            var drawerDoubleBtn = UIFactory.CreateButton("CtxDrawerDouble", panel.transform, "Двойной ящик",
                new Vector2(0, 0), new Vector2(332, BtnH), CreatePairedDrawer);
            AddDrawerRowWhen(CanCreateDoubleDrawer, BtnH, ActionGap,
                drawerDoubleBtn.GetComponent<RectTransform>());

            // Опции пары (виден только у двойного): длина верхнего ящика + удаление.
            var upperLenNames = new List<string>();
            foreach (var l in DrawerConstants.ValidLengths) upperLenNames.Add($"Верхний: L={l} мм");
            _drawerUpperLenDropdown = UIFactory.CreateDropdown("CtxDrawerUpperLen", panel.transform, upperLenNames,
                new Vector2(0, 0), new Vector2(332, 28), OnDrawerUpperLengthChanged);
            AddDrawerRowWhen(HasUpperDrawer, 28f, ActionGap,
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
            var drawerFacadeLbl = UIFactory.CreateLabel("CtxDrawerFacadeLbl", panel.transform, "Фасад ящика:", 15,
                Vector2.zero, new Vector2(340, RotLblH), TextAnchor.MiddleCenter);
            AddDrawerRow(RotLblH, RotLblGap, drawerFacadeLbl.rectTransform);

            _drawerFacadeDropdown = UIFactory.CreateDropdown("CtxDrawerFacade", panel.transform,
                new List<string> { "(нет фасада)" },
                new Vector2(0, 0), new Vector2(332, 28), OnDrawerFacadeSelected);
            AddDrawerRow(28f, ActionGap, _drawerFacadeDropdown.GetComponent<RectTransform>());

            // Окно: тонировка стекла и выступ подоконника.
            var tintOptions = new List<string> { "Прозрачное", "Тонированное" };
            _tintDropdown = UIFactory.CreateDropdown("CtxTint", panel.transform, tintOptions,
                new Vector2(0, 0), new Vector2(332, 28), OnTintSelected);
            AddWindowRow(28f, ActionGap, () => !_currentIsDoor, _tintDropdown.GetComponent<RectTransform>());

            _sillProtrusion = WindowFieldRow(panel.transform, "Подоконник, мм", () => !_currentIsDoor);

            // Дверь: тип створки (стекло/глухая).
            var sashTypeOptions = new List<string> { "Стекло", "Глухая" };
            _sashTypeDropdown = UIFactory.CreateDropdown("CtxSashType", panel.transform, sashTypeOptions,
                new Vector2(0, 0), new Vector2(332, 28), OnSashTypeSelected);
            AddDoorRow(28f, ActionGap, _sashTypeDropdown.GetComponent<RectTransform>());

            // Режим открывания окна и кнопка Открыть/Закрыть (как фасад).
            var winModeOptions = new List<string>
            {
                FacadeDoor.Label(DoorMode.HingeFrontLeft),
                FacadeDoor.Label(DoorMode.HingeFrontRight),
                FacadeDoor.Label(DoorMode.HingeFrontTop),
                FacadeDoor.Label(DoorMode.HingeFrontBottom),
            };
            var winModeDropdown = UIFactory.CreateDropdown("CtxWinMode", panel.transform, winModeOptions,
                new Vector2(0, 0), new Vector2(332, 28), OnWindowModeSelected);
            _winModeDropdown = winModeDropdown;
            AddWindowRow(28f, ActionGap, winModeDropdown.GetComponent<RectTransform>());

            var winDoorBtn = UIFactory.CreateButton("CtxWinDoor", panel.transform, "Открыть",
                new Vector2(0, 0), new Vector2(332, BtnH), ToggleWindowDoor);
            _winDoorButtonLabel = winDoorBtn.GetComponentInChildren<TMP_Text>();
            AddWindowRow(BtnH, ActionGap, winDoorBtn.GetComponent<RectTransform>());

            // Текстура/декор (детали И фасады) — всегда видимая строка (кроме столов).
            var matLbl = UIFactory.CreateLabel("CtxMatLbl", panel.transform, "Текстура:", 15,
                Vector2.zero, new Vector2(340, RotLblH), TextAnchor.MiddleCenter);
            AddRow(RotLblH, RotLblGap, () => !_currentIsTable, matLbl.rectTransform);

            var matOptions = new List<string>();
            foreach (var m in MaterialCatalog.All) matOptions.Add(m.displayName);
            _materialDropdown = UIFactory.CreateDropdown("CtxMaterial", panel.transform, matOptions,
                new Vector2(0, 0), new Vector2(332, 28), OnMaterialSelected);
            AddRow(28f, ActionGap, () => !_currentIsTable, _materialDropdown.GetComponent<RectTransform>());

            // Текстура столешницы (только для столов).
            var tableTopLbl = UIFactory.CreateLabel("CtxTableTopLbl", panel.transform, "Текстура столешницы:", 15,
                Vector2.zero, new Vector2(340, RotLblH), TextAnchor.MiddleCenter);
            AddTableRow(RotLblH, RotLblGap, tableTopLbl.rectTransform);

            _tabletopMaterialDropdown = UIFactory.CreateDropdown("CtxTableTop", panel.transform, matOptions,
                new Vector2(0, 0), new Vector2(332, 28), OnMaterialSelected);
            AddTableRow(28f, ActionGap, _tabletopMaterialDropdown.GetComponent<RectTransform>());

            // Текстура ножек (только для столов).
            var tableLegsLbl = UIFactory.CreateLabel("CtxTableLegsLbl", panel.transform, "Текстура ножек:", 15,
                Vector2.zero, new Vector2(340, RotLblH), TextAnchor.MiddleCenter);
            AddTableRow(RotLblH, RotLblGap, tableLegsLbl.rectTransform);

            var legsOptions = new List<string>();
            foreach (var m in MaterialCatalog.All) legsOptions.Add(m.displayName);
            _legsMaterialDropdown = UIFactory.CreateDropdown("CtxTableLegs", panel.transform, legsOptions,
                new Vector2(0, 0), new Vector2(332, 28), OnLegsMaterialSelected);
            AddTableRow(28f, ActionGap, _legsMaterialDropdown.GetComponent<RectTransform>());

			// Сдвиг ножек внутрь стола (только для столов).
			_legInset = TableFieldRow(panel.transform, "Сдвиг ножек, мм");

			// Высота средней секции опоры (только для опор).
			_midHeight = PillarFieldRow(panel.transform, "Средняя секция, мм");

            // Позиция и поворот — компактная раскладка 3 колонки.
            _x = TriField(panel.transform, "X, м", TriCol1);
            _y = TriField(panel.transform, "Y, м", TriCol2);
            _z = TriField(panel.transform, "Z, м", TriCol3);
            TriEndRow();

            // Поля поворота у окна скрыты: ориентацию диктует стена.
            _rx = TriField(panel.transform, "X°", TriCol1);
            _ry = TriField(panel.transform, "Y°", TriCol2);
            _rz = TriField(panel.transform, "Z°", TriCol3);
            TriEndRow(hideForWindow: true);

			foreach (var f in new[] { _w, _h, _d, _radius, _drawerWidth, _legInset, _midHeight, _sillProtrusion }) f!.contentType = TMP_InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _gapLeft, _gapRight, _gapTop, _gapBottom }) f!.contentType = TMP_InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _x, _y, _z, _rx, _ry, _rz }) f!.contentType = TMP_InputField.ContentType.DecimalNumber;

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

            var apply = UIFactory.CreateButton("CtxApply", panel.transform, "Применить",
                new Vector2(-85, 0), new Vector2(156, 32), Apply);
            var dup = UIFactory.CreateButton("CtxDup", panel.transform, "Дублировать",
                new Vector2(85, 0), new Vector2(156, 32), Duplicate);
            AddRow(32f, ActionGap,
                apply.GetComponent<RectTransform>(),
                dup.GetComponent<RectTransform>());

            var del = UIFactory.CreateButton("CtxDel", panel.transform, "Удалить",
                new Vector2(0, 0), new Vector2(332, 32), Delete);
            AddRow(32f, ActionGap, del.GetComponent<RectTransform>());

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

            _lockToggle = UIFactory.CreateToggle("CtxLock", panel.transform, "Запретить перемещение", false,
                new Vector2(0, 0), new Vector2(332, 26), v => { if (_target != null) _target.Movable = !v; });
            AddRow(26f, 0f, _lockToggle.GetComponent<RectTransform>());

            // Кнопка закрытия живёт в углу панели, вне потока раскладки.
            var closeBtn = UIFactory.CreateButton("CtxClose", panel.transform, "X",
                Vector2.zero, new Vector2(24, 24), Close);
            UIFactory.AnchorTopRight(closeBtn.GetComponent<RectTransform>());
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-4, -4);
            closeBtn.transform.SetAsLastSibling();

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

        private TMP_InputField Row(Transform parent, string label)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateInputField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(120, FieldH));
            AddRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        private TMP_InputField RadialRow(Transform parent, string label)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateInputField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(120, FieldH));
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

            // Ширина X, мм — левый и правый зазор
            AddTopAnchoredChild(UIFactory.CreateLabel("Gap_Ширина X, мм", root.transform, "Ширина X, мм", 13,
                new Vector2(LabelX, -top), new Vector2(130, 20), TextAnchor.MiddleLeft).rectTransform);
            _gapLeft = UIFactory.CreateInputField("F_gapLeft", root.transform, "0",
                new Vector2(gapFieldX, -top), new Vector2(smallFieldW, 22));
            AddTopAnchoredChild(_gapLeft.GetComponent<RectTransform>());
            _gapRight = UIFactory.CreateInputField("F_gapRight", root.transform, "0",
                new Vector2(gapFieldX + smallFieldW + fieldGap, -top), new Vector2(smallFieldW, 22));
            AddTopAnchoredChild(_gapRight.GetComponent<RectTransform>());
            top += fieldH + innerGap;

            // Высота Y, мм — верхний и нижний зазор
            AddTopAnchoredChild(UIFactory.CreateLabel("Gap_Высота Y, мм", root.transform, "Высота Y, мм", 13,
                new Vector2(LabelX, -top), new Vector2(130, 20), TextAnchor.MiddleLeft).rectTransform);
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

        private void AddFacadeRow(GameObject toggleGO, RectTransform rt, float height, float gapAfter)
        {
            AnchorTop(rt);
            _layout.Add(new LayoutRow
            {
                rects = new[] { rt },
                height = height,
                gapAfter = gapAfter,
                facadeOnly = true,
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
            var field = UIFactory.CreateInputField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(120, FieldH));
            AddDrawerRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

		private TMP_InputField TableFieldRow(Transform parent, string label)
		{
			var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
				new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
			var field = UIFactory.CreateInputField("F_" + label, parent, "",
				new Vector2(FieldX, 0), new Vector2(120, FieldH));
			AddTableRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
			return field;
		}

		private TMP_InputField PillarFieldRow(Transform parent, string label)
		{
			var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
				new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
			var field = UIFactory.CreateInputField("F_" + label, parent, "",
				new Vector2(FieldX, 0), new Vector2(120, FieldH));
			AddPillarRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
			return field;
		}

		private TMP_InputField WindowFieldRow(Transform parent, string label, System.Func<bool>? visibleWhen = null)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateInputField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(120, FieldH));
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

		private void Layout(bool isFacade, bool isAssembled, bool isRadial, bool isDrawer, bool isTable, bool isPillar = false, bool isWindow = false, bool isDoor = false)
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
					&& !(row.hideForWindow && isWindow)
					&& (row.visibleWhen == null || row.visibleWhen());

				if (row.toggleGO != null)
					row.toggleGO.SetActive(visible);
				else if (row.facadeOnly || row.assembledOnly || row.radialOnly || row.drawerOnly || row.tableOnly || row.pillarOnly || row.windowOnly || row.doorOnly || row.hideForWindow)
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
            }
        }

        private bool IsAnyFieldFocused()
        {
			foreach (var f in new[] { _name, _w, _h, _d, _radius, _drawerWidth, _legInset, _midHeight, _sillProtrusion, _gapLeft, _gapRight, _gapTop, _gapBottom, _x, _y, _z, _rx, _ry, _rz })
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
            MaybeRefresh(_x, pos.x.ToString("F3"));
            MaybeRefresh(_y, pos.y.ToString("F3"));
            MaybeRefresh(_z, pos.z.ToString("F3"));

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

            var facade = _target as FacadeElement;
            if (facade != null)
            {
                MaybeRefresh(_gapLeft, facade.GapLeft.ToString());
                MaybeRefresh(_gapRight, facade.GapRight.ToString());
                MaybeRefresh(_gapTop, facade.GapTop.ToString());
                MaybeRefresh(_gapBottom, facade.GapBottom.ToString());
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

			var window = _target as WindowElement;
            if (window != null && _sillProtrusion != null)
                MaybeRefresh(_sillProtrusion, window.SillProtrusionMM.ToString());
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
				if (_titleLabel != null)
					_titleLabel.text = isPillar ? "Опора" : isRadiusTable ? "Радиусный стол" : isTable ? "Стол" : isDrawer ? "Ящик GTV" : isWindow ? "Окно" : isDoor ? "Дверь" : (isRadial ? "Радиусная полка" : (isFacade ? "Фасад" : "деталь"));
                if (_typeDropdown != null)
                {
                    _typeDropdown.SetValueWithoutNotify((int)ElementConverter.GetElementType(element));
                    _typeDropdown.RefreshShownValue();
                }

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
                // только для фасадов, радиус — только для радиусной полки, сдвиг ножек — только для столов (включая радиусные), панель сама подгоняется по высоте.
			Layout(isFacade, assembled != null, isRadial, isDrawer, isTable || isRadiusTable, isPillar, isWindow || isDoor, isDoor);

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
            // Правки размеров/позиции применяем к закрытой (логической) позе.
            if (target is FacadeElement fac) { fac.ForceClose(); UpdateDoorButton(fac); }
            if (target is DrawerElement dr) { dr.ForceClose(); UpdateDrawerAnimButton(dr); }
            if (target is WindowElement win) { win.ForceClose(); if (_winDoorButtonLabel != null) _winDoorButtonLabel.text = "Открыть"; }
            if (target is DoorElement doorElApp) { doorElApp.ForceClose(); if (_winDoorButtonLabel != null) _winDoorButtonLabel.text = "Открыть"; }

            var oldDims = target.DimensionsMM;
            var oldPos = target.transform.position;
            var oldRot = target.transform.rotation;

            // Через DrawerLinks: переименование обновляет обратные ссылки
            // (PairedDrawerName пары, AttachedFacadeName ящиков с этим фасадом).
            DrawerLinks.Rename(target, string.IsNullOrWhiteSpace(_name!.text) ? "Board" : _name!.text);

			var radial = target as RadialShelfElement;
			var drawer = target as DrawerElement;
			var pillar = target as PillarElement;
			var table = target as TableElement;
			var radiusTable = target as RadiusTableElement;
			if (radial != null)
            {
                target.DimensionsMM = new Vector3Int(
                    ParseInt(_w!.text, oldDims.x),
                    ParseInt(_h!.text, oldDims.y),
                    ParseInt(_d!.text, oldDims.z));
                radial.CornerRadius = ParseInt(_radius!.text, radial.CornerRadius);
            }
            else if (drawer != null)
            {
                if (_drawerWidth != null) drawer.InternalWidth = ParseInt(_drawerWidth.text, drawer.InternalWidth);
            }
            else if (pillar != null)
            {
                int newTotalH = ParseInt(_h!.text, oldDims.y);
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
                    pillar.MidHeightMM = ParseInt(_midHeight.text, pillar.MidHeightMM);
                }
            }
            else
            {
                // Глубину окна диктует стена — поле Г игнорируется.
                target.DimensionsMM = new Vector3Int(
                    ParseInt(_w!.text, oldDims.x),
                    ParseInt(_h!.text, oldDims.y),
                    target is WindowElement || target is DoorElement ? oldDims.z : ParseInt(_d!.text, oldDims.z));
            }

            if (table != null && _legInset != null)
                table.LegInsetMM = ParseInt(_legInset.text, table.LegInsetMM);

			if (radiusTable != null && _legInset != null)
				radiusTable.LegInsetMM = ParseInt(_legInset.text, radiusTable.LegInsetMM);

			var windowEl = target as WindowElement;
            if (windowEl != null && _sillProtrusion != null)
                windowEl.SillProtrusionMM = ParseInt(_sillProtrusion.text, windowEl.SillProtrusionMM);

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
                facade.GapLeft = ParseInt(_gapLeft!.text, facade.GapLeft);
                facade.GapRight = ParseInt(_gapRight!.text, facade.GapRight);
                facade.GapTop = ParseInt(_gapTop!.text, facade.GapTop);
                facade.GapBottom = ParseInt(_gapBottom!.text, facade.GapBottom);
            }

            target.transform.position = new Vector3(
                ParseFloat(_x!.text, oldPos.x),
                ParseFloat(_y!.text, oldPos.y),
                ParseFloat(_z!.text, oldPos.z));

            // У окна поля поворота скрыты (ориентацию диктует стена) — не трогаем.
            if (!(target is WindowElement) && !(target is DoorElement))
            {
                var euler = oldRot.eulerAngles;
                target.transform.rotation = Quaternion.Euler(
                    ParseFloat(_rx!.text, euler.x),
                    ParseFloat(_ry!.text, euler.y),
                    ParseFloat(_rz!.text, euler.z));
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
            _h!.text = newDims.y.ToString();
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

            RefreshHighlights();

            ClearAllHighlights();
            TrackAllFields();
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
                f.ToggleDoor();
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

        private void OnTypeSelected(int index)
        {
            if (_target == null) return;
            var targetType = (ElementConverter.TargetType)index;
            if (ElementConverter.GetElementType(_target) == targetType) return;

            var go = _target.gameObject;
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
            if (_doorButtonLabel != null)
                _doorButtonLabel.text = (facade != null && facade.IsOpen) ? "Закрыть" : "Открыть";
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
        }

        private static void RefreshHighlights()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private static int ParseInt(string s, int fallback) =>
            int.TryParse(s, out int v) ? v : fallback;

        private static float ParseFloat(string s, float fallback) =>
            float.TryParse(s, out float v) ? v : fallback;

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
            TrackField(_gapLeft, facade != null ? facade.GapLeft.ToString() : "0");
            TrackField(_gapRight, facade != null ? facade.GapRight.ToString() : "0");
            TrackField(_gapTop, facade != null ? facade.GapTop.ToString() : "0");
            TrackField(_gapBottom, facade != null ? facade.GapBottom.ToString() : "0");
            var drawerEl2 = _target as DrawerElement;
            TrackField(_drawerWidth, drawerEl2 != null ? drawerEl2.InternalWidth.ToString() : "400");
			var windowEl2 = _target as WindowElement;
			TrackField(_sillProtrusion, windowEl2 != null ? windowEl2.SillProtrusionMM.ToString() : "50");
			var tableEl2 = _target as TableElement;
			var radiusTableEl2 = _target as RadiusTableElement;
			TrackField(_legInset, tableEl2 != null ? tableEl2.LegInsetMM.ToString() : (radiusTableEl2 != null ? radiusTableEl2.LegInsetMM.ToString() : "100"));
			var pillarEl = _target as PillarElement;
			TrackField(_midHeight, pillarEl != null ? pillarEl.MidHeightMM.ToString() : PillarElement.MidHeightMM_Default.ToString());
            var pos = _target.transform.position;
            TrackField(_x, pos.x.ToString("F3"));
            TrackField(_y, pos.y.ToString("F3"));
            TrackField(_z, pos.z.ToString("F3"));
            var e = _target.transform.eulerAngles;
            TrackField(_rx, e.x.ToString("F1"));
            TrackField(_ry, e.y.ToString("F1"));
            TrackField(_rz, e.z.ToString("F1"));
        }
    }
}
