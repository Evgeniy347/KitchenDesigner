using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ElementHighlighter : MonoBehaviour
    {
        public static ElementHighlighter? Instance { get; private set; }

        public int RefreshCount { get; set; }

        public static bool TintEnabled { get; set; } = true;

        private const float TintEmissionStrength = 0.4f;
        private const float SurfaceTypeTransparent = 1f;
        private const float BlendModeAlpha = 0f;
        private const int ZWriteOff = 0;

        private static readonly Color ValidTintColor = new Color(0.85f, 1f, 0.85f, 1f);
        private static readonly Color InvalidTintColor = new Color(1f, 0.8f, 0.8f, 1f);
        private static readonly Color OutsideEditedModuleColor = new Color(0.35f, 0.35f, 0.38f, 1f);
        private static readonly Color ValidSeeThroughColor = new Color(0.7f, 0.85f, 0.7f, 0.08f);
        private static readonly Color InvalidSeeThroughColor = new Color(0.9f, 0.55f, 0.55f, 0.12f);

        private Material? _validMaterial;
        private Material? _invalidMaterial;
        private Material? _validTransparentMaterial;
        private Material? _invalidTransparentMaterial;
        private Material? _dimmedMaterial;
        private bool _materialsInitialized;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            CreateMaterials();
            RefreshHighlights();
            ModuleEditMode.Changed += RefreshHighlights;
        }

        private void OnDestroy()
        {
            ModuleEditMode.Changed -= RefreshHighlights;
        }

        public void CreateMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            _validMaterial = MakeTinted(shader, ValidTintColor, Color.green);
            _invalidMaterial = MakeTinted(shader, InvalidTintColor, Color.red);

            _dimmedMaterial = new Material(shader);
            _dimmedMaterial.SetColor("_BaseColor", OutsideEditedModuleColor);

            _validTransparentMaterial = MakeTransparent(shader, ValidSeeThroughColor);
            _invalidTransparentMaterial = MakeTransparent(shader, InvalidSeeThroughColor);

            _materialsInitialized = true;
        }

        private static Material MakeTinted(Shader shader, Color baseColor, Color emission)
        {
            var m = new Material(shader);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission * TintEmissionStrength);
            m.SetColor("_BaseColor", baseColor);
            return m;
        }

        public static Material MakeTransparent(Shader shader, Color color)
        {
            var m = new Material(shader);
            m.SetFloat("_Surface", SurfaceTypeTransparent);
            m.SetFloat("_Blend", BlendModeAlpha);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", ZWriteOff);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", color);
            return m;
        }

        public void RefreshHighlights()
        {
            RefreshCount++;
            if (!_materialsInitialized)
            {
                CreateMaterials();
                if (!_materialsInitialized)
                    return;
            }

            var list = PartRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);

            EdgeSubstrate.SyncScene(list);

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

            var list = PartRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);

            bool isValid = !result.violations.Contains(element);
            ApplyMaterial(element, isValid);
        }

        private static bool KeepsItsOwnMaterialAlways(KitchenElement element) =>
            element.GetComponent<BasePlate>() != null || element is LightSourceElement;

        private static bool TintedOnlyByItsOwnDecor(KitchenElement element) =>
            element.GetComponent<Wall>() != null || element is FloorElement;

        private void ApplyMaterial(KitchenElement element, bool isValid)
        {
            if (element is CooktopElement cooktop)
            {
                PaintCooktopChildren(cooktop, isValid);
                return;
            }

            var renderer = element.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            if (KeepsItsOwnMaterialAlways(element)) return;

            bool ownDecorOnly = TintedOnlyByItsOwnDecor(element);

            if (ModuleEditMode.IsActive && !ModuleEditMode.IsEditable(element))
            {
                PaintFlat(element, renderer, _dimmedMaterial!);
                ElementOutline.For(element)?.Hide();
                return;
            }

            if (PhotoMode.ResolveTransparent(element.Transparent))
            {
                PaintFlat(element, renderer, isValid ? _validTransparentMaterial! : _invalidTransparentMaterial!);
                ElementOutline.Ensure(element)?.Show(selected: false);
            }
            else if (ownDecorOnly)
            {
                MaterialManager.ApplyOwnDecor(element);
                ElementOutline.For(element)?.Hide();
                SelectionManager.Instance?.RefreshHighlight(element);
            }
            else if (isValid && (!TintEnabled || MaterialManager.HasCustomDecor(element)))
            {
                MaterialManager.ApplyOwnDecor(element);
                ElementOutline.For(element)?.Hide();
            }
            else
            {
                PaintFlat(element, renderer, isValid ? _validMaterial! : _invalidMaterial!,
                    keepAux: true);
                ElementOutline.For(element)?.Hide();
            }
        }

        internal void PaintCooktopChildren(CooktopElement cooktop, bool isValid)
        {
            if (isValid)
            {
                cooktop.ApplyMaterials();
                return;
            }
            foreach (var mr in cooktop.GetComponentsInChildren<MeshRenderer>())
                if (mr != null) mr.sharedMaterial = _invalidMaterial!;
        }

        internal static void PaintFlat(KitchenElement element, MeshRenderer renderer,
            Material material, bool keepAux = false)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            int count = mesh != null ? mesh.subMeshCount : 1;
            if (count <= 1)
            {
                renderer.material = material;
                return;
            }

            var slots = new Material[count];
            for (int i = 0; i < count; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
            if (keepAux) element.RefreshSubmeshMaterials();
        }
    }
}
