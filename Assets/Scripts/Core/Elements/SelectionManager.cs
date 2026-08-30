using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager? Instance { get; private set; }

        private KitchenElement? _selected;
        private readonly List<KitchenElement> _selectedElements = new List<KitchenElement>();
        private readonly Dictionary<KitchenElement, SavedMaterial> _savedMaterials
            = new Dictionary<KitchenElement, SavedMaterial>();

        public KitchenElement? Selected => _selected;
        public IReadOnlyList<KitchenElement> SelectedElements => _selectedElements;
        public event System.Action<KitchenElement?>? OnSelectionChanged;

        /// <summary>Только для тестов: есть ли сохранённый материал у элемента.</summary>
        public bool HasSavedMaterialFor(KitchenElement element) => _savedMaterials.ContainsKey(element);

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
            // Идёт размещение нового объекта — мышь принадлежит PlacementController.
            if (PlacementController.IsActive)
                return;

            // В режиме инструмента ЛКМ принадлежит ему (рулетка ставит точки
            // замера, пипетка красит), а не выделению.
            if (Tools.ToolMode.MouseCaptured)
                return;

            // Началось перетаскивание — клик отменён, выделение не сжимаем.
            if (ElementMover.IsDragging)
            {
                _collapseCandidate = null;
                return;
            }

            // Отпускание ЛКМ обрабатываем ДО прочих ранних выходов: клик мог
            // начаться на детали, а курсор к моменту отпускания уйти на UI.
            if (Input.GetMouseButtonUp(0))
                HandleClickRelease();

            // Esc в режиме редактирования модуля — выход из режима.
            if (ModuleEditMode.IsActive && Input.GetKeyDown(KeyCode.Escape))
            {
                ModuleEditMode.Exit();
                DeselectAll();
                return;
            }

            // Клик по ручке ресайза не должен менять/снимать выделение. Ручки
            // области накладки живут отдельной системой, но правило то же.
            if (ResizeHandleManager.IsResizing || ResizeHandleManager.PointerOverHandle()
                || TextureOverlayHandles.PointerOverHandle())
                return;

            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                return;

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (Input.GetMouseButtonDown(0))
            {
                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                var clicked = ResolveClickTarget(Input.mousePosition, shiftHeld);

                if (clicked != null)
                    HandleClickOnElement(clicked, ctrlHeld);
                else if (!ctrlHeld)
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

        private void RememberClick(LinkGroup? group)
        {
            _lastClickTime = Time.unscaledTime;
            _lastClickGroupId = group != null ? group.id : 0;
        }

        /// <summary>Обработать клик по элементу (ключая BasePlate и null — клик в пустоту).
        /// Вынесено из Update() для тестирования.</summary>
        public void HandleClickOnElement(KitchenElement? element, bool ctrlHeld)
        {
            _collapseCandidate = null;

            // Пол (BasePlate) не выделяется — клик по нему снимает выделение.
            if (element != null && element.GetComponent<BasePlate>() == null)
            {
                // Режим редактора запрещает выделять часть объектов мышью
                // (напр. стены/пол в «обычном»). Программный Select — без ограничений.
                if (!EditModeManager.IsInteractable(element))
                {
                    if (!ctrlHeld)
                        DeselectAll();
                    return;
                }

                var group = GroupManager.GroupOf(element);

                // Режим редактирования модуля: детали активного модуля
                // выделяются ПОШТУЧНО, всё вне модуля заблокировано.
                if (ModuleEditMode.IsActive)
                {
                    if (!ModuleEditMode.IsEditable(element)) return;
                    if (ctrlHeld) ToggleInSelection(element);
                    else Select(element);
                    return;
                }

                if (ctrlHeld)
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
                {
                    // Часть мультивыделения: на нажатии выборку не трогаем — иначе
                    // пропадёт групповой drag. Если drag так и не начнётся,
                    // HandleClickRelease оставит выделенной одну эту деталь.
                    _selected = element;
                    _collapseCandidate = element;
                }
                else
                    Select(element);

                RememberClick(group);
                return;
            }

            if (!ctrlHeld)
                DeselectAll();
        }

        // Деталь, по которой кликнули внутри мультивыделения. Ждёт отпускания ЛКМ.
        private KitchenElement? _collapseCandidate;

        /// <summary>Отпускание ЛКМ без перетаскивания. Клик без Ctrl по детали из
        /// мультивыделения оставляет выделенной только её. Публично для тестов.</summary>
        public void HandleClickRelease()
        {
            var candidate = _collapseCandidate;
            _collapseCandidate = null;
            if (candidate == null || _selectedElements.Count <= 1) return;
            SelectOnly(new List<KitchenElement> { candidate });
        }

        public static KitchenElement? PickFromOrderedHits(
            IReadOnlyList<KitchenElement> hits, bool shiftHeld)
        {
            foreach (var el in hits)
            {
                if (el == null)
                    continue;
                if (!shiftHeld || !el.Transparent)
                    return el;
            }
            return null;
        }

        private KitchenElement? ResolveClickTarget(Vector3 screenPoint, bool shiftHeld)
        {
            Ray ray = Camera.main.ScreenPointToRay(screenPoint);
            return RaycastTransparentAware(ray, shiftHeld);
        }

        public static KitchenElement? RaycastTransparentAware(Ray ray, bool shiftHeld)
            => RaycastTransparentAware(ray, shiftHeld, out _);

        /// <summary>Тот же поиск, но отдаёт и само попадание: пипетке нужна точка
        /// на поверхности, чтобы понять, в какую накладку текстуры пришёл клик.</summary>
        public static KitchenElement? RaycastTransparentAware(Ray ray, bool shiftHeld, out RaycastHit hit)
        {
            hit = default;
            if (!shiftHeld)
            {
                if (Physics.Raycast(ray, out hit))
                    return hit.collider.GetComponentInParent<KitchenElement>();
                return null;
            }

            var allHits = Physics.RaycastAll(ray);
            System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in allHits)
            {
                var el = h.collider.GetComponentInParent<KitchenElement>();
                if (el == null || el.Transparent) continue;
                hit = h;
                return el;
            }
            return null;
        }

        public static bool IsGizmoCollider(Collider c) =>
            c != null && (c.GetComponentInParent<ResizeHandle>() != null
                || c.GetComponentInParent<TextureOverlayHandle>() != null);

        public static KitchenElement? PickElementFromOrderedColliders(
            IReadOnlyList<Collider> orderedColliders, bool shiftHeld)
        {
            foreach (var col in orderedColliders)
            {
                if (col == null || IsGizmoCollider(col)) continue;
                var el = col.GetComponentInParent<KitchenElement>();
                if (!shiftHeld) return el;
                if (el == null || el.Transparent) continue;
                return el;
            }
            return null;
        }

        public static KitchenElement? RaycastElementThroughGizmos(Ray ray, bool shiftHeld)
        {
            var allHits = Physics.RaycastAll(ray);
            System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));
            var cols = new Collider[allHits.Length];
            for (int i = 0; i < allHits.Length; i++) cols[i] = allHits[i].collider;
            return PickElementFromOrderedColliders(cols, shiftHeld);
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
            if (_selectedElements.Count == 0) return;

            var toRestore = new List<KitchenElement>(_selectedElements);
            _selectedElements.Clear();
            _selected = null;

            foreach (var e in toRestore)
            {
                if (e != null)
                    RestoreMaterial(e);
            }

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

            _selectedElements.Remove(_selected);
            RestoreMaterial(_selected);
            _selected = _selectedElements.Count > 0
                ? _selectedElements[_selectedElements.Count - 1]
                : null;
            OnSelectionChanged?.Invoke(_selected);
        }

        public bool IsSelected(KitchenElement element)
        {
            return _selectedElements.Contains(element);
        }

        // ── Временное снятие подсветки (предпросмотр декора) ───────────
        // Жёлтая заливка выделения перекрашивает деталь, и текстуру под ней
        // оценить нельзя. На время показа декора наведением подсветку снимаем,
        // но САМО выделение оставляем: пользователь ничего не выбирал заново,
        // и меню свойств обязано остаться открытым на том же элементе.

        private KitchenElement? _highlightSuppressed;

        public bool IsHighlightSuppressed(KitchenElement element)
            => element != null && _highlightSuppressed == element;

        /// <summary>Снять подсветку с выделенного элемента, не снимая выделения.
        /// Повторный вызов — не ошибка.</summary>
        public void SuppressHighlight(KitchenElement element)
        {
            if (element == null || _highlightSuppressed == element) return;
            if (!_selectedElements.Contains(element)) return;
            // Флаг ставим ДО восстановления: ApplyForElement по дороге зовёт
            // RefreshHighlight (у стены и пола), и без флага подсветка тут же
            // вернулась бы обратно.
            _highlightSuppressed = element;
            RestoreMaterial(element);
        }

        /// <summary>Вернуть подсветку, снятую <see cref="SuppressHighlight"/>.</summary>
        public void ResumeHighlight(KitchenElement element)
        {
            if (element == null || _highlightSuppressed != element) return;
            _highlightSuppressed = null;
            RefreshHighlight(element);
        }

        private void HighlightSelected(KitchenElement element, bool isMulti)
        {
            if (_highlightSuppressed == element) return;

            var renderer = element.GetComponent<MeshRenderer>();
            var srcMat = renderer != null ? renderer.sharedMaterial : null;
            if (renderer != null && srcMat != null)
            {
                if (!_savedMaterials.ContainsKey(element))
                {
                    _savedMaterials[element] = new SavedMaterial
                    {
                        material = srcMat,
                        color = srcMat.GetColor("_BaseColor")
                    };
                }

                if (element.Transparent)
                {
                    renderer.material = ElementHighlighter.MakeTransparent(
                        srcMat.shader, new Color(1f, 0.9f, 0.4f, 0.12f));
                    ElementOutline.Ensure(element)!.Show(selected: true);
                    return;
                }

                var mat = new Material(srcMat);
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
                if (!MaterialManager.HasCustomDecor(element))
                    renderer.material = saved.material;
            }
            _savedMaterials.Remove(element);

            // Элемент вышел из выделения — снятая подсветка больше не «снята
            // на время», возвращать нечего. Сам предпросмотр этой веткой не
            // задет: там элемент остаётся выделенным.
            if (_highlightSuppressed == element && !_selectedElements.Contains(element))
                _highlightSuppressed = null;

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.ApplyForElement(element);
        }
    }
}
