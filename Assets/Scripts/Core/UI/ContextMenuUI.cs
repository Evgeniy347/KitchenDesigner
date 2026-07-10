using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Контекстное меню по ПКМ на доске: размеры, позиция, действия.</summary>
    public class ContextMenuUI : MonoBehaviour
    {
        private GameObject _root;
        private KitchenElement _target;

        private InputField _name, _w, _h, _d, _x, _y, _z;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("ContextMenu", canvas, Vector2.zero, new Vector2(280, 420));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(-10, -60);
            _root = panel.gameObject;

            UIFactory.CreateLabel("CtxTitle", panel.transform, "Доска", 20,
                new Vector2(0, 185), new Vector2(260, 30), TextAnchor.MiddleCenter);

            _name = Row(panel.transform, "Название", 150, out _);
            _w = Row(panel.transform, "Ширина, мм", 110, out _);
            _h = Row(panel.transform, "Высота, мм", 70, out _);
            _d = Row(panel.transform, "Глубина, мм", 30, out _);
            _x = Row(panel.transform, "X, м", -10, out _);
            _y = Row(panel.transform, "Y, м", -50, out _);
            _z = Row(panel.transform, "Z, м", -90, out _);

            foreach (var f in new[] { _w, _h, _d }) f.contentType = InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _x, _y, _z }) f.contentType = InputField.ContentType.DecimalNumber;

            UIFactory.CreateButton("CtxApply", panel.transform, "Применить",
                new Vector2(-65, -135), new Vector2(120, 34), Apply);
            UIFactory.CreateButton("CtxRotate", panel.transform, "Повернуть 90°",
                new Vector2(65, -135), new Vector2(120, 34), Rotate90);
            UIFactory.CreateButton("CtxDup", panel.transform, "Дублировать",
                new Vector2(-65, -175), new Vector2(120, 34), Duplicate);
            UIFactory.CreateButton("CtxDel", panel.transform, "Удалить",
                new Vector2(65, -175), new Vector2(120, 34), Delete);

            _root.SetActive(false);
        }

        private InputField Row(Transform parent, string label, float y, out Text lbl)
        {
            lbl = UIFactory.CreateLabel("L_" + label, parent, label, 15,
                new Vector2(-70, y), new Vector2(130, 26));
            var field = UIFactory.CreateInputField("F_" + label, parent, "",
                new Vector2(85, y), new Vector2(100, 26));
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
        }

        private void Open(KitchenElement element)
        {
            _target = element;
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.Select(element);

            var dims = element.DimensionsMM;
            var pos = element.transform.position;
            _name.text = element.BoardName;
            _w.text = dims.x.ToString();
            _h.text = dims.y.ToString();
            _d.text = dims.z.ToString();
            _x.text = pos.x.ToString("F3");
            _y.text = pos.y.ToString("F3");
            _z.text = pos.z.ToString("F3");

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

            int w = ParseInt(_w.text, _target.DimensionsMM.x);
            int h = ParseInt(_h.text, _target.DimensionsMM.y);
            int d = ParseInt(_d.text, _target.DimensionsMM.z);
            _target.DimensionsMM = new Vector3Int(w, h, d);

            var pos = _target.transform.position;
            _target.transform.position = new Vector3(
                ParseFloat(_x.text, pos.x),
                ParseFloat(_y.text, pos.y),
                ParseFloat(_z.text, pos.z));

            // Отразить clamp размеров обратно в поля.
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
