using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>
    /// Корневой UI: создаёт Canvas, верхний тулбар и панели (спецификация,
    /// настройки, контекстное меню). Строится процедурно в Start.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private Canvas _canvas;
        private SpecificationPanelUI _specPanel;
        private SettingsPanelUI _settingsPanel;
        private ContextMenuUI _contextMenu;

        public Canvas Canvas => _canvas;
        public const string QuickSaveName = "quicksave";

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _canvas = UIFactory.CreateCanvas("UICanvas");
            BuildToolbar();

            _specPanel = gameObject.AddComponent<SpecificationPanelUI>();
            _specPanel.Build(_canvas.transform);

            _settingsPanel = gameObject.AddComponent<SettingsPanelUI>();
            _settingsPanel.Build(_canvas.transform);

            _contextMenu = gameObject.AddComponent<ContextMenuUI>();
            _contextMenu.Build(_canvas.transform);

            var toast = gameObject.AddComponent<ToastNotification>();
            toast.Build(_canvas.transform);
        }

        private void BuildToolbar()
        {
            var bar = UIFactory.CreatePanel("Toolbar", _canvas.transform, Vector2.zero, Vector2.zero);
            UIFactory.StretchTopBar(bar.rectTransform, 52f);

            float x = 8f;
            const float y = -6f;
            const float h = 40f;

            for (int i = 0; i < AppConstants.PRESET_DIMENSIONS_MM.Length; i++)
            {
                var dims = AppConstants.PRESET_DIMENSIONS_MM[i];
                int idx = i;
                var btn = UIFactory.CreateButton($"Preset{i}", bar.transform, $"{dims.x}×{dims.y}",
                    new Vector2(x, y), new Vector2(90, h), () => SpawnPreset(idx));
                UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
                btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
                x += 96;
            }

            x += 12;
            AddBarButton(bar.transform, "Spec", "Спецификация", ref x, y, h, 150, ToggleSpecification);
            AddBarButton(bar.transform, "Settings", "Настройки", ref x, y, h, 130, ToggleSettings);
            AddBarButton(bar.transform, "Save", "Сохранить", ref x, y, h, 130, QuickSave);
            AddBarButton(bar.transform, "Load", "Загрузить", ref x, y, h, 130, QuickLoad);

            x += 20;
            var alignBtn = UIFactory.CreateButton("Align", bar.transform, "Выравн.",
                new Vector2(x, y), new Vector2(80, h), ShowAlignMenu);
            UIFactory.AnchorTopLeft(alignBtn.GetComponent<RectTransform>());
            alignBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            x += 86;
            var distBtn = UIFactory.CreateButton("Distribute", bar.transform, "Распред.",
                new Vector2(x, y), new Vector2(80, h), DistributeX);
            UIFactory.AnchorTopLeft(distBtn.GetComponent<RectTransform>());
            distBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            x += 86;
        }

        private void AddBarButton(Transform parent, string name, string label, ref float x, float y, float h, float w, System.Action onClick)
        {
            var btn = UIFactory.CreateButton(name, parent, label, new Vector2(x, y), new Vector2(w, h), onClick);
            UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            x += w + 6;
        }

        public void SpawnPreset(int index)
        {
            if (index < 0 || index >= AppConstants.PRESET_DIMENSIONS_MM.Length) return;
            SpawnBoard(AppConstants.PRESET_DIMENSIONS_MM[index]);
        }

        public void SpawnBoard(Vector3Int dims)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS; // на полу
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreateBoard(dims, $"Board {dims.x}x{dims.y}x{dims.z}", pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        private Vector3 GroundPointInFrontOfCamera()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter))
                return ray.GetPoint(enter);
            return cam.transform.position + cam.transform.forward * 2f;
        }

        public void ToggleSpecification()
        {
            if (_specPanel == null) return;
            _settingsPanel.SetVisible(false);
            _specPanel.Toggle();
        }

        public void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            _specPanel.SetVisible(false);
            _settingsPanel.Toggle();
        }

        public void QuickSave()
        {
            if (SaveLoadManager.SaveProject(QuickSaveName))
                Debug.Log("[UI] Saved: " + SaveLoadManager.PathForName(QuickSaveName));
        }

        public void QuickLoad()
        {
            if (SaveLoadManager.LoadProject(QuickSaveName))
                Debug.Log("[UI] Loaded: " + QuickSaveName);
        }

        private void ShowAlignMenu()
        {
            var sel = SelectionManager.Instance;
            if (sel == null || sel.SelectedElements.Count < 2)
            {
                Debug.Log("[UI] Align: select at least 2 boards");
                return;
            }

            var list = new List<KitchenElement>(sel.SelectedElements);
            AlignDistributeTool.Align(list, Axis.X, AlignmentMode.Min);
            AlignDistributeTool.RefreshHighlights();
        }

        private void DistributeX()
        {
            var sel = SelectionManager.Instance;
            if (sel == null || sel.SelectedElements.Count < 3)
            {
                Debug.Log("[UI] Distribute: select at least 3 boards");
                return;
            }

            var list = new List<KitchenElement>(sel.SelectedElements);
            AlignDistributeTool.Distribute(list, Axis.X);
            AlignDistributeTool.RefreshHighlights();
        }
    }
}
