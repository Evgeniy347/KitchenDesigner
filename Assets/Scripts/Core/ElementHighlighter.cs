using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ElementHighlighter : MonoBehaviour
    {
        public static ElementHighlighter Instance { get; private set; }

        private Material _validMaterial;
        private Material _invalidMaterial;
        private Material _validTransparentMaterial;
        private Material _invalidTransparentMaterial;
        private Material _dimmedMaterial;
        private bool _materialsInitialized;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            CreateMaterials();
            RefreshHighlights();
            // Вход/выход из режима редактирования модуля меняет затемнение сцены.
            ModuleEditMode.Changed += RefreshHighlights;
        }

        private void OnDestroy()
        {
            ModuleEditMode.Changed -= RefreshHighlights;
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

            // Затемнение элементов вне редактируемого модуля.
            _dimmedMaterial = new Material(shader);
            _dimmedMaterial.color = new Color(0.35f, 0.35f, 0.38f);

            _validTransparentMaterial = MakeTransparentCopy(_validMaterial);
            _invalidTransparentMaterial = MakeTransparentCopy(_invalidMaterial);

            _materialsInitialized = true;
        }

        private static Material MakeTransparentCopy(Material source)
        {
            var mat = new Material(source);
            mat.SetFloat("_Surface", 1);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            var c = mat.color;
            mat.color = new Color(c.r, c.g, c.b, 0.2f);
            return mat;
        }

        public void RefreshHighlights()
        {
            if (!_materialsInitialized) return;

            var list = BoardRegistry.GetAll();
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

            var list = BoardRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);

            bool isValid = !result.violations.Contains(element);
            ApplyMaterial(element, isValid);
        }

        private void ApplyMaterial(KitchenElement element, bool isValid)
        {
            var renderer = element.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            if (element.GetComponent<BasePlate>() != null || element.GetComponent<Wall>() != null) return;

            // В режиме редактирования модуля всё вне модуля затемнено —
            // визуальный сигнал «заблокировано».
            if (ModuleEditMode.IsActive && !ModuleEditMode.IsEditable(element))
            {
                renderer.material = _dimmedMaterial;
                return;
            }

            renderer.material = element.Transparent
                ? (isValid ? _validTransparentMaterial : _invalidTransparentMaterial)
                : (isValid ? _validMaterial : _invalidMaterial);
        }
    }
}
