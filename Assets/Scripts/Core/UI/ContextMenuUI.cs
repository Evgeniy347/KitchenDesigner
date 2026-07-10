using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Контекстное меню по ПКМ на доске: размеры, позиция, поворот, действия.</summary>
    public class ContextMenuUI : MonoBehaviour
    {
        private GameObject _root;
        private KitchenElement _target;

        private InputField _name, _w, _h, _d, _x, _y, _z, _rx, _ry, _rz;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("ContextMenu", canvas, Vector2.zero, new Vector2(280, 560));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(-10, -60);
            _root = panel.gameObject;

            UIFactory.CreateLabel("CtxTitle", panel.transform, "Доска", 20,
                new Vector2(0, 255), new Vector2(260, 28), TextAnchor.MiddleCenter);

            float y = 220f;
            const float step = 31f;
            _name = Row(panel.transform, "Название", ref y, step);
            _w = Row(panel.transform, "Ширина, мм", ref y, step);
            _h = Row(panel.transform, "Высота, мм", ref y, step);
            _d = Row(panel.transform, "Глубина, мм", ref y, step);
            _x = Row(panel.transform, "X, м", ref y, step);
            _y = Row(panel.transform, "Y, м", ref y, step);
            _z = Row(panel.transform, "Z, м", ref y, step);
            _rx = Row(panel.transform, "Поворот X°", ref y, step);
            _ry = Row(panel.transform, "Поворот Y°", ref y, step);
            _rz = Row(panel.transform, "Поворот Z°", ref y, step);

            foreach (var f in new[] { _w, _h, _d }) f.contentType = InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _x, _y, _z, _rx, _ry, _rz }) f.contentType = InputField.ContentType.DecimalNumber;

            UIFactory.CreateButton("CtxApply", panel.transform, "Применить",
                new Vector2(-65, -150), new Vector2(120, 32), Apply);
            UIFactory.CreateButton("CtxRotate", panel.transform, "Повернуть 90°",
                new Vector2(65, -150), new Vector2(120, 32), Rotate90);
            UIFactory.CreateButton("CtxDup", panel.transform, "Дублировать",
                new Vector2(-65, -186), new Vector2(120, 32), Duplicate);
            UIFactory.CreateButton("CtxDel", panel.transform, "Удалить",
                new Vector2(65, -186), new Vector2(120, 32), Delete);

            _root.SetActive(false);
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
            if (Input.GetMouseButtonDown(1) && !ElementMover.IsDragging)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    var element = hit.collider.GetComponentInParent<KitchenElement>();
                    if (element != null && element.GetComponent<BasePlate>() == null)
                    {
                        Open(element);
                        return;
                    }
                }
                Close();
            }

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

        private void Open(KitchenElement element)
        {
            _target = element;
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.Select(element);

            var dims = element.DimensionsMM;
            _name.text = element.BoardName;
            _w.text = dims.x.ToString();
            _h.text = dims.y.ToString();
            _d.text = dims.z.ToString();
            RefreshTransformFields();

            _root.SetActive(true);
        }

        private void Close()
        {
            _target = null;
            if (_root != null) _root.SetActive(false);
        }

        private void Apply()
        {
            if (_target == null) return;

            _target.BoardName = string.IsNullOrWhiteSpace(_name.text) ? "Board" : _name.text;

            _target.DimensionsMM = new Vector3Int(
                ParseInt(_w.text, _target.DimensionsMM.x),
                ParseInt(_h.text, _target.DimensionsMM.y),
                ParseInt(_d.text, _target.DimensionsMM.z));

            var pos = _target.transform.position;
            _target.transform.position = new Vector3(
                ParseFloat(_x.text, pos.x),
                ParseFloat(_y.text, pos.y),
                ParseFloat(_z.text, pos.z));

            var e = _target.transform.eulerAngles;
            _target.transform.rotation = Quaternion.Euler(
                ParseFloat(_rx.text, e.x),
                ParseFloat(_ry.text, e.y),
                ParseFloat(_rz.text, e.z));

            _w.text = _target.DimensionsMM.x.ToString();
            _h.text = _target.DimensionsMM.y.ToString();
            _d.text = _target.DimensionsMM.z.ToString();

            RefreshHighlights();
        }

        private void Rotate90()
        {
            if (_target == null) return;
            _target.RotateAroundAxis(Vector3.up, 90f);
            RefreshHighlights();
        }

        private void Duplicate()
        {
            if (_target == null) return;
            var dup = ElementFactory.Duplicate(_target);
            var element = dup != null ? dup.GetComponent<KitchenElement>() : null;
            if (element != null) Open(element);
            RefreshHighlights();
        }

        private void Delete()
        {
            if (_target == null) return;
            var go = _target.gameObject;
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.Deselect();
            Close();
            Destroy(go);
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
