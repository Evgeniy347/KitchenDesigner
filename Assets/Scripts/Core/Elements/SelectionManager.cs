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

            // Esc в режиме редактирования модуля — выход из режима.
            if (ModuleEditMode.IsActive && Input.GetKeyDown(KeyCode.Escape))
            {
                ModuleEditMode.Exit();
                DeselectAll();
                return;
            }

            // Клик по ручке ресайза не должен менять/снимать выделение.
            if (ResizeHandleManager.IsResizing || ResizeHandleManager.PointerOverHandle())
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
                        var group = GroupManager.GroupOf(element);

                        // Режим редактирования модуля: детали активного модуля
                        // выделяются ПОШТУЧНО, всё вне модуля заблокировано.
                        if (ModuleEditMode.IsActive)
                        {
                            if (!ModuleEditMode.IsEditable(element)) return;
                            if (ctrl) ToggleInSelection(element);
                            else Select(element);
                            return;
                        }

                        if (ctrl)
                            ToggleInSelection(element);
                        else if (group != null)
                        {
                            // Двойной клик по модулю — вход в режим редактирования.
                            if (IsDoubleClickOnGroup(group))
                            {
                                ModuleEditMode.Enter(group);
                                Select(element);
                            }
                            else
                                SelectOnly(GroupManager.MembersOf(group)); // группа целиком
                        }
                        else if (_selectedElements.Count > 1 && _selectedElements.Contains(element))
                            _selected = element; // часть мультивыделения — сохраняем для группового drag
                        else
                            Select(element);

                        RememberClick(group);
                        return;
                    }
                }

                if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                    DeselectAll();
            }
        }

        // --- Двойной клик по группе (вход в режим редактирования модуля) ---

        private const float DoubleClickSeconds = 0.35f;
        private float _lastClickTime = -10f;
        private int _lastClickGroupId;

        private bool IsDoubleClickOnGroup(LinkGroup group)
        {
            return group != null && group.id == _lastClickGroupId &&
                   Time.unscaledTime - _lastClickTime <= DoubleClickSeconds;
        }

        private void RememberClick(LinkGroup group)
        {
            _lastClickTime = Time.unscaledTime;
            _lastClickGroupId = group != null ? group.id : 0;
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

        /// <summary>Выделить ровно указанный набор элементов (связанная группа).</summary>
        public void SelectOnly(IList<KitchenElement> elements)
        {
            DeselectAll();
            if (elements == null) return;
            foreach (var e in elements)
                if (e != null) AddToSelection(e);
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
                        color = renderer.material.GetColor("_BaseColor")
                    };
                }

                // Прозрачность определяем по ФЛАГУ элемента, а не по альфе
                // текущего материала (иначе подсветка «залипает» в непрозрачную
                // ветку и красит сквозной элемент сплошным цветом).
                if (element.Transparent)
                {
                    // Грань остаётся сквозной (blend-состояния задаёт helper);
                    // выделение показываем жёлтым контуром.
                    renderer.material = ElementHighlighter.MakeTransparent(
                        renderer.material.shader, new Color(1f, 0.9f, 0.4f, 0.12f));
                    ElementOutline.Ensure(element).Show(selected: true);
                    return;
                }

                var mat = new Material(renderer.material);
                mat.EnableKeyword("_EMISSION");
                float intensity = isMulti ? 0.3f : 0.5f;
                mat.SetColor("_EmissionColor", new Color(0.8f, 0.7f, 0.1f) * intensity);
                mat.SetColor("_BaseColor", isMulti
                    ? new Color(1f, 0.97f, 0.7f, 1f)
                    : new Color(1f, 0.95f, 0.6f, 1f));
                renderer.material = mat;
            }
        }

        public void RefreshHighlight(KitchenElement element)
        {
            if (element == null || !_selectedElements.Contains(element)) return;
            _savedMaterials.Remove(element);
            HighlightSelected(element, _selectedElements.Count > 1);
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
