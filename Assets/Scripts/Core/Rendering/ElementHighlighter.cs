using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ElementHighlighter : MonoBehaviour
    {
        public static ElementHighlighter? Instance { get; private set; }

        public int RefreshCount { get; set; }

        // Валидационная тонировка (светло-зелёный «всё в порядке»). Выключена —
        // валидные детали показывают собственный материал/текстуру; нарушения
        // по-прежнему подсвечиваются красным.
        public static bool TintEnabled { get; set; } = true;

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
            // Вход/выход из режима редактирования модуля меняет затемнение сцены.
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

            _validMaterial = new Material(shader);
            _validMaterial.EnableKeyword("_EMISSION");
            _validMaterial.SetColor("_EmissionColor", new Color(0f, 1f, 0f) * 0.4f);
            _validMaterial.SetColor("_BaseColor", new Color(0.85f, 1f, 0.85f, 1f));

            _invalidMaterial = new Material(shader);
            _invalidMaterial.EnableKeyword("_EMISSION");
            _invalidMaterial.SetColor("_EmissionColor", new Color(1f, 0f, 0f) * 0.4f);
            _invalidMaterial.SetColor("_BaseColor", new Color(1f, 0.8f, 0.8f, 1f));

            // Затемнение элементов вне редактируемого модуля.
            _dimmedMaterial = new Material(shader);
            _dimmedMaterial.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.38f, 1f));

            // «Прозрачный» режим: грани почти сквозные (еле заметная тонировка),
            // а форма читается по чёрным рёбрам контура (ElementOutline).
            // Коллайдер не трогаем — клик по-прежнему попадает в объект.
            _validTransparentMaterial = MakeTransparent(shader, new Color(0.7f, 0.85f, 0.7f, 0.08f));
            _invalidTransparentMaterial = MakeTransparent(shader, new Color(0.9f, 0.55f, 0.55f, 0.12f));

            _materialsInitialized = true;
        }

        /// <summary>URP/Lit в режиме прозрачности с заданным цветом (alpha &lt; 1).
        /// Одного `_Surface=1` мало: в рантайме нужно ЯВНО задать blend-состояния,
        /// иначе URP оставит непрозрачный проход и грань нарисуется сплошной.</summary>
        public static Material MakeTransparent(Shader shader, Color color)
        {
            var m = new Material(shader);
            m.SetFloat("_Surface", 1);                       // 1 = Transparent
            m.SetFloat("_Blend", 0);                         // 0 = Alpha blend
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
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

        private void ApplyMaterial(KitchenElement element, bool isValid)
        {
            // Варочная собрана из двух дочерних коробок, на корне рендерера нет —
            // без этой ветки она не могла бы покраснеть вообще, и наезд её выреза
            // на боковину был бы виден только по самой боковине.
            if (element is CooktopElement cooktop)
            {
                if (isValid) cooktop.ApplyMaterials();
                else
                    foreach (var mr in cooktop.GetComponentsInChildren<MeshRenderer>())
                        if (mr != null) mr.sharedMaterial = _invalidMaterial!;
                return;
            }

            var renderer = element.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            // Подложка и лампа держат СВОЙ материал (лампа — светящийся плафон):
            // ни тонировки, ни прозрачности к ним не применяем.
            if (element.GetComponent<BasePlate>() != null || element is LightSourceElement) return;

            // Стена и пол валидационной тонировки не получают — у них всегда свой
            // декор. Но выключатель «Прозрачный» им доступен, поэтому пройти ветку
            // прозрачности ниже они обязаны: раньше метод выходил здесь, и
            // прозрачную стену было НЕЧЕМ вернуть обратно — материал так и
            // оставался сквозным (его пересохраняла подсветка выделения).
            bool ownDecorOnly = element.GetComponent<Wall>() != null || element is FloorElement;

            // В режиме редактирования модуля всё вне модуля затемнено —
            // визуальный сигнал «заблокировано».
            if (ModuleEditMode.IsActive && !ModuleEditMode.IsEditable(element))
            {
                renderer.material = _dimmedMaterial!;
                ElementOutline.For(element)?.Hide();
                return;
            }

            if (PhotoMode.ResolveTransparent(element.Transparent))
            {
                renderer.material = isValid ? _validTransparentMaterial! : _invalidTransparentMaterial!;
                ElementOutline.Ensure(element)?.Show(selected: false);
            }
            else if (ownDecorOnly)
            {
                MaterialManager.ApplyOwnDecor(element);
                ElementOutline.For(element)?.Hide();
                // Декор перебил подсветку выделения — возвращаем её ПОВЕРХ свежего
                // материала. Заодно это чинит саму причину «залипшей» прозрачности:
                // SelectionManager запоминал как «исходный» тот материал, что застал,
                // то есть уже сквозной, и копировал его дальше из выделения в выделение.
                SelectionManager.Instance?.RefreshHighlight(element);
            }
            else if (isValid && (!TintEnabled || MaterialManager.HasCustomDecor(element)))
            {
                // Объекту назначена текстура/декор — показываем ЕЁ, а не плоский
                // валидационный тон (нарушения всё равно видны красным ниже).
                MaterialManager.ApplyOwnDecor(element);
                ElementOutline.For(element)?.Hide();
            }
            else
            {
                renderer.material = isValid ? _validMaterial! : _invalidMaterial!;
                ElementOutline.For(element)?.Hide();
            }
        }
    }
}
