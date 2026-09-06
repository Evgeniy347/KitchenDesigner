using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager? Instance { get; internal set; }

        private KitchenElement? _selected;
        private readonly List<KitchenElement> _selectedElements = new List<KitchenElement>();
        private readonly Dictionary<KitchenElement, List<SavedMaterial>> _savedMaterials
            = new Dictionary<KitchenElement, List<SavedMaterial>>();

        public KitchenElement? Selected => _selected;
        public IReadOnlyList<KitchenElement> SelectedElements => _selectedElements;
        public event System.Action<KitchenElement?>? OnSelectionChanged;

        public bool HasSavedMaterialFor(KitchenElement element) => _savedMaterials.ContainsKey(element);

        private struct SavedMaterial
        {
            public MeshRenderer renderer;
            public Material material;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (PlacementController.IsActive)
                return;

            if (Tools.ToolMode.MouseCaptured)
                return;

            if (ElementMover.IsDragging)
            {
                _collapseCandidate = null;
                return;
            }

            if (Input.GetMouseButtonUp(0))
                HandleClickRelease();

            if (ModuleEditMode.IsActive && Input.GetKeyDown(KeyCode.Escape))
            {
                ModuleEditMode.Exit();
                DeselectAll();
                return;
            }

            if (GizmoPressGuard.BlocksPress(
                    ResizeHandleManager.IsResizing,
                    ResizeHandleManager.PointerOverHandle(),
                    TextureOverlayHandles.Active,
                    TextureOverlayHandles.PointerOverHandle()))
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

        private readonly DoubleClickTracker _clicks = new DoubleClickTracker();
        private List<KitchenElement> _selectionBeforeClick = new List<KitchenElement>();

        public void HandleClickOnElement(KitchenElement? element, bool ctrlHeld)
        {
            _collapseCandidate = null;

            var group = element != null ? GroupManager.GroupOf(element) : null;
            int groupId = group != null ? group.id : 0;
            int elementId = element != null ? element.GetInstanceID() : 0;
            float now = Time.unscaledTime;

            var input = new SceneClickInput
            {
                HasElement = element != null && element.GetComponent<BasePlate>() == null,
                Interactable = element != null && EditModeManager.IsInteractable(element),
                ModuleEditActive = ModuleEditMode.IsActive,
                ModuleEditable = element != null && ModuleEditMode.IsEditable(element),
                CtrlHeld = ctrlHeld,
                InGroup = group != null,
                RepeatsGroup = _clicks.RepeatsGroup(groupId, now),
                RepeatsElement = _clicks.RepeatsElement(elementId, now),
                InMultiSelection = element != null && _selectedElements.Count > 1
                    && _selectedElements.Contains(element),
                Kind = ElementActivator.KindOf(element),
            };

            var selectionBefore = new List<KitchenElement>(_selectedElements);
            var action = SceneClickPlan.Decide(input);
            ApplyClickAction(action, element, group);

            if (!SceneClickPlan.RecordsClick(action)) return;
            if (!SceneClickPlan.KeepsSelectionSnapshot(action)) _selectionBeforeClick = selectionBefore;
            _clicks.Remember(groupId, elementId, now);
        }

        private void ApplyClickAction(SceneClickAction action, KitchenElement? element, LinkGroup? group)
        {
            switch (action)
            {
                case SceneClickAction.DeselectAll:
                    DeselectAll();
                    break;
                case SceneClickAction.ModuleToggleInSelection:
                case SceneClickAction.ToggleInSelection:
                    ToggleInSelection(element!);
                    break;
                case SceneClickAction.ModuleSelect:
                case SceneClickAction.Select:
                    Select(element!);
                    break;
                case SceneClickAction.SelectGroup:
                    SelectOnly(GroupManager.MembersOf(group!));
                    break;
                case SceneClickAction.EnterModuleEdit:
                    ModuleEditMode.Enter(group!);
                    Select(element!);
                    break;
                case SceneClickAction.ActivateSwitch:
                    ElementActivator.Activate(element);
                    SelectOnly(_selectionBeforeClick);
                    break;
                case SceneClickAction.CollapseToClicked:
                    _selected = element;
                    _collapseCandidate = element;
                    break;
            }
        }

        private KitchenElement? _collapseCandidate;

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

        private KitchenElement? _highlightSuppressed;

        public bool IsHighlightSuppressed(KitchenElement element)
            => element != null && _highlightSuppressed == element;

        public void SuppressHighlight(KitchenElement element)
        {
            if (element == null || _highlightSuppressed == element) return;
            if (!_selectedElements.Contains(element)) return;
            _highlightSuppressed = element;
            RestoreMaterial(element);
        }

        public void ResumeHighlight(KitchenElement element)
        {
            if (element == null || _highlightSuppressed != element) return;
            _highlightSuppressed = null;
            RefreshHighlight(element);
        }

        private void HighlightSelected(KitchenElement element, bool isMulti)
        {
            if (_highlightSuppressed == element) return;

            if (!_savedMaterials.TryGetValue(element, out var saved))
            {
                saved = CaptureBody(element);
                if (saved.Count == 0) return;
                _savedMaterials[element] = saved;
            }

            if (element.Transparent)
            {
                foreach (var entry in saved)
                {
                    if (entry.renderer == null || entry.material == null) continue;
                    entry.renderer.material = ElementHighlighter.MakeTransparent(
                        entry.material.shader, new Color(1f, 0.9f, 0.4f, 0.12f));
                }
                ElementOutline.Ensure(element)!.Show(selected: true);
                return;
            }

            float intensity = isMulti ? 0.3f : 0.5f;
            foreach (var entry in saved)
            {
                if (entry.renderer == null || entry.material == null) continue;
                var mat = new Material(entry.material);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.8f, 0.7f, 0.1f) * intensity);
                mat.SetColor("_BaseColor", isMulti
                    ? new Color(1f, 0.97f, 0.7f, 1f)
                    : new Color(1f, 0.95f, 0.6f, 1f));
                entry.renderer.material = mat;
            }
        }

        private static List<SavedMaterial> CaptureBody(KitchenElement element)
        {
            var saved = new List<SavedMaterial>();
            foreach (var renderer in ElementRenderers.BodyOf(element))
            {
                if (renderer == null) continue;
                var srcMat = renderer.sharedMaterial;
                if (srcMat == null) continue;
                saved.Add(new SavedMaterial { renderer = renderer, material = srcMat });
            }
            return saved;
        }

        public void RefreshHighlight(KitchenElement element)
        {
            if (element == null || !_selectedElements.Contains(element)) return;
            _savedMaterials.Remove(element);
            HighlightSelected(element, _selectedElements.Count > 1);
        }

        private void RestoreMaterial(KitchenElement element)
        {
            if (_savedMaterials.TryGetValue(element, out var saved)
                && !MaterialManager.HasCustomDecor(element))
            {
                foreach (var entry in saved)
                {
                    if (entry.renderer == null || entry.material == null) continue;
                    entry.renderer.material = entry.material;
                }
            }
            _savedMaterials.Remove(element);

            if (_highlightSuppressed == element && !_selectedElements.Contains(element))
                _highlightSuppressed = null;

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.ApplyForElement(element);
        }
    }
}
