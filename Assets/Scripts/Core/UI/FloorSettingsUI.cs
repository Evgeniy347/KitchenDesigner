using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class FloorSettingsUI : MonoBehaviour
    {
        public static FloorSettingsUI Instance { get; private set; }

        private GameObject _root;
        private KitchenElement _floorElement;

        private InputField _w, _h, _d, _x, _y, _z;

        private void Awake()
        {
            Instance = this;
        }

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("FloorSettings", canvas, Vector2.zero, new Vector2(280, 260));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(-10, -350);
            _root = panel.gameObject;

            UIFactory.CreateLabel("FlrTitle", panel.transform, "Размеры помещения", 20,
                new Vector2(0, 108), new Vector2(260, 28), TextAnchor.MiddleCenter);

            var closeBtn = UIFactory.CreateButton("FlrClose", panel.transform, "✕",
                new Vector2(124, 110), new Vector2(24, 24), Close);
            UIFactory.AnchorTopRight(closeBtn.GetComponent<RectTransform>());
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-124, -110);
            closeBtn.transform.SetAsLastSibling();

            float y = 76f;
            const float step = 31f;
            _w = Row(panel.transform, "Ширина, мм", ref y, step);
            _h = Row(panel.transform, "Высота, мм", ref y, step);
            _d = Row(panel.transform, "Глубина, мм", ref y, step);
            _x = Row(panel.transform, "X, м", ref y, step);
            _y = Row(panel.transform, "Y, м", ref y, step);
            _z = Row(panel.transform, "Z, м", ref y, step);

            foreach (var f in new[] { _w, _h, _d }) f.contentType = InputField.ContentType.IntegerNumber;
            foreach (var f in new[] { _x, _y, _z }) f.contentType = InputField.ContentType.DecimalNumber;

            UIFactory.CreateButton("FlrApply", panel.transform, "Применить",
                new Vector2(0, -98), new Vector2(248, 32), Apply);

            _root.SetActive(false);
        }

        private InputField Row(Transform parent, string label, ref float y, float step)
        {
            UIFactory.CreateLabel("L_" + label, parent, label, 15, new Vector2(-72, y), new Vector2(130, 24));
            var field = UIFactory.CreateInputField("F_" + label, parent, "", new Vector2(82, y), new Vector2(100, 24));
            y -= step;
            return field;
        }

        private KitchenElement ResolveFloor()
        {
            var go = GameObject.FindWithTag("Floor");
            if (go == null) return null;
            var bp = go.GetComponent<BasePlate>();
            return bp != null ? bp.Element : go.GetComponent<KitchenElement>();
        }

        public void Open()
        {
            _floorElement = ResolveFloor();
            if (_floorElement == null) return;

            var dims = _floorElement.DimensionsMM;
            _w.text = dims.x.ToString();
            _h.text = dims.y.ToString();
            _d.text = dims.z.ToString();

            var pos = _floorElement.transform.position;
            _x.SetTextWithoutNotify(pos.x.ToString("F3"));
            _y.SetTextWithoutNotify(pos.y.ToString("F3"));
            _z.SetTextWithoutNotify(pos.z.ToString("F3"));

            _root.SetActive(true);
        }

        public void Close()
        {
            _floorElement = null;
            if (_root != null) _root.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && _root != null && _root.activeSelf)
                Close();

            if (_root != null && _root.activeSelf && _floorElement != null)
            {
                var pos = _floorElement.transform.position;
                if (!_x.isFocused) _x.SetTextWithoutNotify(pos.x.ToString("F3"));
                if (!_y.isFocused) _y.SetTextWithoutNotify(pos.y.ToString("F3"));
                if (!_z.isFocused) _z.SetTextWithoutNotify(pos.z.ToString("F3"));
            }
        }

        private void Apply()
        {
            if (_floorElement == null) return;

            var oldDims = _floorElement.DimensionsMM;
            var oldPos = _floorElement.transform.position;
            var oldRot = _floorElement.transform.rotation;

            _floorElement.DimensionsMM = new Vector3Int(
                ParseInt(_w.text, oldDims.x),
                ParseInt(_h.text, oldDims.y),
                ParseInt(_d.text, oldDims.z));

            _floorElement.transform.position = new Vector3(
                ParseFloat(_x.text, oldPos.x),
                ParseFloat(_y.text, oldPos.y),
                ParseFloat(_z.text, oldPos.z));

            CommandStack.Execute(new ResizeCommand(_floorElement,
                oldDims, _floorElement.DimensionsMM,
                oldPos, _floorElement.transform.position,
                oldRot, _floorElement.transform.rotation));

            _w.text = _floorElement.DimensionsMM.x.ToString();
            _h.text = _floorElement.DimensionsMM.y.ToString();
            _d.text = _floorElement.DimensionsMM.z.ToString();

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private static int ParseInt(string s, int fallback) =>
            int.TryParse(s, out int v) ? v : fallback;

        private static float ParseFloat(string s, float fallback) =>
            float.TryParse(s, out float v) ? v : fallback;
    }
}
