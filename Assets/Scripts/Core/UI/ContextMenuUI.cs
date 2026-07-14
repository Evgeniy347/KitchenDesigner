using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Контекстное меню по клику ЛКМ на детали: размеры, позиция, поворот, действия.</summary>
    public class ContextMenuUI : MonoBehaviour
    {
        public static ContextMenuUI Instance { get; private set; } = null!;

        private GameObject _root = null!;
        private KitchenElement? _target;
        private TMP_Text _titleLabel = null!;

        private TMP_InputField _name = null!, _w = null!, _h = null!, _d = null!, _radius = null!,
            _gapLeft = null!, _gapRight = null!, _gapTop = null!, _gapBottom = null!,
            _x = null!, _y = null!, _z = null!, _rx = null!, _ry = null!, _rz = null!;
        private Toggle _lockToggle = null!;
        private Toggle _transparentToggle = null!;
        private RectTransform _panelRt = null!;
        private TMP_Text _doorButtonLabel = null!;  // подпись кнопки «Открыть»/«Закрыть»
        private TMP_Dropdown _modeDropdown = null!; // выпадающий список режима открывания
        private TMP_Dropdown _fillDropdown = null!; // центр сборного фасада (Глухой/Витрина/Стекло)
        private TMP_Dropdown _materialDropdown = null!; // выбор текстуры/декора (детали и фасады)
        private TMP_Dropdown _typeDropdown = null!; // конвертация: деталь ⇄ фасад ⇄ сборный фасад
        private TMP_Dropdown _drawerTypeDropdown = null!, _drawerLengthDropdown = null!, _drawerColorDropdown = null!;
        private Toggle _drawerDoubleToggle = null!, _drawerUpperToggle = null!;
        private TMP_InputField _drawerWidth = null!;
        private TMP_Text _drawerAnimLabel = null!;
        private TMP_Dropdown _drawerFacadeDropdown = null!; // прикреплённый фасад (выбор существующего)

        // ── Подсветка изменённых полей ──────────────────────────────────
        private readonly Dictionary<TMP_InputField, string> _cleanValues = new();
        private int _applyFrame = -1;  // защита от двойного Apply
        private bool _opening;  // защита от OnSelectionChanged → Close() внутри Open()

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
            public GameObject toggleGO;   // объект, который включать/выключать по режиму
        }
        private readonly List<LayoutRow> _layout = new();

        // Геометрия
        private const float LabelW = 140f;     // ширина колонки подписей (вмещает «Ширина короба, мм» почти без переноса)
        private const float LabelX = -62f;     // центр подписи (панель 280px → края ±140)
        private const float FieldX = 75f;      // центр поля ввода
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

        private void Awake()
        {
            Instance = this;
        }

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("ContextMenu", canvas, Vector2.zero, new Vector2(280, 560));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(-10, -60);
            _root = panel.gameObject;
            _panelRt = panel.rectTransform;
            _layout.Clear();

            // Заголовок — первая строка потока (стоит вплотную под верхом панели).
            _titleLabel = UIFactory.CreateLabel("CtxTitle", panel.transform, "деталь", 20,
                Vector2.zero, new Vector2(260, TitleH), TextAnchor.MiddleCenter);
            AddRow(TitleH, TitleGap, _titleLabel.rectTransform);

            // Тип детали: конвертация между Part / Facade / AssembledFacade / RadialShelf.
            var typeOptions = new List<string> { "Деталь", "Фасад", "Сборный фасад", "Радиусная полка", "Ящик GTV" };
            _typeDropdown = UIFactory.CreateDropdown("CtxType", panel.transform, typeOptions,
                new Vector2(0, 0), new Vector2(248, 28), OnTypeSelected);
            AddRow(28f, RowGap, _typeDropdown.GetComponent<RectTransform>());

            // Размеры.
            _name = Row(panel.transform, "Название");
            _w = Row(panel.transform, "Ширина, мм");
            _h = Row(panel.transform, "Высота, мм");
            _d = Row(panel.transform, "Глубина, мм");
            _radius = RadialRow(panel.transform, "Радиус, мм");

            // Зазоры (только для фасадов) — блок скрывается в режиме «деталь».
            var gapSection = CreateGapSection(panel.transform, out float gapSectionH);
            AddFacadeRow(gapSection, gapSection.GetComponent<RectTransform>(), gapSectionH, RowGap);

            // Открывание фасада (только фасад): выпадающий список режима (12 рёбер +
            // 6 ящиков) и кнопка Открыть/Закрыть — отдельными строками.
            var modeOptions = new List<string>();
            for (int i = 0; i < FacadeDoor.Count; i++)
                modeOptions.Add(FacadeDoor.Label((DoorMode)i));
            _modeDropdown = UIFactory.CreateDropdown("CtxMode", panel.transform, modeOptions,
                new Vector2(0, 0), new Vector2(248, 28), OnModeSelected);
            AddFacadeRow(28f, RowGap, _modeDropdown.GetComponent<RectTransform>());

            var doorButton = UIFactory.CreateButton("CtxDoor", panel.transform, "Открыть",
                new Vector2(0, 0), new Vector2(248, BtnH), ToggleDoor);
            _doorButtonLabel = doorButton.GetComponentInChildren<TMP_Text>();
            AddFacadeRow(BtnH, ActionGap, doorButton.GetComponent<RectTransform>());

            // Центр сборного фасада (только для сборного): Глухой / Витрина / Стекло.
            var fillOptions = new List<string> { "Глухой (панель)", "Витрина (пусто)", "Стекло" };
            _fillDropdown = UIFactory.CreateDropdown("CtxFill", panel.transform, fillOptions,
                new Vector2(0, 0), new Vector2(248, 28), OnFillSelected);
            AddAssembledRow(28f, ActionGap, _fillDropdown.GetComponent<RectTransform>());

            // Ящик GTV: тип, длина, цвет, ширина, двойной ящик, анимация.
            var drawerTypeNames = new List<string> { "A (86 мм)", "B (120 мм)", "C (168 мм)", "D (200 мм)" };
            _drawerTypeDropdown = UIFactory.CreateDropdown("CtxDrawerType", panel.transform, drawerTypeNames,
                new Vector2(0, 0), new Vector2(248, 28), OnDrawerTypeChanged);
            AddDrawerRow(28f, ActionGap, _drawerTypeDropdown.GetComponent<RectTransform>());

            var drawerLenNames = new List<string>();
            foreach (var l in DrawerConstants.ValidLengths) drawerLenNames.Add($"L={l} мм");
            _drawerLengthDropdown = UIFactory.CreateDropdown("CtxDrawerLen", panel.transform, drawerLenNames,
                new Vector2(0, 0), new Vector2(248, 28), OnDrawerLengthChanged);
            AddDrawerRow(28f, ActionGap, _drawerLengthDropdown.GetComponent<RectTransform>());

            var drawerColorNames = new List<string> { "Антрацит", "Белый", "Чёрный" };
            _drawerColorDropdown = UIFactory.CreateDropdown("CtxDrawerColor", panel.transform, drawerColorNames,
                new Vector2(0, 0), new Vector2(248, 28), OnDrawerColorChanged);
            AddDrawerRow(28f, ActionGap, _drawerColorDropdown.GetComponent<RectTransform>());

            _drawerWidth = Row(panel.transform, "Ширина короба, мм");
            AddDrawerRow(RowH, RowGap, _drawerWidth.GetComponent<RectTransform>());

            _drawerDoubleToggle = UIFactory.CreateToggle("CtxDrawerDouble", panel.transform, "Двойной ящик", false,
                new Vector2(0, 0), new Vector2(248, 28), v => { if (_target is DrawerElement d) d.IsDouble = v; });
            AddDrawerRow(28f, ActionGap, _drawerDoubleToggle.GetComponent<RectTransform>());

            _drawerUpperToggle = UIFactory.CreateToggle("CtxDrawerUpper", panel.transform, "Верхний ящик", false,
                new Vector2(0, 0), new Vector2(248, 28), v => { if (_target is DrawerElement d) d.IsUpperDrawer = v; });
            AddDrawerRow(28f, ActionGap, _drawerUpperToggle.GetComponent<RectTransform>());

            var drawerPairBtn = UIFactory.CreateButton("CtxDrawerPair", panel.transform, "Создать парный ящик",
                new Vector2(0, 0), new Vector2(248, BtnH), CreatePairedDrawer);
            AddDrawerRow(BtnH, ActionGap, drawerPairBtn.GetComponent<RectTransform>());

            var drawerAnimBtn = UIFactory.CreateButton("CtxDrawerAnim", panel.transform, "Открыть",
                new Vector2(0, 0), new Vector2(248, BtnH), CycleDrawerAnimation);
            _drawerAnimLabel = drawerAnimBtn.GetComponentInChildren<TMP_Text>();
            AddDrawerRow(BtnH, ActionGap, drawerAnimBtn.GetComponent<RectTransform>());

            // Фасад ящика: выпадающий список существующих фасадов + кнопки создать/настроить.
            var drawerFacadeLbl = UIFactory.CreateLabel("CtxDrawerFacadeLbl", panel.transform, "Фасад ящика:", 15,
                Vector2.zero, new Vector2(260, RotLblH), TextAnchor.MiddleCenter);
            AddDrawerRow(RotLblH, RotLblGap, drawerFacadeLbl.rectTransform);

            _drawerFacadeDropdown = UIFactory.CreateDropdown("CtxDrawerFacade", panel.transform,
                new List<string> { "(нет фасада)" },
                new Vector2(0, 0), new Vector2(248, 28), OnDrawerFacadeSelected);
            AddDrawerRow(28f, ActionGap, _drawerFacadeDropdown.GetComponent<RectTransform>());

            var drawerCreateFacadeBtn = UIFactory.CreateButton("CtxDrawerCreateFacade", panel.transform, "Создать фасад",
                new Vector2(-65, 0), new Vector2(120, BtnH), CreateFacadeForDrawer);
            var drawerCfgFacadeBtn = UIFactory.CreateButton("CtxDrawerCfgFacade", panel.transform, "Настроить фасад",
                new Vector2(65, 0), new Vector2(120, BtnH), ConfigureAttachedFacade);
            AddDrawerRow(BtnH, ActionGap, drawerCreateFacadeBtn.GetComponent<RectTransform>(),
                drawerCfgFacadeBtn.GetComponent<RectTransform>());

            // Текстура/декор (детали И фасады) — всегда видимая строка.
            var matLbl = UIFactory.CreateLabel("CtxMatLbl", panel.transform, "Текстура:", 15,
                Vector2.zero, new Vector2(260, RotLblH), TextAnchor.MiddleCenter);
            AddRow(RotLblH, RotLblGap, matLbl.rectTransform);

            var matOptions = new List<string>();
            foreach (var m in MaterialCatalog.All) matOptions.Add(m.displayName);
            _materialDropdown = UIFactory.CreateDropdown("CtxMaterial", panel.transform, matOptions,
                new Vector2(0, 0), new Vector2(248, 28), OnMaterialSelected);
            AddRow(28f, ActionGap, _materialDropdown.GetComponent<RectTransform>());

            // Позиция и поворот.
            _x = Row(panel.transform, "X, м");
            _y = Row(panel.transform, "Y, м");
            _z = Row(panel.transform, "Z, м");
            _rx = Row(panel.transform, "Поворот X°");
            _ry = Row(panel.transform, "Поворот Y°");
            _rz = Row(panel.transform, "Поворот Z°");

            foreach (var f in new[] { _w, _h, _d, _radius }) f.contentType = TMP_InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _gapLeft, _gapRight, _gapTop, _gapBottom }) f.contentType = TMP_InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _x, _y, _z, _rx, _ry, _rz }) f.contentType = TMP_InputField.ContentType.DecimalNumber;

            // Повороты на 90° вокруг каждой мировой оси. Отдельные X/Y/Z — чтобы
            // ставить детали вертикально (поворот по X/Z), а не только крутить по Y.
            var rotLbl = UIFactory.CreateLabel("CtxRotLbl", panel.transform, "Повернуть на 90°:", 15,
                Vector2.zero, new Vector2(260, RotLblH), TextAnchor.MiddleCenter);
            AddRow(RotLblH, RotLblGap, rotLbl.rectTransform);

            var rotX = UIFactory.CreateButton("CtxRotX", panel.transform, "X 90°",
                new Vector2(-90, 0), new Vector2(86, BtnH), () => RotateAxis(Vector3.right));
            var rotY = UIFactory.CreateButton("CtxRotY", panel.transform, "Y 90°",
                new Vector2(0, 0), new Vector2(86, BtnH), () => RotateAxis(Vector3.up));
            var rotZ = UIFactory.CreateButton("CtxRotZ", panel.transform, "Z 90°",
                new Vector2(90, 0), new Vector2(86, BtnH), () => RotateAxis(Vector3.forward));
            AddRow(BtnH, ActionGap,
                rotX.GetComponent<RectTransform>(),
                rotY.GetComponent<RectTransform>(),
                rotZ.GetComponent<RectTransform>());

            var apply = UIFactory.CreateButton("CtxApply", panel.transform, "Применить",
                new Vector2(-65, 0), new Vector2(120, 32), Apply);
            var dup = UIFactory.CreateButton("CtxDup", panel.transform, "Дублировать",
                new Vector2(65, 0), new Vector2(120, 32), Duplicate);
            AddRow(32f, ActionGap,
                apply.GetComponent<RectTransform>(),
                dup.GetComponent<RectTransform>());

            var del = UIFactory.CreateButton("CtxDel", panel.transform, "Удалить",
                new Vector2(0, 0), new Vector2(248, 32), Delete);
            AddRow(32f, ActionGap, del.GetComponent<RectTransform>());

            _transparentToggle = UIFactory.CreateToggle("CtxTransparent", panel.transform, "Прозрачный", false,
                new Vector2(0, 0), new Vector2(248, 26), v =>
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
                new Vector2(0, 0), new Vector2(248, 26), v => { if (_target != null) _target.Movable = !v; });
            AddRow(26f, 0f, _lockToggle.GetComponent<RectTransform>());

            // Кнопка закрытия живёт в углу панели, вне потока раскладки.
            var closeBtn = UIFactory.CreateButton("CtxClose", panel.transform, "X",
                Vector2.zero, new Vector2(24, 24), Close);
            UIFactory.AnchorTopRight(closeBtn.GetComponent<RectTransform>());
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-4, -4);
            closeBtn.transform.SetAsLastSibling();

            Layout(isFacade: false, isAssembled: false, isRadial: false, isDrawer: false); // стартовая раскладка (как обычная деталь)
            _root.SetActive(false);

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged += OnSelectionChanged;
        }

        private void OnDestroy()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
        }

        // Меню закрывается, когда выделение ушло с его детали (клик в пустоту,
        // выбор другой детали, удаление). Не закрываем во время Open(), т.к.
        // SelectionManager.Select → DeselectAll → OnSelectionChanged(null) иначе
        // обнуляет _target и роняет RefreshTransformFields.
        private void OnSelectionChanged(KitchenElement? element)
        {
            if (_opening) return;
            if (_root == null || !_root.activeSelf) return;
            if (element == null || element != _target)
                Close();
        }

        // ── Построение элементов ───────────────────────────────────────

        private TMP_InputField Row(Transform parent, string label)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateInputField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(100, FieldH));
            AddRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        private TMP_InputField RadialRow(Transform parent, string label)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateInputField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(100, FieldH));
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
            rt.sizeDelta = new Vector2(260, sectionH);
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

        // Якорим к верхней кромке панели, pivot тоже сверху — тогда
        // anchoredPosition.y = отступ верхней кромки элемента от верха панели
        // (со знаком минус). Это домовая конвенция панелей проекта.
        private static void AnchorTop(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        }

        // ── Раскладка сверху вниз ───────────────────────────────────────

        private void Layout(bool isFacade, bool isAssembled, bool isRadial, bool isDrawer)
        {
            float cursor = TopPad;
            float contentBottom = TopPad;
            foreach (var row in _layout)
            {
                bool visible = (!row.facadeOnly || isFacade)
                    && (!row.assembledOnly || isAssembled)
                    && (!row.radialOnly || isRadial)
                    && (!row.drawerOnly || isDrawer);

                if (row.toggleGO != null)
                    row.toggleGO.SetActive(visible);
                else if (row.facadeOnly || row.assembledOnly || row.radialOnly || row.drawerOnly)
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
            if (Input.GetKeyDown(KeyCode.Escape) && _root != null && _root.activeSelf)
                Close();

            if (_root != null && _root.activeSelf && _target != null)
            {
                RefreshTransformFields();
            }
        }

        private bool IsAnyFieldFocused()
        {
            foreach (var f in new[] { _name, _w, _h, _d, _radius, _drawerWidth, _gapLeft, _gapRight, _gapTop, _gapBottom, _x, _y, _z, _rx, _ry, _rz })
                if (f != null && f.isFocused) return true;
            return false;
        }

        private void RefreshTransformFields()
        {
            if (_target == null) return;
            if (_target is FacadeElement f && !f.IsDoorClosed) return;

            var pos = _target.transform.position;
            MaybeRefresh(_x, pos.x.ToString("F3"));
            MaybeRefresh(_y, pos.y.ToString("F3"));
            MaybeRefresh(_z, pos.z.ToString("F3"));

            var eu = _target.transform.eulerAngles;
            MaybeRefresh(_rx, eu.x.ToString("F1"));
            MaybeRefresh(_ry, eu.y.ToString("F1"));
            MaybeRefresh(_rz, eu.z.ToString("F1"));

            // Размеры, имя, радиус, зазоры — тоже обновляем в реальном времени
            var dims = _target.DimensionsMM;
            var radial = _target as RadialShelfElement;
            if (radial != null)
            {
                MaybeRefresh(_radius, radial.Radius.ToString());
            }
            else
            {
                MaybeRefresh(_w, dims.x.ToString());
                MaybeRefresh(_h, dims.y.ToString());
                MaybeRefresh(_d, dims.z.ToString());
            }

            MaybeRefresh(_name, _target.PartName);

            var facade = _target as FacadeElement;
            if (facade != null)
            {
                MaybeRefresh(_gapLeft, facade.GapLeft.ToString());
                MaybeRefresh(_gapRight, facade.GapRight.ToString());
                MaybeRefresh(_gapTop, facade.GapTop.ToString());
                MaybeRefresh(_gapBottom, facade.GapBottom.ToString());
            }
        }

        /// <summary>Обновить поле, если оно не в фокусе (юзер не редактирует).
        /// Также синхронизирует _cleanValues, чтобы подсветка не сбивалась.</summary>
        private void MaybeRefresh(TMP_InputField field, string newValue)
        {
            if (field == null || field.isFocused) return;
            field.SetTextWithoutNotify(newValue);
            _cleanValues[field] = newValue;
        }

        public void Open(KitchenElement element)
        {
            if (element == null) return;
            _opening = true;
            try
            {
                _target = element;
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);

                bool isFacade = element is FacadeElement;
                bool isRadial = element is RadialShelfElement;
                bool isDrawer = element is DrawerElement;
                if (_titleLabel != null)
                    _titleLabel.text = isDrawer ? "Ящик GTV" : (isRadial ? "Радиусная полка" : (isFacade ? "Фасад" : "деталь"));

                if (_typeDropdown != null)
                {
                    _typeDropdown.SetValueWithoutNotify((int)ElementConverter.GetElementType(element));
                    _typeDropdown.RefreshShownValue();
                }

                var dims = element.DimensionsMM;
                _name.text = element.PartName;
                _w.text = dims.x.ToString();
                _h.text = dims.y.ToString();
                _d.text = dims.z.ToString();
                var radial = element as RadialShelfElement;
                _radius.text = radial != null ? radial.Radius.ToString() : "300";

                var facade = element as FacadeElement;
                if (facade != null)
                {
                    _gapLeft.text = facade.GapLeft.ToString();
                    _gapRight.text = facade.GapRight.ToString();
                    _gapTop.text = facade.GapTop.ToString();
                    _gapBottom.text = facade.GapBottom.ToString();
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
                        _drawerTypeDropdown.SetValueWithoutNotify((int)drawer.Type);
                    if (_drawerLengthDropdown != null)
                        _drawerLengthDropdown.SetValueWithoutNotify(System.Array.IndexOf(DrawerConstants.ValidLengths, drawer.NominalLength));
                    if (_drawerColorDropdown != null)
                        _drawerColorDropdown.SetValueWithoutNotify((int)drawer.Color);
                    if (_drawerWidth != null)
                        _drawerWidth.text = drawer.InternalWidth.ToString();
                    if (_drawerDoubleToggle != null)
                        _drawerDoubleToggle.SetIsOnWithoutNotify(drawer.IsDouble);
                    if (_drawerUpperToggle != null)
                        _drawerUpperToggle.SetIsOnWithoutNotify(drawer.IsUpperDrawer);
                    UpdateDrawerAnimButton(drawer);
                    RebuildDrawerFacadeOptions();
                    SetDrawerFacadeValue(drawer.AttachedFacadeName);
                }

                if (_materialDropdown != null)
                {
                    // Пересобираем список каждый раз — так подгруженные в рантайме
                    // внешние текстуры появляются без перезапуска (reload_textures).
                    RebuildMaterialOptions();
                    _materialDropdown.SetValueWithoutNotify(MaterialIndex(element.MaterialId));
                    _materialDropdown.RefreshShownValue();
                }

                // Пересчитываем раскладку под режим: секция зазоров показывается
                // только для фасадов, радиус — только для радиусной полки, панель сама подгоняется по высоте.
                Layout(isFacade, assembled != null, isRadial, isDrawer);

                RefreshTransformFields();
                _transparentToggle.SetIsOnWithoutNotify(element.Transparent);
                _lockToggle.SetIsOnWithoutNotify(!element.Movable);

                ClearAllHighlights();
                TrackAllFields();

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
            // Правки размеров/позиции применяем к закрытой (логической) позе.
            if (_target is FacadeElement fac) { fac.ForceClose(); UpdateDoorButton(fac); }
            if (_target is DrawerElement dr) { dr.ForceClose(); UpdateDrawerAnimButton(dr); }

            var oldDims = _target.DimensionsMM;
            var oldPos = _target.transform.position;
            var oldRot = _target.transform.rotation;

            // Через DrawerLinks: переименование обновляет обратные ссылки
            // (PairedDrawerName пары, AttachedFacadeName ящиков с этим фасадом).
            DrawerLinks.Rename(_target, string.IsNullOrWhiteSpace(_name.text) ? "Board" : _name.text);

            var radial = _target as RadialShelfElement;
            var drawer = _target as DrawerElement;
            if (radial != null)
            {
                radial.Radius = ParseInt(_radius.text, radial.Radius);
            }
            else if (drawer != null)
            {
                if (_drawerWidth != null) drawer.InternalWidth = ParseInt(_drawerWidth.text, drawer.InternalWidth);
            }
            else
            {
                _target.DimensionsMM = new Vector3Int(
                    ParseInt(_w.text, oldDims.x),
                    ParseInt(_h.text, oldDims.y),
                    ParseInt(_d.text, oldDims.z));
            }

            var facade = _target as FacadeElement;
            if (facade != null)
            {
                facade.GapLeft = ParseInt(_gapLeft.text, facade.GapLeft);
                facade.GapRight = ParseInt(_gapRight.text, facade.GapRight);
                facade.GapTop = ParseInt(_gapTop.text, facade.GapTop);
                facade.GapBottom = ParseInt(_gapBottom.text, facade.GapBottom);
            }

            _target.transform.position = new Vector3(
                ParseFloat(_x.text, oldPos.x),
                ParseFloat(_y.text, oldPos.y),
                ParseFloat(_z.text, oldPos.z));

            var euler = oldRot.eulerAngles;
            _target.transform.rotation = Quaternion.Euler(
                ParseFloat(_rx.text, euler.x),
                ParseFloat(_ry.text, euler.y),
                ParseFloat(_rz.text, euler.z));

            if (KitchenSettings.Instance.BlockOnViolation && WouldCauseViolation())
            {
                _target.DimensionsMM = oldDims;
                _target.transform.position = oldPos;
                _target.transform.rotation = oldRot;
            }
            else
            {
                CommandStack.Execute(new ResizeCommand(_target,
                    oldDims, _target.DimensionsMM,
                    oldPos, _target.transform.position,
                    oldRot, _target.transform.rotation));
            }

            var newDims = _target.DimensionsMM;
            _w.text = newDims.x.ToString();
            _h.text = newDims.y.ToString();
            _d.text = newDims.z.ToString();
            if (radial != null)
                _radius.text = radial.Radius.ToString();

            if (facade != null)
            {
                _gapLeft.text = facade.GapLeft.ToString();
                _gapRight.text = facade.GapRight.ToString();
                _gapTop.text = facade.GapTop.ToString();
                _gapBottom.text = facade.GapBottom.ToString();
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

        private void RotateAxis(Vector3 axis)
        {
            if (_target == null) return;
            var oldRot = _target.transform.rotation;
            _target.RotateAroundAxis(axis, 90f);
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
            _materialDropdown.options = opts;
        }

        private void OnMaterialSelected(int index)
        {
            if (_target == null) return;
            var all = MaterialCatalog.All;
            if (index < 0 || index >= all.Count) return;

            MaterialManager.Apply(_target, all[index]); // задаёт MaterialId + декор
            // Выделенный элемент перекрашен подсветкой выделения — обновляем её,
            // чтобы поверх лёг новый декор; невыделенные обновит RefreshHighlights.
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
            if (_target is DrawerElement d && index >= 0 && index <= 3)
                d.Type = (DrawerType)index;
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
                if (d.IsDouble) d.CycleDoubleState();
                else d.ToggleOpen();
                UpdateDrawerAnimButton(d);
            }
        }

        // Второй короб того же типа вплотную сверху/снизу, связи в обе стороны
        // (DrawerLinks.CreatePair). Повторное нажатие при живой паре — no-op.
        private void CreatePairedDrawer()
        {
            if (!(_target is DrawerElement d)) return;
            var pair = DrawerLinks.CreatePair(d);
            if (pair == null) return;
            CommandStack.Execute(new CreateCommand(pair.gameObject));
            RefreshHighlights();
            Open(d); // обновить тумблеры «двойной/верхний» и подпись кнопки анимации
        }

        private void UpdateDrawerAnimButton(DrawerElement d)
        {
            if (_drawerAnimLabel == null || d == null) return;
            if (d.IsDouble)
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
            foreach (var el in PartRegistry.GetAll())
                if (el is FacadeElement fe && !string.IsNullOrEmpty(fe.PartName))
                    opts.Add(new TMP_Dropdown.OptionData(fe.PartName));
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

        // Размер/позиция фасада = фронт ящика (+Z), линейное открывание (как у ящика).
        private void CreateFacadeForDrawer()
        {
            if (!(_target is DrawerElement d)) return;
            int width = d.InternalWidth;
            int height = DrawerConstants.GetTypeHeight(d.Type);
            const int thickness = 18;
            const int gapMm = 2;
            const int sideGap = 2;

            var closedPos = d.ClosedPosition;
            var rot = d.ClosedRotation;
            float drawerHalfDepth = d.NominalLength * 0.5f * AppConstants.MM_TO_UNITS;
            float facadeHalfDepth = thickness * 0.5f * AppConstants.MM_TO_UNITS;
            float gap = gapMm * AppConstants.MM_TO_UNITS;
            Vector3 front = rot * Vector3.forward;
            Vector3 pos = closedPos + front * (drawerHalfDepth + facadeHalfDepth + gap);
            pos = GridManager.SnapToGrid(pos);

            var dims = new Vector3Int(width, height, thickness);
            // Имя уникально: привязка идёт по имени, дубликат сломал бы поиск.
            var facadeName = DrawerLinks.UniqueName(
                !string.IsNullOrEmpty(d.PartName) ? d.PartName + "_Фасад" : "Фасад ящика");
            var go = ElementFactory.CreateFacade(dims, facadeName, pos, sideGap, sideGap, sideGap, sideGap);
            if (go == null) return;
            var fe = go.GetComponent<FacadeElement>();
            if (fe != null) fe.Mode = DoorMode.DrawerOut;

            CommandStack.Execute(new CreateCommand(go));
            d.AttachedFacadeName = facadeName;
            RebuildDrawerFacadeOptions();
            SetDrawerFacadeValue(facadeName);
            RefreshHighlights();

            // Переключаем меню на свежесозданный фасад — юзер сразу настраивает зазоры/материал.
            if (fe != null) Open(fe);
        }

        private void ConfigureAttachedFacade()
        {
            if (!(_target is DrawerElement d) || string.IsNullOrEmpty(d.AttachedFacadeName)) return;
            foreach (var el in PartRegistry.GetAll())
                if (el is FacadeElement fe && fe.PartName == d.AttachedFacadeName) { Open(fe); return; }
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

        private void TrackField(TMP_InputField field, string cleanValue)
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
            TrackField(_radius, radial != null ? radial.Radius.ToString() : "300");
            var facade = _target as FacadeElement;
            TrackField(_gapLeft, facade != null ? facade.GapLeft.ToString() : "0");
            TrackField(_gapRight, facade != null ? facade.GapRight.ToString() : "0");
            TrackField(_gapTop, facade != null ? facade.GapTop.ToString() : "0");
            TrackField(_gapBottom, facade != null ? facade.GapBottom.ToString() : "0");
            var drawerEl2 = _target as DrawerElement;
            TrackField(_drawerWidth, drawerEl2 != null ? drawerEl2.InternalWidth.ToString() : "400");
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
