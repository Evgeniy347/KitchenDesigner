using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Контекстное меню по клику ЛКМ на доске: размеры, позиция, поворот, действия.</summary>
    public class ContextMenuUI : MonoBehaviour
    {
        public static ContextMenuUI Instance { get; private set; }

        private GameObject _root;
        private KitchenElement _target;
        private Text _titleLabel;

        private InputField _name, _w, _h, _d, _gapW, _gapH, _x, _y, _z, _rx, _ry, _rz;
        private Toggle _lockToggle;
        private Toggle _transparentToggle;
        private RectTransform _panelRt;
        private Text _doorButtonLabel;  // подпись кнопки «Открыть»/«Закрыть»
        private Dropdown _modeDropdown; // выпадающий список режима открывания
        private Dropdown _fillDropdown; // центр сборного фасада (Глухой/Витрина/Стекло)

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
            public bool assembledOnly;    // показывать только для сборного фасада
            public GameObject toggleGO;   // объект, который включать/выключать по режиму
        }
        private readonly List<LayoutRow> _layout = new();

        // Геометрия
        private const float LabelX = -72f;
        private const float FieldX = 82f;
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
            _titleLabel = UIFactory.CreateLabel("CtxTitle", panel.transform, "Доска", 20,
                Vector2.zero, new Vector2(260, TitleH), TextAnchor.MiddleCenter);
            AddRow(TitleH, TitleGap, _titleLabel.rectTransform);

            // Размеры.
            _name = Row(panel.transform, "Название");
            _w = Row(panel.transform, "Ширина, мм");
            _h = Row(panel.transform, "Высота, мм");
            _d = Row(panel.transform, "Глубина, мм");

            // Зазоры (только для фасадов) — блок скрывается в режиме «Доска».
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
            _doorButtonLabel = doorButton.GetComponentInChildren<Text>();
            AddFacadeRow(BtnH, ActionGap, doorButton.GetComponent<RectTransform>());

            // Центр сборного фасада (только для сборного): Глухой / Витрина / Стекло.
            var fillOptions = new List<string> { "Глухой (панель)", "Витрина (пусто)", "Стекло" };
            _fillDropdown = UIFactory.CreateDropdown("CtxFill", panel.transform, fillOptions,
                new Vector2(0, 0), new Vector2(248, 28), OnFillSelected);
            AddAssembledRow(28f, ActionGap, _fillDropdown.GetComponent<RectTransform>());

            // Позиция и поворот.
            _x = Row(panel.transform, "X, м");
            _y = Row(panel.transform, "Y, м");
            _z = Row(panel.transform, "Z, м");
            _rx = Row(panel.transform, "Поворот X°");
            _ry = Row(panel.transform, "Поворот Y°");
            _rz = Row(panel.transform, "Поворот Z°");

            foreach (var f in new[] { _w, _h, _d }) f.contentType = InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _gapW, _gapH }) f.contentType = InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _x, _y, _z, _rx, _ry, _rz }) f.contentType = InputField.ContentType.DecimalNumber;

            // Повороты на 90° вокруг каждой мировой оси. Отдельные X/Y/Z — чтобы
            // ставить доски вертикально (поворот по X/Z), а не только крутить по Y.
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
            var closeBtn = UIFactory.CreateButton("CtxClose", panel.transform, "✕",
                Vector2.zero, new Vector2(24, 24), Close);
            UIFactory.AnchorTopRight(closeBtn.GetComponent<RectTransform>());
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-4, -4);
            closeBtn.transform.SetAsLastSibling();

            Layout(isFacade: false, isAssembled: false); // стартовая раскладка (как обычная доска)
            _root.SetActive(false);

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged += OnSelectionChanged;
        }

        private void OnDestroy()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
        }

        // Меню закрывается, когда выделение ушло с его доски (клик в пустоту,
        // выбор другой доски, удаление).
        private void OnSelectionChanged(KitchenElement element)
        {
            if (_root == null || !_root.activeSelf) return;
            if (element == null || element != _target)
                Close();
        }

        // ── Построение элементов ───────────────────────────────────────

        private InputField Row(Transform parent, string label)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(130, LabelH));
            var field = UIFactory.CreateInputField("F_" + label, parent, "",
                new Vector2(FieldX, 0), new Vector2(100, FieldH));
            AddRow(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
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

            // Содержимое раскладывается сверху вниз (та же конвенция, что и панель):
            // курсор = расстояние от верха секции до верхней кромки строки. Высота
            // секции берётся из курсора, поэтому строки гарантированно помещаются.
            float top = pad;
            AddTopAnchoredChild(UIFactory.CreateLabel("CtxGapHdr", root.transform, "Зазоры:", 14,
                new Vector2(LabelX, -top), new Vector2(130, headerH), TextAnchor.MiddleLeft).rectTransform);
            top += headerH + innerGap;
            _gapW = GapField(root.transform, "Ширина X, мм", -top);
            top += fieldH + innerGap;
            _gapH = GapField(root.transform, "Высота Y, мм", -top);
            top += fieldH + pad;

            sectionH = top;
            rt.sizeDelta = new Vector2(260, sectionH);
            root.SetActive(false);
            return root;
        }

        private static InputField GapField(Transform parent, string label, float y)
        {
            AddTopAnchoredChild(UIFactory.CreateLabel("Gap_" + label, parent, label, 13,
                new Vector2(LabelX, y), new Vector2(130, 20), TextAnchor.MiddleLeft).rectTransform);
            var field = UIFactory.CreateInputField("F_gap_" + label, parent, "0",
                new Vector2(FieldX, y), new Vector2(100, 22));
            AddTopAnchoredChild(field.GetComponent<RectTransform>());
            return field;
        }

        private static void AddTopAnchoredChild(RectTransform rt) => AnchorTop(rt);

        // ── Регистрация строк раскладки ────────────────────────────────

        private void AddRow(float height, float gapAfter, params RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _layout.Add(new LayoutRow { rects = rects, height = height, gapAfter = gapAfter });
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

        // Якорим к верхней кромке панели, pivot тоже сверху — тогда
        // anchoredPosition.y = отступ верхней кромки элемента от верха панели
        // (со знаком минус). Это домовая конвенция панелей проекта.
        private static void AnchorTop(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        }

        // ── Раскладка сверху вниз ───────────────────────────────────────

        private void Layout(bool isFacade, bool isAssembled)
        {
            float cursor = TopPad;
            float contentBottom = TopPad;
            foreach (var row in _layout)
            {
                bool visible = (!row.facadeOnly || isFacade) && (!row.assembledOnly || isAssembled);

                // Скрытие фасад-строк в режиме «Доска»: через контейнер (toggleGO)
                // либо, если контейнера нет, включая/выключая сами элементы строки.
                if (row.toggleGO != null)
                    row.toggleGO.SetActive(visible);
                else if (row.facadeOnly || row.assembledOnly)
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
            // Меню открывается из ElementMover по клику ЛКМ (без перетаскивания).
            // ПКМ теперь вращает камеру.
            if (Input.GetKeyDown(KeyCode.Escape) && _root != null && _root.activeSelf)
                Close();

            // Живое обновление полей позиции/поворота (например, при перетаскивании).
            if (_root != null && _root.activeSelf && _target != null)
                RefreshTransformFields();
        }

        private void RefreshTransformFields()
        {
            // Пока дверца открыта/анимируется, трансформ показывает «открытую» позу —
            // не перетираем поля ею, оставляем закрытые (логические) значения.
            if (_target is FacadeElement f && !f.IsDoorClosed) return;

            var pos = _target.transform.position;
            if (!_x.isFocused) _x.SetTextWithoutNotify(pos.x.ToString("F3"));
            if (!_y.isFocused) _y.SetTextWithoutNotify(pos.y.ToString("F3"));
            if (!_z.isFocused) _z.SetTextWithoutNotify(pos.z.ToString("F3"));

            var e = _target.transform.eulerAngles;
            if (!_rx.isFocused) _rx.SetTextWithoutNotify(e.x.ToString("F1"));
            if (!_ry.isFocused) _ry.SetTextWithoutNotify(e.y.ToString("F1"));
            if (!_rz.isFocused) _rz.SetTextWithoutNotify(e.z.ToString("F1"));
        }

        public void Open(KitchenElement element)
        {
            if (element == null) return;
            _target = element;
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.Select(element);

            bool isFacade = element is FacadeElement;
            if (_titleLabel != null)
                _titleLabel.text = isFacade ? "Фасад" : "Доска";

            var dims = element.DimensionsMM;
            _name.text = element.BoardName;
            _w.text = dims.x.ToString();
            _h.text = dims.y.ToString();
            _d.text = dims.z.ToString();

            var facade = element as FacadeElement;
            if (facade != null)
            {
                _gapW.text = (facade.GapLeft + facade.GapRight).ToString();
                _gapH.text = (facade.GapTop + facade.GapBottom).ToString();
            }
            UpdateDoorButton(facade);
            UpdateModeDropdown(facade);

            var assembled = element as AssembledFacadeElement;
            if (assembled != null && _fillDropdown != null)
                _fillDropdown.SetValueWithoutNotify(FillToIndex(assembled.Fill));

            // Пересчитываем раскладку под режим: секция зазоров показывается
            // только для фасадов, панель сама подгоняется по высоте.
            Layout(isFacade, assembled != null);

            RefreshTransformFields();
            _transparentToggle.SetIsOnWithoutNotify(element.Transparent);
            _lockToggle.SetIsOnWithoutNotify(!element.Movable);

            _root.SetActive(true);
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

            var oldDims = _target.DimensionsMM;
            var oldPos = _target.transform.position;
            var oldRot = _target.transform.rotation;

            _target.BoardName = string.IsNullOrWhiteSpace(_name.text) ? "Board" : _name.text;

            _target.DimensionsMM = new Vector3Int(
                ParseInt(_w.text, oldDims.x),
                ParseInt(_h.text, oldDims.y),
                ParseInt(_d.text, oldDims.z));

            var facade = _target as FacadeElement;
            if (facade != null)
            {
                var totalX = ParseInt(_gapW.text, facade.GapLeft + facade.GapRight);
                var totalY = ParseInt(_gapH.text, facade.GapTop + facade.GapBottom);
                facade.GapLeft = totalX / 2;
                facade.GapRight = totalX - facade.GapLeft;
                facade.GapTop = totalY / 2;
                facade.GapBottom = totalY - facade.GapTop;
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

            _w.text = _target.DimensionsMM.x.ToString();
            _h.text = _target.DimensionsMM.y.ToString();
            _d.text = _target.DimensionsMM.z.ToString();

            if (facade != null)
            {
                _gapW.text = (facade.GapLeft + facade.GapRight).ToString();
                _gapH.text = (facade.GapTop + facade.GapBottom).ToString();
            }

            RefreshHighlights();
        }

        private bool WouldCauseViolation()
        {
            var list = BoardRegistry.GetAll();
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

        private void UpdateDoorButton(FacadeElement facade)
        {
            if (_doorButtonLabel != null)
                _doorButtonLabel.text = (facade != null && facade.IsOpen) ? "Закрыть" : "Открыть";
        }

        private void UpdateModeDropdown(FacadeElement facade)
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
                CommandStack.Execute(new CreateCommand(dup));
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
    }
}
