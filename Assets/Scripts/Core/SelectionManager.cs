using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        private KitchenElement _selected;
        private Material _originalMaterial;
        private Color _originalColor;

        public KitchenElement Selected => _selected;
        public event System.Action<KitchenElement> OnSelectionChanged;

        private void Awake()
        {
            Instance = this;
            Debug.Log("[Selection] Awake: Instance set");
        }

        private void Update()
        {
            if (ElementMover.IsDragging)
                return;

            // Alt+ЛКМ — орбита камеры, не выбор.
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                return;

            // Клик по UI не должен снимать/менять выбор.
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (Input.GetMouseButtonDown(0))
            {
                Debug.Log("[Selection] LMB Down at screen=" + Input.mousePosition);
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Debug.Log("[Selection] Ray origin=" + ray.origin + " dir=" + ray.direction);

                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Debug.Log("[Selection] Raycast hit: " + hit.collider.gameObject.name + " tag=" + hit.collider.tag + " point=" + hit.point);
                    var element = hit.collider.GetComponentInParent<KitchenElement>();
                    if (element != null)
                    {
                        Debug.Log("[Selection] Found KitchenElement: " + element.name + " BoardName=" + element.BoardName);
                        Select(element);
                        return;
                    }
                    else
                    {
                        Debug.Log("[Selection] Hit object has no KitchenElement component");
                    }
                }
                else
                {
                    Debug.Log("[Selection] Raycast missed everything");
                }
                Deselect();
            }
        }

        public void Select(KitchenElement element)
        {
            if (_selected == element)
            {
                Debug.Log("[Selection] Already selected, skip");
                return;
            }
            Debug.Log("[Selection] Выбрана " + element.Describe());
            Deselect();

            _selected = element;
            HighlightSelected();
            OnSelectionChanged?.Invoke(_selected);
        }

        public void Deselect()
        {
            if (_selected != null)
            {
                Debug.Log("[Selection] Deselect: " + _selected.name);
                UnhighlightSelected();
            }
            else
            {
                Debug.Log("[Selection] Deselect: nothing selected");
            }

            _selected = null;
            OnSelectionChanged?.Invoke(null);
        }

        private void HighlightSelected()
        {
            Debug.Log("[Selection] HighlightSelected");
            var renderer = _selected.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.material != null)
            {
                _originalMaterial = renderer.material;
                _originalColor = _originalMaterial.color;
                var mat = new Material(_originalMaterial);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.8f, 0.7f, 0.1f) * 0.5f);
                mat.color = new Color(1f, 0.95f, 0.6f);
                renderer.material = mat;
                Debug.Log("[Selection] Material swapped to yellow emission");
            }
            else
            {
                Debug.Log("[Selection] No MeshRenderer found on " + _selected.name);
            }
        }

        private void UnhighlightSelected()
        {
            Debug.Log("[Selection] UnhighlightSelected");
            var renderer = _selected.GetComponent<MeshRenderer>();
            if (renderer != null && _originalMaterial != null)
            {
                renderer.material = _originalMaterial;
                Debug.Log("[Selection] Material restored");
            }
            else
            {
                Debug.Log("[Selection] No renderer or original material to restore");
            }

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.ApplyForElement(_selected);
        }
    }
}
