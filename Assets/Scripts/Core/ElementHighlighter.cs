using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ElementHighlighter : MonoBehaviour
    {
        public static ElementHighlighter Instance { get; private set; }

        private Material _validMaterial;
        private Material _invalidMaterial;
        private bool _materialsInitialized;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            CreateMaterials();
            RefreshHighlights();
        }

        private void CreateMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            _validMaterial = new Material(shader);
            _validMaterial.EnableKeyword("_EMISSION");
            _validMaterial.SetColor("_EmissionColor", new Color(0f, 1f, 0f) * 0.4f);
            _validMaterial.color = new Color(0.85f, 1f, 0.85f);

            _invalidMaterial = new Material(shader);
            _invalidMaterial.EnableKeyword("_EMISSION");
            _invalidMaterial.SetColor("_EmissionColor", new Color(1f, 0f, 0f) * 0.4f);
            _invalidMaterial.color = new Color(1f, 0.8f, 0.8f);

            _materialsInitialized = true;
        }

        public void RefreshHighlights()
        {
            if (!_materialsInitialized) return;

            var all = FindObjectsByType<KitchenElement>();
            var list = new List<KitchenElement>(all);
            var result = ConstraintValidator.Validate(list);

            foreach (var element in list)
            {
                if (element == null) continue;
                if (SelectionManager.Instance != null && SelectionManager.Instance.Selected == element)
                    continue;

                bool isValid = !result.violations.Contains(element);
                ApplyMaterial(element, isValid);
            }
        }

        public void ApplyForElement(KitchenElement element)
        {
            if (element == null || !_materialsInitialized) return;

            var all = FindObjectsByType<KitchenElement>();
            var list = new List<KitchenElement>(all);
            var result = ConstraintValidator.Validate(list);

            bool isValid = !result.violations.Contains(element);
            ApplyMaterial(element, isValid);
        }

        private void ApplyMaterial(KitchenElement element, bool isValid)
        {
            var renderer = element.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            bool isBasePlate = element.GetComponent<BasePlate>() != null;
            if (isBasePlate) return;

            renderer.material = isValid ? _validMaterial : _invalidMaterial;
        }
    }
}
