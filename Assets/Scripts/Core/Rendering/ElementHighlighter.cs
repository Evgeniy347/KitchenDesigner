using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ElementHighlighter : MonoBehaviour
    {
        public static ElementHighlighter? Instance { get; internal set; }

        public int RefreshCount { get; set; }

        public static bool ViolationTintVisible { get; set; } = true;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            RefreshHighlights();
            ModuleEditMode.Changed += RefreshHighlights;
        }

        private void OnDestroy()
        {
            ModuleEditMode.Changed -= RefreshHighlights;
        }

        public static Material MakeTransparent(Shader shader, Color color) =>
            TransparentMaterial.Make(shader, color);

        public void RefreshHighlights()
        {
            if (HighlightBatch.Suspended)
            {
                HighlightBatch.Defer();
                return;
            }

            RefreshCount++;

            var list = PartRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);

            EdgeSubstrate.SyncScene(list);

            foreach (var element in list)
            {
                if (element == null) continue;
                if (!PhotoMode.Active && SelectionManager.Instance != null
                    && SelectionManager.Instance.Selected == element)
                    continue;

                bool isValid = !result.violations.Contains(element);
                ApplyMaterial(element, isValid);
            }
        }

        public void ApplyForElement(KitchenElement element)
        {
            if (element == null) return;

            var list = PartRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);

            bool isValid = !result.violations.Contains(element);
            ApplyMaterial(element, isValid);
        }

        private static ValidityPaint PaintFor(KitchenElement element, bool isValid)
        {
            if (ModuleEditMode.IsActive && !ModuleEditMode.IsEditable(element))
                return ValidityPaint.Dimmed;

            bool violating = !isValid && ViolationTintVisible;

            if (PhotoMode.ResolveTransparent(element.Transparent))
                return violating ? ValidityPaint.SeeThroughViolation : ValidityPaint.SeeThrough;

            return violating ? ValidityPaint.Violation : ValidityPaint.Own;
        }

        internal static void ApplyMaterial(KitchenElement element, bool isValid)
        {
            var body = ElementRenderers.BodyOf(element);
            if (body.Count == 0) return;

            var paint = PaintFor(element, isValid);
            PaintBody(element, body, paint,
                keepAux: paint == ValidityPaint.Own || paint == ValidityPaint.Violation);

            if (paint == ValidityPaint.SeeThrough || paint == ValidityPaint.SeeThroughViolation)
                ElementOutline.Ensure(element)?.Show(selected: false);
            else
                ElementOutline.For(element)?.Hide();
        }

        private static void PaintBody(KitchenElement element, List<MeshRenderer> body,
            ValidityPaint paint, bool keepAux)
        {
            foreach (var renderer in body)
            {
                if (renderer == null) continue;
                var material = ValidityTint.Of(paint, renderer.sharedMaterial);
                if (material == null || ReferenceEquals(material, renderer.sharedMaterial)) continue;
                PaintFlat(element, renderer, material, keepAux);
            }
        }

        internal static void PaintFlat(KitchenElement element, MeshRenderer renderer,
            Material material, bool keepAux = false)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            int count = mesh != null ? mesh.subMeshCount : 1;
            if (count <= 1)
            {
                renderer.sharedMaterial = material;
                return;
            }

            var slots = new Material[count];
            for (int i = 0; i < count; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
            if (keepAux) element.RefreshSubmeshMaterials();
        }
    }
}
