using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class SettingsPanelUI : MonoBehaviour, IProjectWindow
    {
        private const float PanelW = 600;
        private const float PanelH = 900;
        private const float TitleY = PanelH * 0.5f - 55;
        private const float TabY = PanelH * 0.5f - 94;
        private const float ContentTopY = PanelH * 0.5f - 138;
        private const float CloseH = 40f;
        private const float CloseY = -PanelH * 0.5f + 54;
        private const float PanelSidePad = 40f;

        private const float BodyTopInset = PanelH * 0.5f - ContentTopY - SettingsRowFactory.RowH * 0.5f;
        private const float BodyBottomInset = PanelH * 0.5f + CloseY + CloseH * 0.5f + UIStyle.GapInner;

        private static readonly string[] TabLabels =
            { "Проект", "Вид", "Управление", "Фото режим", "Свет", "MCP", "О программе" };

        private readonly SettingsRowFactory _rows = new();
        private readonly SettingsTabStrip _tabs = new();

        private GameObject? _root;
        private WindowBody? _body;
        private SettingsProjectTab? _projectTab;
        private SettingsViewTab? _viewTab;
        private SettingsPhotoTab? _photoTab;
        private SettingsMcpTab? _mcpTab;

        public void Build(Transform canvas)
        {
            _photoTab?.Dispose();
            _viewTab?.Dispose();

            var panel = UIFactory.CreatePanel("SettingsPanel", canvas, Vector2.zero, new Vector2(PanelW, PanelH));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("SetTitle", panel.transform, "Настройки", UIStyle.FontWindowTitle,
                new Vector2(0, TitleY), new Vector2(PanelW - PanelSidePad, 36), TextAnchor.MiddleCenter);

            var s = KitchenSettings.Instance;
            if (s == null)
            {
                _root.SetActive(false);
                return;
            }

            _tabs.BuildButtons(panel.transform, TabLabels, PanelW - PanelSidePad, TabY);

            _body = WindowBody.Create(panel.rectTransform, BodyTopInset, BodyBottomInset,
                PanelSidePad * 0.5f);
            _tabs.AfterSwitch = () => _body?.Fit();

            _projectTab = new SettingsProjectTab(_rows, RefreshDependentStates);
            _viewTab = new SettingsViewTab(_rows, RefreshDependentStates);
            _photoTab = new SettingsPhotoTab(_rows);

            _projectTab.Build(AddPage("Tab_Project"), s, ContentTopY);
            _viewTab.Build(AddPage("Tab_View"), ContentTopY);
            new SettingsControlTab(_rows).Build(AddPage("Tab_Control"), s, ContentTopY);
            _photoTab.Build(AddPage("Tab_Photo"), s, ContentTopY);
            new SettingsLightTab(_rows).Build(AddPage("Tab_Light"), s, ContentTopY);
            _mcpTab = new SettingsMcpTab(_rows);
            _mcpTab.Build(AddPage("Tab_Mcp"), ContentTopY);
            new SettingsAboutTab().Build(AddPage("Tab_About"), ContentTopY);

            _tabs.Switch(0);

            _viewTab.FollowEditMode();

            UIFactory.CreateButton("SetClose", panel.transform, "Закрыть",
                new Vector2(0, CloseY), new Vector2(160, CloseH),
                () => SetVisible(false));

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            _body.Fit();
            _root.SetActive(false);
        }

        private Transform AddPage(string name) =>
            _tabs.AddPage(_body!.Content, name, _body!.PanelOriginY).transform;

        private void OnDestroy()
        {
            ProjectWindows.Unregister(this);
            _photoTab?.Dispose();
            _viewTab?.Dispose();
        }

        internal void SyncFromSettings()
        {
            _rows.ReadBackFromSettings();
            _photoTab?.RefreshPresetLabel();
            _mcpTab?.Refresh();
        }

        private void RefreshDependentStates()
        {
            if (_root == null) return;
            var s = KitchenSettings.Instance;
            if (s == null) return;
            _projectTab?.RefreshDependentStates(s);
            _viewTab?.Refresh();
            _body?.Fit();
        }

        public string WindowId => "settings";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
            if (!visible) return;
            SyncFromSettings();
            _photoTab?.SyncActiveToggle();
            RefreshDependentStates();
        }
    }
}
