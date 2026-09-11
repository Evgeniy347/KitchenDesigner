using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ElementHighlighter : MonoBehaviour
    {
        public static ElementHighlighter? Instance { get; internal set; }

        public int RefreshCount { get; set; }

        private static readonly HashSet<KitchenElement> _violating = new HashSet<KitchenElement>();

        [System.ThreadStatic] private static int _bodiesVisited;

        [System.ThreadStatic] private static int _bodiesRepainted;

        public static int TakeBodiesVisited()
        {
            int n = _bodiesVisited;
            _bodiesVisited = 0;
            return n;
        }

        public static int TakeBodiesRepainted()
        {
            int n = _bodiesRepainted;
            _bodiesRepainted = 0;
            return n;
        }

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

            using var _ = PerfMarkers.HighlighterRefresh.Auto();

            RefreshCount++;

            var list = PartRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);

            EdgeSubstrate.SyncScene(list);

            _violating.Clear();
            foreach (var v in result.violations)
                if (v != null) _violating.Add(v);

            foreach (var element in list)
            {
                if (element == null) continue;
                if (!PhotoMode.Active && SelectionManager.Instance != null
                    && SelectionManager.Instance.Selected == element)
                    continue;

                ApplyMaterial(element, !_violating.Contains(element));
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

            _bodiesVisited++;
            var paint = PaintFor(element, isValid);
            if (PaintBody(element, body, paint,
                    keepAux: paint == ValidityPaint.Own || paint == ValidityPaint.Violation) > 0)
                _bodiesRepainted++;

            if (paint == ValidityPaint.SeeThrough || paint == ValidityPaint.SeeThroughViolation)
                ElementOutline.Ensure(element)?.Show(selected: false);
            else
                ElementOutline.For(element)?.Hide();
        }

        private static int PaintBody(KitchenElement element, List<MeshRenderer> body,
            ValidityPaint paint, bool keepAux)
        {
            int repainted = 0;
            foreach (var renderer in body)
            {
                if (renderer == null) continue;
                var material = ValidityTint.Of(paint, renderer.sharedMaterial);
                if (material == null || ReferenceEquals(material, renderer.sharedMaterial)) continue;
                PaintFlat(element, renderer, material, keepAux);
                repainted++;
            }
            return repainted;
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
