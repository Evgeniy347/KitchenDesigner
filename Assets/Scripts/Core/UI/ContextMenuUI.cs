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
        private GameObject _gapRow;
        private readonly List<(RectTransform rt, float baseY)> _postGapElements = new();
        private RectTransform _panelRt;
        private float _panelBaseH;
        private const float GAP_ROW_H = 62f;

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

            const float rowStartY = 220f;
            const float rowStep = 31f;
            const float rotLabelGap = 23f;
            const float rotBtnGap = 4f;
            const float actionGap = 8f;
            const float btnH = 28f;
            const float labelH = 22f;
            float y = rowStartY;
            _name = Row(panel.transform, "Название", ref y, rowStep);
            _w = Row(panel.transform, "Ширина, мм", ref y, rowStep);
            _h = Row(panel.transform, "Высота, мм", ref y, rowStep);
            _d = Row(panel.transform, "Глубина, мм", ref y, rowStep);

            // ── Зазоры (только для фасадов) ──
            _gapRow = CreateGapSection(panel.transform, ref y);

            _postGapElements.Clear();
            System.Action<InputField> track = f =>
            {
                if (f != null)
                {
                    var rt = f.GetComponent<RectTransform>();
                    _postGapElements.Add((rt, rt.anchoredPosition.y));
                }
            };
            _x = Row(panel.transform, "X, м", ref y, rowStep); track(_x);
            _y = Row(panel.transform, "Y, м", ref y, rowStep); track(_y);
            _z = Row(panel.transform, "Z, м", ref y, rowStep); track(_z);
            _rx = Row(panel.transform, "Поворот X°", ref y, rowStep); track(_rx);
            _ry = Row(panel.transform, "Поворот Y°", ref y, rowStep); track(_ry);
            _rz = Row(panel.transform, "Поворот Z°", ref y, rowStep); track(_rz);

            foreach (var f in new[] { _w, _h, _d }) f.contentType = InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _gapW, _gapH }) f.contentType = InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _x, _y, _z, _rx, _ry, _rz }) f.contentType = InputField.ContentType.DecimalNumber;

            // Повороты на 90° вокруг каждой мировой оси. Отдельные X/Y/Z — чтобы
            // ставить доски вертикально (поворот по X/Z), а не только крутить по Y.
            float rotLabelY = y - rotLabelGap;
            float rotBtnY = rotLabelY - labelH - rotBtnGap;
            float actionY = rotBtnY - btnH - actionGap;
            var rotLbl = UIFactory.CreateLabel("CtxRotLbl", panel.transform, "Повернуть на 90°:", 15,
                new Vector2(0, rotLabelY), new Vector2(260, labelH), TextAnchor.MiddleCenter);
            _postGapElements.Add((rotLbl.rectTransform, rotLbl.rectTransform.anchoredPosition.y));
            var rotX = UIFactory.CreateButton("CtxRotX", panel.transform, "X 90°",
                new Vector2(-90, rotBtnY), new Vector2(86, btnH), () => RotateAxis(Vector3.right));
            _postGapElements.Add((rotX.GetComponent<RectTransform>(), rotBtnY));
            var rotY = UIFactory.CreateButton("CtxRotY", panel.transform, "Y 90°",
                new Vector2(0, rotBtnY), new Vector2(86, btnH), () => RotateAxis(Vector3.up));
            _postGapElements.Add((rotY.GetComponent<RectTransform>(), rotBtnY));
            var rotZ = UIFactory.CreateButton("CtxRotZ", panel.transform, "Z 90°",
                new Vector2(90, rotBtnY), new Vector2(86, btnH), () => RotateAxis(Vector3.forward));
            _postGapElements.Add((rotZ.GetComponent<RectTransform>(), rotBtnY));

            var apply = UIFactory.CreateButton("CtxApply", panel.transform, "Применить",
                new Vector2(-65, actionY), new Vector2(120, 32), Apply);
            _postGapElements.Add((apply.GetComponent<RectTransform>(), actionY));
            var dup = UIFactory.CreateButton("CtxDup", panel.transform, "Дублировать",
                new Vector2(65, actionY), new Vector2(120, 32), Duplicate);
            _postGapElements.Add((dup.GetComponent<RectTransform>(), actionY));
            var del = UIFactory.CreateButton("CtxDel", panel.transform, "Удалить",
                new Vector2(0, actionY - 36), new Vector2(248, 32), Delete);
            _postGapElements.Add((del.GetComponent<RectTransform>(), actionY - 36));

            _lockToggle = UIFactory.CreateToggle("CtxLock", panel.transform, "Запретить перемещение", false,
                new Vector2(0, actionY - 74), new Vector2(248, 26), v => { if (_target != null) _target.Movable = !v; });
            var lockRt = _lockToggle.GetComponent<RectTransform>();
            _postGapElements.Add((lockRt, actionY - 74));

            _titleLabel = UIFactory.CreateLabel("CtxTitle", panel.transform, "Доска", 20,
                Vector2.zero, new Vector2(260, 28), TextAnchor.MiddleCenter);
            _titleLabel.rectTransform.anchorMin = new Vector2(0, 1);
            _titleLabel.rectTransform.anchorMax = new Vector2(1, 1);
            _titleLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _titleLabel.rectTransform.anchoredPosition = Vector2.zero;

            var closeBtn = UIFactory.CreateButton("CtxClose", panel.transform, "✕",
                Vector2.zero, new Vector2(24, 24), Close);
            UIFactory.AnchorTopRight(closeBtn.GetComponent<RectTransform>());
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-4, -4);
            closeBtn.transform.SetAsLastSibling();

            float topContent = (rowStartY + 35) + 14f;
            float bottomContent = (actionY - 74) - 13f;
            float halfHeight = Mathf.Max(topContent, -bottomContent);
            _panelBaseH = halfHeight * 2f + 40f;
            panel.rectTransform.sizeDelta = new Vector2(280, _panelBaseH);

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

        private GameObject CreateGapSection(Transform parent, ref float y)
        {
            var root = new GameObject("_GapSection");
            var rt = root.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(260, 64);

            UIFactory.CreateLabel("CtxGapHdr", root.transform, "Зазоры:", 14,
                new Vector2(-72, 0), new Vector2(130, 20), TextAnchor.MiddleLeft);

            _gapW = GapField(root.transform, "Ширина X, мм", -72, 82, -26);
            _gapH = GapField(root.transform, "Высота Y, мм", -72, 82, -52);

            y -= GAP_ROW_H;
            root.SetActive(false);
            return root;
        }

        private static InputField GapField(Transform parent, string label, float labelX, float fieldX, float y)
        {
            UIFactory.CreateLabel("Gap_" + label, parent, label, 13,
                new Vector2(labelX, y), new Vector2(130, 20), TextAnchor.MiddleLeft);
            return UIFactory.CreateInputField("F_gap_" + label, parent, "0",
                new Vector2(fieldX, y), new Vector2(100, 22));
        }

        private InputField Row(Transform parent, string label, ref float y, float step)
        {
            UIFactory.CreateLabel("L_" + label, parent, label, 15, new Vector2(-72, y), new Vector2(130, 24));
            var field = UIFactory.CreateInputField("F_" + label, parent, "", new Vector2(82, y), new Vector2(100, 24));
            y -= step;
            return field;
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

            if (_titleLabel != null)
                _titleLabel.text = element is FacadeElement ? "Фасад" : "Доска";

            var dims = element.DimensionsMM;
            _name.text = element.BoardName;
            _w.text = dims.x.ToString();
            _h.text = dims.y.ToString();
            _d.text = dims.z.ToString();

            var facade = element as FacadeElement;
            if (_gapRow != null)
            {
                _gapRow.SetActive(facade != null);
                if (facade != null)
                {
                    _gapW.text = (facade.GapLeft + facade.GapRight).ToString();
                    _gapH.text = (facade.GapTop + facade.GapBottom).ToString();
                }
            }

            float shift = facade != null ? 0f : GAP_ROW_H;
            foreach (var entry in _postGapElements)
            {
                if (entry.rt != null)
                    entry.rt.anchoredPosition = new Vector2(
                        entry.rt.anchoredPosition.x, entry.baseY + shift);
            }
            if (_panelRt != null)
                _panelRt.sizeDelta = new Vector2(_panelRt.sizeDelta.x, _panelBaseH - shift);

            RefreshTransformFields();

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
