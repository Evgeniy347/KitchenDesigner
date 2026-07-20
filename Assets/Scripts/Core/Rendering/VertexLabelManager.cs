using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// При включении (кнопка "Вершины" в тулбаре) рисует буквенные метки A-H
    /// в восьми вершинах выделенного элемента. Текст биллбордится к камере.
    /// </summary>
    public class VertexLabelManager : MonoBehaviour
    {
        public static bool Enabled { get; set; }
        public static VertexLabelManager? Instance { get; private set; }

        private static readonly string[] VertexNames = { "A", "B", "C", "D", "E", "F", "G", "H" };

        private readonly Vector3[] _corners = new Vector3[8];
        private readonly TextMeshPro?[] _labels = new TextMeshPro?[8];
        private GameObject? _root;
        private KitchenElement? _current;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            var sel = SelectionManager.Instance;
            if (sel != null)
                sel.OnSelectionChanged += OnSelectionChanged;
        }

        private void OnDestroy()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
            DestroyLabels();
        }

        private void OnSelectionChanged(KitchenElement? element)
        {
            DestroyLabels();
            _current = element;
            if (Enabled && element != null) BuildLabels(element);
        }

        public static void Toggle()
        {
            Enabled = !Enabled;
            if (Instance != null)
            {
                if (!Enabled) Instance.DestroyLabels();
                else if (SelectionManager.Instance?.Selected != null)
                    Instance.BuildLabels(SelectionManager.Instance.Selected);
            }
        }

        public void Refresh()
        {
            DestroyLabels();
            if (_current != null && Enabled)
                BuildLabels(_current);
        }

        private void BuildLabels(KitchenElement element)
        {
            if (_root != null) return;

            var font = LoadFont();
            if (font == null) return;

            _root = new GameObject("__VertexLabels");

            BoxWireframe.WorldCorners(element.transform.localToWorldMatrix, _corners);
            Vector3 center = element.transform.position;

            for (int i = 0; i < 8; i++)
            {
                var go = new GameObject("V_" + VertexNames[i]);
                go.transform.SetParent(_root.transform, true);

                var dir = (_corners[i] - center).normalized;
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;
                go.transform.position = _corners[i] + dir * 0.025f;

                var tmp = go.AddComponent<TextMeshPro>();
                tmp.font = font;
                tmp.text = VertexNames[i];
                tmp.fontSize = 3f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(1f, 0.95f, 0.3f, 1f); // ярко-жёлтый

                var mr = go.GetComponent<MeshRenderer>();
                mr.sortingOrder = 100;

                _labels[i] = tmp;
            }
        }

        private void DestroyLabels()
        {
            if (_root != null) Destroy(_root);
            _root = null;
            for (int i = 0; i < 8; i++) _labels[i] = null;
        }

        private void LateUpdate()
        {
            if (_root == null || _current == null) return;
            if (!_current.gameObject.activeInHierarchy)
            {
                DestroyLabels();
                return;
            }

            BoxWireframe.WorldCorners(_current.transform.localToWorldMatrix, _corners);
            Vector3 center = _current.transform.position;
            var cam = Camera.main;

            for (int i = 0; i < 8; i++)
            {
                var label = _labels[i];
                if (label == null) continue;

                var dir = (_corners[i] - center).normalized;
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;
                label.transform.position = _corners[i] + dir * 0.025f;

                if (cam != null)
                    label.transform.rotation = cam.transform.rotation;
            }
        }

        private static TMP_FontAsset? LoadFont()
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts/LiberationSans SDF");
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
            }
            return font;
        }
    }
}
