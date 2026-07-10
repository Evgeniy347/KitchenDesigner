using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        private KitchenElement _selected;
        private readonly List<KitchenElement> _selectedElements = new List<KitchenElement>();
        private readonly Dictionary<KitchenElement, SavedMaterial> _savedMaterials
            = new Dictionary<KitchenElement, SavedMaterial>();

        public KitchenElement Selected => _selected;
        public IReadOnlyList<KitchenElement> SelectedElements => _selectedElements;
        public event System.Action<KitchenElement> OnSelectionChanged;

        private struct SavedMaterial
        {
            public Material material;
            public Color color;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (ElementMover.IsDragging)
                return;

            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                return;

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    var element = hit.collider.GetComponentInParent<KitchenElement>();
                    // Пол (BasePlate) не выделяется — клик по нему снимает выделение.
                    if (element != null && element.GetComponent<BasePlate>() == null)
                    {
                        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                        if (ctrl)
                            ToggleInSelection(element);
                        else
                            Select(element);
                        return;
                    }
                }

                if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                    DeselectAll();
            }
        }

        public void Select(KitchenElement element)
        {
            if (_selected == element)
                return;

            DeselectAll();

            _selected = element;
            _selectedElements.Add(element);
            HighlightSelected(element, true);
            OnSelectionChanged?.Invoke(_selected);
        }

        public void ToggleInSelection(KitchenElement element)
        {
            if (_selectedElements.Contains(element))
            {
                _selectedElements.Remove(element);
                RestoreMaterial(element);

                if (_selected == element)
                {
                    _selected = _selectedElements.Count > 0 ? _selectedElements[_selectedElements.Count - 1] : null;
                }
            }
            else
            {
                _selectedElements.Add(element);
                HighlightSelected(element, true);
                _selected = element;
            }

            OnSelectionChanged?.Invoke(_selected);
        }

        public void AddToSelection(KitchenElement element)
        {
            if (_selectedElements.Contains(element)) return;

            if (_selectedElements.Count == 0)
                _selected = element;

            _selectedElements.Add(element);
            HighlightSelected(element, true);
            OnSelectionChanged?.Invoke(_selected);
        }

        public void DeselectAll()
        {
            foreach (var e in _selectedElements)
            {
                if (e != null)
                    RestoreMaterial(e);
            }

            _selectedElements.Clear();
            _selected = null;
            OnSelectionChanged?.Invoke(null);
        }

        public void Deselect()
        {
            if (_selected == null) return;
            if (_selectedElements.Count <= 1)
            {
                DeselectAll();
                return;
            }

            RestoreMaterial(_selected);
            _selectedElements.Remove(_selected);
            _selected = _selectedElements.Count > 0
                ? _selectedElements[_selectedElements.Count - 1]
                : null;
            OnSelectionChanged?.Invoke(_selected);
        }

        public bool IsSelected(KitchenElement element)
        {
            return _selectedElements.Contains(element);
        }

        private void HighlightSelected(KitchenElement element, bool isMulti)
        {
            var renderer = element.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.material != null)
            {
                if (!_savedMaterials.ContainsKey(element))
                {
                    _savedMaterials[element] = new SavedMaterial
                    {
                        material = renderer.material,
                        color = renderer.material.color
                    };
                }

                var mat = new Material(renderer.material);
                mat.EnableKeyword("_EMISSION");
                float intensity = isMulti ? 0.3f : 0.5f;
                mat.SetColor("_EmissionColor", new Color(0.8f, 0.7f, 0.1f) * intensity);
                mat.color = isMulti ? new Color(1f, 0.97f, 0.7f) : new Color(1f, 0.95f, 0.6f);
                renderer.material = mat;
            }
        }

        private void RestoreMaterial(KitchenElement element)
        {
            var renderer = element.GetComponent<MeshRenderer>();
            if (renderer != null && _savedMaterials.TryGetValue(element, out var saved))
            {
                renderer.material = saved.material;
            }
            _savedMaterials.Remove(element);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.ApplyForElement(element);
        }
    }
}
